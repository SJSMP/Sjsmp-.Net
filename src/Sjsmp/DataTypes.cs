using System;
using System.Collections.Generic;
using System.Linq;

namespace Sjsmp
{
    public static class DataTypes
    {
        private static readonly IReadOnlyDictionary<string, Type> s_nameToType;
        private static readonly IReadOnlyDictionary<Type, string> s_typeToName;
        private static readonly IReadOnlyDictionary<string, bool> s_allowShowGraphForName;
        private static readonly ISet<Type> s_intTypes;
        private static readonly ISet<Type> s_floatTypes;

        static DataTypes()
        {
            Dictionary<string, Type> names = new Dictionary<string, Type>();
            Dictionary<Type, string> types = new Dictionary<Type, string>();
            Dictionary<string, bool> graphs = new Dictionary<string, bool>();
            ISet<Type> intTypes = new HashSet<Type>();
            ISet<Type> floatTypes = new HashSet<Type>();

            AddType(names, types, graphs, "void",   typeof(void),   false, null);
            AddType(names, types, graphs, "string", typeof(string), false, null);
            AddType(names, types, graphs, "float",  typeof(float),  true,  floatTypes);
            AddType(names, types, graphs, "double", typeof(double), true,  floatTypes);
            AddType(names, types, graphs, "int32",  typeof(Int32),  true,  intTypes);
            AddType(names, types, graphs, "uint32", typeof(UInt32), true,  intTypes);
            AddType(names, types, graphs, "int64",  typeof(Int64),  true,  intTypes);
            //AddType(names, types, graphs, "uint64", typeof(UInt64), true,  intTypes);
            AddType(names, types, graphs, "int16",  typeof(Int16),  true,  intTypes);
            AddType(names, types, graphs, "uint16", typeof(UInt16), true,  intTypes);
            AddType(names, types, graphs, "int8",   typeof(sbyte),  true,  intTypes);
            AddType(names, types, graphs, "uint8",  typeof(byte),   true,  intTypes);
            AddType(names, types, graphs, "bool",   typeof(bool),   false, null);

            s_nameToType = names;
            s_typeToName = types;
            s_allowShowGraphForName = graphs;
            s_intTypes = intTypes;
            s_floatTypes = floatTypes;
        }

        private static void AddType(
            Dictionary<string, Type> names, 
            Dictionary<Type, string> types, 
            Dictionary<string, bool> allowShowGraphForName, 
            string name, 
            Type type, 
            bool allowShowGraph,
            ISet<Type> typeSet)
        {
            names.Add(name, type);
            types.Add(type, name);
            allowShowGraphForName.Add(name, allowShowGraph);
            if (typeSet != null)
            {
                typeSet.Add(type);
            }
        }

        public static string TypeToName(Type type)
        {
            string name;
            if (!s_typeToName.TryGetValue(type, out name))
            {
                throw new SjsmpDataTypesException("Type '" + type.Name + "' is not allowed in SJMP");
            }
            return name;
        }

        public static bool TypeToName(Type type, out string name)
        {
            return s_typeToName.TryGetValue(type, out name);
        }

        public static Type NameToType(string name)
        {
            Type type;
            if (!s_nameToType.TryGetValue(name, out type))
            {
                throw new SjsmpDataTypesException("Type name '" + name + "' is not allowed");
            };
            return type;
        }

        public static Type[] GetPossibleTypes()
        {
            return s_typeToName.Keys.ToArray();
        }

        public static string[] GetPossibleTypeNames()
        {
            return s_nameToType.Keys.ToArray();
        }

        public static bool IsGraphAllowed(string name)
        {
            bool allowed;
            if (!s_allowShowGraphForName.TryGetValue(name, out allowed))
            {
                throw new SjsmpDataTypesException("Type name '" + name + "' is not allowed");
            };
            return allowed;
        }

        public static bool IsIntType(Type type)
        {
            return s_intTypes.Contains(type);
        }

        public static bool IsFloatType(Type type)
        {
            return s_floatTypes.Contains(type);
        }
    }
}
