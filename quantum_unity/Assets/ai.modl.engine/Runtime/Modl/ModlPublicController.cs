using System;
using System.Collections.Generic;
using Modl.ExportedTypes;
using Modl.Internal;
using UnityEngine;

namespace Modl
{
    /// <summary>
    /// This class exposes the public controls exposed to the plugin user
    /// </summary>
    public static class ModlPublicController
    {
        /// <summary>
        /// Event that triggers when modl:Engine executes a state reload,
        /// can be used to handle setting state values tagged "ReadOnly" in BotConfiguration.
        /// </summary>
#pragma warning disable 0067 //disable unused variable warning that happens when the plugin is added to a new project
        public static event Action<ModlLoadStateData> OnLoadState;
#pragma warning restore 0067
        
        /// <summary>
        /// Property that reflects the current state of the transmission
        /// between the UnityPlugin and Modl AI Engine
        /// </summary>
        public static bool IsTransmitting
        {

            get {
#if MODL_AUTOMATIC_TESTING && (!UNITY_EDITOR || MODL_BRAIN)
                return ModlPluginManager.Instance.IsTransmitting;
#elif !MODL_AUTOMATIC_TESTING && UNITY_EDITOR
                Debug.LogWarning("Modl testing is not enabled. You may need to wrap any modl code in the MODL_AUTOMATIC_TESTING scripting define.");
                return false;
#else
                //playmode with modl enabled and no local brain, so no need to start transmitting, and no need for a warning.
                return false;
#endif
                }

        }

        /// <summary>
        /// Property that reflects the current state of the transmission
        /// between the UnityPlugin and Modl AI Engine
        /// </summary>
        public static bool IsPaused
        {

            get {
#if MODL_AUTOMATIC_TESTING
                return ModlPluginManager.Instance.IsPaused;
#else
                Debug.LogWarning("Modl testing is not enabled. You may need to wrap any modl code in the MODL_AUTOMATIC_TESTING scripting define.");
                return false;
#endif
                }
        }

        

        /// <summary>
        /// Method that triggers the action in order to start the transmission
        /// </summary>
        public static void Start()
        {
#if MODL_AUTOMATIC_TESTING && (!UNITY_EDITOR || MODL_BRAIN)
            //The event adder checks if the callback is subscribed before adding, so no need for us to check.
            ModlPluginManager.Instance.OnLoadState += HandleOnLoadState;
            ModlPluginManager.Instance.StartTransmitting();
#elif !MODL_AUTOMATIC_TESTING && UNITY_EDITOR
            Debug.LogWarning("Modl testing is not enabled. You may need to wrap any modl code in the MODL_AUTOMATIC_TESTING scripting define.");
#else
            //playmode with modl enabled and no local brain, so no need to start transmitting, and no need for a warning.
#endif
        }

        private static void HandleOnLoadState(List<LoadStateData> list)
        {
#if MODL_AUTOMATIC_TESTING
            OnLoadState?.Invoke(new ModlLoadStateData(list));
#endif
        }

        /// <summary>
        /// Method that triggers the action in order to pause the transmission.
        /// </summary>
        public static void Pause()
        {
#if MODL_AUTOMATIC_TESTING
            ModlPluginManager.Instance.PauseTransmitting();
#else
            Debug.LogWarning("Modl testing is not enabled. You may need to wrap any modl code in the MODL_AUTOMATIC_TESTING scripting define.");
#endif
        }

        /// <summary>
        /// Marks a state as terminal and reloads a previous non-terminal state.
        /// </summary>
        public static void InvokeTerminalStateUpdate()
        {
#if MODL_AUTOMATIC_TESTING
            ModlPluginManager.Instance.TerminalStateUpdate();
#else
            Debug.LogWarning("Modl testing is not enabled. You may need to wrap any modl code in the MODL_AUTOMATIC_TESTING scripting define.");
#endif
        }
    }
}
