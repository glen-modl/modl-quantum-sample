using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Modl.Proto;
using UnityEngine;
using Enum = System.Enum;
using Random = UnityEngine.Random;
using Type = System.Type;

namespace Modl.Internal.RuntimeData
{
    public static class ConversionUtils
    {
        private static readonly Dictionary<string, Assembly> _assemblyDict = AppDomain.CurrentDomain.GetAssemblies()
            .ToDictionary(x => x.FullName, x => x);

        public static int GetDimensionSize(ValueRange field)
        {
            switch (field.Type)
            {
                case ValueRange.Types.Type.Space:
                {
                    return field.Dims.Aggregate(0, (acc, x) => acc + GetDimensionSize(x));
                }
                case ValueRange.Types.Type.DiscreteDimension:
                case ValueRange.Types.Type.ContinuousDimension:
                case ValueRange.Types.Type.BooleanDimension:
                case ValueRange.Types.Type.CategoricalDimension:
                case ValueRange.Types.Type.StringDimension:
                {
                    return 1;
                }
                case ValueRange.Types.Type.Unknown:
                {
                    throw new ArgumentOutOfRangeException();
                }
                default:
                {
                    throw new ArgumentOutOfRangeException();
                }
            }
        }

        public static IEnumerable<float> GetBufferValues(MemberInfo member, object obj)
        {
            object filedVal;
            switch (member)
            {
                case FieldInfo    info: filedVal = info.GetValue(obj); break;
                case PropertyInfo info: filedVal = info.GetValue(obj); break;
                default: throw new FormatException($"Member kind of {member.Name} for type {member.MemberType} not supported");
            }

            return GetBufferValues(filedVal, filedVal.GetType());
        }
        
        
        public static List<Value> GetObsBufferValues(MemberInfo member, object obj)
        {
            object filedVal;
            switch (member)
            {
                case FieldInfo    info: filedVal = info.GetValue(obj); break;
                case PropertyInfo info: filedVal = info.GetValue(obj); break;
                default:throw new FormatException($"Member kind of {member.Name} for type {member.DeclaringType} not supported");
            }
            return GetObsBufferValues(filedVal, filedVal.GetType());
        }

        public static object GetObjectValue(float[] values, MemberInfo member)
        {
            Type type;
            switch (member)
            {
                case FieldInfo    info: type = info.FieldType;    break;
                case PropertyInfo info: type = info.PropertyType; break;
                default: throw new FormatException($"Member kind of {member.Name} for type {member.DeclaringType} not supported");
            }

            // we need the lenght of the prop for recursion, but it's not needed above
            var (objectValue, _) = GetObjectValue(values, type);
            return objectValue;
        }

        public static object GetObsObjectValue(Value[] values, MemberInfo member)
        {
            Type type;
            switch (member)
            {
                case FieldInfo    info: type = info.FieldType;    break;
                case PropertyInfo info: type = info.PropertyType; break;
                default:throw new FormatException($"Member kind of {member.Name} for type {member.DeclaringType} not supported");
            }

            // we need the lenght of the prop for recursion, but it's not needed above
            var (objectValue, _) = GetObsObjectValue(values, type);
            return objectValue;
        }

        
        #region MemberInfo utils
        
        public static (MemberInfo info, Type componentType) MemberGetInfo(string typeId, string memberId)
        {
            // TODO still ugly, how can we fix this?
            var typeInfo = typeId.Split('|');
            var assemblyPath = typeInfo[0];
            var componentPath = typeInfo[1];

            var type = _assemblyDict[assemblyPath].GetType(componentPath);
            
            // FIXME this is dangerous. What if none? What if multiple?
            return (type.GetMember(memberId)[0], type);
        }

        public static void MemberSetValue(MemberInfo member, Component component, object val)
        {
            switch (member)
            {
                case FieldInfo info:
                {
                    info.SetValue(component, val);
                    break;
                }
                case PropertyInfo info:
                {
                    info.SetValue(component, val);
                    break;
                }
                default:
                    throw new FormatException(
                        $"Member kind of {member.Name} for type {member.DeclaringType} not supported");
            }
        }
        
        #endregion

        public static ActionVector SampleAction(ValueRange space)
        {
            
            switch (space.Type)
            {
                case ValueRange.Types.Type.Space:
                {
                    var result = new ActionVector();
                    foreach (var dim in space.Dims)
                    {
                        result.Values.AddRange(SampleAction(dim).Values);
                    }

                    return result;
                }
                case ValueRange.Types.Type.DiscreteDimension:
                case ValueRange.Types.Type.ContinuousDimension:
                case ValueRange.Types.Type.CategoricalDimension:
                {

                    return new ActionVector {Values = { new Value{ NumberValue = Random.Range(space.MinValue, space.MaxValue) }}};
                }
                case ValueRange.Types.Type.BooleanDimension:
                {
                    return new ActionVector  {Values = { new Value{ NumberValue = Random.Range(0, 2) } }};
                }
                case ValueRange.Types.Type.StringDimension:
                case ValueRange.Types.Type.Unknown:
                {
                    throw new ArgumentOutOfRangeException();
                }
                default:
                {
                    Debug.LogError(space.Name);
                    throw new ArgumentOutOfRangeException();
                }
            }
        }

