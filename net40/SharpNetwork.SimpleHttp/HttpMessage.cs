using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;

using SharpNetwork.Core;

namespace SharpNetwork.SimpleHttp
{
    public class HttpMessage
    {
        public const int WEB_MSG_BUF_CODE = -1;
        public const int WEB_MSG_DATA_CODE = -2;
        public const int WEB_MSG_TASK_CODE = -3;
        //public const int WEB_MSG_HEADER_CODE = -4;
        //public const int WEB_MSG_URL_INFO_CODE = -5;

        public const int STATE_WAIT_FOR_HEADER = 0;
        public const int STATE_WAIT_FOR_BODY = 1;
        public const int STATE_READY = 2;

        public const int MSG_TYPE_UNDEFINED = 0;
        public const int MSG_TYPE_HANDSHAKE = 1;
        public const int MSG_TYPE_STRING = 2;
        public const int MSG_TYPE_BINARY = 3;

        public enum HttpStatusCode
        {
            // for a full list of status codes, see..
            // https://en.wikipedia.org/wiki/List_of_HTTP_status_codes

            Continue = 100,

            Ok = 200,
            Created = 201,
            Accepted = 202,
            MovedPermanently = 301,
            Found = 302,
            NotModified = 304,
            BadRequest = 400,
            Forbidden = 403,
            NotFound = 404,
            MethodNotAllowed = 405,
            InternalServerError = 500
        }

        public int ReceivingState { get; set; }

        public int ContentSize { get; set; }
        public int MessageType { get; set; }

        public Dictionary<string, string> Headers { get; set; }

        //public Uri Url { get; set; }

        public string RequestUrl { get; set; }
        public string RequestMethod { get; set; }

        public int StatusCode { get; set; }
        public string ReasonPhrase { get; set; }

        public string ProtocolVersion { get; set; }

        public string MessageContent { get; set; }
        public byte[] RawContent { get; set; }

        private ICommonJsonCodec m_JsonCodec = null;
        private static ICommonJsonCodec m_CurrentJsonCodec = null;
        private static ICommonJsonCodec m_DefaultJsonCodec = new SimpleJsonCodec();
        public static ICommonJsonCodec DefaultJsonCodec
        {
            get
            {
                return m_DefaultJsonCodec;
            }
        }
        public static ICommonJsonCodec JsonCodec
        {
            get
            {
                return m_CurrentJsonCodec == null ? m_DefaultJsonCodec : m_CurrentJsonCodec;
            }
            set
            {
                m_CurrentJsonCodec = value;
            }
        }

        public HttpMessage()
        {
            ReceivingState = STATE_WAIT_FOR_HEADER;

            ContentSize = 0;
            MessageType = 0;

            StatusCode = 200;
            ReasonPhrase = "OK";

            ProtocolVersion = "";

            RequestUrl = "/";
            RequestMethod = "";

            MessageContent = "";
            RawContent = null;

            //Url = null;

            Headers = new Dictionary<string, string>();

            m_JsonCodec = m_CurrentJsonCodec;
            if (m_JsonCodec == null) m_JsonCodec = m_DefaultJsonCodec;
        }

        public HttpMessage(string msgContent)
            : this()
        {
            MessageType = MSG_TYPE_STRING;
            MessageContent = msgContent;

            ProtocolVersion = "HTTP/1.0";
        }

        public HttpMessage(string reqUrl, string reqMethod, string msgContent)
            : this(msgContent)
        {
            RequestUrl = reqUrl;
            RequestMethod = reqMethod;
        }

        public HttpMessage(int statusCode, string reasonPhrase, string msgContent)
            : this(msgContent)
        {
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
        }

        public HttpMessage(byte[] msgContent)
            : this()
        {
            MessageType = MSG_TYPE_BINARY;
            RawContent = msgContent;
            ContentSize = msgContent.Length;

            ProtocolVersion = "HTTP/1.0";
        }

        public HttpMessage(byte[] msgContent, int msgSize)
            : this(msgContent)
        {
            ContentSize = msgSize;
        }

        public HttpMessage(int statusCode, string reasonPhrase, byte[] msgContent)
            : this(msgContent)
        {
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
        }

