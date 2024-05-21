using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Modl.Proto;
using UnityEngine;

using static Modl.Internal.RuntimeData.ConversionUtils;
using Type = System.Type;

namespace Modl.Internal
{
    /// <summary>
    /// Responsible on getting and setting the current state
    /// Responsible on setting the values of game action
    /// </summary>
    public class DataHandler
    {
        #region Auxiliary Structures (indexed by prefab Id)
        private readonly Dictionary<string, MemberEntry[]> _prefabsTrackedObjects;
        private readonly Dictionary<string, MemberEntry[]> _prefabsTrackedActions;
        private readonly Dictionary<string, MemberEntry[]> _prefabsTrackedFeatures;
        #endregion
        
        #region Runtime Structures (indexed by runtime object id)
        private readonly Dictionary<string, RuntimeMember[]> _runtimeTrackedObjects;
        private readonly Dictionary<string, RuntimeMember[]> _runtimeTrackedActions;
        private readonly Dictionary<string, RuntimeMember[]> _runtimeTrackedFeatures;
        private readonly Dictionary<string, string> _runtimeObjectPrefabParent;
        #endregion
        
        #region Getters for the runtime structures.
        public Dictionary<string, RuntimeMember[]> GetRuntimeTrackedObjects => _runtimeTrackedObjects;
        public Dictionary<string, RuntimeMember[]> GetRuntimeTrackedActions => _runtimeTrackedActions;
        public Dictionary<string, RuntimeMember[]> GetRuntimeTrackedFeatures => _runtimeTrackedFeatures;
        #endregion

        public DataHandler (GameConfig config)
        {
            _runtimeTrackedObjects = new Dictionary<string, RuntimeMember[]>();
            _runtimeTrackedActions = new Dictionary<string, RuntimeMember[]>();
            _runtimeTrackedFeatures = new Dictionary<string, RuntimeMember[]>();

            CacheTrackedFields(config.ObjectSpace, out _prefabsTrackedObjects);
            CacheTrackedFields(config.ActionSpace, out _prefabsTrackedActions);
            CacheTrackedFields(config.FeatureSpace, out _prefabsTrackedFeatures);
            _runtimeObjectPrefabParent = new Dictionary<string, string>();
        }

        #region Data Collection

        public RepeatedField<ObjectVector> GetFrameData()
        {
            var ret = new RepeatedField<ObjectVector>();

            foreach (var entry in _runtimeTrackedObjects)
            {
                var runtimeId = entry.Key;
                if (!_runtimeObjectPrefabParent.ContainsKey(runtimeId))
                {
                    Debug.LogError($"No instance with key [{runtimeId}] found in ObjectPrefabParent cache, DO NOT track objects that are destroyed during runtime.");
                    continue;
                }
                var prefabId = _runtimeObjectPrefabParent[runtimeId];
                var frameProps = new RepeatedField<Value>();

                for (var j = 0; j < _runtimeTrackedObjects[runtimeId].Length; j++)
                {
                    var info = _runtimeTrackedObjects[runtimeId][j];
                    var toAdd = GetObsBufferValues(info.info, info.component);
                    foreach (var e in toAdd)
                    {
                        frameProps.Add(e);
                    }
                }

                var objVec = new ObjectVector
                {
                    Id = prefabId,
                    RuntimeId = runtimeId,
                };
                
                objVec.Values.AddRange(frameProps);
                
                ret.Add(objVec);
            }

            return ret;
        }

        public float[] GetFrameFeatures()
        {
            var ret = new List<float>();
            foreach (var kv in _runtimeTrackedFeatures)
            {
                var features = kv.Value;
                foreach (var info in features)
                {
                    ret.AddRange(GetBufferValues(info.info, info.component));
                }
            }

            return ret.ToArray();
        }
        
        #endregion
        
        #region Data Application
        
        // public void Update(RepeatedField<ObjectVector> data, float[] actions)
        // {
        //     if (data != null)
        //     {
        //         ApplyFrameData(data);
        //     }
        //
        //     if (actions != null)
        //     {
        //         ApplyActions(actions);
        //     }
        // }

        public void ApplyFrameData(RepeatedField<ObjectVector> objects)
        {
            foreach (ObjectVector obj in objects)
            {
                var runtimeId = obj.RuntimeId;
                if (!_runtimeTrackedObjects.ContainsKey(runtimeId))
                {
                    Debug.LogError($"Key [{runtimeId}] not found in TrackedObjects, DO NOT track objects that are destroyed during runtime.");
                    continue;
                }
                
                var trackedFields = _runtimeTrackedObjects[runtimeId];
                var dataBuffer = obj.Values;
                int dataIdx = 0;

                foreach (var info in trackedFields)
                {
                    if (info.readOnly)
                    {
                        dataIdx += info.bufferSize;
                        continue;
                    }
                    
                    var val = GetObsObjectValue(dataBuffer.Skip(dataIdx).Take(info.bufferSize).ToArray(), info.info);
                    MemberSetValue(info.info, info.component, val);
                    dataIdx += info.bufferSize;
                }
            }
        }

