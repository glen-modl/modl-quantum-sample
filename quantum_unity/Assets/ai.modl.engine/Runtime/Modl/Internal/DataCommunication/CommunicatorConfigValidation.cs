using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Modl.Proto;
using UnityEngine;
using Random = UnityEngine.Random;

using static Modl.Internal.RuntimeData.ConversionUtils;

namespace Modl.Internal.DataCommunication
{
    public class CommunicatorConfigValidation : ICommunicator
    {
        private const int ARCHIVE_SIZE = 2000;
        private const int VALIDATION_TEST_EXPLORATION_STEPS = 5;  //5 seconds of 1s Heartbeats.
        private const int LOAD_FREQ = 10;
        
        private readonly ValueRange _objectSpace;
        private readonly ValueRange _actionSpace;
        private readonly ValueRange _featureSpace;
        private readonly List<RepeatedField<ObjectVector>> _archive;
        private RepeatedField<FeatureVector> _latestFeatureSet;
        private RepeatedField<FeatureVector> _previousFeatureSet;
        
        private RepeatedField<ObjectVector> _tmp;
        private int _currHeartbeat;
        private ValidationSteps _currentValidationStep;
        private int _currentDimension;
        private int _errorCounter;

        public enum ValidationSteps
        {
            ActionSpaceDimensions,
            ObjectSpaceDimensions,
            FeatureSpaceDimensions,
            Everything
        }

        public CommunicatorConfigValidation(GameConfig config, ValidationSteps validationStep = ValidationSteps.Everything)
        {
            _actionSpace = config.ActionSpace;
            _objectSpace = config.ObjectSpace;
            _featureSpace = config.FeatureSpace;
            
            _currentValidationStep = validationStep;
            ShowValidationStepMessage();

            _archive = new List<RepeatedField<ObjectVector>>();
            _latestFeatureSet = new RepeatedField<FeatureVector>();
            _previousFeatureSet = new RepeatedField<FeatureVector>();
            
#if UNITY_EDITOR
            //Ensure that the ModlPluginManager starts transmission for validation purposes,
            //in case the developer hasn't called ModlPublicController.Start() in their code.
            UnityEditor.EditorApplication.delayCall += () => { ModlPluginManager.Instance.StartTransmitting(); };
#endif
        }

        public bool Connect() => true;

        public void Close()
        {   
            //DO Nothing
        }

        public Initialization ReceiveInit() => new Initialization() { GameSpeed = 1, HeartbeatInterval = 1f };

        public bool Send(Observation observation)
        {
            Debug.Log("Pretending to Send Observations");

            _previousFeatureSet = _latestFeatureSet;
            _latestFeatureSet = observation.Features;
            
            _archive.Add(new RepeatedField<ObjectVector> {observation.Objects} );
            if (_archive.Count > ARCHIVE_SIZE)
                _archive.RemoveAt(0);

            return true;
        }
        
        public Command ReceiveCommand()
        {
            switch (_currentValidationStep)
            {
                case ValidationSteps.ActionSpaceDimensions: return DoActionSpaceValidation();
                case ValidationSteps.ObjectSpaceDimensions: return DoObjectSpaceValidation();
                case ValidationSteps.FeatureSpaceDimensions: return DoFeatureSpaceValidation();
                case ValidationSteps.Everything:            return SimulateSampleAndLoad();
                default:                                    throw new ArgumentOutOfRangeException();
            }
        }

