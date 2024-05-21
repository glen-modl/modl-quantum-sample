using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System;


namespace Modl.Editor.UI
{
    public class BotConfigScriptableObject : ScriptableObject
    {
        public GameObject playerObject;
        public List<BotConfig> botGameObjectList = new List<BotConfig>();
        public List<ExplorationSpace> explorationSpaceList = new List<ExplorationSpace>();
        public List<SampledState> sampledStateList = new List<SampledState>();
    }
}