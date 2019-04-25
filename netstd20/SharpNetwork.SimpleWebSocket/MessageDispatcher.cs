using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using System.Text;

using SharpNetwork.Core;

namespace SharpNetwork.SimpleWebSocket
{
    public class MessageDispatcher : IMessageDispatcher
    {
        protected Dictionary<string, IMessageHandler> m_Handlers = new Dictionary<string, IMessageHandler>();

        // it is not thread safe ...
        public void AddMessageHandler(IMessageHandler handler) 
        {
            string messageName = handler.GetMessageType().Name;
            if (m_Handlers.ContainsKey(messageName)) m_Handlers.Remove(messageName);
            m_Handlers.Add(messageName, handler);
        }

        public void AddHandler(object handler)
        {
            if (handler is IMessageHandler) AddMessageHandler(handler as IMessageHandler);
        }

        // and this is not thread safe, too ... 
        // but "A Dictionary can support multiple readers concurrently, as long as the collection is not modified."
        public IMessageHandler GetHandler(string messageName)
        {
            IMessageHandler result = null;
            if (m_Handlers.TryGetValue(messageName, out result)) return result;
            else return null;
        }

        public virtual List<IMessageHandler> GetHandlers()
        {
            List<IMessageHandler> result = new List<IMessageHandler>();
            foreach (var item in m_Handlers) result.Add(item.Value);
            return result;
        }