        private Command SimulateSampleAndLoad()
        {
            var actions = new RepeatedField<ActionVector>();
            var obs = new RepeatedField<ObjectVector>();

            if (GetDimensionSize(_actionSpace) == 0)
            {
                if (_errorCounter == 0)
                {
                    ModlPluginManager.Instance.inGameLogger.ShowMessage("==== NO ACTIONS DEFINED ====" +
                                                                        "\nPlease define bot input before previewing.",
                        fadeTime: 10f, logType: LogType.Error);
                }

                _errorCounter++;
            }
            else if (GetDimensionSize(_featureSpace) == 0)
            {
                if (_errorCounter == 0)
                {
                    ModlPluginManager.Instance.inGameLogger.ShowMessage("==== NO EXPLORATION VALUES DEFINED ====" +
                                                                        "\nPlease define bot exploration values before previewing.",
                        fadeTime: 10f, logType: LogType.Error);
                }

                _errorCounter++;
            }
            else if (GetDimensionSize(_objectSpace) == 0)
            {
                if (_errorCounter == 0)
                {
                    ModlPluginManager.Instance.inGameLogger.ShowMessage("==== NO SAMPLE VALUES DEFINED ====" +
                                                                        "\nPlease define state tracking variables before using a State Sampling bot.",
                        fadeTime: 10f, logType: LogType.Error);
                }

                _errorCounter++;
            }
            else if (_archive.Count > 0 && _archive[0].Count == 0)
            {
                if (_errorCounter == 0)
                {
                    ModlPluginManager.Instance.inGameLogger.ShowMessage("==== NO SAMPLED VALUES FOUND ====" +
                                                                        "\nPlease ensure you're tracking variables in the scene before using a State Sampling bot.",
                        fadeTime: 10f, logType: LogType.Error);
                }
                
                _errorCounter++;
            }
            
#if UNITY_EDITOR
            if(_errorCounter > 5){
                UnityEditor.EditorApplication.ExitPlaymode();
            }
#endif

            var load = _currHeartbeat % LOAD_FREQ == 0;
            var cmdString = load ? "LOAD" : "ACT";
            Debug.Log($"Pretending to Receive\t{cmdString}");

            if (load)
            {
                obs = _archive[Random.Range(0, _archive.Count - 1)];
            }
            else
            {
                actions = new RepeatedField<ActionVector> {SampleAction(_actionSpace)};
            }

            UpdateHeartbeatAndValidationStep();
            
            
            
            return new Command(MakeFramePayload(actions, obs, load ? Command.Types.Type.Load : Command.Types.Type.Act));
        }

        private Command DoObjectSpaceValidation()
        {
            var obs = new RepeatedField<ObjectVector>();
            
            //Sample the actions
            var actions = new RepeatedField<ActionVector> { SampleAction(_actionSpace) };
            
            //First few times we're in here, just perform the action and ignore the test.
            if (_archive.Count < 5)
            {
                return new Command(MakeFramePayload(actions, obs, Command.Types.Type.Act));
            }
            
            var randObs = _archive[Random.Range(0, _archive.Count - 1)];
            //TODO: do some length checking here!
            var obsNode = randObs[_currentDimension];
            
            if (obsNode != null && GetDimensionSize(_objectSpace) > 0)
            {
                var dimension = GetObjectFromSpace(_objectSpace, obsNode.Id);
                //Using number of objects as dimension size, instead of number of fields.
                var dimensionSize = randObs.Count;
                
                obs.Add(obsNode);
                
                //Skip FPSTracker object.
                if (dimension.Name == "FPSTracker" || dimension.Name == "ModlWaypoint")
                {
                    ModlPluginManager.Instance.inGameLogger.ShowMessage(
                        $"==== Skipping load for {dimension.Name} ====\n");
                    _currentDimension++;
                    _currHeartbeat = 0;
                    UpdateHeartbeatAndValidationStep(dimensionSize);
                    return new Command(MakeFramePayload(actions, obs, Command.Types.Type.Act));
                }

                var bufferSize = GetDimensionSize(dimension);

                var trackedFields = "Tracked fields:\n";
                for (int i = 0; i < bufferSize; i++)
                {
                    trackedFields += $"{DimensionToString(dimension, i)}\n";
                }
                
                
                ModlPluginManager.Instance.inGameLogger.ShowMessage(
                    $"==== Testing observation load ====\n" +
                    $"{obsNode.RuntimeId}\n" +
                    $"See values console logs.",
                    $"Testing observation load [{obsNode.RuntimeId}].\n" +
                    $"See Fields and Values below:\n" +
                    $"\n" +
                    $"{trackedFields}\n" +
                    $"Values:\n" +
                    $"{string.Join(",\n", obsNode.Values.Select(item => item.KindCase == Value.KindOneofCase.NumberValue ? $"{item.NumberValue}" : item.KindCase == Value.KindOneofCase.StringValue ? item.StringValue : item.KindCase == Value.KindOneofCase.ListValue ? $"List[{string.Join(",", item.ListValue)}]" : "Value type not set"))}\n",
                    Camera.main != null ? Camera.main.gameObject : null);

                UpdateHeartbeatAndValidationStep(dimensionSize);
            }
            else
            {
                if (_errorCounter == 0 && randObs.Count > 0 && obsNode != null)
                {
                    ModlPluginManager.Instance.inGameLogger.ShowMessage("==== NO SAMPLE VALUES DEFINED ====" +
                                                                        "\nPlease define state tracking variables before using a State Sampling bot.",
                                                                        fadeTime: 10f, logType: LogType.Error);
                }
                else if (_errorCounter == 0)
                {
                    ModlPluginManager.Instance.inGameLogger.ShowMessage("==== NO SAMPLED VALUES FOUND ====" +
                                                                        "\nPlease ensure you're tracking variables in the scene before using a State Sampling bot.",
                        fadeTime: 10f, logType: LogType.Error);
                }

                _errorCounter++;
                
#if UNITY_EDITOR
                if(_errorCounter > 5){
                    UnityEditor.EditorApplication.ExitPlaymode();
                }
#endif
            }
            
            return new Command(MakeFramePayload(actions, obs, Command.Types.Type.Load));
        }

