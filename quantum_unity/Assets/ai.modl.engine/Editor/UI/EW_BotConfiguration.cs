using Modl.Internal;
using Modl.Internal.DataCommunication;

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditorInternal;
using System.Linq;
using System;
using System.Reflection;
using Modl.Proto;

namespace Modl.Editor.UI
{
    [System.Serializable]
    public class EventDef
    {
        public enum EventTypeVisible
        {
            CUSTOM,
            GLITCH
        }
        public string eventName;
        public EventTypeVisible eventType;
    }
    public class EventDefScriptableObject : ScriptableObject
    {
        public List<EventDef> eventDefList = new List<EventDef>();
    }

    [InitializeOnLoad]
    class DataLossCallbacks
    {
        static DataLossCallbacks()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosing += SceneClosing;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void PromptSave()
        {
            if (EditorWindow.HasOpenInstances<EW_BotConfiguration>())
            {
                EW_BotConfiguration window = (EW_BotConfiguration)EditorWindow.GetWindow(typeof(EW_BotConfiguration));
                window.OnDestroy();
                window.Show();
                window.Repaint();
            }
        }

        static void RefreshWindow()
        {
            if (EditorWindow.HasOpenInstances<EW_BotConfiguration>())
            {
                EW_BotConfiguration window = (EW_BotConfiguration)EditorWindow.GetWindow(typeof(EW_BotConfiguration));
                window.Show();
                window.Repaint();
            }
        }
    
