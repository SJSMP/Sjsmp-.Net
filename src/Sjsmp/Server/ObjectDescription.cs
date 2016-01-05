using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Collections;

namespace Sjsmp.Server
{
    internal sealed class ObjectDescription
    {
        private readonly object m_object;
        internal readonly string name; 
        private readonly string m_description;
        private readonly string m_group;

        internal IReadOnlyDictionary<string, PropertyOrFieldDescription> properties { get { return m_properties; } }
        internal IReadOnlyDictionary<string, ActionDescription> actions { get { return m_actions; } }

        private readonly Dictionary<string, PropertyOrFieldDescription> m_properties = new Dictionary<string, PropertyOrFieldDescription>();
        private readonly Dictionary<string, ActionDescription> m_actions = new Dictionary<string, ActionDescription>();

        public ObjectDescription(object obj, string name, string description, string group, bool acceptComponentModelAttrributes)
        {
            m_object = obj;
            this.name = name;
            m_description = description;
            m_group = group;

            foreach (PropertyInfo pi in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                SjsmpPropertyAttribute attr = pi.GetCustomAttribute<SjsmpPropertyAttribute>();
                if (attr != null)
                {
                    m_properties.Add(pi.Name, PropertyDescription.CreateWithSjmpAttribute(pi, attr));
                }
            }
            if (acceptComponentModelAttrributes)
            {
                foreach (PropertyInfo pi in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!m_properties.ContainsKey(pi.Name))
                    {
                        m_properties.Add(pi.Name, PropertyDescription.CreateWithComponentModel(pi));
                    }
                }
            }

            FieldInfo[] fieldInfos = obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (FieldInfo fi in fieldInfos)
            {
                SjsmpPropertyAttribute attr = fi.GetCustomAttribute<SjsmpPropertyAttribute>();
                if (attr != null)
                {
                    m_properties.Add(fi.Name, new FieldDescription(fi, attr));
                }
            }

            MethodInfo[] methInfos = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (MethodInfo ai in methInfos)
            {
                SjsmpActionAttribute attr = ai.GetCustomAttribute<SjsmpActionAttribute>();
                if (attr != null)
                {
                    if (m_actions.ContainsKey(ai.Name))
                    {
                        throw new SjsmpServerException("Duplicate action name " + ai.Name);
                    }
                    m_actions.Add(ai.Name, new ActionDescription(ai, attr));
                }
            }
        }