        private Command DoActionSpaceValidation()
        {
            var obs = new RepeatedField<ObjectVector>();

            var dimensionSize = GetDimensionSize(_actionSpace);
            var actions = new RepeatedField<ActionVector>();

            if (dimensionSize > 0)
            {
                var sampledActions = SampleAction(_actionSpace);
                var action = sampledActions.Values.ElementAt(_currentDimension);
                var actionIdentifier = DimensionToString(_actionSpace, _currentDimension, true);

                ModlPluginManager.Instance.inGameLogger.ShowMessage(
                    $"==== Testing bot input configuration ====\n" +
                    $"{actionIdentifier}\n" +
                    $"Value=[{action}]",
                    $"Testing input [{actionIdentifier}] with value ({action})", Camera.main != null ? Camera.main.gameObject : null);

                var actionVector = new ActionVector();
                for (int i = 0; i < dimensionSize; i++)
                {
                    actionVector.Values.Add(new Value(Value.ForNumber(0)) );    
                }
                
                actionVector.Values[_currentDimension] = action;
                actions.Add(actionVector);

                UpdateHeartbeatAndValidationStep(dimensionSize);
            }
            else
            {
                if (_errorCounter == 0)
                {
                    ModlPluginManager.Instance.inGameLogger.ShowMessage("==== NO ACTIONS DEFINED ====" +
                                                                        "\nPlease define bot input before previewing.",
                                                                        fadeTime: 10f, logType: LogType.Error);
                }

                _errorCounter++;

#if UNITY_EDITOR
                if (_errorCounter > 5)
                {
                    UnityEditor.EditorApplication.ExitPlaymode();
                }
#endif
            }

            return new Command(MakeFramePayload(actions, obs, Command.Types.Type.Act));
        }
        