        static void SceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene)
        {
            PromptSave();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state) {
                case PlayModeStateChange.ExitingEditMode:
                    PromptSave();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    RefreshWindow();
                    break;
            }
        }
    }

    static class BotConfigToolTips
    {
        public const string BotPlayer = "Select the object the bot will control around your game, e.g., the player character prefab.";
        public const string BotControl = "The input settings define what the bot controls in the game.";
        public const string InputVariables = "Select the variables the bot will set to control the game, e.g., movement variables.";
        public const string ExplorationBehaviour = "The settings define how the bot explores the game. The bot seeks new states of the exploration space and regularly resets back to visited states to continue exploration from there.";
        public const string ExplorationSpaceDimension = "Select the variables that define the search space the bot will explore. If the bot should explore as many locations on a level as possible, then select the player position as the exploration variable.";
        public const string GameStateVariables = "Select the variables to save when the bot registers a new state in the exploration space, e.g., the player's position or state of doors.\n" +
                                                    "Variables defined in the Exploration Space need also to be added here. ";
        public const string SamplingInterval = "Define the size of the division of the exploration space. The size determines how much the state has to change for the bot to register it as a new state in the exploration space.";
        public const string ReadOnly = "Check if you don't want the value to reset when the bot resets. ";
        public const string Range = "Define the minimum and maximum values the bot can choose between when controlling the input variable.";
    }

    public class EW_BotConfiguration : EditorWindow
    {
        Vector2 scrollPos;
        float windowHeight;
        private const int scrollBuffer = 100;

        ReorderableList botConfigReList;
        ReorderableList explorationSpaceReList;
        ReorderableList sampledStateReList;
        SerializedObject serializedBotConfigObj;
        SerializedProperty botConfigProperty;
        SerializedProperty explorationSpaceProperty;
        SerializedProperty sampledStateProperty;
        SerializedProperty playerObjectProperty;

        public string modlProjectId;

        Rect botConfigHeight;
        Rect explorationSpaceHeight;
        Rect sampledStateHeight;
        Rect playerHeight;
        Rect buttonHeight;

        SerializedObject serializedEventDefObj;
        SerializedProperty eventDefProperty;

        int minFps = 0;

        private GameConfig _config;
        private static bool _configIsDirty;

        private const string startedFromPreviewBotSetup = "PREVIEWING_BOT_SETUP";

        GUIStyle header = new GUIStyle();

        //[MenuItem("modl/Configuration", priority = -20)]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            EW_BotConfiguration window = (EW_BotConfiguration)EditorWindow.GetWindow(typeof(EW_BotConfiguration));
            window.titleContent = new GUIContent("Bot Configuration");
            window.minSize = new Vector2(400, 400);
            window.Show();
        }

        private void OnEnable()
        {
            header.fontSize = 15;
            header.fontStyle = FontStyle.Bold;
            if (EditorGUIUtility.isProSkin)
            {
                header.normal.textColor = Color.white;
            }
            else
            {
                header.normal.textColor = Color.black;
            }

            _config = new RuntimeFileSystemInterface().ReadConfigFile();
            minFps = (int)_config.MinFps;

            modlProjectId = EditorPrefs.GetString("ModlProjectId");

            BotConfigScriptableObject botConfigObj = ScriptableObject.CreateInstance<BotConfigScriptableObject>();
         
            serializedBotConfigObj = new UnityEditor.SerializedObject(botConfigObj);
            DontDestroyOnLoad(serializedBotConfigObj.targetObject); 

            playerObjectProperty = serializedBotConfigObj.FindProperty("playerObject");

            // Bot config section
            botConfigProperty = serializedBotConfigObj.FindProperty("botGameObjectList");
            int actionSpaceRow = 0;

            foreach (var actionSpacePrefab in _config.ActionSpace.Dims)
            {
                string prefabName = actionSpacePrefab.Name;
                bool exists = GlobalObjectId.TryParse(actionSpacePrefab.Id, out var globalObjectId);
                var prefabObj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
                if (exists && prefabObj)
                {
                    BotConfig botConf = new BotConfig();
                    botConf.botObject = prefabObj as GameObject;

                    // do GetVariablesForPrefab to recreate the lists, then set the popups to the index needed?
                    foreach (var actionSpaceComponent in actionSpacePrefab.Dims)
                    {
                        var compId = actionSpaceComponent.Id;
                        string compName = actionSpaceComponent.Name;
                        botConf.botComponent = compName;

                        List<string> componentsAndMembers = new List<string>(); //this is the list used for the popups
                        List<string> components = new List<string>();
                        List<string> componentIds = new List<string>();
                        List<MemberInfo> members = new List<MemberInfo>();
                        List<string> memberTypesList = new List<string>();

                        (componentsAndMembers, members, components, memberTypesList, componentIds) = ConfigurationUtils.GetVariablesForPrefab(prefabObj);

                        foreach (var actionSpaceVar in actionSpaceComponent.Dims)
                        {
                            foreach (var componentAndMember in componentsAndMembers)
                            {
                                string compFromType;
                                int substringIndex;
                                string memberString;

                                (compFromType, substringIndex, memberString) = ConfigurationUtils.SplitComponentAndMember(componentAndMember);

                                if (compFromType == compName && memberString == actionSpaceVar.Name)
                                {
                                    //get index for popup list
                                    botConf.botVariablePath = componentAndMember;
                                    int index = componentsAndMembers.IndexOf(componentAndMember);
                                    botConf.variableInt = index;
                                }
                            }

                            float[] minArray = {0,0,0,0};
                            float[] maxArray = {0,0,0,0};
                            var actionSpaceValues = actionSpaceVar.Dims.ToArray();

                            for (int i = 0; i<actionSpaceValues.Count(); i++)
                            {
                                minArray[i] = actionSpaceValues[i].MinValue;
                                maxArray[i] = actionSpaceValues[i].MaxValue;
                            }
                            Vector4 min = new Vector4(minArray[0], minArray[1], minArray[2], minArray[3]);
                            Vector4 max = new Vector4(maxArray[0], maxArray[1], maxArray[2], maxArray[3]);
                            botConf.min = min;
                            botConf.max = max;

                            botConfigProperty.InsertArrayElementAtIndex(actionSpaceRow);
                            var prop = botConfigProperty.GetArrayElementAtIndex(actionSpaceRow);

                            var botPrefabObjectFromFile = prop.FindPropertyRelative("botObject");
                            var variableIntObjectFromFile = prop.FindPropertyRelative("variableInt");
                            var variableMinObjectFromFile = prop.FindPropertyRelative("min");
                            var variableMaxObjectFromFile = prop.FindPropertyRelative("max");

                            botPrefabObjectFromFile.objectReferenceValue = botConf.botObject;
                            variableIntObjectFromFile.intValue = botConf.variableInt;
                            variableMinObjectFromFile.vector4Value = botConf.min;
                            variableMaxObjectFromFile.vector4Value = botConf.max;


                            serializedBotConfigObj.ApplyModifiedProperties();
                            actionSpaceRow++;

                        }
                    }
                }
                else
                {
                    Debug.LogError("Can't find prefab " + prefabName);
                }
            }

            botConfigReList = new ReorderableList(serializedBotConfigObj, botConfigProperty, true, true, true, true);
            botConfigReList.headerHeight = 0;

            botConfigReList.elementHeightCallback = (botConfigIndex) => { return (EditorGUIUtility.singleLineHeight *2) + 5; };
            botConfigReList.onChangedCallback = (ReorderableList l) => {
                _configIsDirty = true;
            };
            botConfigReList.drawElementCallback = (Rect botConfigRect, int botConfigIndex, bool isActive, bool isFocused) =>
            {
                var element = botConfigReList.serializedProperty.GetArrayElementAtIndex(botConfigIndex);

                var botPrefabObject = element.FindPropertyRelative("botObject");
                var botComponentObject = element.FindPropertyRelative("botComponent");
                var botVariableObject = element.FindPropertyRelative("botVariablePath");
                var variableIntObject = element.FindPropertyRelative("variableInt");
                var botMemberTypeObject = element.FindPropertyRelative("memberType");
                var variableMinObject = element.FindPropertyRelative("min");
                var variableMaxObject = element.FindPropertyRelative("max");

                // Use BotConfigDrawer for the main part of the ui
                EditorGUI.PropertyField(botConfigRect, element, GUIContent.none);

                // Add extra fields
                if (botPrefabObject.objectReferenceValue != null)
                {
                    if (botVariableObject.stringValue != null)
                    {
                        string varTypeString = botMemberTypeObject.stringValue.Split('.').Last();

                        System.Type varType = typeof(string); //needs to be initialized

                        if (System.Type.GetType(botMemberTypeObject.stringValue) != null)
                        {
                            varType = System.Type.GetType(botMemberTypeObject.stringValue);
                        }
                        else
                        {
                            varType = System.Type.GetType(botMemberTypeObject.stringValue +','+ botMemberTypeObject.stringValue.Split('.').First());
                        }
                        var minLabelRect = new Rect(botConfigRect.x + 410, botConfigRect.y - 10, 80, botConfigRect.height);
                        var minVectorRect = new Rect(botConfigRect.x + 480, botConfigRect.y, 150, botConfigRect.height);
                        var maxLabelRect = new Rect(botConfigRect.x + 410, botConfigRect.y + 10, 80, botConfigRect.height);
                        var maxVectorRect = new Rect(botConfigRect.x + 480, botConfigRect.y + 20, 150, botConfigRect.height);
                        GUIContent minLabel = new GUIContent("Min:", BotConfigToolTips.Range);
                        GUIContent maxLabel = new GUIContent("Max:", BotConfigToolTips.Range);

                        // TODO: nicer way to check for multi dimensional values? use reflection?
                        // TODO: use an enum?
                        switch (varTypeString)
                        {
                            case "Boolean":
                            case "String":
                                variableMinObject.vector4Value = new Vector4(0, 0);
                                variableMaxObject.vector4Value = new Vector4(0, 0);
                                //no extra field needed, but these are primitive so they needs its own case
                                break;
                            case "Vector3":
                                EditorGUI.LabelField(minLabelRect, minLabel,  EditorStyles.label);
                                variableMinObject.vector4Value = EditorGUI.Vector3Field(minVectorRect, GUIContent.none, variableMinObject.vector4Value);
                                EditorGUI.LabelField(maxLabelRect, maxLabel,  EditorStyles.label);
                                variableMaxObject.vector4Value = EditorGUI.Vector3Field(maxVectorRect, GUIContent.none, variableMaxObject.vector4Value);
                                break;
                            case "Vector2":
                                EditorGUI.LabelField(minLabelRect, minLabel,  EditorStyles.label);
                                variableMinObject.vector4Value = EditorGUI.Vector2Field(minVectorRect, GUIContent.none, variableMinObject.vector4Value);
                                EditorGUI.LabelField(maxLabelRect, maxLabel,  EditorStyles.label);
                                variableMaxObject.vector4Value = EditorGUI.Vector2Field(maxVectorRect, GUIContent.none, variableMaxObject.vector4Value);
                                break;
                            case "Vector4":
                            case "Quaternion":
                                EditorGUI.LabelField(minLabelRect, minLabel,  EditorStyles.label);
                                variableMinObject.vector4Value = EditorGUI.Vector4Field(minVectorRect, GUIContent.none, variableMinObject.vector4Value);
                                EditorGUI.LabelField(maxLabelRect, maxLabel,  EditorStyles.label);
                                variableMaxObject.vector4Value = EditorGUI.Vector4Field(maxVectorRect, GUIContent.none, variableMaxObject.vector4Value);
                                break;
                            default:
                                float vector1Min = variableMinObject.vector4Value.x;
                                float vector1Max = variableMaxObject.vector4Value.x;
                                EditorGUI.LabelField(minLabelRect, minLabel,  EditorStyles.label);
                                vector1Min = EditorGUI.FloatField(new Rect(botConfigRect.x + 480, botConfigRect.y, 150, EditorGUIUtility.singleLineHeight), GUIContent.none, vector1Min);
                                variableMinObject.vector4Value = new Vector4(vector1Min, 0);
                                EditorGUI.LabelField(maxLabelRect, maxLabel,  EditorStyles.label);
                                vector1Max = EditorGUI.FloatField(new Rect(botConfigRect.x + 480, botConfigRect.y + 20, 150, EditorGUIUtility.singleLineHeight), GUIContent.none, vector1Max);
                                variableMaxObject.vector4Value = new Vector4(vector1Max, 0);
                                break;
                        }
                    }
                }
            };
            botConfigReList.onAddCallback = (ReorderableList l) => {
            var index = l.serializedProperty.arraySize;
            l.serializedProperty.arraySize++;
            l.index = index;
            var element = l.serializedProperty.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("variableInt").intValue = 0;
            _configIsDirty = true;
            };
            

            // exploration space section
            explorationSpaceProperty = serializedBotConfigObj.FindProperty("explorationSpaceList");

            int featureSpaceRow = 0;
            int granularityIndex = 0;

            foreach (var featureSpacePrefab in _config.FeatureSpace.Dims)
            {
                string prefabName = featureSpacePrefab.Name;
                bool exists = GlobalObjectId.TryParse(featureSpacePrefab.Id, out var globalObjectId);
                if (exists)
                {
                    ExplorationSpace explorationSpace = new ExplorationSpace();
                    
                    var prefabObj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
                    explorationSpace.botObject = prefabObj as GameObject;

                    foreach (var featureSpaceComponent in featureSpacePrefab.Dims)
                    {
                        var compId = featureSpaceComponent.Id;
                        string compName = featureSpaceComponent.Name;
                        explorationSpace.botComponent = compName;

                        List<string> componentsAndMembers = new List<string>(); //this is the list used for the popups
                        List<string> components = new List<string>();
                        List<string> componentIds = new List<string>();
                        List<MemberInfo> members = new List<MemberInfo>();
                        List<string> memberTypesList = new List<string>();

                        (componentsAndMembers, members, components, memberTypesList, componentIds) = ConfigurationUtils.GetVariablesForPrefab(prefabObj);

                        foreach (var featureSpaceVar in featureSpaceComponent.Dims)
                        {
                            foreach (var componentAndMember in componentsAndMembers)
                            {
                                string compFromType;
                                int substringIndex;
                                string memberString;

                                (compFromType, substringIndex, memberString) = ConfigurationUtils.SplitComponentAndMember(componentAndMember);

                                if (compFromType == compName && memberString == featureSpaceVar.Name)
                                {
                                    //get index for popup list
                                    explorationSpace.botVariablePath = componentAndMember;
                                    int index = componentsAndMembers.IndexOf(componentAndMember);
                                    explorationSpace.variableInt = index;
                                }
                            }

                            int vectorSize = featureSpaceVar.Dims.Count();

                            if (vectorSize == 0)
                            {
                                // if the variable isn't a vector, just set the vector size to 1 so that the granularity can be stored
                                vectorSize = 1;
                            }

                            float[] granularityArray = {0,0,0,0};

                            for (int i = 0; i<vectorSize; i++)
                            {
                                granularityArray[i] = _config.FeatureGranularity.ToArray()[granularityIndex];
                                granularityIndex++;
                            }
                            Vector4 samplingInterval = new Vector4(granularityArray[0], granularityArray[1], granularityArray[2], granularityArray[3]);

                            explorationSpace.samplingInterval = samplingInterval;

                            explorationSpaceProperty.InsertArrayElementAtIndex(featureSpaceRow);
                            var prop = explorationSpaceProperty.GetArrayElementAtIndex(featureSpaceRow);

                            var botPrefabObjectFromFile = prop.FindPropertyRelative("botObject");
                            var variableIntObjectFromFile = prop.FindPropertyRelative("variableInt");
                            var samplingIntervalObjectFromFile = prop.FindPropertyRelative("samplingInterval");

                            botPrefabObjectFromFile.objectReferenceValue = explorationSpace.botObject;
                            variableIntObjectFromFile.intValue = explorationSpace.variableInt;
                            samplingIntervalObjectFromFile.vector4Value = explorationSpace.samplingInterval;

                            serializedBotConfigObj.ApplyModifiedProperties();
                            featureSpaceRow++;
                        }
                    }
                }
                else
                {
                    Debug.LogError("Can't find prefab " + prefabName);
                }
            }

            explorationSpaceReList = new ReorderableList(serializedBotConfigObj, explorationSpaceProperty, true, true, true, true);
            explorationSpaceReList.headerHeight = 0;

            explorationSpaceReList.onChangedCallback = (ReorderableList l) => {
                _configIsDirty = true;
            };
            explorationSpaceReList.elementHeightCallback = (explorationSpaceIndex) => { return (EditorGUIUtility.singleLineHeight *2) + 5; };
            explorationSpaceReList.drawElementCallback = (Rect explorationSpaceRect, int explorationSpaceIndex, bool isActive, bool isFocused) =>
            {
                var element = explorationSpaceReList.serializedProperty.GetArrayElementAtIndex(explorationSpaceIndex);

                var botPrefabObject = element.FindPropertyRelative("botObject");
                var botComponentObject = element.FindPropertyRelative("botComponent");
                var botVariableObject = element.FindPropertyRelative("botVariablePath");
                var variableIntObject = element.FindPropertyRelative("variableInt");
                var botMemberTypeObject = element.FindPropertyRelative("memberType");
                var samplingIntervalObject = element.FindPropertyRelative("samplingInterval");

                // Use BotConfigDrawer
                EditorGUI.PropertyField(explorationSpaceRect, element, GUIContent.none);

                if (botPrefabObject.objectReferenceValue != null)
                {
                    if (botVariableObject.stringValue != null)
                    {
                        string varTypeString = botMemberTypeObject.stringValue.Split('.').Last();

                        System.Type varType = typeof(string); //needs to be initialized

                        if (System.Type.GetType(botMemberTypeObject.stringValue) != null)
                        {
                            varType = System.Type.GetType(botMemberTypeObject.stringValue);
                        }
                        else
                        {
                            varType = System.Type.GetType(botMemberTypeObject.stringValue +','+ botMemberTypeObject.stringValue.Split('.').First());
                        }
                        var labelRect = new Rect(explorationSpaceRect.x + 410, explorationSpaceRect.y - 10, 80, explorationSpaceRect.height);
                        var vectorRect = new Rect(explorationSpaceRect.x + 480, explorationSpaceRect.y, 150, explorationSpaceRect.height);
                        GUIContent intervalLabel = new GUIContent("Interval:", BotConfigToolTips.SamplingInterval);

                        // TODO: nicer way to check for multi dimensional values? use reflection?
                        // TODO: use an enum
                        switch (varTypeString)
                        {
                            case "Boolean":
                            case "String":
                                //no extra field needed, but these are primitive so they needs its own case
                                break;
                            case "Vector3":
                                EditorGUI.LabelField(labelRect, intervalLabel,  EditorStyles.label);
                                samplingIntervalObject.vector4Value = EditorGUI.Vector3Field(vectorRect, GUIContent.none, samplingIntervalObject.vector4Value);
                                break;
                            case "Vector2":
                                EditorGUI.LabelField(labelRect, intervalLabel,  EditorStyles.label);
                                samplingIntervalObject.vector4Value = EditorGUI.Vector2Field(vectorRect, GUIContent.none, samplingIntervalObject.vector4Value);
                                break;
                            case "Vector4":
                            case "Quaternion":
                                EditorGUI.LabelField(labelRect, intervalLabel,  EditorStyles.label);
                                samplingIntervalObject.vector4Value = EditorGUI.Vector4Field(vectorRect, GUIContent.none, samplingIntervalObject.vector4Value);
                                break;
                            default:
                                float vector1Min = samplingIntervalObject.vector4Value.x;
                                float vector1Max = samplingIntervalObject.vector4Value.x;
                                EditorGUI.LabelField(labelRect, intervalLabel,  EditorStyles.label);
                                vector1Min = EditorGUI.FloatField(new Rect(explorationSpaceRect.x + 480, explorationSpaceRect.y, 150, EditorGUIUtility.singleLineHeight), GUIContent.none, vector1Min);
                                samplingIntervalObject.vector4Value = new Vector4(vector1Min, 0);
                                break;
                        }
                    }
                }
            };
            explorationSpaceReList.onAddCallback = (ReorderableList l) => {
            var index = l.serializedProperty.arraySize;
            l.serializedProperty.arraySize++;
            l.index = index;
            var element = l.serializedProperty.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("variableInt").intValue = 0;
            element.FindPropertyRelative("samplingInterval").vector4Value = Vector4.one;
            _configIsDirty = true;
            };

            // sampled state variables section
            sampledStateProperty = serializedBotConfigObj.FindProperty("sampledStateList");

            int objectSpaceRow = 0;

            foreach (var objectSpacePrefab in _config.ObjectSpace.Dims)
            {
                string prefabName = objectSpacePrefab.Name;
                bool exists = GlobalObjectId.TryParse(objectSpacePrefab.Id, out var globalObjectId);
                if (exists)
                {
                    SampledState sampledState = new SampledState();
                    
                    var prefabObj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
                    sampledState.botObject = prefabObj as GameObject;


                    if (objectSpacePrefab.Tags.Contains(ConfigurationUtils.PlayerTag))
                    {
                        playerObjectProperty.objectReferenceValue = prefabObj;
                    }

                    // do GetVariablesForPrefab to recreate the lists, then set the popups to the index needed?
                    foreach (var objectSpaceComponent in objectSpacePrefab.Dims)
                    {
                        var compId = objectSpaceComponent.Id;
                        string compName = objectSpaceComponent.Name;
                        sampledState.botComponent = compName;

                        List<string> componentsAndMembers = new List<string>(); //this is the list used for the popups
                        List<string> components = new List<string>();
                        List<string> componentIds = new List<string>();
                        List<MemberInfo> members = new List<MemberInfo>();
                        List<string> memberTypesList = new List<string>();

                        (componentsAndMembers, members, components, memberTypesList, componentIds) = ConfigurationUtils.GetVariablesForPrefab(prefabObj);

                        foreach (var objectSpaceVar in objectSpaceComponent.Dims)
                        {
                            foreach (var componentAndMember in componentsAndMembers)
                            {
                                string compFromType;
                                int substringIndex;
                                string memberString;

                                (compFromType, substringIndex, memberString) = ConfigurationUtils.SplitComponentAndMember(componentAndMember);

                                if (compFromType == compName && memberString == objectSpaceVar.Name)
                                {
                                    //get index for popup list
                                    sampledState.botVariablePath = componentAndMember;
                                    int index = componentsAndMembers.IndexOf(componentAndMember);
                                    sampledState.variableInt = index;
                                }
                            }

                            sampledStateProperty.InsertArrayElementAtIndex(objectSpaceRow);
                            var prop = sampledStateProperty.GetArrayElementAtIndex(objectSpaceRow);

                            var botPrefabObjectFromFile = prop.FindPropertyRelative("botObject");
                            var variableIntObjectFromFile = prop.FindPropertyRelative("variableInt");
                            var readOnlyObjectFromFile = prop.FindPropertyRelative("readOnly");

                            botPrefabObjectFromFile.objectReferenceValue = sampledState.botObject;
                            variableIntObjectFromFile.intValue = sampledState.variableInt;
                            if (objectSpaceVar.Tags.Contains(ConfigurationUtils.ReadOnlyTag))
                            {
                                readOnlyObjectFromFile.boolValue = true;
                            }
                            else
                            {
                                readOnlyObjectFromFile.boolValue = false;
                            }

                            serializedBotConfigObj.ApplyModifiedProperties();
                            objectSpaceRow++;
                        }
                    }
                }
                else
                {
                    Debug.LogError("Can't find prefab " + prefabName);
                }
            }

            sampledStateReList = new ReorderableList(serializedBotConfigObj, sampledStateProperty, true, true, true, true);

            sampledStateReList.headerHeight = 0;

            sampledStateReList.onChangedCallback = (ReorderableList l) => {
                _configIsDirty = true;
            };
            sampledStateReList.elementHeightCallback = (sampledStateIndex) => { return (EditorGUIUtility.singleLineHeight *2) + 5; };
            sampledStateReList.drawElementCallback = (Rect sampledStateRect, int sampledStateIndex, bool isActive, bool isFocused) =>
            {
                var element = sampledStateReList.serializedProperty.GetArrayElementAtIndex(sampledStateIndex);

                var botPrefabObject = element.FindPropertyRelative("botObject");
                var botComponentObject = element.FindPropertyRelative("botComponent");
                var botVariableObject = element.FindPropertyRelative("botVariablePath");
                var variableIntObject = element.FindPropertyRelative("variableInt");
                var readOnlyObject = element.FindPropertyRelative("readOnly");

                // Use BotConfigDrawer
                EditorGUI.PropertyField(sampledStateRect, element, GUIContent.none);

                if (botPrefabObject.objectReferenceValue != null)
                {
                    if (botVariableObject.stringValue != null)
                    {
                        var labelRect = new Rect(sampledStateRect.x + 410, sampledStateRect.y - 10, 80, sampledStateRect.height);
                        var toggleRect = new Rect(sampledStateRect.x + 480, sampledStateRect.y - 10, 50, sampledStateRect.height);

                        EditorGUI.LabelField(labelRect, new GUIContent("Read Only:", BotConfigToolTips.ReadOnly),  EditorStyles.label);

                        readOnlyObject.boolValue = EditorGUI.Toggle(toggleRect, readOnlyObject.boolValue);
                    }
                }
            };
            sampledStateReList.onAddCallback = (ReorderableList l) => {
            var index = l.serializedProperty.arraySize;
            l.serializedProperty.arraySize++;
            l.index = index;
            var element = l.serializedProperty.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("variableInt").intValue = 0;
            _configIsDirty = true;
            };
            
            //event defs
            EventDefScriptableObject eventDefListObj = ScriptableObject.CreateInstance<EventDefScriptableObject>();
         
            serializedEventDefObj = new UnityEditor.SerializedObject(eventDefListObj);
            DontDestroyOnLoad(serializedEventDefObj.targetObject); 

            eventDefProperty = serializedEventDefObj.FindProperty("eventDefList");
            int eventDefRow = 0;

            foreach (var eventDefinition in _config.EventDefs)
            {
                // read event defs from config file
                string eventTypeString = eventDefinition.Type.ToString();

                EventDef eventDef = new EventDef();
                eventDef.eventName = eventDefinition.Name;
                eventDef.eventType = (EventDef.EventTypeVisible)Enum.Parse(typeof(EventDef.EventTypeVisible), eventTypeString);

                eventDefProperty.InsertArrayElementAtIndex(eventDefRow);
                var prop = eventDefProperty.GetArrayElementAtIndex(eventDefRow);

                var eventNameFromFile = prop.FindPropertyRelative("eventName");
                var eventTypeFromFile = prop.FindPropertyRelative("eventType");

                eventNameFromFile.stringValue = eventDef.eventName;
                eventTypeFromFile.enumValueIndex = (int)eventDef.eventType;

                serializedEventDefObj.ApplyModifiedProperties();
                eventDefRow++;
            }

        }


        void OnGUI()
        {
            float elementHeight = playerHeight.height+botConfigHeight.height+explorationSpaceHeight.height+sampledStateHeight.height+buttonHeight.height;
            if (elementHeight != 0)
            {
                windowHeight = elementHeight + scrollBuffer;
            }

            playerHeight = EditorGUILayout.BeginVertical();
            
            scrollPos = GUI.BeginScrollView(new Rect(0, 0, this.position.width, this.position.height), scrollPos, new Rect(0, 0, 700, windowHeight), false, false);
            if (serializedBotConfigObj.targetObject == null)
            {
                OnEnable();
            }
            serializedBotConfigObj.Update();

            using (var modlProjectIdChanged = new EditorGUI.ChangeCheckScope())
            {
                modlProjectId = EditorGUILayout.TextField("Modl Project ID ", modlProjectId);
                if (modlProjectIdChanged.changed)
                {
                    EditorPrefs.SetString("ModlProjectId", modlProjectId);
                }
            }
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(new GUIContent("Bot Control", BotConfigToolTips.BotControl), header, GUILayout.MaxWidth(200));
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Player", EditorStyles.boldLabel);
            using (var playerChanged = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(playerObjectProperty, new GUIContent("Player Prefab", BotConfigToolTips.BotPlayer));
                if (playerChanged.changed)
                {
                    _configIsDirty = true;
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            

            botConfigHeight = EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(new GUIContent("Input Variables", BotConfigToolTips.InputVariables), EditorStyles.boldLabel, GUILayout.MaxWidth(200));
            using (var botConfigChanged = new EditorGUI.ChangeCheckScope())
            {
                botConfigReList.DoLayoutList();
                if (botConfigChanged.changed)
                {
                    _configIsDirty = true;
                }
            }
            if (GUILayout.Button("Preview Bot Input"))
            {
#if MODL_AUTOMATIC_TESTING
                EditorPrefs.SetBool(startedFromPreviewBotSetup, true);
                ModlPluginManager.CommunicatorPrefState = (int)ModlPluginManager.ModlCommunicatorType.ConfigValidation;
                ModlPluginManager.ValidationModePrefState = (int)CommunicatorConfigValidation.ValidationSteps.ActionSpaceDimensions;
                EditorApplication.EnterPlaymode();
#else
                Debug.LogWarning("In the menu bar, go to 'modl'->'Toggle Modl Testing' to enable modl testing \n");
#endif
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            explorationSpaceHeight = EditorGUILayout.BeginVertical();

            EditorGUILayout.LabelField(new GUIContent("Exploration Behavior", BotConfigToolTips.ExplorationBehaviour), header, GUILayout.MaxWidth(200));
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(new GUIContent("Exploration Space", BotConfigToolTips.ExplorationSpaceDimension), EditorStyles.boldLabel, GUILayout.MaxWidth(200));
            using (var explorationSpaceChanged = new EditorGUI.ChangeCheckScope())
            {
                explorationSpaceReList.DoLayoutList();
                if (explorationSpaceChanged.changed)
                {
                    _configIsDirty = true;
                }
            }
            
            if (GUILayout.Button("Preview Exploration Space"))
            {
#if MODL_AUTOMATIC_TESTING
                EditorPrefs.SetBool(startedFromPreviewBotSetup, true);
                ModlPluginManager.CommunicatorPrefState = (int)ModlPluginManager.ModlCommunicatorType.ConfigValidation;
                ModlPluginManager.ValidationModePrefState = (int)CommunicatorConfigValidation.ValidationSteps.FeatureSpaceDimensions;
                EditorApplication.EnterPlaymode();
#else
                Debug.LogWarning("In the menu bar, go to 'modl'->'Toggle Modl Testing' to enable modl testing \n");
#endif
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            sampledStateHeight = EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(new GUIContent("Game State Variables", BotConfigToolTips.GameStateVariables), EditorStyles.boldLabel, GUILayout.MaxWidth(200));
            using (var sampledStateChanged = new EditorGUI.ChangeCheckScope())
            {
                sampledStateReList.DoLayoutList();
                if (sampledStateChanged.changed)
                {
                    _configIsDirty = true;
                }
            }
            
            if (GUILayout.Button("Preview Game State Variables"))
            {
#if MODL_AUTOMATIC_TESTING
                EditorPrefs.SetBool(startedFromPreviewBotSetup, true);
                ModlPluginManager.CommunicatorPrefState = (int)ModlPluginManager.ModlCommunicatorType.ConfigValidation;
                ModlPluginManager.ValidationModePrefState = (int)CommunicatorConfigValidation.ValidationSteps.ObjectSpaceDimensions;
                EditorApplication.EnterPlaymode();
#else
                Debug.LogWarning("In the menu bar, go to 'modl'->'Toggle Modl Testing' to enable modl testing \n");
#endif
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            serializedBotConfigObj.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            buttonHeight = EditorGUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Full Bot Preview"))
            {
#if MODL_AUTOMATIC_TESTING
                EditorPrefs.SetBool(startedFromPreviewBotSetup, true);
                ModlPluginManager.CommunicatorPrefState = (int)ModlPluginManager.ModlCommunicatorType.ConfigValidation;
                ModlPluginManager.ValidationModePrefState = (int)CommunicatorConfigValidation.ValidationSteps.Everything;
                EditorApplication.EnterPlaymode();
#else
                Debug.LogWarning("In the menu bar, go to 'modl'->'Toggle Modl Testing' to enable modl testing \n");
#endif
            }

            if (GUILayout.Button("Save Setup"))
            {
                GameConfig payload = SaveConfig();
                if (payload != null)
                {
                    new RuntimeFileSystemInterface().WriteConfigFile(payload);
                }
            }
            GUILayout.EndHorizontal();  

            GUI.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Captures the window closing event, checks for unsaved changes,
        /// and tries to save or calls <see cref="PreventClosingWindow"/> to force the window to stay open.
        /// </summary>
        public void OnDestroy()
        {
            if (!_configIsDirty) return;

            GameConfig payload = SaveConfig();

            if (EditorUtility.DisplayDialog(
                    "Save changes to modl configuration?",
                    "You have unsaved modl configuration changes, would you like to save them?",
                    "Yes", "No"))
            {
                new RuntimeFileSystemInterface().WriteConfigFile(payload);
            }
        }

        bool PrefabCheck(string prefabId, string prefabName)
        {
            if (prefabId == "GlobalObjectId_V1-0-00000000000000000000000000000000-0-0")
            {
                Debug.LogError("Game Config cannot be saved: Invalid object " + prefabName +". Object must be a prefab");
                return true;
            }
            else
            {
                return false;
            }
        }

        private GameConfig SaveConfig()
        {
            _configIsDirty = false;
            bool saveDataIsValid = true;
            
            botConfigProperty = serializedBotConfigObj.FindProperty("botGameObjectList");
            int botConfigArraySize = botConfigProperty.arraySize;

            List<BotConfig> botConfigList = new List<BotConfig>();
            
            for (int i = 0; i < botConfigArraySize; i++)
            {
                SerializedProperty element = botConfigProperty.GetArrayElementAtIndex(i);

                var botPrefabObject = element.FindPropertyRelative("botObject");
                var prefabIdObject = element.FindPropertyRelative("prefabId");
                var botComponentObject = element.FindPropertyRelative("botComponent");
                var assemblyStringObject = element.FindPropertyRelative("assemblyString");
                var botVariableObject = element.FindPropertyRelative("botVariablePath");
                var variableIntObject = element.FindPropertyRelative("variableInt");
                var botMemberObject = element.FindPropertyRelative("memberName");
                var botMemberTypeObject = element.FindPropertyRelative("memberType");
                var variableMinObject = element.FindPropertyRelative("min");
                var variableMaxObject = element.FindPropertyRelative("max");

                if (botPrefabObject.objectReferenceValue == null)
                {
                    Debug.LogError($"Game Config cannot be saved: blank row in Bot Configuration section. Please remove this before saving.");
                    saveDataIsValid = false;
                    break;
                }

                if (PrefabUtility.IsPartOfPrefabAsset(botPrefabObject.objectReferenceValue))
                {
                    prefabIdObject.stringValue = GlobalObjectId.GetGlobalObjectIdSlow(botPrefabObject.objectReferenceValue).ToString();
                }
                else
                {
                    // If the conversion is unsuccessful, the default null ID is 'GlobalObjectId_V1-0-00000000000000000000000000000000-0-0'
                    prefabIdObject.stringValue = GlobalObjectId.GetGlobalObjectIdSlow(PrefabUtility.GetCorrespondingObjectFromSource(botPrefabObject.objectReferenceValue)).ToString();
                }

                BotConfig botConf = new BotConfig();
                botConf.botObject = (GameObject)botPrefabObject.objectReferenceValue as GameObject;
                botConf.prefabId = prefabIdObject.stringValue;
                botConf.botComponent = botComponentObject.stringValue;
                botConf.assemblyString = assemblyStringObject.stringValue;
                botConf.botVariablePath = botVariableObject.stringValue;
                botConf.variableInt = variableIntObject.intValue;
                botConf.memberName = botMemberObject.stringValue;
                botConf.memberType = botMemberTypeObject.stringValue;
                botConf.min = variableMinObject.vector4Value;
                botConf.max = variableMaxObject.vector4Value;

                int botIndex = botConfigList.FindIndex(item => (item.botObject.name == botPrefabObject.objectReferenceValue.name && item.botVariablePath == botVariableObject.stringValue));
                if (botIndex >= 0) 
                {
                    Debug.LogError($"Game Config cannot be saved: duplicated rows of {botConf.botObject.name}.{botConf.botComponent}.{botConf.memberName} in Bot Configuration section");
                    saveDataIsValid = false;
                    break;
                }

                if (botConf.min.x > botConf.max.x || botConf.min.y > botConf.max.y || botConf.min.z > botConf.max.z || botConf.min.w > botConf.max.w)
                {
                    Debug.LogError($"Game Config cannot be saved: minimum value can not be higher than maximum value ({botConf.botObject.name}.{botConf.botComponent}.{botConf.memberName})");
                    saveDataIsValid = false;
                    break;
                }

                if (PrefabCheck(botConf.prefabId, botPrefabObject.objectReferenceValue.name))
                {
                    saveDataIsValid = false;
                    break;
                }

                botConfigList.Add(botConf);
            }
            
            botConfigList = botConfigList.OrderBy(b=>b.botObject.name).ToList();


            explorationSpaceProperty = serializedBotConfigObj.FindProperty("explorationSpaceList");
            int explorationSpaceArraySize = explorationSpaceProperty.arraySize;
            List<ExplorationSpace> explorationSpaceList = new List<ExplorationSpace>();

            for (int i = 0; i < explorationSpaceArraySize; i++)
            {
                SerializedProperty element = explorationSpaceProperty.GetArrayElementAtIndex(i);

                var botPrefabObject = element.FindPropertyRelative("botObject");
                var prefabIdObject = element.FindPropertyRelative("prefabId");
                var botComponentObject = element.FindPropertyRelative("botComponent");
                var assemblyStringObject = element.FindPropertyRelative("assemblyString");
                var botVariableObject = element.FindPropertyRelative("botVariablePath");
                var variableIntObject = element.FindPropertyRelative("variableInt");
                var botMemberObject = element.FindPropertyRelative("memberName");
                var botMemberTypeObject = element.FindPropertyRelative("memberType");
                var samplingIntervalObject = element.FindPropertyRelative("samplingInterval");

                if (botPrefabObject.objectReferenceValue == null)
                {
                    Debug.LogError($"Game Config cannot be saved: blank row in Exploration Space section. Please remove this before saving.");
                    saveDataIsValid = false;
                    break;
                }

                if (PrefabUtility.IsPartOfPrefabAsset(botPrefabObject.objectReferenceValue))
                {
                    prefabIdObject.stringValue = GlobalObjectId.GetGlobalObjectIdSlow(botPrefabObject.objectReferenceValue).ToString();
                }
                else
                {
                    // If the conversion is unsuccessful, the default null ID is 'GlobalObjectId_V1-0-00000000000000000000000000000000-0-0'
                    prefabIdObject.stringValue = GlobalObjectId.GetGlobalObjectIdSlow(PrefabUtility.GetCorrespondingObjectFromSource(botPrefabObject.objectReferenceValue)).ToString();
                }

                ExplorationSpace explorationSpace = new ExplorationSpace();
                explorationSpace.botObject = (GameObject)botPrefabObject.objectReferenceValue as GameObject;
                explorationSpace.prefabId = prefabIdObject.stringValue;
                explorationSpace.botComponent = botComponentObject.stringValue;
                explorationSpace.assemblyString = assemblyStringObject.stringValue;
                explorationSpace.botVariablePath = botVariableObject.stringValue;
                explorationSpace.variableInt = variableIntObject.intValue;
                explorationSpace.memberName = botMemberObject.stringValue;
                explorationSpace.memberType = botMemberTypeObject.stringValue;
                explorationSpace.samplingInterval = samplingIntervalObject.vector4Value;

                int explorationIndex = explorationSpaceList.FindIndex(item => (item.botObject.name == botPrefabObject.objectReferenceValue.name && item.botVariablePath == botVariableObject.stringValue));
                if (explorationIndex >= 0) 
                {
                    Debug.LogError($"Game Config cannot be saved: duplicated rows of {explorationSpace.botObject.name}.{explorationSpace.botComponent}.{explorationSpace.memberName} in Exploration Space section");
                    saveDataIsValid = false;
                    break;
                }

                if (explorationSpace.samplingInterval == Vector4.zero)
                {
                    Debug.LogError($"Game Config cannot be saved: Exploration Space Interval cannot be 0 ({explorationSpace.botObject.name}.{explorationSpace.botComponent}.{explorationSpace.memberName})");
                    saveDataIsValid = false;
                    break;
                }

                if (PrefabCheck(explorationSpace.prefabId, botPrefabObject.objectReferenceValue.name))
                {
                    saveDataIsValid = false;
                    break;
                }

                explorationSpaceList.Add(explorationSpace);
            }
            explorationSpaceList = explorationSpaceList.OrderBy(b=>b.botObject.name).ToList();


            sampledStateProperty = serializedBotConfigObj.FindProperty("sampledStateList");
            int sampledStateArraySize = sampledStateProperty.arraySize;
            List<SampledState> sampledStateList = new List<SampledState>();

            for (int i = 0; i < sampledStateArraySize; i++)
            {
                SerializedProperty element = sampledStateProperty.GetArrayElementAtIndex(i);

                var botPrefabObject = element.FindPropertyRelative("botObject");
                var prefabIdObject = element.FindPropertyRelative("prefabId");
                var botComponentObject = element.FindPropertyRelative("botComponent");
                var assemblyStringObject = element.FindPropertyRelative("assemblyString");
                var botVariableObject = element.FindPropertyRelative("botVariablePath");
                var variableIntObject = element.FindPropertyRelative("variableInt");
                var botMemberObject = element.FindPropertyRelative("memberName");
                var botMemberTypeObject = element.FindPropertyRelative("memberType");
                var readOnlyObject = element.FindPropertyRelative("readOnly");

                if (botPrefabObject.objectReferenceValue == null)
                {
                    Debug.LogError($"Game Config cannot be saved: blank row in Sampled State section. Please remove this before saving.");
                    saveDataIsValid = false;
                    break;
                }

                if (PrefabUtility.IsPartOfPrefabAsset(botPrefabObject.objectReferenceValue))
                {
                    prefabIdObject.stringValue = GlobalObjectId.GetGlobalObjectIdSlow(botPrefabObject.objectReferenceValue).ToString();
                }
                else
                {
                    // If the conversion is unsuccessful, the default null ID is 'GlobalObjectId_V1-0-00000000000000000000000000000000-0-0'
                    prefabIdObject.stringValue = GlobalObjectId.GetGlobalObjectIdSlow(PrefabUtility.GetCorrespondingObjectFromSource(botPrefabObject.objectReferenceValue)).ToString();
                }

                SampledState sampledState = new SampledState();
                sampledState.botObject = (GameObject)botPrefabObject.objectReferenceValue as GameObject;
                sampledState.prefabId = prefabIdObject.stringValue;
                sampledState.botComponent = botComponentObject.stringValue;
                sampledState.assemblyString = assemblyStringObject.stringValue;
                sampledState.botVariablePath = botVariableObject.stringValue;
                sampledState.variableInt = variableIntObject.intValue;
                sampledState.memberName = botMemberObject.stringValue;
                sampledState.memberType = botMemberTypeObject.stringValue;
                sampledState.readOnly = readOnlyObject.boolValue;

                int stateIndex = sampledStateList.FindIndex(item => (item.botObject.name == botPrefabObject.objectReferenceValue.name && item.botVariablePath == botVariableObject.stringValue));
                if (stateIndex >= 0) 
                {
                    Debug.LogError($"Game Config cannot be saved: duplicated rows of {sampledState.botObject.name}.{sampledState.botComponent}.{sampledState.memberName} in Sampled State section");
                    saveDataIsValid = false;
                    break;
                }

                if (PrefabCheck(sampledState.prefabId, botPrefabObject.objectReferenceValue.name))
                {
                    saveDataIsValid = false;
                    break;
                }

                sampledStateList.Add(sampledState);
            }
            sampledStateList = sampledStateList.OrderBy(b=>b.botObject.name).ToList();

            
            var payload = CreatePayloadFromArrays(botConfigList, explorationSpaceList, sampledStateList);
            if (payload == null)
            {
                saveDataIsValid = false;
            }

            if (saveDataIsValid)
            {

                eventDefProperty = serializedEventDefObj.FindProperty("eventDefList");

                 
                int eventDefArraySize = eventDefProperty.arraySize;
                
                for (int i = 0; i < eventDefArraySize; i++)
                {
                    SerializedProperty element = eventDefProperty.GetArrayElementAtIndex(i);

                    var eventDefNameObject = element.FindPropertyRelative("eventName");
                    var eventDefTypeObject = element.FindPropertyRelative("eventType");
                    var eventDefPayload = new Proto.EventDef { Name = eventDefNameObject.stringValue, Type = (Modl.Proto.EventDef.Types.Type)eventDefTypeObject.enumValueIndex};
                    
                    payload.EventDefs.Add(eventDefPayload);
                }
                
                payload.MinFps = minFps;
                payload.BrainVersion = ModlPluginManager.BrainVersion;
                return payload;
            }
            else
            {
                return null;
            }
        }

        public GameConfig CreatePayloadFromArrays(List<BotConfig> botConfigList, List<ExplorationSpace> explorationSpaceList, List<SampledState> sampledStateList)
        {
            bool shouldBreak = false;

            var actionSpaceList = new List<ValueRange>();
            var featureSpaceList = new List<ValueRange>();
            var objectSpaceList = new List<ValueRange>();
            var sensorSpaceList = new List<ValueRange>();

            var groupedActionPrefabList = botConfigList.GroupBy(b => b.botObject.name).Select(grp => grp.ToList()).ToList();
            foreach (var prefabList in groupedActionPrefabList)
            {
                string prefabName = prefabList.First().botObject.name;
                string prefabId = "";
                var componentDimList = new List<ValueRange>();
                var groupedCompList = prefabList.GroupBy(c => c.botComponent).Select(grp => grp.ToList()).ToList();
                foreach (var compList in groupedCompList)
                {
                    string componentName = compList.First().botComponent;
                    string componentId = "";
                    var varList = new List<ValueRange>();
                    
                    foreach (BotConfig botConfig in compList)
                    {
                        prefabId = botConfig.prefabId;

                        Type type = Type.GetType(botConfig.memberType);

                        if (botConfig.min == Vector4.zero && botConfig.max == Vector4.zero)
                        {
                            if (type == null)
                            {
                                // type returns null on Vectors and Quaternions which causes an error, so this gives a friendly error message
                                Debug.LogError($"Game Config cannot be saved: Cannot save values set to 0 on {prefabName}.{componentName}");
                                shouldBreak = true;
                                break;
                            }
                            else
                            {
                                var variableDim = new ValueRange
                                {
                                    Name = botConfig.memberName,
                                    Type = ConversionUtilsConfigUI.GetDimensionType(type),
                                    MinValue = 0,
                                    MaxValue = 0,
                                    Id = botConfig.memberName,
                                };
                                varList.Add(variableDim);
                            }
                        }
                        else
                        {
                            var values = new List<ValueRange>();
                            if (botConfig.min.x != 0 || botConfig.max.x != 0)
                            {
                                if (type != null)
                                {
                                    ValueRange variableDimX = ConfigurationUtils.CreateGameConfigDimensionPayload(botConfig.memberName, type, botConfig.min.x, botConfig.max.x, "botConfig");
                                    values.Add(variableDimX);
                                }
                                else
                                {
                                    ValueRange variableDimX = ConfigurationUtils.CreateGameConfigDimensionPayload("x", typeof(float), botConfig.min.x, botConfig.max.x, "botConfig");
                                    values.Add(variableDimX);
                                }
                            }
                            if (botConfig.min.y != 0 || botConfig.max.y != 0)
                            {
                                ValueRange variableDimY = ConfigurationUtils.CreateGameConfigDimensionPayload("y", typeof(float), botConfig.min.y, botConfig.max.y, "botConfig");
                                values.Add(variableDimY);
                            }
                            if (botConfig.min.z != 0 || botConfig.max.z != 0)
                            {
                                ValueRange variableDimZ = ConfigurationUtils.CreateGameConfigDimensionPayload("z", typeof(float), botConfig.min.z, botConfig.max.z, "botConfig");
                                values.Add(variableDimZ);
                            }
                            if (botConfig.min.w != 0 || botConfig.max.w != 0)
                            {
                                ValueRange variableDimW = ConfigurationUtils.CreateGameConfigDimensionPayload("w", typeof(float), botConfig.min.w, botConfig.max.w, "botConfig");
                                values.Add(variableDimW);
                            }

                            ValueRange variableDim = ConfigurationUtils.CreateGameConfigDimensionPayloadSpace(botConfig.memberName, values);
                            variableDim.Id = botConfig.memberName;
                            varList.Add(variableDim);

                        }

                        componentId = botConfig.assemblyString;
                    }

                    if (shouldBreak)
                    {
                        break;
                    }

                    ValueRange componentDim = ConfigurationUtils.CreateGameConfigDimensionPayloadSpace(componentName, varList);
                    componentDim.Id = componentId;
                    componentDimList.Add(componentDim);
                }
                ValueRange prefabDim = ConfigurationUtils.CreateGameConfigDimensionPayloadSpace(prefabName, componentDimList);
                prefabDim.Id = prefabId;
                actionSpaceList.Add(prefabDim);
            }


            List<float> granularity = new List<float>();
            var groupedFeaturePrefabList = explorationSpaceList.GroupBy(b => b.botObject.name).Select(grp => grp.ToList()).ToList();
            foreach (var prefabList in groupedFeaturePrefabList)
            {
                string prefabName = prefabList.First().botObject.name;
                string prefabId = "";
                var componentDimList = new List<ValueRange>();
                var groupedCompList = prefabList.GroupBy(c => c.botComponent).Select(grp => grp.ToList()).ToList();
                foreach (var compList in groupedCompList)
                {
                    string componentName = compList.First().botComponent;
                    string componentId = "";
                    var varList = new List<ValueRange>();
                    foreach (ExplorationSpace explorationSpace in compList)
                    {
                        prefabId = explorationSpace.prefabId;
                        System.Type type = System.Type.GetType(explorationSpace.memberType);

                        if (type != null)
                        {
                            ValueRange variableDim = ConfigurationUtils.CreateGameConfigDimensionPayload(explorationSpace.memberName, type, 0, 0, "explorationSpace");
                            if (explorationSpace.samplingInterval.x != 0)
                            {
                                granularity.Add(explorationSpace.samplingInterval.x);
                            }

                            varList.Add(variableDim);
                        }
                        else
                        {
                            var values = new List<ValueRange>();
                            string varTypeString = explorationSpace.memberType.Split('.').Last();
                            switch (varTypeString)
                            {
                                case "Vector2":
                                    ValueRange variableDimX = ConfigurationUtils.CreateGameConfigDimensionPayload("x", typeof(float), 0, 0, "explorationSpace");
                                    granularity.Add(explorationSpace.samplingInterval.x);
                                    values.Add(variableDimX);
                                    ValueRange variableDimY = ConfigurationUtils.CreateGameConfigDimensionPayload("y", typeof(float), 0, 0, "explorationSpace");
                                    granularity.Add(explorationSpace.samplingInterval.y);
                                    values.Add(variableDimY);
                                    break;
                                case "Vector3":
                                    variableDimX = ConfigurationUtils.CreateGameConfigDimensionPayload("x", typeof(float), 0, 0, "explorationSpace");
                                    granularity.Add(explorationSpace.samplingInterval.x);
                                    values.Add(variableDimX);
                                    variableDimY = ConfigurationUtils.CreateGameConfigDimensionPayload("y", typeof(float), 0, 0, "explorationSpace");
                                    granularity.Add(explorationSpace.samplingInterval.y);
                                    values.Add(variableDimY);
                                    ValueRange variableDimZ = ConfigurationUtils.CreateGameConfigDimensionPayload("z", typeof(float), 0, 0, "explorationSpace");
                                    granularity.Add(explorationSpace.samplingInterval.z);
                                    values.Add(variableDimZ);
                                    break;
                                case "Vector4":
                                case "Quaternion":
                                    variableDimX = ConfigurationUtils.CreateGameConfigDimensionPayload("x", typeof(float), 0, 0, "explorationSpace");
                                    granularity.Add(explorationSpace.samplingInterval.x);
                                    values.Add(variableDimX);
                                    variableDimY = ConfigurationUtils.CreateGameConfigDimensionPayload("y", typeof(float), 0, 0, "explorationSpace");
                                    granularity.Add(explorationSpace.samplingInterval.y);
                                    values.Add(variableDimY);
                                    variableDimZ = ConfigurationUtils.CreateGameConfigDimensionPayload("z", typeof(float), 0, 0, "explorationSpace");
                                    granularity.Add(explorationSpace.samplingInterval.z);
                                    values.Add(variableDimZ);
                                    ValueRange variableDimW = ConfigurationUtils.CreateGameConfigDimensionPayload("w", typeof(float), 0, 0, "explorationSpace");
                                    granularity.Add(explorationSpace.samplingInterval.w);
                                    values.Add(variableDimW);
                                    break;
                                default:
                                    break;
                            }

                            ValueRange variableDim = ConfigurationUtils.CreateGameConfigDimensionPayloadSpace(explorationSpace.memberName, values);
                            variableDim.Id = explorationSpace.memberName;
                            varList.Add(variableDim);

                        }
                        componentId = explorationSpace.assemblyString;
                    }
                    ValueRange componentDim = ConfigurationUtils.CreateGameConfigDimensionPayloadSpace(componentName, varList);
                    componentDim.Id = componentId;
                    componentDimList.Add(componentDim);
                }
                ValueRange prefabDim = ConfigurationUtils.CreateGameConfigDimensionPayloadSpace(prefabName, componentDimList);
                prefabDim.Id = prefabId;
                featureSpaceList.Add(prefabDim);
            }

            var groupedObjectPrefabList = sampledStateList.GroupBy(b => b.botObject.name).Select(grp => grp.ToList()).ToList();
            foreach (var prefabList in groupedObjectPrefabList)
            {
                string prefabName = prefabList.First().botObject.name;
                string prefabId = "";
                var componentDimList = new List<ValueRange>();
                var groupedCompList = prefabList.GroupBy(c => c.botComponent).Select(grp => grp.ToList()).ToList();
                bool prefabIsWaypoint = false;
                bool contains = prefabList.Any(p => p.botComponent == "ModlWaypoint");
                if (contains)
                {
                    
                    prefabIsWaypoint = true;
                }
                foreach (var compList in groupedCompList)
                {
                    string componentName = compList.First().botComponent;
                    
                    string componentId = "";
                    var varList = new List<ValueRange>();
                    foreach (SampledState sampledState in compList)
                    {
                        prefabId = sampledState.prefabId;
                        System.Type type = System.Type.GetType(sampledState.memberType);

                        if (type != null)
                        {
                            ValueRange variableDim = ConfigurationUtils.CreateGameConfigDimensionPayload(sampledState.memberName, type, 0, 0, "sampledState");
                            if (sampledState.readOnly == true)
                            {
                                variableDim.Tags.Add(ConfigurationUtils.ReadOnlyTag);
                            }

                            if (sampledState.memberName == "index" && componentName == "ModlWaypoint")
                            {
                                variableDim.Tags.Add(ConfigurationUtils.WaypointIndexTag);
                            }
                            else if (componentName == "ModlWaypoint")
                            {
                                //We assume that anything else can be used as a waypoint
                                variableDim.Tags.Add(ConfigurationUtils.WaypointPositionTag);
                            }

                            varList.Add(variableDim);
                        }
                        else
                        {
                            var values = new List<ValueRange>();
                            string varTypeString = sampledState.memberType.Split('.').Last();
                            switch (varTypeString)
                            {
                                case "Vector2":
                                    ValueRange variableDimX = ConfigurationUtils.CreateGameConfigDimensionPayload("x", typeof(float), 0, 0, "sampledState");
                                    values.Add(variableDimX);
                                    ValueRange variableDimY = ConfigurationUtils.CreateGameConfigDimensionPayload("y", typeof(float), 0, 0, "sampledState");
                                    values.Add(variableDimY);
                                    break;
                                case "Vector3":
                                    variableDimX = ConfigurationUtils.CreateGameConfigDimensionPayload("x", typeof(float), 0, 0, "sampledState");
                                    values.Add(variableDimX);
                                    variableDimY = ConfigurationUtils.CreateGameConfigDimensionPayload("y", typeof(float), 0, 0, "sampledState");
                                    values.Add(variableDimY);
                                    ValueRange variableDimZ = ConfigurationUtils.CreateGameConfigDimensionPayload("z", typeof(float), 0, 0, "sampledState");
                                    values.Add(variableDimZ);
                                    break;
                                case "Vector4":
                                case "Quaternion":
                                    variableDimX = ConfigurationUtils.CreateGameConfigDimensionPayload("x", typeof(float), 0, 0, "sampledState");
                                    values.Add(variableDimX);
                                    variableDimY = ConfigurationUtils.CreateGameConfigDimensionPayload("y", typeof(float), 0, 0, "sampledState");
                                    values.Add(variableDimY);
                                    variableDimZ = ConfigurationUtils.CreateGameConfigDimensionPayload("z", typeof(float), 0, 0, "sampledState");
                                    values.Add(variableDimZ);
                                    ValueRange variableDimW = ConfigurationUtils.CreateGameConfigDimensionPayload("w", typeof(float), 0, 0, "sampledState");
                                    values.Add(variableDimW);
                                    break;
                                default:
                                    break;
                            }

                            ValueRange variableDim = ConfigurationUtils.CreateGameConfigDimensionPayloadSpace(sampledState.memberName, values);
                            if (sampledState.readOnly == true)
                            {
                                variableDim.Tags.Add(ConfigurationUtils.ReadOnlyTag);
                            }

                            
                            if (sampledState.memberName == "position")
                            {
                                if (prefabIsWaypoint == true)
                                {
                                    variableDim.Tags.Add(ConfigurationUtils.WaypointPositionTag);
                                }
                                else
                                {
                                    if (!variableDim.Tags.Contains(ConfigurationUtils.PlayerPositionTag))
                                    {
                                        variableDim.Tags.Add(ConfigurationUtils.PlayerPositionTag);
                                    }
                                }
                            }

                            variableDim.Id = sampledState.memberName;
                            varList.Add(variableDim);

                        }
                        componentId = sampledState.assemblyString;
                    }
                    ValueRange componentDim = ConfigurationUtils.CreateGameConfigDimensionPayloadSpace(componentName, varList);
                    componentDim.Id = componentId;
                    componentDimList.Add(componentDim);
                }
                ValueRange prefabDim = ConfigurationUtils.CreateGameConfigDimensionPayloadSpace(prefabName, componentDimList);
                prefabDim.Id = prefabId;

                string prefabObjectName = "";
                GameObject prefabObj = (GameObject)playerObjectProperty.objectReferenceValue as GameObject;
                if (prefabObj != null)
                {
                    prefabObjectName = prefabObj.name;
                }
                
                if (prefabName == prefabObjectName || (ConfigurationUtils.GetTag(prefabName).Equals(ConfigurationUtils.PlayerTag, StringComparison.InvariantCultureIgnoreCase)))
                {
                    if (!prefabDim.Tags.Contains(ConfigurationUtils.PlayerTag))
                    {
                        prefabDim.Tags.Add(ConfigurationUtils.PlayerTag);
                    }
                }
                
                objectSpaceList.Add(prefabDim);
            }

            ValueRange actionSpace = ConfigurationUtils.CreateGameConfigDimensionPayloadSpace("Inputs", actionSpaceList);
            ValueRange featureSpace = ConfigurationUtils.CreateGameConfigDimensionPayloadSpace("Observations", featureSpaceList);
            ValueRange objectSpace = ConfigurationUtils.CreateGameConfigDimensionPayloadSpace("Extras", objectSpaceList);
            ValueRange sensorSpace = ConfigurationUtils.CreateGameConfigDimensionPayloadSpace("SENSOR_SPACE", sensorSpaceList);

            if (objectSpace.Dims.Count(child =>child.Tags.Contains(ConfigurationUtils.PlayerTag)) >1)
            {
                Debug.LogError("The bot configuration contains more than one object tagged Player");
            }

            foreach (float interval in granularity)
            {
                if (interval<=0)
                {
                    // Can't give context to this error message because granularity just gets added to a big list separate from the feature space
                    Debug.LogError($"Game Config cannot be saved: Exploration Space Interval cannot contain negative values or zero");
                    shouldBreak = true;
                    break;
                }
            }
            
            GameConfig payload = ConfigurationUtils.CreateGameConfigPayload(actionSpace, featureSpace, objectSpace, sensorSpace, granularity);            

            if (shouldBreak)
            {
                return null;
            }
            else
            {
                return payload;
            }
        }
    }
}


