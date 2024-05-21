
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Modl.Internal.DataCommunication;
using Modl.Internal.Utils;
using Modl.Proto;
using UnityEditor;

namespace Modl.Internal {
    /// <summary>
    /// The manager for our plugin system. It is responsible of the main flow of whole plugin
    /// </summary>
    public class ModlPluginManager : Singleton<ModlPluginManager>
    {
       // Used to decide which communicator to use: ConfigValidator or socket/Brain!
        private const string CommunicatorPref = "MODL_COMM_TYPE";
        public const string BrainVersion = "11.2.0";

        public enum ModlCommunicatorType
        {
            None = 0,
            ConfigValidation = 1,
            Brain          = 2
        }
        
#if !UNITY_EDITOR
        // When not in the editor, always default to using the Brain.
        public const int CommunicatorPrefState = (int)ModlCommunicatorType.Brain;
#else
        public static int CommunicatorPrefState
        {
            get => UnityEditor.EditorPrefs.GetInt(CommunicatorPref, (int)ModlCommunicatorType.None);
            set => UnityEditor.EditorPrefs.SetInt(CommunicatorPref, value);
        }
        
        private const string ValidationModePref = "MODL_COMM_VALIDATION_TYPE";
        public static int ValidationModePrefState
        {
            get => UnityEditor.EditorPrefs.GetInt(ValidationModePref, (int)CommunicatorConfigValidation.ValidationSteps.Everything);
            set => UnityEditor.EditorPrefs.SetInt(ValidationModePref, value);
        }
#endif
      
#region Singleton Static bool handling 
        public static bool ApplicationIsQuitting => applicationIsQuitting;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnEnterPlayMode]
        private static void ResetApplicationIsQuitting()
        {
            // This ensures that the singleton gets created when starting and stopping playmode in the editor.
            applicationIsQuitting = false;
        }
#endif
        
#endregion
        
        private ModlDebugMessage _inGameLogger;
        public ModlDebugMessage inGameLogger
        {
            get
            {
                if (_inGameLogger == null)
                {
                    var go = new GameObject("ModlDebugMessageLogger", typeof(ModlDebugMessage));
                    go.transform.parent = transform;
                    _inGameLogger = go.GetComponent<ModlDebugMessage>();
                }

                return _inGameLogger;
            }
        }

        // must be cached while we figure out how to solve mem deallocation problem
        private GameConfig _config;
        private RuntimeFileSystemInterface _FS;
        private ICommunicator _Comm;
        private UpdateLoopHandler _ULH;

        private IObservationConsumer _obsConsumer;

        private bool _transmitting;
        private bool _paused;

        private float _heartBeatInterval;
        private float _timeSinceLastHeartbeat;
        
        private const bool SEND_LOW_FPS_EVENTS = false;
        private const bool SEND_AVG_FPS_DATA = false;
        private const int FRAMES_FOR_FPS_ROLLING_AVG = 10;
        private Queue<float> _rollingFrameDurations;
        private bool _startTriggeringFPSEvents = false;

        private Action<List<LoadStateData>> _onLoadState;
        public event Action<List<LoadStateData>> OnLoadState
        {
            add
            {
                if (_onLoadState == null || !_onLoadState.GetInvocationList().Contains(value))
                {
                    _onLoadState += value;
                }
            }
            remove => _onLoadState -= value;
        }

        /// <summary>
        /// Prepare all the different modules in our plugin to be used when the game starts
        /// Subscribes to Editor's state changes.
        /// </summary>
        private void Awake()
        {
            _FS = new RuntimeFileSystemInterface();
            _config = _FS.ReadConfigFile();
            _Comm =  CommunicatorFactory();
            _ULH = new UpdateLoopHandler(_config);

            _obsConsumer = ObservationConsumerFactory.Create();
            _obsConsumer.Initialize();

            _rollingFrameDurations = new Queue<float>(FRAMES_FOR_FPS_ROLLING_AVG);
        }
        
        //TODO: implement communication interface in the plugin, so we can pass different types of communicators.
        private ICommunicator CommunicatorFactory()
        {
#if !UNITY_EDITOR
            //Always add the socket communicator when not running in the editor!
            return new CommunicatorSocket();
#else
            switch (CommunicatorPrefState)
            {
                case (int)ModlCommunicatorType.None:
                    return null;
                case (int)ModlCommunicatorType.Brain:
                    return new CommunicatorSocket();

                case (int)ModlCommunicatorType.ConfigValidation:
                    return new CommunicatorConfigValidation(_config, (CommunicatorConfigValidation.ValidationSteps)ValidationModePrefState);

                default:
                    throw new ArgumentException();
            }
#endif
        }


        private void LateUpdate()
        {
            if (_transmitting)
            {
                //Checks heartbeat and tracks Avg. and Low FPS
                if (ShouldHeartbeat(Time.unscaledDeltaTime))
                {
                    ModlRuntimeUpdate();
                }
            }
        }

        public override void OnDestroy()
        {
            _Comm?.Close();
            base.OnDestroy();
        }

        /// <summary>
        /// Starts transmitting data to the backend
        /// </summary>
        [ContextMenu("Start transmitting")]
        public void StartTransmitting()
        {
            if (_Comm == null) return;
            
            if (_paused)
            {
                _paused = false;
                _transmitting = true;
                return;
            }
            
            if (_transmitting) return;

            //NOTE: actually listen to the connection!
            bool result = _Comm.Connect();
            
            _transmitting = result;
            if (_transmitting)
            {
                //MBR.ResetLoop()
                var initMsg = _Comm.ReceiveInit();
                _ULH.InitializeFirstFrame(initMsg);
                _heartBeatInterval = initMsg.HeartbeatInterval;
            }
            else
            {
                Debug.LogError("Could not connect to the Modl backend!");
            }
        }