        public bool IsString()
        {
            return MessageType == MSG_TYPE_STRING;
        }

        public bool IsBinary()
        {
            return MessageType == MSG_TYPE_BINARY;
        }

        public void SetHeaders(IDictionary<string, string> headers)
        {
            if (headers != null) Headers = new Dictionary<string, string>(headers);
        }

        public static Stack<Object> GetSessionBuffer(Session session, bool needCheck = false)
        {
            Stack<Object> result = null;
            Dictionary<int, object> attrMap = session.GetAttributes();

            if (needCheck)
            {
                lock (attrMap)
                {
                    if (attrMap.ContainsKey(WEB_MSG_BUF_CODE))
                    {
                        result = attrMap[WEB_MSG_BUF_CODE] as Stack<Object>;
                        if (result == null) attrMap.Remove(WEB_MSG_BUF_CODE);
                    }
                    if (result == null)
                    {
                        result = new Stack<Object>();
                        attrMap.Add(WEB_MSG_BUF_CODE, result);
                    }
                }
            }
            else
            {
                result = attrMap[WEB_MSG_BUF_CODE] as Stack<Object>;
            }

            return result;
        }

        public static ConcurrentDictionary<string, object> GetSessionData(Session session, bool needCheck = false)
        {
            ConcurrentDictionary<string, object> result = null;
            Dictionary<int, object> attrMap = session.GetAttributes();

            if (needCheck)
            {
                lock (attrMap)
                {
                    if (attrMap.ContainsKey(WEB_MSG_DATA_CODE))
                    {
                        result = attrMap[WEB_MSG_DATA_CODE] as ConcurrentDictionary<string, object>;
                        if (result == null) attrMap.Remove(WEB_MSG_DATA_CODE);
                    }
                    if (result == null)
                    {
                        result = new ConcurrentDictionary<string, object>();
                        attrMap.Add(WEB_MSG_DATA_CODE, result);
                    }
                }
            }
            else
            {
                result = attrMap[WEB_MSG_DATA_CODE] as ConcurrentDictionary<string, object>;
            }

            return result;
        }

        /*
        public static Dictionary<string, string> GetIncomingHeaders(Session session, bool needCheck = false)
        {
            Dictionary<string, string> result = null;
            Dictionary<int, object> attrMap = session.GetAttributes();

            if (needCheck)
            {
                lock (attrMap)
                {
                    if (attrMap.ContainsKey(WEB_MSG_HEADER_CODE))
                    {
                        result = attrMap[WEB_MSG_HEADER_CODE] as Dictionary<string, string>;
                        if (result == null) attrMap.Remove(WEB_MSG_HEADER_CODE);
                    }
                    if (result == null)
                    {
                        result = new Dictionary<string, string>();
                        attrMap.Add(WEB_MSG_HEADER_CODE, result);
                    }
                }
            }
            else
            {
                result = attrMap[WEB_MSG_HEADER_CODE] as Dictionary<string, string>;
            }

            return result;
        }

        public static void SetIncomingHeaders(Session session, Dictionary<string, string> headers, bool needCheck = false)
        {
            Dictionary<int, object> attrMap = session.GetAttributes();

            if (needCheck)
            {
                lock (attrMap)
                {
                    if (attrMap.ContainsKey(WEB_MSG_HEADER_CODE))
                    {
                        attrMap[WEB_MSG_HEADER_CODE] = headers;
                    }
                    else
                    {
                        attrMap.Add(WEB_MSG_HEADER_CODE, headers);
                    }
                }
            }
            else
            {
                attrMap[WEB_MSG_HEADER_CODE] = headers;
            }
        }

        public static Dictionary<string, string> GetRequestUrlInfo(Session session, bool needCheck = false)
        {
            Dictionary<string, string> result = null;
            Dictionary<int, object> attrMap = session.GetAttributes();

            if (needCheck)
            {
                lock (attrMap)
                {
                    if (attrMap.ContainsKey(WEB_MSG_URL_INFO_CODE))
                    {
                        result = attrMap[WEB_MSG_URL_INFO_CODE] as Dictionary<string, string>;
                        if (result == null) attrMap.Remove(WEB_MSG_URL_INFO_CODE);
                    }
                    if (result == null)
                    {
                        result = new Dictionary<string, string>();
                        attrMap.Add(WEB_MSG_URL_INFO_CODE, result);
                    }
                }
            }
            else
            {
                result = attrMap[WEB_MSG_URL_INFO_CODE] as Dictionary<string, string>;
            }

            return result;
        }

        public static void SetRequestUrlInfo(Session session, Dictionary<string, string> info, bool needCheck = false)
        {
            Dictionary<int, object> attrMap = session.GetAttributes();

            if (needCheck)
            {
                lock (attrMap)
                {
                    if (attrMap.ContainsKey(WEB_MSG_URL_INFO_CODE))
                    {
                        attrMap[WEB_MSG_URL_INFO_CODE] = info;
                    }
                    else
                    {
                        attrMap.Add(WEB_MSG_URL_INFO_CODE, info);
                    }
                }
            }
            else
            {
                attrMap[WEB_MSG_URL_INFO_CODE] = info;
            }
        }
        */

