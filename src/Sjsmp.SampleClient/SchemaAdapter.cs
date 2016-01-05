using Newtonsoft.Json.Linq;
using Sjsmp;
using Sjsmp.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sjsmp.SampleClient
{
    internal class DefaultAdapter : ICustomTypeDescriptor
    {
        protected object m_propertyOwner;
        protected PropertyDescriptorCollection m_properties;

        public AttributeCollection GetAttributes()
        {
            return TypeDescriptor.GetAttributes(this, true);
        }

        public string GetClassName()
        {
            return TypeDescriptor.GetClassName(this, true);
        }

        public string GetComponentName()
        {
            return TypeDescriptor.GetComponentName(this, true);
        }

        public TypeConverter GetConverter()
        {
            return TypeDescriptor.GetConverter(this, true);
        }

        public EventDescriptor GetDefaultEvent()
        {
            return TypeDescriptor.GetDefaultEvent(this, true);
        }

        public PropertyDescriptor GetDefaultProperty()
        {
            return null;
        }

        public object GetEditor(Type editorBaseType)
        {
            return TypeDescriptor.GetEditor(this, editorBaseType, true);
        }

        public EventDescriptorCollection GetEvents(Attribute[] attributes)
        {
            return TypeDescriptor.GetEvents(this, attributes, true);
        }

        public EventDescriptorCollection GetEvents()
        {
            return TypeDescriptor.GetEvents(this, true);
        }

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            return m_properties;
        }

        public PropertyDescriptorCollection GetProperties()
        {
            return ((ICustomTypeDescriptor)this).GetProperties(new Attribute[0]);
        }

        public object GetPropertyOwner(PropertyDescriptor pd)
        {
            return m_propertyOwner;
        }

        public override string ToString()
        {
            return "";
        }
    }
    
    internal sealed class SchemaAdapter : DefaultAdapter, INotifyPropertyChanged
    {
        private readonly SjmpClient m_client;
        private JObject m_currentSchema;
        private string m_currentSchemaVersion;
        private Dictionary<string, ObjectAdapter> m_objectByNames = new Dictionary<string, ObjectAdapter>();

        public delegate void JsonReceived(JObject jObj);
        public event JsonReceived JsonReceivedEvent;

        public delegate void ActionResult(string objectName, string actionName, JToken resultToken);
        public event ActionResult ActionResultEvent;
        public event PropertyChangedEventHandler PropertyChanged;

        public SchemaAdapter(string schemaUrl, ClientAuth auth)
        {
            m_client = new SjmpClient(schemaUrl, auth);
            m_propertyOwner = this;
            JsonReceivedEvent += (jObj) => { };
            ActionResultEvent += (objectName, actionName, resultToken) => { };
            PropertyChanged += (sender, e) => { };
        }

        internal void RefreshSchema()
        {
            JObject newSchema = m_client.GetSchema();
            JsonReceivedEvent(newSchema);

            string newSchemaVersion = (string)newSchema["schema_version"];
            if (newSchemaVersion != null
                && (m_currentSchemaVersion == null || newSchemaVersion != m_currentSchemaVersion)
                )
            {
                //schema changed
                m_currentSchema = newSchema;
                m_currentSchemaVersion = newSchemaVersion;
                m_propertyOwner = m_currentSchema;
                RebuildStructure();
                PropertyChanged(this, new PropertyChangedEventArgs(string.Empty));
            }
        }

        internal void RefreshPropertyValues()
        {
            JObject values = m_client.GetProperties();
            JsonReceivedEvent(values);
            HandlePropertiesRefreshResult(values);
        }

        private void HandlePropertiesRefreshResult(JObject properitesValuesMessage)
        {
            JObject objectsContainer = (JObject)properitesValuesMessage["objects"];
            foreach (KeyValuePair<string, JToken> pair in objectsContainer)
            {
                if (pair.Value is JObject)
                {
                    string objectName = pair.Key;
                    JObject values = (JObject)pair.Value;
                    ObjectAdapter objectAdapter;
                    if (m_objectByNames.TryGetValue(objectName, out objectAdapter))
                    {
                        objectAdapter.UpdatePropertyValues(objectName, values);
                    }
                }
            }
        }

        internal void NotifyPropertyChanged(object owner, string propertyName)
        {
            PropertyChanged(owner, new PropertyChangedEventArgs(propertyName));
        }

        private void RebuildStructure()
        {
            m_objectByNames.Clear();
            List<PropertyDescriptor> properties = new List<PropertyDescriptor>();
            if (m_currentSchema != null)
            {
                foreach (KeyValuePair<string, JToken> pair in m_currentSchema)
                {
                    if (pair.Value is JValue)
                    {
                        properties.Add(new SchemaPropertyDescriptor(this, pair.Key, (JValue)pair.Value));
                    }
                    else if (pair.Value is JObject)
                    {
                        JObject jContainer = (JObject)pair.Value;
                        switch (pair.Key)
                        {
                        case "objects":
                            foreach (KeyValuePair<string, JToken> innerPair in jContainer)
                            {
                                if (innerPair.Value is JObject)
                                {
                                    string objectName = innerPair.Key;
                                    JObject innerObject = (JObject)innerPair.Value;
                                    ObjectAdapter objectAdapter = new ObjectAdapter(this, innerPair.Key, innerObject);
                                    m_objectByNames.Add(objectName, objectAdapter); 
                                    properties.Add(
                                        new AdapterPropertyDescriptor<ObjectAdapter>(
                                            objectAdapter,
                                            objectName,
                                            new Attribute[] { 
                                                new CategoryAttribute((string)innerObject["group"]),
                                                new DescriptionAttribute((string)innerObject["description"])
                                            }
                                        )
                                    );
                                }
                            }
                            break;
                        }
                    }
                }
            }
            PropertyDescriptor[] props = (PropertyDescriptor[])properties.ToArray<PropertyDescriptor>();

            m_properties = new PropertyDescriptorCollection(props);
        }

        internal void SetPropertyValue(string objectName, string propertyName, object value)
        {
            JObject result = m_client.SetProperty(objectName, propertyName, value);
            JsonReceivedEvent(result);
        }

        internal void ExecuteAction(string objectName, string actionName, Dictionary<string, object> paramValues)
        {
            JObject result = m_client.Execute(objectName, actionName, paramValues);
            JsonReceivedEvent(result);
            ActionResultEvent(objectName, actionName, result["value"]);
        }
    }

    internal sealed class SchemaPropertyDescriptor : PropertyDescriptor
    {
        private readonly Type m_type;
        private readonly object m_value;

        internal SchemaPropertyDescriptor(SchemaAdapter adapter, string name, JValue jValue)
            : base(name, null)
        {
            m_value = jValue.Value;
            if (m_value == null)
            {
                m_type = typeof(void);
            }
            m_type = m_value.GetType();

            string descr = "";
            switch (name)
            {
            case "result":
                descr = "Request result, should be 'ok' if request succeeded";
                break;
            case "type":
                descr = "Type of schema, should be 'SimpleJMP/schema'";
                break;
            case "version":
                descr = "Version of schema, should be '1.0'";
                break;
            case "name":
                descr = "Identifying name of the SJMP service";
                break;
            case "description":
                descr = "Comprehensive description of the SJMP service";
                break;
            case "group":
                descr = "Used to group services when there are many of them";
                break;
            case "port":
                descr = "SJMP service port. Used when 'schema push' method is used";
                break;
            case "schema_version":
                descr = "String used to determine if schema has changed since last time";
                break;
            }

            this.AttributeArray = new Attribute[] { 
                    new CategoryAttribute(" Schema"),
                    new DescriptionAttribute(descr)
                };
        }

        public override Type PropertyType
        {
            get { return m_type; }
        }

        public override void SetValue(object component, object value)
        {
            throw new NotSupportedException();
        }

        public override object GetValue(object component)
        {
            return m_value;
        }

        public override bool IsReadOnly
        {
            get { return true; }
        }

        public override Type ComponentType
        {
            get { return null; }
        }

        public override bool CanResetValue(object component)
        {
            return false;
        }

        public override void ResetValue(object component)
        {
        }

        public override bool ShouldSerializeValue(object component)
        {
            return false;
        }
    }

    internal sealed class AdapterPropertyDescriptor<T> : PropertyDescriptor
    {
        private readonly T m_adapter;

        internal AdapterPropertyDescriptor(T adapter, string name, Attribute[] attributes)
            : base(name, attributes)
        {
            m_adapter = adapter;
        }

        public override Type PropertyType
        {
            get { return typeof(T); }
        }

        public override void SetValue(object component, object value)
        {
            throw new NotSupportedException();
        }

        public override object GetValue(object component)
        {
            return m_adapter;
        }

        public override bool IsReadOnly
        {
            get { return true; }
        }

        public override Type ComponentType
        {
            get { return null; }
        }

        public override bool CanResetValue(object component)
        {
            return false;
        }

        public override void ResetValue(object component)
        {
        }

        public override bool ShouldSerializeValue(object component)
        {
            return false;
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    internal sealed class ObjectAdapter : DefaultAdapter
    {
        private readonly SchemaAdapter m_schemaAdapter;
        private readonly string m_name;
        private readonly Dictionary<string, PropertyPropertyDescriptor> m_propertiesByName = new Dictionary<string, PropertyPropertyDescriptor>();

        internal ObjectAdapter(SchemaAdapter adapter, string name, JObject jObject)
        {
            m_schemaAdapter = adapter;
            m_name = name;

            List<PropertyDescriptor> properties = new List<PropertyDescriptor>();

            foreach (KeyValuePair<string, JToken> pair in jObject)
            {
                if (pair.Value is JObject)
                {
                    JObject jContainer = (JObject)pair.Value;
                    switch (pair.Key)
                    {
                    case "properties":
                        properties.Add(
                            new AdapterPropertyDescriptor<PropertiesListAdapter>(
                                new PropertiesListAdapter(this, jContainer, m_propertiesByName),
                                pair.Key,
                                null
                            )
                        );
                        break;
                    case "actions":
                        properties.Add(
                            new AdapterPropertyDescriptor<ActionsListAdapter>(
                                new ActionsListAdapter(this, jContainer),
                                pair.Key,
                                null
                            )
                        );
                        break;
                    }
                }
            }
        
            PropertyDescriptor[] props = (PropertyDescriptor[])properties.ToArray<PropertyDescriptor>();
            m_properties = new PropertyDescriptorCollection(props);
            m_propertyOwner = this;
        }

        internal void SetPropertyValue(string propertyName, object value)
        {
            m_schemaAdapter.SetPropertyValue(m_name, propertyName, value);
        }

        internal void ExecuteAction(string actionName, Dictionary<string, object> paramValues)
        {
            m_schemaAdapter.ExecuteAction(m_name, actionName, paramValues);
        }

        internal void UpdatePropertyValues(string objectName, JObject values)
        {
            foreach (KeyValuePair<string, JToken> pair in values)
            {
                string propertyName = pair.Key;
                if (pair.Value is JValue)
                {
                    JValue value = (JValue)pair.Value;
                    PropertyPropertyDescriptor propDescr;
                    if (m_propertiesByName.TryGetValue(propertyName, out propDescr))
                    {
                        if (propDescr.SetValueFromServer(value.Value))
                        {
                            m_schemaAdapter.NotifyPropertyChanged(this, propertyName);
                        }
                    }
                }
            }
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    internal sealed class PropertiesListAdapter : DefaultAdapter
    {
        internal PropertiesListAdapter(ObjectAdapter adapter, JObject jObject, Dictionary<string, PropertyPropertyDescriptor> propertiesByName)
        {
            List<PropertyDescriptor> properties = new List<PropertyDescriptor>();

            foreach (KeyValuePair<string, JToken> pair in jObject)
            {
                if (pair.Value is JObject)
                {
                    string propertyName = pair.Key;
                    PropertyPropertyDescriptor descriptor = new PropertyPropertyDescriptor(adapter, propertyName, (JObject)pair.Value);
                    propertiesByName.Add(propertyName, descriptor);
                    properties.Add(descriptor);
                }
            }

            PropertyDescriptor[] props = (PropertyDescriptor[])properties.ToArray<PropertyDescriptor>();
            m_properties = new PropertyDescriptorCollection(props);
            m_propertyOwner = adapter;
        }

        public override string ToString()
        {
            return "...";
        }
    }

    internal sealed class PropertyPropertyDescriptor : PropertyDescriptor
    {
        private readonly ObjectAdapter m_objectAdapter;
        private readonly string m_name;
        private Type m_type;
        private bool m_isReadonly;
        private bool m_showGraph;
        private object m_value;
        private object m_limitsMin;
        private object m_limitsMax;

        internal PropertyPropertyDescriptor(ObjectAdapter adapter, string name, JObject jObject)
            : base(name, null)
        {
            m_objectAdapter = adapter;
            m_name = name;
            m_value = null;

            string typeName = (string)jObject["type"];
            m_type = DataTypes.NameToType(typeName);

            m_isReadonly = (bool)jObject["readonly"];
            m_showGraph = (bool?)jObject["show_graph"] ?? false;
            string description = (string)jObject["description"];
            if (m_showGraph)
            {
                description += "\n(showGraph)";
            }
            JObject limits = (JObject)jObject["limits"];
            if (limits != null)
            {
                m_limitsMin = Convert.ChangeType(((JValue)limits["min"]).Value, m_type);
                m_limitsMax = Convert.ChangeType(((JValue)limits["max"]).Value, m_type);
                description += String.Format("\nValue limits: min = {0}; max = {1}", m_limitsMin, m_limitsMax);
            }
            else
            {
                m_limitsMin = null;
                m_limitsMax = null;
            }

            this.AttributeArray = new Attribute[] { 
                    new CategoryAttribute("Properties"),
                    new DescriptionAttribute(description),
                    //new ReadOnlyAttribute(m_isReadonly),
                };
        }

        public override Type PropertyType
        {
            get { return m_type; }
        }

        internal bool SetValueFromServer(object value)
        {
            if (m_value != value)
            {
                m_value = value;
                return true;
            }
            return false;
        }

        public override void SetValue(object component, object value)
        {
            if (m_isReadonly)
            {
                throw new NotSupportedException();
            }
            if (m_limitsMin != null && Comparer.Default.Compare(value, m_limitsMin) < 0)
            {
                throw new SjsmpException("Trying to set a value that is less than minimal");
            }
            if (m_limitsMax != null && Comparer.Default.Compare(value, m_limitsMax) > 0)
            {
                throw new SjsmpException("Trying to set a value that is greater than maximal");
            }
            m_value = value;
            m_objectAdapter.SetPropertyValue(m_name, value);
        }

        public override object GetValue(object component)
        {
            return m_value;
        }

        public override bool IsReadOnly
        {
            get { return m_isReadonly; }
        }

        public override Type ComponentType
        {
            get { return null; }
        }

        public override bool CanResetValue(object component)
        {
            return false;
        }

        public override void ResetValue(object component)
        {
        }

        public override bool ShouldSerializeValue(object component)
        {
            return false;
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    internal sealed class ActionsListAdapter : DefaultAdapter
    {
        internal ActionsListAdapter(ObjectAdapter adapter, JObject jObject)
        {
            List<PropertyDescriptor> properties = new List<PropertyDescriptor>();

            foreach (KeyValuePair<string, JToken> pair in jObject)
            {
                if (pair.Value is JObject)
                {
                    properties.Add(new ActionPropertyDescriptor(adapter, pair.Key, (JObject)pair.Value));
                }
            }

            PropertyDescriptor[] props = (PropertyDescriptor[])properties.ToArray<PropertyDescriptor>();
            m_properties = new PropertyDescriptorCollection(props);
            m_propertyOwner = adapter;
        }

        public override string ToString()
        {
            return "...";
        }
    }

    internal sealed class ActionPropertyDescriptor : PropertyDescriptor
    {
        internal readonly ActionAdapter actionAdapter;

        internal ActionPropertyDescriptor(ObjectAdapter adapter, string name, JObject jObject)
            : base(name, null)
        {
            actionAdapter = new ActionAdapter(adapter, name, jObject);

            this.AttributeArray = new Attribute[] { 
                    new CategoryAttribute("Actions"),
                    new DisplayNameAttribute(actionAdapter.displayName),
                    new DescriptionAttribute((string)jObject["description"])
                };
        }

        public override Type PropertyType
        {
            get { return typeof(ActionAdapter); }
        }

        public override void SetValue(object component, object value)
        {
            throw new NotSupportedException();
        }

        public override object GetValue(object component)
        {
            return actionAdapter;
        }

        public override bool IsReadOnly
        {
            get { return true; }
        }

        public override Type ComponentType
        {
            get { return null; }
        }

        public override bool CanResetValue(object component)
        {
            return false;
        }

        public override void ResetValue(object component)
        {
        }

        public override bool ShouldSerializeValue(object component)
        {
            return false;
        }
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    [Editor(typeof(ActonExecutorPropertyGridEditor), typeof(UITypeEditor))]
    internal sealed class ActionAdapter : DefaultAdapter
    {
        private readonly Dictionary<string, object> m_paramValues = new Dictionary<string, object>();
        private readonly ObjectAdapter m_objectAdapter;
        private readonly string m_name;
        internal readonly string displayName;
        internal readonly bool requireConfirm;

        internal ActionAdapter(ObjectAdapter adapter, string name, JObject jObject)
        {
            m_objectAdapter = adapter;
            m_name = name;
            this.requireConfirm = ((bool?)jObject["require_confirm"]) ?? false;

            StringBuilder displayName = new StringBuilder();
            displayName.Append((string)jObject["result"])
                .Append(" ")
                .Append(name)
                .Append("(");

            
            List<PropertyDescriptor> properties = new List<PropertyDescriptor>();

            int paramIndex = 0;
            foreach (KeyValuePair<string, JToken> pair in jObject)
            {
                if (pair.Value is JObject)
                {
                    if (pair.Key == "parameters")
                    {
                        JObject container = (JObject)pair.Value;
                        foreach (KeyValuePair<string, JToken> innerPair in container)
                        {
                            if (innerPair.Value is JObject)
                            {
                                string paramName = innerPair.Key;
                                JObject paramJObj = (JObject)innerPair.Value;
                                if (paramIndex > 0)
                                {
                                    displayName.Append(", ");
                                }
                                displayName
                                    .Append((string)paramJObj["type"])
                                    .Append(" ")
                                    .Append(paramName);
                                ++paramIndex;

                                properties.Add(new ActionParamPropertyDescriptor(this, paramName, paramJObj));
                            }
                        }
                    }
                }
            }
            PropertyDescriptor[] props = (PropertyDescriptor[])properties.ToArray<PropertyDescriptor>();
            m_properties = new PropertyDescriptorCollection(props);
            m_propertyOwner = m_objectAdapter;

            displayName.Append(")");
            this.displayName = displayName.ToString();
        }

        public override string ToString()
        {
            return "press to execute > ";
        }

        internal void SetParameterValue(string name, object value)
        {
            m_paramValues[name] = value;
        }

        internal void ExecuteAction()
        {
            m_objectAdapter.ExecuteAction(m_name, m_paramValues);
        }
    }

    internal sealed class ActionParamPropertyDescriptor : PropertyDescriptor
    {
        private readonly ActionAdapter m_actionAdapter;
        private readonly string m_name;
        private Type m_type;
        private object m_value;

        internal ActionParamPropertyDescriptor(ActionAdapter adapter, string name, JObject jObject)
            : base(name, null)
        {
            m_actionAdapter = adapter;
            m_name = name;

            string typeName = (string)jObject["type"];
            m_type = DataTypes.NameToType(typeName);

            m_value = null;

            this.AttributeArray = new Attribute[] { 
                    new CategoryAttribute("Parameters"),
                    new DescriptionAttribute((string)jObject["description"])
                };
        }

        public override Type PropertyType
        {
            get { return m_type; }
        }

        public override void SetValue(object component, object value)
        {
            m_value = value;
            m_actionAdapter.SetParameterValue(m_name, value);
        }

        public override object GetValue(object component)
        {
            return m_value;
        }

        public override bool IsReadOnly
        {
            get { return false; }
        }

        public override Type ComponentType
        {
            get { return null; }
        }

        public override bool CanResetValue(object component)
        {
            return false;
        }

        public override void ResetValue(object component)
        {
        }

        public override bool ShouldSerializeValue(object component)
        {
            return false;
        }
    }

    internal sealed class ActonExecutorPropertyGridEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            if (context != null)
            {
                ActionPropertyDescriptor desc = context.PropertyDescriptor as ActionPropertyDescriptor;
                if (desc != null)
                {
                    ActionAdapter adapter = desc.actionAdapter;
                    if (!adapter.requireConfirm
                        || MessageBox.Show("Confirm execution of action " + adapter.displayName, "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes
                        )
                    {
                        adapter.ExecuteAction();
                    }
                }
            }

            return value;
        }
    }

    internal sealed class ClientAuth : IClientCredentials
    {
        public string Username { get; private set; }
        public string Password { get; private set; }

        internal ClientAuth(string username, string password)
        {
            this.Username = username;
            this.Password = password;
        }
    }
}
