using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;

using static Modl.Editor.UI.ConversionUtilsConfigUI;

namespace Modl.Editor.UI
{
    [System.Serializable]
    public class Bot
    {
        public GameObject botObject;
        public string prefabId;
        public string botComponent;
        public string assemblyString;
        public string memberName;
        public string memberType;
        public string botVariablePath;
        public int variableInt;
    }

    [System.Serializable]
    public class BotConfig : Bot
    {
        public Vector4 min;
        public Vector4 max;
    }

    [System.Serializable]
    public class ExplorationSpace : Bot
    {
        public Vector4 samplingInterval;
    }

    [System.Serializable]
    public class SampledState : Bot
    {
        public bool readOnly;
    }

    [CustomPropertyDrawer(typeof(Bot), true)]
    public class BotConfigDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Calculate rects
            var botObjectRect = new Rect(position.x, position.y, 150, position.height / 2);
            var botComponentRect = new Rect(position.x + 155, position.y, 250, position.height + 5);
            var botVariableRect = new Rect(position.x + 405, position.y, 150, position.height);

            var botPrefabObject = property.FindPropertyRelative("botObject");
            var botComponentObject = property.FindPropertyRelative("botComponent");
            var assemblyStringObject = property.FindPropertyRelative("assemblyString");
            var botVariableObject = property.FindPropertyRelative("botVariablePath");
            var variableIntObject = property.FindPropertyRelative("variableInt");
            var botMemberObject = property.FindPropertyRelative("memberName");
            var botMemberTypeObject = property.FindPropertyRelative("memberType");

            using (var prefabChanged = new EditorGUI.ChangeCheckScope())
            {
                // Draw fields - pass GUIContent.none to each so they are drawn without labels
                EditorGUI.PropertyField(botObjectRect, botPrefabObject, GUIContent.none);
                if (prefabChanged.changed)
                {
                    // Reset the component/member dropdown to zero if a different prefab is dragged into the object field
                    variableIntObject.intValue = 0;
                }
            }     

            // Use this if we want the component to be draggable
            if (botPrefabObject.objectReferenceValue != null)
            {
                List<string> componentsAndMembers = new List<string>();
                List<string> components = new List<string>();
                List<string> componentIds = new List<string>();
                List<MemberInfo> members = new List<MemberInfo>();
                List<string> memberTypesList = new List<string>();

                (componentsAndMembers, members, components, memberTypesList, componentIds) = ConfigurationUtils.GetVariablesForPrefab(botPrefabObject.objectReferenceValue);
                variableIntObject.intValue = EditorGUI.Popup(botComponentRect, variableIntObject.intValue, componentsAndMembers.ToArray());

                botVariableObject.stringValue = componentsAndMembers.ToArray()[variableIntObject.intValue];
                botMemberObject.stringValue = (members.ToArray()[variableIntObject.intValue]).Name;
                botComponentObject.stringValue = (components.ToArray()[variableIntObject.intValue]);
                botMemberTypeObject.stringValue = (memberTypesList.ToArray()[variableIntObject.intValue]);
                
                assemblyStringObject.stringValue = (componentIds.ToArray()[variableIntObject.intValue]);

            }


            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight *2) + 5;
        }


    }
}