        public static ValueRange GetObjectFromSpace(ValueRange space, string id)
        {
            switch (space.Type)
            {
                case ValueRange.Types.Type.Space:
                {
                    if (space.Id == id)
                    {
                        return space;
                    }

                    foreach (var dim in space.Dims)
                    {
                        var found = GetObjectFromSpace(dim, id);
                        if (found != null) return found;
                    }
                }
                    break;
                case ValueRange.Types.Type.DiscreteDimension:
                case ValueRange.Types.Type.ContinuousDimension:
                case ValueRange.Types.Type.BooleanDimension:
                case ValueRange.Types.Type.CategoricalDimension:
                    break;
                case ValueRange.Types.Type.StringDimension:
                case ValueRange.Types.Type.Unknown:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return null;
        }

        public static string DimensionToString(ValueRange space, int index, bool showRanges = false) =>
            IDimensionToString(space, index, showRanges).identifier;

        private static IEnumerable<float> GetBufferValues(object obj, Type type)
        {
            List<float> result = new List<float>();
            if (type.IsEnum)
            {
                result.Add(Array.IndexOf(Enum.GetValues(type), obj));
            }
            else if (type == typeof(bool))
            {
                result.Add((bool) obj ? 1 : 0);
            }
            else if (type == typeof(int))
            {
                result.Add(Convert.ToSingle(obj));
            }
            else if (type.IsPrimitive)
            {
                result.Add((float) obj);
            }
            else if (type.IsArray)
            {
                result.Add(((Array) obj).Length);
                foreach (var v in (Array) obj) result.AddRange(GetBufferValues(v, type.GetElementType()));
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                result.Add(((List<object>) obj).Count);
                foreach (var v in (List<object>) obj)
                    result.AddRange(GetBufferValues(v, type.GetGenericArguments()[0]));
            }
            else if (type.IsValueType)
            {
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
                foreach (var f in fields)
                {
                    var temp = f.GetValue(obj);
                    result.AddRange(GetBufferValues(temp, temp.GetType()));
                }
            }

            return result;
        }

        private static (object, int) GetObjectValue(float[] values, Type type)
        {
            if (values.Length == 0 && type == typeof(bool))
            {
                return (false, 0);
            }

            if (values.Length == 0 && type.IsPrimitive)
            {
                return (0, 0);
            }

            if (values.Length == 0)
            {
                return (null, 0);
            }

            if (type.IsEnum)
            {
                return (Enum.GetValues(type).GetValue((int) values[0]), 1);
            }

            if (type == typeof(bool))
            {
                return ((int) values[0] == 1, 1);
            }

            if (type.IsPrimitive)
            {
                return (Convert.ChangeType(values[0], type), 1);
            }

            if (type.IsArray)
            {
                int length = 1;
                object[] result = new object[(int) values[0]];
                for (int i = 0; i < values[0]; i++)
                {
                    var (tmpObj, tmpLength) = GetObjectValue(
                        values.Skip(length).ToArray(),
                        type.GetElementType()
                    );
                    result[i] = tmpObj;
                    length += tmpLength;
                }

                return (result, length);
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                int length = 1;
                List<object> result = new List<object>();
                for (int i = 0; i < values[0]; i++)
                {
                    var (tmpObj, tmpLength) = GetObjectValue(
                        values.Skip(length).ToArray(),
                        type.GetGenericArguments()[0]
                    );
                    result.Add(tmpObj);
                    length += tmpLength;
                }

                return (result, length);
            }

            if (type.IsValueType)
            {
                int length = 0;
                object result = Activator.CreateInstance(type);
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
                foreach (var f in fields)
                {
                    var (tmpObj, tmpLength) = GetObjectValue(values.Skip(length).ToArray(), f.FieldType);
                    f.SetValue(result, tmpObj);
                    length += tmpLength;
                }

                return (result, length);
            }

            throw new ArgumentException("Unrecognized type");
        }

        private static List<Value> GetObsBufferValues(object obj, Type type)
        {
            List<Value> results = new List<Value>();
            if (type.IsEnum) { results.Add(Value.ForNumber(Array.IndexOf(Enum.GetValues(type), obj))); }
            else if (type == typeof(bool)) { results.Add(Value.ForNumber((bool)obj ? 1 : 0)); }
            else if (type == typeof(int)) { results.Add(Value.ForNumber(Convert.ToSingle(obj))); }
            else if (type == typeof(string)) { results.Add(Value.ForString((string)obj)); }
            else if (type.IsPrimitive) { results.Add(Value.ForNumber((float)obj)); }
            else if (type.IsArray)
            {
                if(type.GetElementType() == typeof(float))
                {
                    results.Add(Value.ForList( ((float[]) obj).Select(item => Value.ForNumber(item)).ToArray()));
                }
                else
                {
                    throw new ArgumentException("[MODL] TMP nested arrays not handled yet");
                    // TODO this code is correct, but parsing the result in `GetObsObjectValue` is tricky
                    // results.Add(new ObsValueType(((Array)obj).Length));
                    // foreach (var v in (Array)obj) results.AddRange(GetObsBufferValues(v, type.GetElementType()));
                }
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (type.GetGenericArguments()[0] == typeof(float))
                {
                    results.Add(Value.ForList( ((List<float>)obj).Select(item => Value.ForNumber(item)).ToArray()));
                }
                //TODO: do something about this, it's for recursing through lists of things
                // else
                // {
                //     results.Add(new Value(((List<object>)obj).Count));
                //     foreach (var v in (List<object>)obj)
                //         results.AddRange(GetObsBufferValues(v, type.GetGenericArguments()[0]));
                // }

            }
            else if (type.IsValueType)
            {
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
                foreach (var f in fields)
                {
                    var temp = f.GetValue(obj);
                    results.AddRange(GetObsBufferValues(temp, temp.GetType()));
                }
            }

            return results;
        }

        private static (object, int) GetObsObjectValue(Value[] values, Type type)
        {
            if (values.Length == 0)
            {
                throw new ArgumentException("[MODL] ObsValue empty");
            }

            if (type.IsEnum)            { return (Enum.GetValues(type).GetValue((int) values[0].NumberValue), 1); }
            if (type == typeof(bool))   { return ((int)values[0].NumberValue == 1, 1); }
            if (type == typeof(string)) { return (values[0].StringValue, 1); }
            if (type.IsPrimitive)       { return (Convert.ChangeType(values[0].NumberValue, type), 1); }
            
            if (type.IsArray && type.GetElementType() == typeof(float))
            {
                return (values[0].ListValue.Values.ToArray(), 1);
            }
            if (type.IsArray)
            {
                // [10/22] protobuf is in the process of changing, so this will need to change with that too
                throw new ArgumentException("[MODL] TMP nested arrays not handled yet");
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>) && type.GetGenericArguments()[0] == typeof(float))
            {

                return (values[0].ListValue.Values.ToList(), 1);
            }
            if (type.IsValueType)
            {
                int length = 0;
                object result = Activator.CreateInstance(type);
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
                foreach (var f in fields)
                {
                    var (tmpObj, tmpLength) = GetObsObjectValue(values.Skip(length).ToArray(), f.FieldType);
                    f.SetValue(result, tmpObj);
                    length += tmpLength;
                }

                return (result, length);
            }

            throw new ArgumentException("Unrecognized type");
        }