        public static void SetSessionData(Session session, string dataName, object dataValue)
        {
            var dataMap = GetSessionData(session);
            if (dataMap.ContainsKey(dataName)) dataMap[dataName] = dataValue;
            else dataMap.TryAdd(dataName, dataValue);
        }

        public static object GetSessionData(Session session, string dataName)
        {
            var dataMap = GetSessionData(session);
            if (dataMap.ContainsKey(dataName)) return dataMap[dataName];
            else return null;
        }

        public static TaskFactory GetSingleTaskFactory(Session session, bool needCheck = false)
        {
            TaskFactory result = null;
            Dictionary<int, object> attrMap = session.GetAttributes();

            if (needCheck)
            {
                lock (attrMap)
                {
                    if (attrMap.ContainsKey(WEB_MSG_TASK_CODE))
                    {
                        result = attrMap[WEB_MSG_TASK_CODE] as TaskFactory;
                        if (result == null) attrMap.Remove(WEB_MSG_TASK_CODE);
                    }
                    if (result == null)
                    {
                        // this one-thread limitation could make sure that messages can be processed in correct order
                        result = new TaskFactory(new LimitedConcurrencyLevelTaskScheduler(1));
                        attrMap.Add(WEB_MSG_TASK_CODE, result);
                    }
                }
            }
            else
            {
                result = attrMap[WEB_MSG_TASK_CODE] as TaskFactory;
            }

            return result;
        }

        public void FromJsonObject<T>(T obj) where T : class
        {
            var msgContent = m_JsonCodec.ToJsonString(obj);
            MessageType = MSG_TYPE_STRING;
            MessageContent = msgContent;
            ProtocolVersion = "HTTP/1.0";
        }

        public T ToJsonObject<T>() where T : class
        {
            try
            {
                if (MessageContent.Length > 0)
                {
                    return m_JsonCodec.ToJsonObject<T>(MessageContent);
                }
                else
                {
                    return default(T);
                }
            }
            catch
            {
                return default(T);
            }
        }

        public static U ToJsonObject<U>(String str) where U : class
        {
            try
            {
                if (str.Length > 0)
                {
                    var codec = m_CurrentJsonCodec == null ? m_DefaultJsonCodec : m_CurrentJsonCodec;
                    return codec.ToJsonObject<U>(str);
                }
                else
                {
                    return default(U);
                }
            }
            catch
            {
                return default(U);
            }
        }

        public static String ToJsonString<U>(U obj) where U : class
        {
            string str = "";
            try
            {
                var codec = m_CurrentJsonCodec == null ? m_DefaultJsonCodec : m_CurrentJsonCodec;
                str = codec.ToJsonString(obj);
            }
            catch { }
            return str;
        }

        public static void Send<TMessage>(Session session, TMessage obj, IDictionary<string, string> headers = null) where TMessage : class
        {
            var msg = new HttpMessage(HttpMessage.ToJsonString<TMessage>(obj));
            if (headers != null) msg.SetHeaders(headers);
            session.Send(msg);
        }

        public static void Send(Session session, string str, IDictionary<string, string> headers = null)
        {
            var msg = new HttpMessage(str);
            if (headers != null) msg.SetHeaders(headers);
            session.Send(msg);
        }


    }

}
