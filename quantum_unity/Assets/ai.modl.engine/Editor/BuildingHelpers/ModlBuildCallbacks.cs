using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Modl.Editor.UI;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

using Modl.Internal;
using Modl.Internal.DataCommunication;
using Modl.Proto;
using Object = UnityEngine.Object;

using UnityEditor.VSAttribution.Modl;


namespace Modl.Editor.Builds
{
#if MODL_AUTOMATIC_TESTING

    /// <summary>
    /// Async callbacks to add modl logic to existing building pipelines.
    /// </summary>
    /// <remarks>
    /// Unity has two completely different logics to handle pre and post build callbacks.
    /// We can't do anything about it but abide.
    /// </remarks>
    public class ModlBuildCallbacks : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private static ModlBuildCallbacks _instance;
        
        // pre build callback execution order.
        // We would like to be executed as very last callback so we put the bigger number allowed
        public int callbackOrder => int.MinValue;

        
        public static void OnPreprocessBuild() => new ModlBuildCallbacks().OnPreprocessBuild(null);
        
        public static void OnPostProcessBuild() => new ModlBuildCallbacks().OnPostprocessBuild(null);
        
        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                // disable asset importing while we're modifying the prefabs.
                AssetDatabase.StartAssetEditing();
                
                var config = new RuntimeFileSystemInterface().ReadConfigFile();
                var trackedObjectKeys = GetAllPrefabIds(config);
                
                // add handles to prefabs
                foreach (string key in trackedObjectKeys)
                {
                    var prefab = ModlBuildUtils.GetUnityObject<GameObject>(key);

                    // modl prefabs already contain a handle.
                    var handle = prefab.GetComponent<ModlObjectHandle>();
                    if (!handle)
                    {
                        handle = prefab.AddComponent<ModlObjectHandle>();
                        handle.parentReference = key;
                    }
                }
                
                // ensure the prefabs have been saved.
                AssetDatabase.SaveAssets();  
                
                // copy config to build folder
                if (report != null)
                {
                    string modlProjectId = EditorPrefs.GetString("ModlProjectId");
                    if (!String.IsNullOrEmpty(modlProjectId))
                    {
                        var result = VSAttribution.SendAttributionEvent("build", "modl.ai", modlProjectId);
                    }
                    else
                    {
                        throw new BuildFailedException("Project ID invalid");
                    }

                    var folder = Path.GetDirectoryName(report.summary.outputPath);
                    File.Copy(RuntimeFileSystemInterface.CONFIG_PATH, Path.Combine(folder, "game_config.json"), true);
                    
                    //Copy selected maps
                    if (File.Exists(LevelCaptureUtils.LevelConfigName))
                    {
                        const string modlLevelImages = "modl_level_images";
                        
                        var levelFolder = Path.Combine(folder, modlLevelImages);
                        Directory.CreateDirectory(levelFolder);

                        try
                        {
                            var exportSelection =
                                JsonUtility.FromJson<EW_LevelConfiguration.ModlLevelConfigurationExport>(
                                    File.ReadAllText(LevelCaptureUtils.LevelConfigName));

                            foreach (var selectedMap in exportSelection.MapList.Select(item => item.MapImageFile))
                            {
                                var selectedFileName = Path.GetFileName(selectedMap);
                                if (!string.IsNullOrWhiteSpace(selectedFileName) &&
                                    File.Exists($"{selectedMap}.json") && File.Exists($"{selectedMap}.png"))
                                {
                                    File.Copy($"{selectedMap}.json", Path.Combine(levelFolder, $"{selectedFileName}.json"), true);
                                    File.Copy($"{selectedMap}.png", Path.Combine(levelFolder, $"{selectedFileName}.png"), true);
                                }
                                else if (!string.IsNullOrWhiteSpace(selectedFileName))
                                {
                                    Debug.LogError(
                                        $"Captured level image/json was not found [{selectedMap}.png or {selectedMap}.json], please make sure that all maps selected for export exist.");
                                }
                            }

                            //Strip paths from the selected maps list and save 'modl_map_config.json' to the build folder.
                            for (var index = 0; index < exportSelection.MapList.Count; index++)
                            {
                                if (!String.IsNullOrEmpty(exportSelection.MapList[index].MapImageFile))
                                {
                                    exportSelection.MapList[index].MapImageFile = Path.Combine(modlLevelImages,
                                        Path.GetFileName(exportSelection.MapList[index].MapImageFile));
                                }
                            }

                            File.WriteAllText(Path.Combine(folder, LevelCaptureUtils.LevelConfigName),
                                JsonUtility.ToJson(exportSelection, true));
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                            Debug.LogError("Failed to copy Modl Map Definitions to build. Possible corruption of 'modl_map_config.json', see error message above.");
                        }
                    }
                    else
                    {
                        Debug.Log($"No 'modl_map_config.json' found, use the 'modl/Map Selection Overview' to select captured to export with the build.");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw new BuildFailedException("Unexpected error in modl preprocess build callback. See error message above");
            }
            finally
            {
                // finally ensure we release the AssetDatabase
                AssetDatabase.StopAssetEditing();
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            try
            {
                // disable asset importing while we're modifying the prefabs.
                AssetDatabase.StartAssetEditing();

                var config = new RuntimeFileSystemInterface().ReadConfigFile();
                var trackedObjectKeys = GetAllPrefabIds(config);
            
                // remove handles from prefabs
                foreach (string key in trackedObjectKeys)
                {
                    var prefab = ModlBuildUtils.GetUnityObject<GameObject>(key);

                    var handle = prefab.GetComponent<ModlObjectHandle>();
                    // modl prefabs contain a handle that should not be destroyed.
                    if (!handle.stickToPrefab)
                    {
                        Object.DestroyImmediate(handle, true);
                    }
                }
            
                // ensure the prefabs have been saved.
                AssetDatabase.SaveAssets();
            }
            finally
            {
                // finally ensure we release the AssetDatabase
                AssetDatabase.StopAssetEditing();
            }
        }
        
        /// <summary>
        /// Extracts all IDs from the game config (objects have it plain at the first level dimension,
        /// features and actions need to be extracted from Member info)  
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        private static IEnumerable<string> GetAllPrefabIds(GameConfig config) =>
            config.FeatureSpace.Dims
                .Concat(config.ObjectSpace.Dims)
                .Concat(config.ActionSpace.Dims)
                .Select(x => x.Id)
                .Distinct();
    }
#endif
}
