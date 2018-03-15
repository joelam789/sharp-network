using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

using SharpNetwork.Core;

namespace SharpNetwork.SimpleProtocol
{

    /// <summary>
    /// Base network message
    /// 
    /// Format:
    /// 
    /// |                          Header (16 bytes, 4 integer values)                        |     Content (any length, UTF8 string, JSON format)    |
    /// |_____________________________________________________________________________________|_______________________________________________________|
    /// |                    |                     |                    |                     |                                                       |
    /// |    Header Flag     |    Message Type     |    Message Flags   |    Content Size     |                 JSON Message                          |
    /// | (Integer, 4 bytes) | (Integer, 4 bytes)  | (Integer, 4 bytes) | (Integer, 4 bytes)  |                 (any length)                          |
    /// 
    /// </summary>
    public class NetMessage
    {
        public const int NET_MSG_BUF_CODE = -1;
        public const int NET_MSG_DATA_CODE = -2;
        public const int NET_MSG_TASK_CODE = -3;

        public const int STATE_WAIT_FOR_HEADER = 0;
        public const int STATE_WAIT_FOR_BODY = 1;
        public const int STATE_READY = 2;

        public const int FLAG_STRING = 1;
        public const int FLAG_JSON = 2;
        public const int FLAG_COMPRESSION = 8;
        public const int FLAG_ENCRYPTION = 16;
        public const int FLAG_ORDER = 32;

        public const int HEADER_SIZE = 16;
        public const int SIGN_SIZE = 4;
        public const int TYPE_SIZE = 4;
        public const int FLAG_SIZE = 4;
        public const int LEN_SIZE = 4;

        public const int HEADER_SIGN = 0;

        //public static string TYPE_FORMAT = "{0,-" + TYPE_SIZE + "}";
        //public static string FLAG_FORMAT = "{0,-" + FLAG_SIZE + "}";
        //public static string LEN_FORMAT  = "{0,-" + (HEADER_SIZE - TYPE_SIZE - FLAG_SIZE) + "}";

        public int ReceivingState { get; set; }

        public int VirtualHeaderSize { get; set; }
        public byte HeaderFlag { get; set; }
        public byte MaskFlag { get; set; }
        public byte[] MaskBytes { get; set; }

        public int ContentSize { get; set; }
        public int MessageType { get; set; }
        public int MessageFlag { get; set; }
        public int IoFlag { get; set; }
        public string MessageContent { get; set; }
        public byte[] RawContent { get; set; }

        //private static ConcurrentExclusiveSchedulerPair m_TaskSchedulerPair 
        //    = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, 1);

        private static ConcurrentExclusiveSchedulerPair m_TaskSchedulerPair
            = new ConcurrentExclusiveSchedulerPair();

        private IJsonCodec m_JsonCodec = null;
        private static IJsonCodec m_CurrentJsonCodec = null;
        private static IJsonCodec m_DefaultJsonCodec = new SimpleJsonCodec();
        public static IJsonCodec DefaultJsonCodec
        {
            get
            {
                return m_DefaultJsonCodec;
            }
        }
        public static IJsonCodec JsonCodec
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

        public NetMessage()
        {
            ReceivingState = STATE_WAIT_FOR_HEADER;

            VirtualHeaderSize = 0;
            HeaderFlag = 0;
            MaskFlag = 0;
            MaskBytes = null;

            ContentSize = 0;
            MessageType = 0;
            // message content will be a JSON string without compression or encryption by default
            MessageFlag = FLAG_STRING | FLAG_JSON;
            IoFlag = 0;
            MessageContent = "";
            RawContent = null;

            m_JsonCodec = m_CurrentJsonCodec;
            if (m_JsonCodec == null) m_JsonCodec = m_DefaultJsonCodec;
        }

        public NetMessage(int msgType, string msgContent)
            : this()
        {
            MessageType = msgType;
            MessageFlag = MessageFlag | FLAG_STRING;
            MessageContent = msgContent;
        }

        public NetMessage(int msgType, string msgContent, int msgFlag)
            : this()
        {
            MessageType = msgType;
            MessageFlag = msgFlag | FLAG_STRING;
            MessageContent = msgContent;
        }

        public NetMessage(int msgType, byte[] msgContent, int msgFlag)
            : this()
        {
            MessageType = msgType;
            MessageFlag = msgFlag;
            RawContent = msgContent;
        }