        private Command DoFeatureSpaceValidation()
        {
            var actions = new RepeatedField<ActionVector>();
            var obs = new RepeatedField<ObjectVector>();
            
            var cmdString = "ACT";
            Debug.Log($"Pretending to Receive\t{cmdString}");
            
            //Sample all input actions
            actions = new RepeatedField<ActionVector> { SampleAction(_actionSpace) };

            //First time we're in here, just perform the action and ignore the test.
            if (_latestFeatureSet.Count != _previousFeatureSet.Count)
            {
                return new Command(MakeFramePayload(actions, obs, Command.Types.Type.Act));
            }
            
            var dimensionSize = GetDimensionSize(_featureSpace);
            if (dimensionSize > 0)
            {
                var featureIdentifier = DimensionToString(_featureSpace, _currentDimension);
                var latestFeatureValue = _latestFeatureSet.First().Values[_currentDimension];
                var previousFeatureValue = _previousFeatureSet.First().Values[_currentDimension];

                ModlPluginManager.Instance.inGameLogger.ShowMessage(
                    $"==== Testing bot exploration configuration ====\n" +
                    $"{featureIdentifier}\n" +
                    $"Value Change = [{latestFeatureValue - previousFeatureValue}]",
                    $"Comparing [{featureIdentifier}] latest and previous value ({latestFeatureValue} | {previousFeatureValue}). Value Change = [{latestFeatureValue - previousFeatureValue}]", Camera.main != null ? Camera.main.gameObject : null);

                UpdateHeartbeatAndValidationStep(dimensionSize);
            }
            else
            {
                if (_errorCounter == 0)
                {
                    ModlPluginManager.Instance.inGameLogger.ShowMessage("==== NO EXPLORATION VALUES DEFINED ====" +
                                                                        "\nPlease define bot exploration values before previewing.",
                        fadeTime: 10f, logType: LogType.Error);
                }

                _errorCounter++;

#if UNITY_EDITOR
                if (_errorCounter > 5)
                {
                    UnityEditor.EditorApplication.ExitPlaymode();
                }
#endif
            }
            
            return new Command(MakeFramePayload(actions, obs, Command.Types.Type.Act));
        }

        private void ShowValidationStepMessage()
        {
            switch (_currentValidationStep)
            {
                case ValidationSteps.Everything:
                    ModlPluginManager.Instance.inGameLogger.ShowMessage("==== Testing everything at once ====" +
                                                                        "\nExit 'Playmode' to end the test at anytime.", fadeTime:10f);
                    break;
                case ValidationSteps.ObjectSpaceDimensions:
                    ModlPluginManager.Instance.inGameLogger.ShowMessage("==== Testing value sampling configuration ====" +
                                                                        "\nExit 'Playmode' to end the test at anytime.", fadeTime:10f);
                    break;
                case ValidationSteps.ActionSpaceDimensions:
                    ModlPluginManager.Instance.inGameLogger.ShowMessage("==== Testing bot input configuration ====" +
                                                                        "\nExit 'Playmode' to end the test at anytime.", fadeTime:10f);
                    break;
                case ValidationSteps.FeatureSpaceDimensions:
                    ModlPluginManager.Instance.inGameLogger.ShowMessage("==== Testing exploration configuration ====" +
                                                                        "\nExit 'Playmode' to end the test at anytime.", fadeTime:10f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool UpdateHeartbeatAndValidationStep(int dimensionSize = 0)
        {
            _currHeartbeat++;
            if (_currentValidationStep != ValidationSteps.Everything &&
                _currHeartbeat % VALIDATION_TEST_EXPLORATION_STEPS == 0)
            {
                _currentDimension++;
            }

            if (_currentValidationStep == ValidationSteps.Everything || _currentDimension < dimensionSize)
            {
                return false;
            }
            
            //Once all dimensions of the currentValidationStep has been tested, start over
            _currentDimension = 0;
            _currHeartbeat = 0; 
            ModlPluginManager.Instance.inGameLogger.ShowMessage("==== All parameters have been tested, starting over ====" +
                                                                "\nExit 'Playmode' to end the test at anytime.", fadeTime:10f);
            return true;
        }
        
        public static Command MakeFramePayload(RepeatedField<ActionVector> actions, RepeatedField<ObjectVector> obs, Command.Types.Type type)
        {
            var act = new Command
            {
                Type = type,
                Actions = { actions },
                Objects = { obs }
            };
            return act;
        }
    }
}