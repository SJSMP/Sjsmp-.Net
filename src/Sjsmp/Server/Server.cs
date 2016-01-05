using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NHttp;
using NLog;
using System.Threading.Tasks;

namespace Sjsmp.Server
{
    public sealed class SjmpServer : IDisposable
    {
        private const ushort PORT_MIN = 40234;
        private const ushort PORT_MAX = PORT_MIN + 1000;

        private const int MAX_REQUEST_LENGTH = 1 * 1024 * 1024;
        private const int MAX_RESPONSE_LENGTH = 1 * 1024 * 1024;
        private const int SHEMA_PUSH_INTERVAL_MS = 1000 * 60;   //1 minute

        public readonly string SJMP_VERSION = "1.0";

        private HttpServer m_server;
        private readonly JsonSerializerSettings m_jsonSettings = new JsonSerializerSettings() {
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Error,
        };
        private readonly Encoding m_utf8 = new UTF8Encoding(false);
        private readonly Logger m_logger = LogManager.GetCurrentClassLogger();
        private readonly ushort m_port;
        private readonly string m_name;
        private readonly string m_description;
        private readonly string m_group;
        private readonly IServerAuthorization m_auth;
        private readonly Uri m_schemaPushUrl;
                
        private string m_schema;
        private Int64 m_schemaVersionNumber = 0;
        private readonly ReaderWriterLockSlim m_schemaLock = new ReaderWriterLockSlim();
        private readonly Dictionary<object, ObjectDescription> m_objects = new Dictionary<object, ObjectDescription>();
        private readonly Dictionary<string, object> m_objectNames = new Dictionary<string, object>();

        private readonly System.Threading.Timer m_timer;

        /// <summary>
        /// Constructs Server object.
        /// </summary>
        /// <param name="name">Server name - appears as this in JSON schema.</param>
        /// <param name="description">Server description - appears in JSON schema.</param>
        /// <param name="group">Server group - appears in JSON schema.</param>
        /// <param name="startPort">Minimum TCP port to bind to. Use 0 for default value (40234).</param>
        /// <param name="endPort">Maximum TCP port to bind to. Use 0 for default value (41234)</param>
        /// <param name="auth">Server uses HTTP Basic auth, if auth object is provided. Use null to disable auth check.</param>
        /// <param name="schemaPushUrl">Auto-push schema to specified URL. Null to disable.</param>
        public SjmpServer(
            string name, 
            string description,
            string group,
            ushort startPort = 0,
            ushort endPort = 0,
            IServerAuthorization auth = null, 
            string schemaPushUrl = null
        )
        {
            m_logger.Info("Starting SjmpServer");

            if (startPort == 0)
            {
                startPort = PORT_MIN;
            };

            if (endPort == 0)
            {
                endPort = PORT_MAX;
            };

            m_name = name;
            m_description = description;
            m_group = group;
            m_port = startPort;
            m_auth = auth;
            m_schemaPushUrl = null;

            m_server = new HttpServer();
            m_server.RequestReceived += RequestReceived;

            while (true)
            {
                RefreshSchema();

                try
                {
                    m_server.EndPoint = new IPEndPoint(IPAddress.Any, m_port);
                    m_server.Start();
                    break;
                }
                catch (NHttpException e)
                {
                    if (e.InnerException is SocketException)
                    {
                        m_logger.Info("Can not bind to port " + m_port + ", trying next");
                    }
                    else
                    {
                        throw new SjsmpServerException("Server start error", e);
                    };
                };

                if (++m_port > endPort)
                {
                    throw new SjsmpServerException("Can not find free TCP port in range " + startPort + " - " + endPort + " to bind to");
                };
            };
            m_logger.Info("Listening TCP port " + m_port);

            if (schemaPushUrl != null)
            {
                try
                {
                    m_schemaPushUrl = new Uri(schemaPushUrl);
                    m_logger.Info("Starting schema push thread for url " + m_schemaPushUrl);
                    m_timer = new Timer(ShemaPushJob, null, SHEMA_PUSH_INTERVAL_MS / 10, SHEMA_PUSH_INTERVAL_MS);
                }
                catch (Exception e)
                {
                    m_logger.Error(e, "Error starting schema push");
                }
            }
        }