        public void ApplyActions(float[] data)
        {
            foreach (var kv in _runtimeTrackedActions)
            {
                int dataIdx = 0;
                var actions = kv.Value;

                foreach (var info in actions)
                {
                    var val = GetObjectValue(data.Skip(dataIdx).Take(info.bufferSize).ToArray(), info.info);
                    MemberSetValue(info.info, info.component, val);
                    dataIdx += info.bufferSize;
                }
            }
        }
        
        #endregion
        
        #region Object Registration API
        
        public void TrackObject(ModlObjectHandle handle) => CacheRuntimeStructures(handle);

        public void UntrackObject(ModlObjectHandle handle)
        {
            var id = handle.sceneBasedID;
            _runtimeTrackedObjects.Remove(id);
            _runtimeTrackedActions.Remove(id);
            _runtimeTrackedFeatures.Remove(id);
        }
        
        #endregion

        #region Caches

        private static void CacheTrackedFields(ValueRange space, out Dictionary<string, MemberEntry[]> cache) 
        {
            var accumulator = new Dictionary<string, List<MemberEntry>>();

            foreach (var prefabDim in space.Dims)
            {
                // prefab
                string prefabId = prefabDim.Id;
                if (!accumulator.ContainsKey(prefabId))
                {
                    accumulator[prefabId] = new List<MemberEntry>();
                }
                
                // component
                var members = prefabDim.Dims.SelectMany(componentDim => componentDim.Dims.Select(memberDim =>
                {
                    var (info, componentType) = MemberGetInfo(componentDim.Id, memberDim.Id);
                    return new MemberEntry
                        {
                            info = info, 
                            ComponentType = componentType, 
                            bufferSize = GetDimensionSize(memberDim), 
                            readOnly = memberDim.Tags.Contains("readOnly")};
                }));
                accumulator[prefabId].AddRange(members);
            }

            cache = accumulator.ToDictionary(x => x.Key, x => x.Value.ToArray());
        }

        private void CacheRuntimeStructures(ModlObjectHandle obj)
        {
            RuntimeMember[] _DoCache(in MemberEntry[] space) => space.Select(x => new RuntimeMember
                {
                    info = x.info,
                    component = obj.GetComponent(x.ComponentType),
                    bufferSize = x.bufferSize,
                    readOnly = x.readOnly,
                })
                .ToArray();
            
            //Builds an instance ID based on the location in the scene hierarchy.
            string GetSceneBasedInstanceID(Transform t)
            {
                var s = new StringBuilder();

                //prepend the objects name (for easier debugging):
                s.Append(t.gameObject.name);
                s.Append("_");
                
                //prepend the scene it belongs to (hierarchy uniqueness only guaranteed per scene):
                s.Append(t.gameObject.scene.name);
                s.Append("_");

                //Then add the scene hierarchy indices, from leaf to root.
                while (t != null)
                {
                    s.Append(t.GetSiblingIndex());
                    s.Append("_");
                    t = t.parent;
                }

                return s.ToString();
            }
            
            //Get scene based ID
            var id = GetSceneBasedInstanceID(obj.transform);
            //Store it on the ModlObjectHandle in case the scene changes over time.
            obj.sceneBasedID = id;
            
            /*TODO: if the id already exists this should check the tracked object is available (i.e. not null)
             *   and if it is, check the GetInstanceID() is the same (this would indicate an object of the same type
             *   has been instantiated in the same place in the hierarchy).
             *   Then we should append the GetInstanceID(), as the object was not available at load time.
             */
            if (_prefabsTrackedObjects.ContainsKey(obj.parentReference)) _runtimeTrackedObjects[id] = _DoCache(_prefabsTrackedObjects[obj.parentReference]);
            if (_prefabsTrackedActions.ContainsKey(obj.parentReference)) _runtimeTrackedActions[id] = _DoCache(_prefabsTrackedActions[obj.parentReference]);
            if (_prefabsTrackedFeatures.ContainsKey(obj.parentReference)) _runtimeTrackedFeatures[id] = _DoCache(_prefabsTrackedFeatures[obj.parentReference]);
            _runtimeObjectPrefabParent[id] = obj.parentReference;
        }

        #endregion
        
        #region Helper Structs

        private struct MemberEntry
        {
            public MemberInfo info; 
            public Type ComponentType;
            public int bufferSize;
            public bool readOnly;
        }
        
        public struct RuntimeMember
        {
            public MemberInfo info;
            public Component component;
            public int bufferSize;
            public bool readOnly;
        }
        
        #endregion
    }
}
