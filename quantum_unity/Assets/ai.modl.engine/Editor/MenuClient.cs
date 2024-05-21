using Modl.Editor.UI;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.Build;

namespace Modl.Editor
{
    public static class MenuClient
    {
        private const string ModlConfiguration = "modl/Bot Configuration";
        private const string ModlLevelConfiguration = "modl/Level Configuration";
        
        private const string ModlTestingEnabled = "modl/Toggle modl Testing";

        private const string ModlLevelCapture = "modl/Level Utils/Capture Custom Level Image or Section";
        private const string ModlLevelSectionMerge = "modl/Level Utils/Merge Captured Level Sections";

        private const string ModlPlatformUrl = "modl/modl:test Platform ";
        
        private const int DataCollectionLocationPriority = 1;
        private const int InputToggleLocationPriority = 450;
        private const int PlatformUrlPriority = 500;
        private const int UtilsLevelLocationPriority = 100;
        
        [MenuItem(ModlConfiguration, priority = DataCollectionLocationPriority)]
        private static void OpenConfiguration()
        {
            // Get existing open window or if none, make a new one:
            EW_BotConfiguration window = (EW_BotConfiguration)EditorWindow.GetWindow(typeof(EW_BotConfiguration));
            window.titleContent = new GUIContent("Bot Configuration");
            window.minSize = new Vector2(400, 400);
            window.Show();
        }
        
        [MenuItem(ModlLevelCapture, priority = DataCollectionLocationPriority +2)]
        private static void OpenLevelCapture()
        {
            // Get existing open window or if none, make a new one:
            EW_LevelCapture window = EditorWindow.GetWindow<EW_LevelCapture>();
            window.minSize = new Vector2(600, 420);

            window.titleContent = new GUIContent("Level Image Capturing");
            window.Show();
        }
        
        [MenuItem(ModlLevelSectionMerge, priority = DataCollectionLocationPriority +2)]
        private static void OpenLevelMerge()
        {
            EW_LevelMerging window = EditorWindow.GetWindow<EW_LevelMerging>();
            window.minSize = new Vector2(480, 330);
            
            window.titleContent = new GUIContent("Level Section Merging");
            window.Show();
        }
        
        [MenuItem(ModlLevelConfiguration, priority = DataCollectionLocationPriority +1)]
        private static void OpenLevelSelection()
        {
            // Get existing open window or if none, make a new one:
            EW_LevelConfiguration window = EditorWindow.GetWindow<EW_LevelConfiguration>();
            window.minSize = new Vector2(420, 420);

            window.titleContent = new GUIContent("Level Configuration");
            window.Show();
        }

        [MenuItem(ModlTestingEnabled, true, priority = InputToggleLocationPriority)]
        private static bool EnableToggleModlTesting()
        {
            #if MODL_AUTOMATIC_TESTING
            Menu.SetChecked(ModlTestingEnabled, true);
            #else
            Menu.SetChecked(ModlTestingEnabled, false);
            #endif
            return !EditorApplication.isCompiling;
        }

        [MenuItem(ModlTestingEnabled, priority = InputToggleLocationPriority)]
        private static void ToggleScriptingDefine()
        {
            #if UNITY_2021_3_OR_NEWER
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone, out string[] defines);
            List<string> scriptingDefineList = new List<string>();
            scriptingDefineList.AddRange(defines);
            if (ScriptingDefineEnabled())
            {
                int index = scriptingDefineList.IndexOf("MODL_AUTOMATIC_TESTING");
                if (index != -1)
                {
                    scriptingDefineList.RemoveAt(index);
                }
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, scriptingDefineList.ToArray());
            }
            else
            {
                // Don't overwrite any other scripting defines in the project, and don't add MODL_AUTOMATIC_TESTING more than once
                if (!scriptingDefineList.Contains("MODL_AUTOMATIC_TESTING"))
                {
                    scriptingDefineList.Add("MODL_AUTOMATIC_TESTING");
                }
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, scriptingDefineList.ToArray());
            }
            #else
            string definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
            List<string> scriptingDefineList = new List<string>();
            if (definesString.Contains(";"))
            {
                string[] defines = definesString.Split(';');
                scriptingDefineList.AddRange(defines);
            }
            else
            {
                scriptingDefineList.Add(definesString);
            }
            
            if (ScriptingDefineEnabled())
            {
                int index = scriptingDefineList.IndexOf("MODL_AUTOMATIC_TESTING");
                if (index != -1)
                {
                    scriptingDefineList.RemoveAt(index);
                }
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, string.Join(";", scriptingDefineList.ToArray()));
            }
            else
            {
                // Don't overwrite any other scripting defines in the project, and don't add MODL_AUTOMATIC_TESTING more than once
                if (!scriptingDefineList.Contains("MODL_AUTOMATIC_TESTING"))
                {
                    scriptingDefineList.Add("MODL_AUTOMATIC_TESTING");
                }
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, string.Join(";", scriptingDefineList.ToArray()));
            }
            #endif
        }

        [MenuItem(ModlPlatformUrl, priority = PlatformUrlPriority)]
        private static void OpenModlPlatform()
        {
            Application.OpenURL("https://engine.app.modl.ai/?loc=unity");
        }


        private static bool ScriptingDefineEnabled()
        {
            #if MODL_AUTOMATIC_TESTING
            return true;
            #else
            return false;
            #endif
        }
    }
}