        [ContextMenu("Start transmitting", true)]
        private bool CanStartTransmission()
        {
            return !_transmitting || _paused;
        }
        
        /// <summary>
        /// Makes an Observation and marks it as a terminal state,
        /// which will trigger the brain to send a LOAD command with a previous game state back.
        /// </summary>
        public void TerminalStateUpdate()
        {
            if (_transmitting)
            {
                /* if we change to async, this has to force a "sync" wait*/
                ModlRuntimeUpdate(true);
            }
        }

        private void ModlRuntimeUpdate(bool isTerminal = false)
        {
            var InFrame = new Observation();
            _ULH.GetObservationForFrame(InFrame);

            InFrame.Terminal = isTerminal;
            
            //According to http://unity3d.com/support/documentation/ScriptReference/MonoCompatibility Guid.NewGuid() should be available everywhere...
            InFrame.Id = Guid.NewGuid().ToString();

            if (!_Comm.Send(InFrame))
            {
                //TODO: What? There was an error sending out our data - How to proceed?
                Debug.LogError("Error sending Observation to the Brain");
            }

            { // Receive loop - TODO: Move into it's own method, to make this setup asynchronous
                
                //TODO: Check if communication data from the brain is available
                // Action Application
                // Receive Actions from the brain and apply them to the Game
                Command receiveCommand = _Comm.ReceiveCommand();
                _ULH.ApplyCommandForFrame(receiveCommand);

                { // SQS logging part of the loop - TODO: if we store the latest action set, we could do this right after sending the observation to the brain!

                    if (receiveCommand.Type is Command.Types.Type.Load)
                    {
                        // Adds an event to signal a LOAD command
                        var loadEvent = CreateLoadEvent(receiveCommand.ObsId);
                        InFrame.Events.Add(loadEvent);
                    }

                    _obsConsumer.OnObservation(InFrame);
                    
                    if (receiveCommand.Type is Command.Types.Type.Shutdown)
                    {
                        Debug.Log("ModlPluginManager: Received SHUTDOWN from the Brain, initiating shut down!");
                        StartCoroutine(ShutDown());
                    }
                }
            }
        }

        private IEnumerator ShutDown()
        {
            // Logs the state of the consumer right before performing the shutdown
            Debug.Log($"ModlPluginManager: Pausing transmissions and shutting down ObservationConsumer: {_obsConsumer}");
            PauseTransmitting();
            _obsConsumer.Deinitialize();
            yield return new WaitForConsumer(_obsConsumer);
            
            Debug.Log($"ModlPluginManager: Shutting down the game!");
#if UNITY_EDITOR
            EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        private Proto.Event CreateLoadEvent(string obsId)
        {
            var protoStringValue = Google.Protobuf.WellKnownTypes.Value.ForString(obsId);
            var loadEvent = new Proto.Event
            {
                Timestamp = DateTime.UtcNow.ToUnixTime(),
                Id = "-1",
                Name = "MODL_DEFAULT::LOAD",
            };
            loadEvent.Payload.Add(protoStringValue);
            return loadEvent;
        }

        private bool ShouldHeartbeat(float deltaTime)
        {
            var isHeartBeat = false;
            
            _timeSinceLastHeartbeat += deltaTime;
            if (_timeSinceLastHeartbeat > _heartBeatInterval)
            {
                isHeartBeat = true;
                _timeSinceLastHeartbeat = 0;
            }

            if (SEND_LOW_FPS_EVENTS || SEND_AVG_FPS_DATA)
            {
                _rollingFrameDurations.Enqueue(deltaTime);
                if (_rollingFrameDurations.Count == FRAMES_FOR_FPS_ROLLING_AVG)
                {
                    var rollingAvgFrameDuration = _rollingFrameDurations.Average();
                    var rollingAvgFps = 1.0f / rollingAvgFrameDuration;

                    if (rollingAvgFps >= _config.MinFps && !_startTriggeringFPSEvents)
                    {
                        _startTriggeringFPSEvents = true;
                    }

                    if (_startTriggeringFPSEvents)
                    {
                        if (SEND_LOW_FPS_EVENTS && rollingAvgFps < _config.MinFps)
                        {
                            EventData.CreateEventLogItAndAddToCache("MODL_DEFAULT::LowFPS", DateTime.Now, new object[]{rollingAvgFps});
                        }

                        if (SEND_AVG_FPS_DATA && isHeartBeat)
                        {
                            EventData.CreateEventLogItAndAddToCache("MODL_DEFAULT::AvgFPS", DateTime.Now, new object[]{rollingAvgFps});
                        }
                    }
                    // dequeue to make room for the next value
                    _rollingFrameDurations.Dequeue();
                }
            }

            return isHeartBeat;
        }
        
        /// <summary>
        /// Stops transmitting data to the backend
        /// (can be resumed calling <see cref="StartTransmitting"/> again)
        /// </summary>
        /// <remarks>
        /// NotYetImplemented
        /// </remarks>
        [ContextMenu("Pause transmitting")]
        public void PauseTransmitting()
        {
            if (_transmitting)
            {
                _transmitting = false;
                _paused = true;
            }
        }

        [ContextMenu("Pause transmitting", true)]
        private bool CanPause()
        {
            return _transmitting && !_paused;
        }

        public bool IsTransmitting => _transmitting;
        public bool IsPaused => _paused;

        public void LoadStateSignal(List<LoadStateData> loadObservationValues)
        {
            _onLoadState?.Invoke(loadObservationValues);
        }
        
        public void TrackObject(ModlObjectHandle handle) => _ULH.TrackObject(handle);
        public void UntrackObject(ModlObjectHandle handle) => _ULH.UntrackObject(handle);
    }
}
