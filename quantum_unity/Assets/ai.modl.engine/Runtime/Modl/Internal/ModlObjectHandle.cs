using UnityEngine;

namespace Modl.Internal
{
    /// <summary>
    /// Script that is responsible of tracking game objects
    /// This script is attached automatically by the interface during the build to
    /// all the tracked prefabs
    /// </summary>
    public class ModlObjectHandle : MonoBehaviour
    {
        // This is an index to the location of that object in the tracked object array
        // in the DataHandler class
        public string parentReference;
        
        /// <summary>
        /// Scene based ID, generated based on the instance location in the object hierarchy.
        /// The ID is not relying on Unity's GetInstanceID() that changes during scene reload.
        /// Generation of the ID occurs in <see cref="DataHandler.CacheRuntimeStructures"> DataHandler.CacheRuntimeStructures </see>.
        /// </summary>
        public string sceneBasedID;

        
        /// <summary>
        /// Used to tell the build callbacks to leave the object handle on the prefab.
        /// Defined to handle modl prefabs, that plugin users can't modify.
        /// </summary>
        public bool stickToPrefab;
        
        /// <summary>
        /// Automatically registering the object to the dataHandler to be tracked
        /// </summary>
        private void Start()
        {
#if MODL_AUTOMATIC_TESTING
            var plugin = ModlPluginManager.Instance;
            if (plugin != null)
                plugin.TrackObject(this);
#endif
        }

        /// <summary>
        /// Automatically de-register the object from the dataHandler so it won't be tracked anymore
        /// </summary>
        private void OnDestroy()
        {
#if MODL_AUTOMATIC_TESTING
            //If ModlPluginManager has been destroyed, no need to untrack object.
            if (ModlPluginManager.ApplicationIsQuitting) return;
            
            var plugin = ModlPluginManager.Instance;
            if (plugin != null)
                plugin.UntrackObject(this);
#endif
        }
    }
}
