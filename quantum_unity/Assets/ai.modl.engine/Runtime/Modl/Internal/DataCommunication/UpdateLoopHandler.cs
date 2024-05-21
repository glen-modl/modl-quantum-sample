using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Modl.Internal.RuntimeData;
using Modl.Proto;
using UnityEngine;
using Enum = System.Enum;

namespace Modl.Internal.DataCommunication
{
    public class UpdateLoopHandler
    {
        private readonly DataHandler _dataHandler;

        private float[] _actions; 

        public UpdateLoopHandler(GameConfig config)
        {
            _dataHandler = new DataHandler(config);

            _actions = new float[ConversionUtils.GetDimensionSize(config.ActionSpace)];
        }
        
        public void InitializeFirstFrame(Initialization initMsg)
        {
            Time.timeScale = initMsg.GameSpeed;
        }

        public void GetObservationForFrame(Observation inPayload)
        {
            //inPayload.FrameNumber = Time.frameCount;

            inPayload.Objects.Add(_dataHandler.GetFrameData());

            var features = _dataHandler.GetFrameFeatures();
            
            inPayload.Features.Add( new FeatureVector { Values = { features }});
            inPayload.Actions.Add( new ActionVector{ Values = { _actions.Select(item => Value.ForNumber( item )) }});
            inPayload.Sensors.Add(new SensorVector());

            var events = EventData.GetCachedEvents();

            foreach (var ev in events) {
                var evPayload = new Modl.Proto.Event
                    {
                        //TODO: Re-enable or change this once we re-design how events are defined in the game_config, after GDC 2023.
                        Id = "-1",//_eventIds[ev.Name];
                        Name = ev.Name,
                        Timestamp = ev.Timestamp.ToUnixTime(),
                    };
                evPayload.Payload.Add(ev.Payload.Select(Payload2EventGenValue) );
                
                inPayload.Events.Add(evPayload);
            }
        }

        public void ApplyCommandForFrame(Command outPayload)
        {
            if (outPayload.Actions.Count > 0)
            {
                _actions = outPayload.Actions.First().Values.Select(item => float.Parse(item.NumberValue.ToString())).ToArray();
                _dataHandler.ApplyActions(_actions);
            }

            //_dataHandler.Update(outPayload.Objects, _actions);
            if (outPayload.Objects.Count <= 0) return;
            _dataHandler.ApplyFrameData(outPayload.Objects);
            
            var observationValues = new List<LoadStateData>(); 
            foreach (var obj in outPayload.Objects)
            {
                var trackedFields = _dataHandler.GetRuntimeTrackedObjects[obj.RuntimeId];
                    
                var dataBuffer = obj.Values;
                var dataIdx = 0;
                
                foreach (var info in trackedFields)
                {
                    //TODO: Consider filtering out all non-readOnly values.
                        
                    var val = ConversionUtils.GetObsObjectValue(dataBuffer.Skip(dataIdx).Take(info.bufferSize).ToArray(), info.info);
                    observationValues.Add(new LoadStateData
                    {
                        sceneBasedObjectID = obj.RuntimeId,
                        readOnly = info.readOnly,
                        runtimeComponent = info.component,
                        memberInfo = info.info,
                        value = val
                    });
                        
                    dataIdx += info.bufferSize;
                }
            }
                
            ModlPluginManager.Instance.LoadStateSignal(observationValues);
        }

        public void TrackObject(ModlObjectHandle handle) => _dataHandler.TrackObject(handle);
        public void UntrackObject(ModlObjectHandle handle) => _dataHandler.UntrackObject(handle);
        
        // FIXME: this seems to be running the check that we're also doing in EventReporter.cs
        private static Value Payload2EventGenValue(object val)
        {
            switch (val)
            {
                case float  f: return Value.ForNumber(f);
                case double d: return Value.ForNumber(d);
                case decimal d: return Value.ForString(d.ToString());
                case int  i: return Value.ForNumber(i);
                case uint  i: return Value.ForNumber(i);
                case short  i: return Value.ForNumber(i);
                case ushort  i: return Value.ForNumber(i);
                case long  i: return Value.ForNumber(i);
                case ulong  i: return Value.ForNumber(i);
                case bool  b: return Value.ForBool(b);
                case Enum e: return Value.ForString(Enum.GetName(e.GetType(), e));
                case string s: return Value.ForString(s);
                default: throw new ArgumentException("Payload type not supported");
            }
        }
    }
}
