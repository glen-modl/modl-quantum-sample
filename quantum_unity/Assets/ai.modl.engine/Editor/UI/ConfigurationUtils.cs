using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Modl.Proto;
using UnityEngine;
using static Modl.Editor.UI.ConversionUtilsConfigUI;
using Type = System.Type;

namespace Modl.Editor.UI
{
    public static class ConfigurationUtils
    {
        public const string ReadOnlyTag = "readOnly";
        public const string PlayerTag = "player";
        public const string PlayerPositionTag = "position";
        public const string WaypointPositionTag = "waypoint";
        public const string WaypointIndexTag = "waypoint_index";


        public static string GetTag(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                return go.tag;
            }

            return name;
        }

        public static ValueRange CreateGameConfigDimensionPayload(string objectName, Type objectType, float objectMinValue, float objectMaxValue, string space)
        {
            Modl.Proto.ValueRange.Types.Type dimType = GetDimensionType(objectType);

            if ((objectMinValue == 0 && objectMaxValue == 0) && space == "botConfig")
            {
                var gcdp = new ValueRange{ Name = objectName, Type = dimType, Id = objectName };
                return gcdp;
            }
            else 
            {
                var gcdp = new ValueRange{ Name = objectName, Type = dimType, MinValue = objectMinValue, MaxValue = objectMaxValue, Id = objectName };
                return gcdp;
            }
            
        }

        public static ValueRange CreateGameConfigDimensionPayloadSpace(string spaceName, List<ValueRange> dimensions)
        {
            ValueRange space = new ValueRange
            {
                Name = spaceName,
                Type = ValueRange.Types.Type.Space,
            };
            foreach (var dim in dimensions)
            {
                space.Dims.Add (dim);   
            }
            
            return space;
        }

        public static GameConfig CreateGameConfigPayload(ValueRange objActionSpace, ValueRange objFeatureSpace, ValueRange objObjectSpace, ValueRange objSensorSpace, List<float> feature_granularity)
        {
            GameConfig payload = new GameConfig
            {
                ActionSpace = objActionSpace,
                FeatureSpace = objFeatureSpace,
                ObjectSpace = objObjectSpace,
                SensorSpace = objSensorSpace
            };

            payload.FeatureGranularity.Add(feature_granularity);
            return payload;
        }

        public static (List<string>, List<MemberInfo>, List<string>, List<string>, List<string>) GetVariablesForPrefab(object botPrefab)
        {
            List<string> variables = new List<string>();
            List<string> components = new List<string>();
            List<string> componentIds = new List<string>();
            List<MemberInfo> membersList = new List<MemberInfo>();
            List<string> memberTypesList = new List<string>();

            foreach (var component in ((GameObject)botPrefab).transform.GetComponents<Component>())
            {
                MemberInfo[] members = (component.GetType()).GetMembers(BindingFlags.Instance | BindingFlags.Public).Where(x => x.MemberType == MemberTypes.Field || x.MemberType == MemberTypes.Property).ToArray();
                foreach (var member in members)
                {
                    var compType = component.GetType();
                    string compTypeString = component.GetType().ToString().Split('.').Last();
                    
                    string asmMemberType = GetMemberType(member).AssemblyQualifiedName.Split(',').First();

                    string compId = $"{compType.Assembly.FullName}|{compType.FullName}";

                    System.Type varType = typeof(string); //needs to be initialized

                    if (System.Type.GetType(asmMemberType) != null)
                    {
                        varType = System.Type.GetType(asmMemberType);
                    }
                    else
                    {
                        varType = System.Type.GetType(asmMemberType+','+ asmMemberType.Split('.').First());
                    }

                    if (varType != null)
                    {
                        if (varType.IsPrimitive || asmMemberType.Contains("Vector") || asmMemberType.Contains("Quaternion") || varType == typeof(string))
                        {
                            variables.Add(CombineComponentAndMember(compTypeString, member));
                            membersList.Add(member);
                            components.Add(compTypeString);
                            memberTypesList.Add(asmMemberType);
                            componentIds.Add(compId);
                        }
                    }
                }
            }
            return (variables, membersList, components, memberTypesList, componentIds);
        }

        public static string CombineComponentAndMember(string componentName, MemberInfo member)
        {
            string memberName = member.Name;
            string memberType = GetTypePrettyName(GetMemberType(member));

            return componentName + '/' + memberName + "(" + memberType + ")";
        }

        public static (string, int, string) SplitComponentAndMember(string componentAndMember)
        {
            string compFromType = componentAndMember.Split('/')[0];
            int substringIndex = componentAndMember.Split('/')[1].IndexOf('(');
            string memberString = componentAndMember.Split('/')[1].Substring(0, substringIndex);

            return (compFromType, substringIndex, memberString);
        }
    }
}