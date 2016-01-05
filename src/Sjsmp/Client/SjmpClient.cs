using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sjsmp.Client
{
    public sealed class SjmpClient
    {
        private const int MAX_RESPONSE_LENGTH = 1 * 1024 * 1024;
        
        private static readonly JsonSerializerSettings m_jsonSettings = new JsonSerializerSettings() {
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        private readonly string m_url;
        private readonly UserCredentials m_auth;
        private int m_token;

        /// <summary>
        /// Creates SJMPClient
        /// </summary>
        /// <param name="url">URL to connect to.</param>
        /// <param name="auth">Auth credentials. Null if not needed.</param>
        public SjmpClient(string url, IClientCredentials auth = null) 
        {
            this.m_url = url;
            if (auth != null)
            {
                this.m_auth = new UserCredentials(auth);
            };
        }

        public JObject GetSchema()
        {
            WebRequest webRequest = WebRequest.Create(m_url);
            webRequest.Method = "POST";
            webRequest.Proxy = null; // Speeds up first query
            if (m_auth != null)
            {
                webRequest.PreAuthenticate = true;
                webRequest.Credentials = m_auth;
            };
            JObject schema = ExecuteRequest(webRequest);

            return schema;
        }

        public JObject GetProperties(string objectName = null, string propertyName = null)
        {
            JObject request = new JObject() { 
                { "action", "get_properties" },
            };

            if (objectName != null)
            {
                request.Add("object_name", objectName);
                if (propertyName != null)
                {
                    request.Add("property_name", propertyName);
                }
            }

            JObject result = SendRequest(request);
            return result;
        }

        public JObject SetProperty(string objectName, string propertyName, object value)
        {
            JObject request = new JObject() { 
                { "action", "set_property" },
                { "object_name", objectName },
                { "property_name", propertyName },
                { "value", new JValue(value) },
            };
            JObject result = SendRequest(request);
            return result;
        }

        public JObject Execute(string objectName, string actionName, IEnumerable<KeyValuePair<string, object>> parameters)
        {
            JObject request = new JObject() { 
                { "action", "execute" },
                { "object_name", objectName },
                { "action_name", actionName },
            };

            JObject parametersJson = new JObject();
            foreach (KeyValuePair<string, object> pair in parameters)
            {
                parametersJson.Add(pair.Key, new JValue(pair.Value));
            }
            request.Add("parameters", parametersJson);

            JObject result = SendRequest(request);
            return result;
        }

        private static JObject ExecuteRequest(WebRequest webRequest)
        {
            using (WebResponse webResp = webRequest.GetResponse())
            {
                string content = ReadResponse(webResp);
                try
                {
                    JObject obj = JsonConvert.DeserializeObject<JObject>(content, m_jsonSettings);
                    return obj;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        private static string ReadResponse(WebResponse webResp)
        {
            if (webResp is HttpWebResponse)
            {
                HttpWebResponse httpResp = (HttpWebResponse)webResp;
                if (httpResp.StatusCode != HttpStatusCode.OK)
                {
                    throw new SjsmpException("Http status code is not OK but " + httpResp.StatusCode);
                }
            }
            using (StreamReader reader = new StreamReader(webResp.GetResponseStream()))
            {
                StringBuilder builder = new StringBuilder();
                char[] buf = new char[4096];
                while (reader.Peek() >= 0) 
                {
                    int readCount = reader.Read(buf, 0, buf.Length);
                    builder.Append(buf, 0, readCount);

                    if (builder.Length >= MAX_RESPONSE_LENGTH)
                    {
                        throw new ArgumentException("Length of the response it too large");
                    }
                }
                return builder.ToString();
            }
        }

        private JObject SendRequest(JObject requestObj)
        {
            requestObj.Add("request_id", Interlocked.Increment(ref m_token).ToString());
            string json = requestObj.ToString(m_jsonSettings.Formatting);

            WebRequest webRequest = WebRequest.Create(m_url);
            webRequest.Proxy = null;
            webRequest.Method = "POST";
            webRequest.ContentType = "text/json";
            if (m_auth != null)
            {
                webRequest.PreAuthenticate = true;
                webRequest.Credentials = m_auth;
            };

            using (StreamWriter writer = new StreamWriter(webRequest.GetRequestStream()))
            {
                writer.Write(json);
            }

            JObject result = ExecuteRequest(webRequest);
            if ((string)result["result"] != "ok")
            {
                throw new SjsmpException("result is not ok : " + (string)result["result"] + "; message is " + (string)result["message"]);
            }
            return result;
        }

        private sealed class UserCredentials : ICredentials
        {
            private readonly NetworkCredential m_credential;

            internal UserCredentials(IClientCredentials clientCredentials)
            {
                m_credential = new NetworkCredential(clientCredentials.Username, clientCredentials.Password);
            }

            public NetworkCredential GetCredential(Uri uri, string authType)
            {
                return m_credential;
            }
        }

    }
}