        private void ShemaPushJob(object state)
        {
            try
            {
                WebRequest webRequest = WebRequest.Create(m_schemaPushUrl);
                webRequest.Proxy = null;
                webRequest.Method = "POST";
                webRequest.ContentType = "text/json";
                using (StreamWriter writer = new StreamWriter(webRequest.GetRequestStream()))
                {
                    writer.Write(m_schema);
                }

                JObject jResp;
                using (WebResponse webResp = webRequest.GetResponse())
                {
                    string content = ReadResponse(webResp);
                    try
                    {
                        jResp = JsonConvert.DeserializeObject<JObject>(content, m_jsonSettings);
                    }
                    catch (Exception)
                    {
                        jResp = null;
                    }
                }

                if (jResp == null)
                {
                    throw new SjsmpServerException("Schema push result is empty");
                }

                if ((string)jResp["result"] != "ok")
                {
                    throw new SjsmpServerException("Schema push result is not ok : " + (string)jResp["result"] + "; message is " + (string)jResp["message"]);
                }
            }
            catch (Exception e)
            {
                m_logger.Warn(e, "Failed to push schema");
            }
        }

        private static string ReadResponse(WebResponse webResp)
        {
            if (webResp is HttpWebResponse)
            {
                HttpWebResponse httpResp = (HttpWebResponse)webResp;
                if (httpResp.StatusCode != HttpStatusCode.OK)
                {
                    throw new SjsmpServerException("Schema push http status code is not OK but " + httpResp.StatusCode);
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

#region HTTP request handling
        private void RequestReceived(object sender, HttpRequestEventArgs e)
        {
            HttpRequest request = e.Request;

            if (m_auth != null)
            {
                if (!TryAuth(request, m_auth))
                {
                    m_logger.Trace("[" + request.UserHostAddress + "][error] Auth false, returning Unauthorized");
                    e.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    e.Response.StatusDescription = HttpResponseCodeStatus.Get(HttpStatusCode.Unauthorized);
                    e.Response.Headers.Add("WWW-Authenticate", "Basic realm=\"" + this.m_name.Replace("\"", "") + "\"");
                    return;
                };
            };

            string requestId = null;

            // Method allowed
            if (request.HttpMethod != "POST")
            {
                m_logger.Trace("[" + request.UserHostAddress + "][error] Wrong request method");
                MakeReponseError(e.Response, requestId, "Wrong request method", HttpStatusCode.Forbidden);
                return;
            };

            try
            {
                string path = request.Path;
                string body = ReadInput(request);
                if (String.IsNullOrWhiteSpace(body))
                {
                    ProcessSchemaRequest(e);
                    return;
                }

                RequestCommandWrapper command = new RequestCommandWrapper(body, m_jsonSettings);
                requestId = command.requestId;
                ProcessServiceRequest(e, command);
            }
            catch (Exception ex)
            {
                bool needError = !(ex is ArgumentException);
                m_logger.Log(needError? LogLevel.Error : LogLevel.Warn, "[" + request.UserHostAddress + "][error] Exception: \n" + ex.ToString());
                MakeReponseError(e.Response, requestId, "exception: " + ex.Message);
                return;
            };
        }

        private void ProcessSchemaRequest(HttpRequestEventArgs e)
        {
            HttpResponse response = e.Response;
            MakeResponse(response, HttpStatusCode.OK, m_schema);
        }

        private void ProcessServiceRequest(HttpRequestEventArgs e, RequestCommandWrapper command)
        {
            JObject responseObject;
            switch (command.action)
            {
            case "get_properties":
                responseObject = ProcessGetProperties(command);
                break;
            case "set_property":
                responseObject = ProcessSetProperty(command);
                break;
            case "execute":
                responseObject = ProcessExecute(command);
                break;
            default:
                throw new ApplicationException("unsupported action '" + command.action + "'");
            }
            HttpResponse response = e.Response;
            MakeResponse(response, HttpStatusCode.OK, responseObject);
        }

        private static string ReadInput(HttpRequest request)
        {
            if (request.InputStream == null)
            {
                return null;
            }
            using (StreamReader reader = new StreamReader(request.InputStream))
            {
                StringBuilder builder = new StringBuilder();
                char[] buf = new char[1024];
                while (reader.Peek() >= 0) 
                {
                    int readCount = reader.Read(buf, 0, buf.Length);
                    builder.Append(buf, 0, readCount);

                    if (builder.Length >= MAX_REQUEST_LENGTH)
                    {
                        throw new ArgumentException("Length of the request it too large");
                    }
                }
                string result = builder.ToString();
                return result;
            }
        }
#endregion

#region Request handlers
        private JObject ProcessGetProperties(RequestCommandWrapper command)
        {
            Debug.Assert(command.action == "get_properties");

            JObject ret = new JObject();
            ret.Add("request_id", command.requestId);
            ret.Add("result", "ok");

            string objectName;
            GetTokenAsScalar<string>(command.jObject, "object_name", out objectName);
            string propertyName;
            GetTokenAsScalar<string>(command.jObject, "property_name", out propertyName);

            JObject objects = new JObject();

            m_schemaLock.EnterReadLock();
            try
            {
                if (objectName == null)
                {
                    foreach (KeyValuePair<object, ObjectDescription> descr in m_objects)
                    {
                        JObject obj = new JObject();
                        foreach (KeyValuePair<string, PropertyOrFieldDescription> pair in descr.Value.properties)
                        {
                            obj.Add(pair.Key, pair.Value.GetValue(descr.Key));
                        }
                        objects.Add(descr.Value.name, obj);
                    }
                }
                else
                {
                    object obj;
                    if (m_objectNames.TryGetValue(objectName, out obj))
                    {
                        ObjectDescription descr = m_objects[obj];
                        JObject jObj = new JObject();
                        if (propertyName == null)
                        {
                            foreach (KeyValuePair<string, PropertyOrFieldDescription> pair in descr.properties)
                            {
                                jObj.Add(pair.Key, pair.Value.GetValue(obj));
                            }
                        }
                        else
                        {
                            PropertyOrFieldDescription propDescr;
                            if (descr.properties.TryGetValue(propertyName, out propDescr))
                            {
                                jObj.Add(propDescr.name, propDescr.GetValue(obj));
                            }
                            else
                            {
                                throw new ArgumentException("Unknown property '" + propertyName + "'");
                            };
                        }
                        objects.Add(descr.name, jObj);
                    }
                    else
                    {
                        throw new ArgumentException("Unknown object '" + objectName + "'");
                    }
                }
            }
            finally
            {
                m_schemaLock.ExitReadLock();
            };

            ret.Add("objects", objects);            
            return ret;
        }

        private JObject ProcessSetProperty(RequestCommandWrapper command)
        {
            Debug.Assert(command.action == "set_property");

            string objectName;
            GetTokenAsScalar<string>(command.jObject, "object_name", out objectName);
            string propertyName;
            GetTokenAsScalar<string>(command.jObject, "property_name", out propertyName);
            object value;
            GetTokenAsScalar<object>(command.jObject, "value", out value);

            if (objectName == null
                || propertyName == null)
            {
                throw new ArgumentException("You must set object_name and property_name fields");
            };

            m_schemaLock.EnterReadLock();
            try
            {
                object obj;
                if (!m_objectNames.TryGetValue(objectName, out obj))
                {
                    throw new ArgumentException("Unknown object '" + objectName + "'");
                };

                ObjectDescription descr = m_objects[obj];

                PropertyOrFieldDescription propDescr;
                if (!descr.properties.TryGetValue(propertyName, out propDescr))
                {
                    throw new ArgumentException("Unknown property '" + propertyName + "'");
                };

                propDescr.SetValue(obj, value);
            }
            finally
            {
                m_schemaLock.ExitReadLock();
            };

            JObject ret = new JObject();
            ret.Add("request_id", command.requestId);
            ret.Add("result", "ok");
            return ret;
        }

        private JObject ProcessExecute(RequestCommandWrapper command)
        {
            Debug.Assert(command.action == "execute");

            string objectName;
            GetTokenAsScalar<string>(command.jObject, "object_name", out objectName);
            string actionName;
            GetTokenAsScalar<string>(command.jObject, "action_name", out actionName);
            JObject parameters;
            GetTokenAsJObject(command.jObject, "parameters", out parameters);

            if (objectName == null 
                || actionName == null
                || parameters == null)
            {
                throw new ArgumentException("You must set object_name, action_name and parameters fields");
            };

            JObject ret = new JObject();

            m_schemaLock.EnterReadLock();
            try
            {
                object obj;
                if (!m_objectNames.TryGetValue(objectName, out obj))
                {
                    throw new ArgumentException("Unknown object '" + objectName + "'");
                };

                ObjectDescription descr = m_objects[obj];

                ActionDescription actionDescr;
                if (!descr.actions.TryGetValue(actionName, out actionDescr))
                {
                    throw new ArgumentException("Unknown action '" + actionName + "'");
                };

                JValue value = actionDescr.Call(obj, parameters);
                ret.Add("value", value);
            }
            finally
            {
                m_schemaLock.ExitReadLock();
            };
            
            ret.Add("request_id", command.requestId);
            ret.Add("result", "ok");
            return ret;
        }
#endregion

#region Helpers
        private void MakeReponseOk(HttpResponse response, string requestId, JObject body)
        {
            body.Add("request_id", requestId);
            body.Add("result", "ok");
            MakeResponse(response, HttpStatusCode.OK, body);
        }

        private void MakeReponseError(HttpResponse response, string requestId, string message, HttpStatusCode code = HttpStatusCode.InternalServerError)
        {
            MakeResponse(response, code, new JObject() {
                { "request_id", requestId },
                { "result", "error" },
                { "message", message }
            });
        }

        private void MakeResponse(HttpResponse response, HttpStatusCode code, JObject body)
        {
            string bodyText = body.ToString(m_jsonSettings.Formatting);
            MakeResponse(response, code, bodyText);       
        }

        private void MakeResponse(HttpResponse response, HttpStatusCode code, string bodyText)
        {
            response.StatusCode = (int)code;
            response.StatusDescription = HttpResponseCodeStatus.Get(code);
            response.ContentType = "text/json";

            byte[] utf8Body = m_utf8.GetBytes(bodyText);
            response.OutputStream.Write(utf8Body, 0, utf8Body.Length);
        }
#endregion

#region Interaction
        public void RegisterObject(object obj)
        {
            SjsmpObjectAttribute[] objAttr = (SjsmpObjectAttribute[])obj.GetType().GetCustomAttributes(typeof(SjsmpObjectAttribute), false);
            if (objAttr == null || objAttr.Length != 1)
            {
                throw new ArgumentException("Passed object should have SjmpObjectAttribute: " + obj);
            }
            RegisterObject(obj, objAttr[0].name, objAttr[0].description, objAttr[0].group);
        }


        public void RegisterObject(object obj, string name, string description, string group = "", bool acceptComponentModelAttributes = false, bool immediatePushSchema = true)
        {
            ObjectDescription descr = new ObjectDescription(obj, name, description, group, acceptComponentModelAttributes);
            m_schemaLock.EnterWriteLock();
            try
            {
                if (m_objects.ContainsKey(obj) || m_objectNames.ContainsKey(name))
                {
                    throw new ArgumentException("object already registered: " + obj + ", name '" + name + "'");
                }
                m_objects.Add(obj, descr);
                m_objectNames.Add(name, obj);

                RefreshSchema();
            }
            finally
            {
                m_schemaLock.ExitWriteLock();
            }

            if (m_schemaPushUrl != null && immediatePushSchema)
            {
                Task.Run(() => ShemaPushJob(null));
            }
        }

        public bool UnRegisterObject(object obj, bool immediatePushSchema = true)
        {
            bool removed;
            m_schemaLock.EnterWriteLock();
            try
            {
                ObjectDescription descr;
                if (!m_objects.TryGetValue(obj, out descr))
                {
                    throw new SjsmpServerException("Object '" + obj + "' not found");
                };

                removed = m_objects.Remove(obj);
                bool removedName = m_objectNames.Remove(descr.name);
                if (removedName != removed)
                {
                    throw new SjsmpServerException("Wrong internal state: object names '" + descr.name + "' not found in name index");
                }

                RefreshSchema();
            }
            finally
            {
                m_schemaLock.ExitWriteLock();
            }

            if (m_schemaPushUrl != null && immediatePushSchema)
            {
                Task.Run(() => ShemaPushJob(null));
            }
            return removed;
        }

        private void RefreshSchema()
        {
            ++m_schemaVersionNumber;
            Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            JObject jsonObject = new JObject() {
                { "result", "ok" },
                { "type", "SimpleJMP/schema" },
                { "version", SJMP_VERSION },
                { "name", m_name },
                { "description", m_description },
                { "group", m_group },
                { "port", m_port },
                { "schema_version", string.Format("{0}.{1}", unixTimestamp, m_schemaVersionNumber) }
            };

            JObject objects = new JObject();
            foreach (ObjectDescription descr in m_objects.Values)
            {
                objects.Add(descr.name, descr.ToJObject());
            }
            jsonObject.Add("objects", objects);

            m_schema = jsonObject.ToString(m_jsonSettings.Formatting);            
        }
#endregion

#region HTTP Basic Auth helpers
        private static readonly char[] s_authSplitHelper = new char[] { ':' };
        internal static bool TryAuth(HttpRequest request, IServerAuthorization authorization)
        {
            Dictionary<string, string> headers = NameValuedCollectionToDictionary(request.Headers);

            string auth;
            if (!headers.TryGetValue("Authorization", out auth))
            {
                //m_logger.Trace("[" + request.UserHostAddress + "][false] No 'Authorization' header");
                return false;
            };

            byte[] tempConverted = Convert.FromBase64String(auth.Replace("Basic ", "").Trim());
            string userInfo = Encoding.UTF8.GetString(tempConverted);
            string[] usernamePassword = userInfo.Split(s_authSplitHelper, StringSplitOptions.RemoveEmptyEntries);
            if (usernamePassword.Length != 2)
            {
                //m_logger.Trace("[" + request.UserHostAddress + "][false] Can not split usernamePassword");
                return false;
            };

            string username = usernamePassword[0];
            string password = usernamePassword[1];

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                //m_logger.Trace("[" + request.UserHostAddress + "][false] Username or password is empty");
                return false;
            };

            bool result = authorization.CheckAccess(username, password);
            //m_logger.Trace("[" + request.UserHostAddress + "][" + result + "] User '" + username + "' auth");
            return result;
        }

        private static Dictionary<string, string> NameValuedCollectionToDictionary(NameValueCollection collection)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>(collection.Count);
            for (int i = 0; i < collection.Count; ++i)
            {
                string name = collection.GetKey(i);
                ret[name] = collection[name];
            };
            return ret;
        }
#endregion

        private static bool GetTokenAsScalar<T>(JObject obj, string name, out T value)
        {
            JToken token;
            if (!obj.TryGetValue(name, out token))
            {
                value = default(T);
                return false;
            }
            JValue jValue = (JValue)token;
            value = (T)jValue.Value;
            return true;
        }

        private static bool GetTokenAsJObject(JObject obj, string name, out JObject value)
        {
            JToken token;
            if (!obj.TryGetValue(name, out token))
            {
                value = null;
                return false;
            }
            value = (JObject)token;
            return true;
        }

        public void Dispose()
        {
            if (m_timer != null)
            {
                m_timer.Dispose();
            }
            if (m_server != null)
            {
                m_server.Dispose();
                m_server = null;
            };
            m_logger.Info("SjmpServer stopped");
        }
    }
}

