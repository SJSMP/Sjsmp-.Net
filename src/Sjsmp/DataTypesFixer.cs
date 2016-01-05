using System;
using System.Collections.Generic;

namespace Sjsmp
{
    internal static class DataTypesFixer
    {
        private static IReadOnlyDictionary<Type, HashSet<Type>> s_types;

        static DataTypesFixer()
        {
            Dictionary<Type, HashSet<Type>> types = new Dictionary<Type, HashSet<Type>>();
            types.Add(
                typeof(Int64), 
                new HashSet<Type>() {
                    typeof(Int32),
                    typeof(Int16),
                    typeof(sbyte),
                    typeof(UInt32),
                    typeof(UInt16),
                    typeof(byte),
                    typeof(double),
                    typeof(float)
                }
            );
            types.Add(
                typeof(double),
                new HashSet<Type>() {
                    typeof(float)
                }
            );
            s_types = types;
        }

        internal static object FixLongType(object obj, Type shouldBeType)
        {
            Type objType = obj.GetType();
            
            /*
             * This all needed because Newtonsoft.JSON serializer
             * always deserialize integers as int64, not honoring 
             * smaller types like int32.
             */

            HashSet<Type> possibleTypes;
            if (!s_types.TryGetValue(objType, out possibleTypes))
            {
                return obj;
            };

            if (!possibleTypes.Contains(shouldBeType))
            {
                return obj;
            };

            object retValue = Convert.ChangeType(obj, shouldBeType);
            return retValue;
        }
    }
}
