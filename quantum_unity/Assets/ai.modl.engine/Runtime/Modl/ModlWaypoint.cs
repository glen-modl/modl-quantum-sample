using UnityEngine;

namespace Modl
{
    /// <summary>
    /// Attach this to any prefab and track it's transform.position or a similar goal,
    /// and it will automatically be tagged as a "waypoint".
    ///
    /// You also need to add the index here as well, so it will be marked with "waypoint_index".
    /// </summary>
    public class ModlWaypoint : MonoBehaviour
    {
        public int index;
    }
}

