using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sjsmp.Server
{
    internal sealed class RequestCommandWrapper
    {
        private readonly JObject m_obj;

        public JObject jObject
        {
            get { return m_obj; }
        }

        public object this[string param]
        {
            get { return m_obj[param]; }
        }

        public readonly string requestId;
        public readonly string action;

        public Dictionary<string, object> getExecuteParams()
        {
            return (Dictionary<string, object>)this["parameters"];
        }

        public RequestCommandWrapper(string body, JsonSerializerSettings settings)
        {
            m_obj = JsonConvert.DeserializeObject<JObject>(body, settings);
            requestId = (string)((JValue)this["request_id"]).Value;
            action = (string)((JValue)this["action"]).Value;
        }
    }
}
