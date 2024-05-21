using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Modl.Internal;

namespace Modl
{
    /// <summary>
    /// Reports events to the modl AI Engine.
    /// </summary>
    public static class EventReporter
    {
        private static readonly Type[] AllowedTypes = { typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(string), typeof(float), typeof(bool), typeof(double), typeof(Enum) };

        /// <summary>
        /// Reports the Event to the modl AI Engine, including every provided payload object.
        /// </summary>
        /// <param name="name">The name of the Event</param>
        /// <param name="payload">Contextual data, provided with the Event.</param>
        public static void Report(string name, params object[] payload)
        {
            var onlyAllowedTypes = true;
            Type notAllowedType = null;

            // Only allow an IEnumerable if it is the only payload, but ignore strings
            if (payload.Length == 1 && !(payload[0] is string) && payload[0] is IEnumerable enumerable)
            {
                var arrayType = payload[0].GetType().GetElementType();

                if (AllowedTypes.All(type => type != arrayType))
                {
                    onlyAllowedTypes = false;
                    notAllowedType = arrayType;
                }
                
                if (onlyAllowedTypes)
                {
                    payload = enumerable.Cast<object>().ToArray();
                }
            }
            else
            {
                foreach(var obj in payload)
                {
                    if (AllowedTypes.All(type => type != obj.GetType()))
                    {
                        onlyAllowedTypes = false;
                        notAllowedType = obj.GetType();
                    }
                }              
            }

            if (onlyAllowedTypes)
            {
                //NOTE: Also logs the event to the unity log, which the platform relies on
                EventData.CreateEventLogItAndAddToCache(name, DateTime.Now, payload);
            }
            else
            {
                Debug.LogError($"ModlError;{Time.frameCount};{DateTime.Now};{name}: trying to send not allowed type: {notAllowedType}");
            }
            
        }
    }
}
