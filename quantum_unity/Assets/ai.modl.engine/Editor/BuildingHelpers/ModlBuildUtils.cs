using System.Linq;
using Modl.Internal;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Experimental;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Modl.Editor.Builds
{
    public static class ModlBuildUtils
    {
        private const string PROGRESS_TITLE = "modl Cleanup Utility";

        public static string GetObjectId(Object obj)
        {
            if (obj is Component component)
                return TMPGetComponentId(component);
            
            return GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
        }
        
        /// <summary>
        /// Right now we need this ID to contain info that can be used at runtime.
        /// TODO: cache GlobalObjectID => Type into ModlObjectHandle and use GetObjectId instead 
        /// </summary>
        /// <param name="component"></param>
        /// <returns></returns>
        private static string TMPGetComponentId(Component component)
        {
            var t = component.GetType();
            return $"{t.Assembly.FullName}|{t.FullName}";
        } 
        
        public static T GetUnityObject<T>(string key) where T : Object
        {
            if (!GlobalObjectId.TryParse(key, out var id))
            {
                Debug.LogError($"[{key}] is not a GlobalObjectId");
                throw new BuildFailedException("Unexpected error in modl preprocess build callback. See error message above");
            }
            
            Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);

            if (obj is T ret)
            {
                return ret;
            }

            Debug.LogError($"[{obj}] is not a {typeof(T)} (from {key})");
            throw new BuildFailedException("Unexpected error in modl preprocess build callback. See error message above");
        }
        
        public static void CleanupProject()
        {
            try
            {
                // disable asset importing while we're modifying the prefabs.
                AssetDatabase.StartAssetEditing();

                bool dirtyPrefab;
                string[] paths = AssetDatabase.GetAllAssetPaths();
                float steps = paths
                    .Count(x => x.EndsWith(".prefab") || x.EndsWith(".unity"));
                int currStep = 0;

                // clean handles
                foreach (string path in paths.Where(x => x.EndsWith(".prefab")))
                {
                    EditorUtility.DisplayProgressBar(PROGRESS_TITLE, "Cleaning prefabs...", ++currStep / steps);
                    GameObject prefab = EditorResources.Load<GameObject>(path);

                    if (prefab == null)
                    {
                        continue;
                    }

                    dirtyPrefab = false;
                    foreach (ModlObjectHandle handle in prefab.GetComponentsInChildren<ModlObjectHandle>())
                    {
                        Object.DestroyImmediate(handle, true);
                        dirtyPrefab = true;
                    }

                    if (dirtyPrefab)
                    {
                        PrefabUtility.SavePrefabAsset(prefab);
                    }
                }
            }
            finally
            {
                // ensure the progress bar is closed even if something fails.
                EditorUtility.ClearProgressBar();
                
                // finally ensure we release the AssetDatabase
                AssetDatabase.StopAssetEditing();
            }
        }
    }
}