        internal JObject ToJObject()
        {
            JObject result = new JObject() {
                { "description", m_description },
                { "group", m_group }
            };

            JObject properties = new JObject();
            foreach (PropertyOrFieldDescription pd in m_properties.Values)
            {
                properties.Add(pd.name, pd.ToJObject());
            }
            result.Add("properties", properties);

            JObject actions = new JObject();
            foreach (ActionDescription ad in m_actions.Values)
            {
                actions.Add(ad.name, ad.ToJObject());
            }
            result.Add("actions", actions);
            return result;
        }
    }

    internal interface PropertyOrFieldDescription
    {
        string name { get; }
        JObject ToJObject();
        JValue GetValue(object obj);
        void SetValue(object obj, object value);
    }

    internal sealed class PropertyDescription : PropertyOrFieldDescription
    {
        private readonly PropertyInfo m_pi;
        private readonly string m_description;
        private readonly bool m_readonly;
        private readonly bool m_showGraph;
        private readonly string m_sjmpTypeName;
        private readonly bool m_needToString;
        private readonly SjsmpPropertyLimitsAttribute m_limits;

        public string name { get { return m_pi.Name; } }

        private PropertyDescription(PropertyInfo pi, string description, bool isReadonly, bool showGraph)
        {
            m_pi = pi;
            m_description = description;
            m_readonly = isReadonly;
            m_showGraph = showGraph;
            Type type = pi.PropertyType;

            if (!DataTypes.TypeToName(type, out m_sjmpTypeName))
            {
                m_sjmpTypeName = DataTypes.TypeToName(typeof(string));
                m_needToString = true;
                m_readonly = true;
            }
            else
            {
                m_needToString = false;
            }
            
            if (m_showGraph && !DataTypes.IsGraphAllowed(m_sjmpTypeName))
            {
                throw new SjsmpServerException("Having 'showGraph' for type '" + m_sjmpTypeName + "' is not allowed. " + m_pi.DeclaringType + "." + m_pi.Name);
            }

            m_limits = pi.GetCustomAttribute<SjsmpPropertyLimitsAttribute>();
            if (m_limits != null)
            {
                if (!m_limits.useFloat)
                {
                    if (!DataTypes.IsIntType(type))
                    {
                        throw new SjsmpServerException("Having int PropertyLimits for type '" + m_sjmpTypeName + "' is not allowed." + m_pi.DeclaringType + "." + m_pi.Name);
                    }
                }
                else
                {
                    if (!DataTypes.IsFloatType(type))
                    {
                        throw new SjsmpServerException("Having float PropertyLimits for type '" + m_sjmpTypeName + "' is not allowed. " + m_pi.DeclaringType + "." + m_pi.Name);
                    }
                }
            }
        }

        public static PropertyDescription CreateWithSjmpAttribute(PropertyInfo pi, SjsmpPropertyAttribute attr)
        {
            return new PropertyDescription(pi, attr.description, !pi.CanWrite || attr.isReadonly, attr.showGraph);
        }

        public static PropertyDescription CreateWithComponentModel(PropertyInfo pi)
        {
            DisplayNameAttribute dnAttr = pi.GetCustomAttribute<DisplayNameAttribute>();
            string description = dnAttr != null? dnAttr.DisplayName : pi.Name;
            
            ReadOnlyAttribute roAttr = pi.GetCustomAttribute<ReadOnlyAttribute>();
            bool isReadonly = false;
            if (roAttr != null && roAttr.IsReadOnly)
            {
                isReadonly = true;
            }
            if (!pi.CanWrite || pi.GetSetMethod(false) == null)
            {
                isReadonly = true;
            }
            return new PropertyDescription(pi, description, isReadonly, false);
        }

        public JObject ToJObject()
        {
            JObject result = new JObject() {
                { "type", m_sjmpTypeName },
                { "readonly", m_readonly },
                { "description", m_description }
            };
            if (m_showGraph)
            {
                result.Add("show_graph", true);
            }
            if (m_limits != null)
            {
                result.Add("limits",
                    m_limits.useFloat?
                        new JObject() {
                            {"min", (double)m_limits.min },
                            {"max", (double)m_limits.max }
                        }
                        :
                        new JObject() {
                            {"min", (long)m_limits.min },
                            {"max", (long)m_limits.max }
                        }
                );
            }
            return result;
        }

        public JValue GetValue(object obj)
        {
            object objVal = m_pi.GetValue(obj);
            if (m_needToString && objVal != null)
            {
                objVal = objVal.ToString();
            }
            JValue value = new JValue(objVal);
            return value;
        }

        public void SetValue(object obj, object value)
        {
            if (m_readonly)
            {
                throw new ApplicationException("Attempt to set value for readonly property " + m_pi.DeclaringType + "." + m_pi.Name);
            }

            value = DataTypesFixer.FixLongType(value, m_pi.PropertyType);

            if (m_limits != null && Comparer.Default.Compare(value, Convert.ChangeType(m_limits.min, m_pi.PropertyType)) < 0)
            {
                throw new SjsmpServerException("Trying to set a value that is less than minimal");
            }
            if (m_limits != null && Comparer.Default.Compare(value, Convert.ChangeType(m_limits.max, m_pi.PropertyType)) > 0)
            {
                throw new SjsmpServerException("Trying to set a value that is greater than maximal");
            }

            m_pi.SetValue(obj, value);            
        }
    }

    internal sealed class FieldDescription : PropertyOrFieldDescription
    {
        private readonly FieldInfo m_fi;
        private readonly SjsmpPropertyAttribute m_attr;

        public string name { get { return m_fi.Name; } }

        public FieldDescription(FieldInfo pi, SjsmpPropertyAttribute attr)
        {
            this.m_fi = pi;
            this.m_attr = attr;
        }

        public JObject ToJObject()
        {
            bool isReadonly = m_attr.isReadonly;
            string typeName = DataTypes.TypeToName(m_fi.FieldType);
            JObject result = new JObject() {
                { "type", typeName },
                { "readonly", isReadonly },
                { "description", m_attr.description }
            };
            if (m_attr.showGraph)
            {
                if (!DataTypes.IsGraphAllowed(typeName))
                {
                    throw new SjsmpServerException("Having 'showGraph' for type '" + typeName + "' is not allowed. " + m_fi.DeclaringType + "." + m_fi.Name);
                }
                result.Add("show_graph", true);
            }
            return result;
        }

        public JValue GetValue(object obj)
        {
            JValue value = new JValue(m_fi.GetValue(obj));
            return value;
        }

        public void SetValue(object obj, object value)
        {
            value = DataTypesFixer.FixLongType(value, m_fi.FieldType);
            m_fi.SetValue(obj, value);
        }
    }

    internal sealed class ActionDescription
    {
        private readonly MethodInfo m_ai;
        private readonly Dictionary<string, Parameter> m_parameterInfo = new Dictionary<string, Parameter>();
        private readonly SjsmpActionAttribute m_attr;

        public string name { get { return m_ai.Name; } }

        public ActionDescription(MethodInfo ai, SjsmpActionAttribute attr)
        {
            this.m_ai = ai;
            this.m_attr = attr;

            ParameterInfo[] pInfos = m_ai.GetParameters();
            for (int i = 0; i < pInfos.Length; ++i)
            {
                ParameterInfo pInfo = pInfos[i];
                m_parameterInfo.Add(pInfo.Name, new Parameter(i, pInfo.ParameterType));
            };
        }

        internal JObject ToJObject()
        {
            JObject result = new JObject() {
                { "result", DataTypes.TypeToName(m_ai.ReturnType) },
                { "description", m_attr.description },
                { "require_confirm", m_attr.requireConfirm }
            };

            JObject parameters = new JObject();
            foreach (ParameterInfo pi in m_ai.GetParameters())
            {
                JObject parameter = new JObject() {
                    { "type", DataTypes.TypeToName(pi.ParameterType) }
                };
                SjsmpActionParameterAttribute attr = pi.GetCustomAttribute<SjsmpActionParameterAttribute>();
                if (attr != null)
                {
                    parameter.Add("description", attr.description);
                }
                parameters.Add(pi.Name, parameter);
            }
            result.Add("parameters", parameters);
            return result;
        }

        internal JValue Call(object obj, JObject parameters)
        {
            object[] arguments = new object[m_parameterInfo.Count];
            foreach (KeyValuePair<string, JToken> pair in parameters)
            {
                string name = pair.Key;
                JToken token = pair.Value;
                if (!(token is JValue))
                {
                    throw new ArgumentException("Argument '" + name + "' must be a scalar type");
                };
                JValue jValue = (JValue)token;
                object value = jValue.Value;

                Parameter parameter;
                if (!m_parameterInfo.TryGetValue(name, out parameter))
                {
                    throw new ArgumentException("Argument '" + name + "' does not exist");
                };

                value = DataTypesFixer.FixLongType(value, parameter.type);
                arguments[parameter.index] = value;
            };
            object retValue = m_ai.Invoke(obj, arguments);
            JValue retJValue = new JValue(retValue);
            return retJValue;
        }

        private struct Parameter
        {
            internal readonly int index;
            internal readonly Type type;

            internal Parameter(int index, Type type)
            {
                this.index = index;
                this.type = type;
            }
        }
    }
}
