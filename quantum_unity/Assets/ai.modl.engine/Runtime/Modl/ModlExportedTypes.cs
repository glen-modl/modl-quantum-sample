using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Modl.Internal;

namespace Modl.ExportedTypes
{
    public class LoadedEntry
    {
        public MemberInfo memberInfo; 
        public object value;
        public bool markedReadOnly;
    }
    
    public class ModlLoadStateData
    {
        private readonly IEnumerable<LoadStateData> _data;

        public IEnumerable<string> GetAllObjectIDs() => _data
            .Select(item => item.sceneBasedObjectID);
        
        public IEnumerable<Component> GetAllComponents() => _data
            .Select(item => item.runtimeComponent);

        public IEnumerable<Component> GetComponentsForGameObject(string sceneBasedObjectID) => _data
            .Where(item => item.sceneBasedObjectID == sceneBasedObjectID)
            .Select(item => item.runtimeComponent);

        public IEnumerable<Component> GetComponentsForGameObject(GameObject gameObject) => _data
            .Where(item => item.runtimeComponent.gameObject == gameObject)
            .Select(item => item.runtimeComponent);
        
        public IEnumerable<LoadedEntry> GetLoadedEntries(Component component)
        {
            return _data
                .Where(item => item.runtimeComponent == component)
                .Select(loadStateData => new LoadedEntry {memberInfo = loadStateData.memberInfo, value = loadStateData.value, markedReadOnly = loadStateData.readOnly});
        }
        
        public LoadedEntry GetLoadedEntry(Component component, string memberName)
        {
            var first = _data.FirstOrDefault(item => item.runtimeComponent == component && item.memberInfo.Name == memberName);

            if (first == null) return null;
            
            return new LoadedEntry {memberInfo = first.memberInfo, value = first.value, markedReadOnly = first.readOnly};
        }
        
        /// <summary>
        /// Internal constructor to transform the Modl.Internal.LoadStateData struct into the exported type.
        /// </summary>
        internal ModlLoadStateData(IEnumerable<LoadStateData> data)
        {
            _data = data;
        }
    }
}