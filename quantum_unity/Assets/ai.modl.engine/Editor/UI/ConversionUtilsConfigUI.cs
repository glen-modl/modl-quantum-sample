using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace Modl.Editor.UI
{
    public static class ConversionUtilsConfigUI
    {
        public static Modl.Proto.ValueRange.Types.Type GetDimensionType(Type type)
        {
            while (true)
            {
                if (type == null)           throw new ArgumentException("[MODL] Can't get protobuf type for null");
                
                if (type.IsEnum)            return Modl.Proto.ValueRange.Types.Type.CategoricalDimension;
                if (type == typeof(bool))   return Modl.Proto.ValueRange.Types.Type.BooleanDimension;
                if (type == typeof(int))    return Modl.Proto.ValueRange.Types.Type.DiscreteDimension;
                if (type == typeof(string)) return Modl.Proto.ValueRange.Types.Type.StringDimension;
                if (type.IsPrimitive)       return Modl.Proto.ValueRange.Types.Type.ContinuousDimension;
                
                if (type.IsArray)
                {
                    type = type.GetElementType();
                    continue;
                }
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    type = type.GetElementType();
                    continue;
                }
                
                return type.IsValueType
                    ? Modl.Proto.ValueRange.Types.Type.Space
                    : Modl.Proto.ValueRange.Types.Type.Unknown;
            }
        }
        
        public static Type GetMemberType(MemberInfo m)
        {
            switch (m)
            {
                case FieldInfo    f: return f.FieldType;
                case PropertyInfo p: return p.PropertyType;
                default            : return m.DeclaringType;
            }
        }
        
        public static string GetTypePrettyName(Type t)
        {
            const string sep = ", ";
            string ret = t.Name;

            if (t.IsGenericType)
            {
                ret = $"{ret}<{string.Join(sep, t.GetGenericArguments().Select(x => x.Name))}>";
            }
            return ret;
        }

        
    }
}