        public bool IsString()
        {
            return (MessageFlag & FLAG_STRING) != 0;
        }

        public bool IsJsonString()
        {
            return (MessageFlag & FLAG_STRING) != 0 && (MessageFlag & FLAG_JSON) != 0;
        }

        public bool IsCompressed()
        {
            return (MessageFlag & FLAG_COMPRESSION) != 0;
        }

        public bool IsEncrypted()
        {
            return (MessageFlag & FLAG_ENCRYPTION) != 0;
        }

        public bool IsOrdered()
        {
            return (MessageFlag & FLAG_ORDER) != 0;
        }

        public void FromJsonObject<T>(T obj) where T : class
        {
            MessageContent = m_JsonCodec.ToJsonString(obj);
            MessageFlag = MessageFlag | FLAG_STRING | FLAG_JSON;
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
                    if (attrMap.ContainsKey(NET_MSG_BUF_CODE))
                    {
                        result = attrMap[NET_MSG_BUF_CODE] as Stack<Object>;
                        if (result == null) attrMap.Remove(NET_MSG_BUF_CODE);
                    }
                    if (result == null)
                    {
                        result = new Stack<Object>();
                        attrMap.Add(NET_MSG_BUF_CODE, result);
                    }
                }
            }
            else
            {
                result = attrMap[NET_MSG_BUF_CODE] as Stack<Object>;
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
                    if (attrMap.ContainsKey(NET_MSG_DATA_CODE))
                    {
                        result = attrMap[NET_MSG_DATA_CODE] as Dictionary<string, object>;
                        if (result == null) attrMap.Remove(NET_MSG_DATA_CODE);
                    }
                    if (result == null)
                    {
                        result = new Dictionary<string, object>();
                        attrMap.Add(NET_MSG_DATA_CODE, result);
                    }
                }
            }
            else
            {
                result = attrMap[NET_MSG_DATA_CODE] as Dictionary<string, object>;
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
                    if (attrMap.ContainsKey(NET_MSG_TASK_CODE))
                    {
                        result = attrMap[NET_MSG_TASK_CODE] as TaskFactory;
                        if (result == null) attrMap.Remove(NET_MSG_TASK_CODE);
                    }
                    if (result == null)
                    {
                        // this one-thread limitation could make sure that messages can be processed in correct order
                        //result = new TaskFactory(new LimitedConcurrencyLevelTaskScheduler(1));
                        result = new TaskFactory(m_TaskSchedulerPair.ExclusiveScheduler);
                        attrMap.Add(NET_MSG_TASK_CODE, result);
                    }
                }
            }
            else
            {
                result = attrMap[NET_MSG_TASK_CODE] as TaskFactory;
            }

            return result;
        }

        public static void Send<T>(Session session, int msgType, T obj, int flag = 0) where T : class
        {
            if (session != null)
            {
                NetMessage msg = new NetMessage();
                msg.MessageType = msgType;

                if (obj is Byte[])
                {
                    msg.MessageFlag = 0;
                    if (flag != 0) msg.MessageFlag = msg.MessageFlag | flag;
                    msg.RawContent = obj as byte[];
                }
                else if (obj is String)
                {
                    msg.MessageFlag = NetMessage.FLAG_STRING;
                    if (flag != 0) msg.MessageFlag = msg.MessageFlag | flag;
                    msg.MessageContent = obj as string;
                }
                else
                {
                    msg.MessageFlag = NetMessage.FLAG_STRING | NetMessage.FLAG_JSON;
                    if (flag != 0) msg.MessageFlag = msg.MessageFlag | flag;
                    msg.FromJsonObject<T>(obj);
                }

                session.Send(msg);
            }
        }
    }

    public interface IJsonCodec
    {
        string ToJsonString(object obj);
        T ToJsonObject<T>(string str) where T : class;
    }

    public class SimpleJsonCodec : IJsonCodec
    {
        public string ToJsonString(object obj)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());
            using (MemoryStream ms = new MemoryStream())
            {
                serializer.WriteObject(ms, obj);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public T ToJsonObject<T>(string str) where T : class
        {
            if (str == null || str.Length <= 0) return null;
            else
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(str)))
                {
                    return serializer.ReadObject(ms) as T;
                }
            }
        }

    }
}
