using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;

using SharpNetwork.Core;

namespace SharpNetwork.SimpleWebSocket
{
    public class WebMessage
    {
        public const int WEB_MSG_BUF_CODE = -1;
        public const int WEB_MSG_DATA_CODE = -2;
        public const int WEB_MSG_TASK_CODE = -3;

        public const int STATE_WAIT_FOR_HEADER = 0;
        public const int STATE_WAIT_FOR_BODY = 1;
        public const int STATE_READY = 2;

        public const int MSG_TYPE_UNDEFINED = 0;
        public const int MSG_TYPE_HANDSHAKE = 1;
        public const int MSG_TYPE_STRING = 2;
        public const int MSG_TYPE_BINARY = 3;
        public const int MSG_TYPE_PING = 4;
        public const int MSG_TYPE_PONG = 5;
        public const int MSG_TYPE_CLOSE = 6;

        public const string WEBSOCK_HANDSHAKE_REQUEST_MSG = "GET {0} HTTP/1.1" + "\r\n"
                                                          + "Upgrade: WebSocket" + "\r\n"
                                                          + "Connection: Upgrade" + "\r\n"
                                                          + "Sec-WebSocket-Version: 13" + "\r\n"
                                                          + "Sec-WebSocket-Key: {1}" + "\r\n"
                                                          + "Host: {2}" + "\r\n"
                                                          + "Origin: {3}" + "\r\n"
                                                          + "\r\n"
                                                          ;

        public int ReceivingState { get; set; }

        public int VirtualHeaderSize { get; set; }
        public byte HeaderFlag { get; set; }
        public byte MaskFlag { get; set; }
        public byte[] MaskBytes { get; set; }

        public int ContentSize { get; set; }
        public int MessageType { get; set; }

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

        public WebMessage()
        {
            ReceivingState = STATE_WAIT_FOR_HEADER;

            VirtualHeaderSize = 0;
            HeaderFlag = 0;
            MaskFlag = 0;
            MaskBytes = null;

            ContentSize = 0;
            MessageType = 0;

            MessageContent = "";
            RawContent = null;

            m_JsonCodec = m_CurrentJsonCodec;
            if (m_JsonCodec == null) m_JsonCodec = m_DefaultJsonCodec;
        }

        public WebMessage(string msgContent)
            : this()
        {
            MessageType = MSG_TYPE_STRING;
            MessageContent = msgContent;
        }

        public WebMessage(byte[] msgContent)
            : this()
        {
            MessageType = MSG_TYPE_BINARY;
            RawContent = msgContent;
            ContentSize = msgContent.Length;
        }

        public WebMessage(byte[] msgContent, int msgSize)
            : this()
        {
            MessageType = MSG_TYPE_BINARY;
            RawContent = msgContent;
            ContentSize = msgSize;
        }

        public bool IsString()
        {
            return MessageType == MSG_TYPE_STRING;
        }

        public bool IsBinary()
        {
            return MessageType == MSG_TYPE_BINARY;
        }

        public bool IsPingFrame()
        {
            return MessageType == MSG_TYPE_PING;
        }

        public bool IsPongFrame()
        {
            return MessageType == MSG_TYPE_PONG;
        }

        public bool IsCloseFrame()
        {
            return MessageType == MSG_TYPE_CLOSE;
        }