        private static (string identifier, int i) IDimensionToString(
            ValueRange space,
            int index,
            bool showRanges
        )
        {
            switch (space.Type)
            {
                case ValueRange.Types.Type.Space:
                {
                    foreach (var dim in space.Dims)
                    {
                        var (identifier, i) = IDimensionToString(dim, index, showRanges);
                        index = i;
                        if (index < 0)
                        {
                            return ($"{space.Name}->{identifier}", index);
                        }
                    }

                    return (string.Empty, index);
                }
                case ValueRange.Types.Type.DiscreteDimension:
                case ValueRange.Types.Type.ContinuousDimension:
                case ValueRange.Types.Type.CategoricalDimension:
                {
                    --index;
                    if (showRanges)
                    {
                        return (index < 0 ? $"{space.Name} (Min:{space.MinValue}, Max:{space.MaxValue})" : string.Empty,
                            index);
                    }

                    return (index < 0 ? $"{space.Name}" : string.Empty, index);
                }
                case ValueRange.Types.Type.BooleanDimension:
                {
                    --index;
                    if (showRanges)
                    {
                        return (index < 0 ? $"{space.Name} (True/False)" : string.Empty, index);
                    }

                    return (index < 0 ? $"{space.Name}" : string.Empty, index);
                }
                case ValueRange.Types.Type.StringDimension:
                case ValueRange.Types.Type.Unknown:
                {
                    throw new ArgumentOutOfRangeException();
                }
                default:
                {
                    Debug.LogError(space.Name);
                    throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}
