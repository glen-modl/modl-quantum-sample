using System;
using System.Collections.Generic;
using UnityEngine;

namespace Modl.Internal
{
    public struct EventData
    {
        public string   Name        { get; }
        public DateTime Timestamp   { get; }
        public object[] Payload     { get; }

        private EventData(string name, DateTime timestamp, object[] payload)
        {
            Timestamp = timestamp;
            Name      = name;
            Payload   = payload;
        }
        
        
        #region static Cache

        private static readonly List<EventData> EventCache = new List<EventData>();

        public static void CreateEventLogItAndAddToCache(string name, DateTime time, object[] payload)
        {
            //NOTE: The platform currently depends on this Log call
            var payloadString = (payload.Length > 0 ? ";" : "") + string.Join(";", payload);
            Debug.Log($"ModlEvent;{Time.frameCount};{DateTime.Now};{name}{payloadString}");
            
            EventCache.Add(new EventData(name, time, payload));
        }

        public static IEnumerable<EventData> GetCachedEvents()
        {
            var ret =  EventCache.ToArray();
            EventCache.Clear();
            return ret; 
        }

        #endregion
    }
}