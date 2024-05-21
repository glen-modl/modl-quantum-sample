using UnityEditor;

namespace Modl.Editor.Builds
{ 
    
    [InitializeOnLoad]
    public class ModlAutoBuild
    {
        //Used to decide whether to setup Modl bot communication or not, when entering playmode!!
        private const string StartedFromPreviewBotSetup = "PREVIEWING_BOT_SETUP";
        
        static ModlAutoBuild()
        {
            SetTestComm();
        }

        private static bool ScriptingDefineEnabled()
        {
            #if MODL_AUTOMATIC_TESTING
            return true;
            #else
            return false;
            #endif
        }

        public static bool SetTestComm()
        {
            bool enabled = ScriptingDefineEnabled();

            if (enabled)
            {
                EnableTestComm();
            }
            else
            {
                DisableTestComm();
            }

            return enabled;
        }
        
        private static void EnableTestComm()  => EditorApplication.playModeStateChanged += StateChangeCallback;
        private static void DisableTestComm() => EditorApplication.playModeStateChanged -= StateChangeCallback;

        private static void StateChangeCallback(PlayModeStateChange stateChange)
        {
            switch (stateChange)
            {
                case PlayModeStateChange.ExitingEditMode:
#if MODL_AUTOMATIC_TESTING
                    ModlBuildCallbacks.OnPreprocessBuild();
            #if MODL_BRAIN
                    //When running with the MODL_BRAIN Scripting define, make sure we use the Brain communicator when not previewing.
                    if (!EditorPrefs.GetBool(StartedFromPreviewBotSetup, false))
                    {
                        Internal.ModlPluginManager.CommunicatorPrefState = (int)Internal.ModlPluginManager.ModlCommunicatorType.Brain;    
                    }
            #endif
#endif
                    break;
                case PlayModeStateChange.EnteredEditMode:
#if MODL_AUTOMATIC_TESTING
                    ModlBuildCallbacks.OnPostProcessBuild();
                    
                    //Reset these so we default to the right communicator.
                    EditorPrefs.SetBool(StartedFromPreviewBotSetup, false);
                    Internal.ModlPluginManager.CommunicatorPrefState = (int)Internal.ModlPluginManager.ModlCommunicatorType.None;
#endif
                    break;
            }
        }
        
    }
    
}