        public void FromJsonObject<T>(T obj) where T : class
        {
            MessageContent = m_JsonCodec.ToJsonString(obj);
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

        public static Dictionary<string, object> GetSessionData(Session session, bool needCheck = false)
        {
            Dictionary<string, object> result = null;
            Dictionary<int, object> attrMap = session.GetAttributes();

            if (needCheck)
            {
                lock (attrMap)
                {
                    if (attrMap.ContainsKey(WEB_MSG_DATA_CODE))
                    {
                        result = attrMap[WEB_MSG_DATA_CODE] as Dictionary<string, object>;
                        if (result == null) attrMap.Remove(WEB_MSG_DATA_CODE);
                    }
                    if (result == null)
                    {
                        result = new Dictionary<string, object>();
                        attrMap.Add(WEB_MSG_DATA_CODE, result);
                    }
                }
            }
            else
            {
                result = attrMap[WEB_MSG_DATA_CODE] as Dictionary<string, object>;
            }

            return result;
        }

        public static void SetSessionData(Session session, string dataName, object dataValue)
        {
            Dictionary<string, object> dataMap = GetSessionData(session);
            if (dataMap.ContainsKey(dataName)) dataMap.Remove(dataName);
            dataMap.Add(dataName, dataValue);
        }

        public static object GetSessionData(Session session, string dataName)
        {
            Dictionary<string, object> dataMap = GetSessionData(session);
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

        public static WebMessage CreatePingMessage(byte[] body = null)
        {
            WebMessage msg = new WebMessage("");

            msg.MessageType = MSG_TYPE_PING;

            if (body == null)
            {
                msg.RawContent = null;
                msg.ContentSize = 0;
            }
            else
            {
                msg.RawContent = (byte[])body.Clone();
                msg.ContentSize = body.Length;
            }

            return msg;
        }

        public static WebMessage CreatePongMessage(byte[] body = null)
        {
            WebMessage msg = new WebMessage("");

            msg.MessageType = MSG_TYPE_PONG;

            if (body == null)
            {
                msg.RawContent = null;
                msg.ContentSize = 0;
            }
            else
            {
                msg.RawContent = (byte[])body.Clone();
                msg.ContentSize = body.Length;
            }

            return msg;
        }

        public static void SendHandshakeRequest(Session session, Uri uri)
        {
            if (session == null || uri == null) return;

            string path = uri.PathAndQuery.Trim();
            if (path.Length <= 0 || path.First() != '/') path = "/" + path;

            Random generator = new Random();
            int part1 = generator.Next(10000000, 99999999);
            int part2 = generator.Next(10000000, 99999999);

            string key = Convert.ToBase64String(Encoding.UTF8.GetBytes(part1.ToString() + part2.ToString()));

            string host = uri.Host + ":" + uri.Port;

            string origin = "ws://" + host;

            string handshakeMsg = String.Format(WEBSOCK_HANDSHAKE_REQUEST_MSG, path, key, host, origin);

            session.Send(handshakeMsg);
        }

        public static void Send<T>(Session session, T obj, bool needmask = false) where T : class
        {
            if (session != null)
            {
                WebMessage msg = new WebMessage();

                if (obj is Byte[])
                {
                    msg.MessageType = MSG_TYPE_BINARY;
                    msg.RawContent = obj as byte[];
                    msg.ContentSize = msg.RawContent.Length;
                }
                else if (obj is String)
                {
                    msg.MessageType = MSG_TYPE_STRING;
                    msg.MessageContent = obj as string;
                    msg.ContentSize = msg.MessageContent.Length;
                }
                else
                {
                    msg.MessageType = MSG_TYPE_STRING;
                    msg.FromJsonObject<T>(obj);
                    msg.ContentSize = msg.MessageContent.Length;
                }

                msg.MaskFlag = needmask ? (byte)1 : (byte)0;

                session.Send(msg);
            }
        }

        public static void SendString(Session session, string str, bool needmask = false)
        {
            if (session == null || str == null) return;
            WebMessage msg = new WebMessage(str);
            msg.MaskFlag = needmask ? (byte)1 : (byte)0;
            session.Send(msg);
        }

        public static void SendByteArray(Session session, byte[] bytes, int length = -1, bool needmask = false)
        {
            if (session == null || bytes == null) return;

            int bufsize = length;
            if (bufsize < 0) bufsize = bytes.Length;

            WebMessage msg = new WebMessage(bytes, bufsize);
            msg.MaskFlag = needmask ? (byte)1 : (byte)0;
            session.Send(msg);
        }

        public static void SendByteArray<T>(Session session, T obj, byte[] bytes, int length = -1, bool needmask = false) where T : class
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);

            string str = WebMessage.ToJsonString<T>(obj);
            byte[] msg = Encoding.UTF8.GetBytes(str);

            int flag = 0;
            int len = length;
            if (len < 0) len = bytes.Length;

            writer.Write(msg, 0, msg.Length);
            writer.Write(flag);
            writer.Write(bytes, 0, len);

            byte[] all = stream.ToArray();

            WebMessage webmsg = new WebMessage(all, all.Length);
            webmsg.MaskFlag = needmask ? (byte)1 : (byte)0;
            session.Send(webmsg);
        }
    }

    [DataContract]
    public class JsonMessage
    {
        [DataMember(Name = "msg")]
        public string MessageName { get; set; }

        public JsonMessage()
        {
            MessageName = this.GetType().Name;
        }
    }
}