        protected bool IsSubclassOfRawGeneric(Type toCheck, Type genericType)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var currentType = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (genericType == currentType)
                {
                    return true;
                }
                toCheck = toCheck.BaseType;
            }
            return false;
        }

        public virtual void LoadHandlers(string nameSpace = "", Assembly assembly = null)
        {
            Assembly targetAssembly = assembly;
            if (targetAssembly == null) targetAssembly = Assembly.GetEntryAssembly();

            if (targetAssembly != null)
            {
                var types = targetAssembly.GetTypes();
                foreach (var t in types)
                {
                    if ((nameSpace != null && nameSpace.Length > 0) ? t.Namespace == nameSpace : t.Namespace.Length > 0)
                    {
                        if (IsSubclassOfRawGeneric(t, typeof(MessageHandler<>)))
                        {
                            AddMessageHandler((IMessageHandler)Activator.CreateInstance(t));
                        }
                    }
                }
            }
        }

        protected virtual MessageSessionData GetSessionData(Session session, object data)
        {
            MessageSessionData result = null;
            if (data is string) result = new MessageSessionData(session, data as string);
            else if (data is byte[])
            {
                Byte[] bytes = data as byte[];
                int len = bytes.Length;
                int last = len - 4;
                int bodylen = -1;
                for (int i = 0; i <= last; i++)
                {
                    if (bytes[i] == 0 && bytes[i + 1] == 0
                        && bytes[i + 2] == 0 && bytes[i + 3] == 0)
                    {
                        bodylen = last - i;
                        break;
                    }
                }
                if (bodylen >= 0)
                {
                    byte[] body = new byte[bodylen];
                    string message = Encoding.UTF8.GetString(bytes, 0, len - bodylen - 4);
                    if (bodylen > 0) Buffer.BlockCopy(bytes, len - bodylen - 4, body, 0, bodylen);
                    result = new MessageSessionData(session, message, body);
                }
            }
            return result;
        }

        public virtual bool Dispatch(Session session, object msg)
        {
            if (session == null || msg == null) return false;

            return ProcessMessage(GetSessionData(session, msg));

        }

        protected virtual bool ProcessMessage(MessageSessionData data)
        {
            if (data == null) return false;
            string message = data.Message;
            if (message != null && message.Length > 0)
            {
                JsonMessage msg = WebMessage.ToJsonObject<JsonMessage>(message);
                if (msg != null && msg.MessageName != null && msg.MessageName.Length > 0)
                {
                    IMessageHandler handler = GetHandler(msg.MessageName);
                    if (handler != null)
                    {
                        handler.Handle(data);
                        return true;
                    }
                }
            }
            return false;
        }

        public bool CanProcessBinary()
        {
            return true;
        }

    }

    public class MessageSessionData
    {
        private Session m_Session = null;
        private string m_Message = null;
        private byte[] m_Data = null;

        public MessageSessionData(Session session, string msg, byte[] data)
        {
            m_Session = session;
            m_Message = msg;
            m_Data = data;
        }

        public MessageSessionData(Session session, string msg)
            : this(session, msg, null) { }

        public Session Session
        {
            get { return m_Session; }
        }

        public string Message
        {
            get { return m_Message; }
        }

        public byte[] Data
        {
            get { return m_Data; }
        }

    }

    /*
    public class GenericMessageDispatcher : IMessageDispatcher
    {
        protected List<IGenericMessageHandler> m_Handlers = new List<IGenericMessageHandler>();
        protected List<List<IMessageChecker>> m_Rules = new List<List<IMessageChecker>>();

        private ExpandoObjectConverter m_MapConverter = new ExpandoObjectConverter();

        protected string m_HandlerGroupName = "JSON";

        public GenericMessageDispatcher(string handlerGroupName = "JSON")
        {
            m_HandlerGroupName = handlerGroupName;
        }

        public virtual void AddMessageHandler(IGenericMessageHandler handler)
        {
            AddMessageHandler(handler, m_HandlerGroupName);
        }

        public void AddHandler(object handler)
        {
            if (handler is IGenericMessageHandler) AddMessageHandler(handler as IGenericMessageHandler);
        }

        public virtual void AddHandlerWithMessageCheckers(IGenericMessageHandler handler, params IMessageChecker[] checkers)
        {
            m_Handlers.Add(handler);
            m_Rules.Add(new List<IMessageChecker>(checkers));
        }

        protected virtual void AddMessageHandler(IGenericMessageHandler handler, string groupName)
        {
            handler.CreateMessageCheckers();
            var rules = handler.GetMessageCheckers(groupName);
            if (rules == null || rules.Count <= 0) return;
            else
            {
                m_Handlers.Add(handler);
                m_Rules.Add(rules);
            }
        }

        protected bool IsSubclassOfRawGeneric(Type toCheck, Type genericType)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var currentType = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (genericType == currentType)
                {
                    return true;
                }
                toCheck = toCheck.BaseType;
            }
            return false;
        }

        public virtual void LoadHandlers(string nameSpace = "", Assembly assembly = null)
        {
            Assembly targetAssembly = assembly;
            if (targetAssembly == null) targetAssembly = Assembly.GetEntryAssembly();

            if (targetAssembly != null)
            {
                var types = targetAssembly.GetTypes();
                foreach (var t in types)
                {
                    if ((nameSpace != null && nameSpace.Length > 0) ? t.Namespace == nameSpace : t.Namespace.Length > 0)
                    {
                        if (IsSubclassOfRawGeneric(t, typeof(GenericMessageHandler<>)))
                        {
                            AddMessageHandler((IGenericMessageHandler)Activator.CreateInstance(t));
                        }
                    }
                }
            }
        }

        public virtual bool Dispatch(Session session, object msg)
        {
            string strmsg = msg as string;
            if (strmsg == null || strmsg.Length <= 0) return false;

            bool found = false;

            var obj = JsonConvert.DeserializeObject<ExpandoObject>(strmsg, m_MapConverter);
            for (int i = 0; i < m_Handlers.Count; i++)
            {
                int pass = 0;
                var rules = m_Rules[i];
                foreach (var rule in rules)
                {
                    if (rule.Check(obj)) pass++;
                    else break;
                }
                if (pass >= rules.Count)
                {
                    found = true;
                    m_Handlers[i].Handle(session, strmsg);
                }
                if (found) break;
            }

            return found;
        }

        public bool CanProcessBinary()
        {
            return false;
        }

    }
    */
}
