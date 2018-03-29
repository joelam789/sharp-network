using System;
using System.Collections.Generic;

using SharpNetwork.Core;

namespace SharpNetwork.SimpleWebSocket
{
    public class MessageHandler<T> : IMessageHandler where T : JsonMessage
    {
        public Type GetMessageType()
        {
            return typeof(T);
        }

        public virtual void Handle(object data)
        {
            MessageSessionData sessionData = data as MessageSessionData;
            if (sessionData != null)
            {
                Session session = sessionData.Session;
                string message = sessionData.Message;
                byte[] bytes = sessionData.Data;

                if (session != null && message != null && message.Length > 0)
                {
                    T msg = WebMessage.ToJsonObject<T>(message);
                    if (msg != null)
                    {
                        if (bytes == null || bytes.Length <= 0) ProcessMessage(session, msg);
                        else ProcessMessage(session, msg, bytes);
                    }
                }
            }
        }

        public void Send<TMessage>(Session session, TMessage obj) where TMessage : class
        {
            WebMessage.Send<TMessage>(session, obj);
        }

        public void SendString(Session session, string str)
        {
            WebMessage.SendString(session, str);
        }

        public void SendByteArray(Session session, byte[] bytes, int length = -1)
        {
            WebMessage.SendByteArray(session, bytes, length);
        }

        public void SendByteArray<TMessage>(Session session, TMessage obj, byte[] bytes, int length = -1) where TMessage : class
        {
            WebMessage.SendByteArray<TMessage>(session, obj, bytes, length);
        }

        public virtual void ProcessMessage(Session session, T msg)
        {
            // to be overridden ...
            return;
        }

        public virtual void ProcessMessage(Session session, T msg, byte[] data)
        {
            // to be overridden ...
            return;
        }

    }

    public class GenericMessageHandler<T> : IGenericMessageHandler where T : class
    {
        protected Dictionary<string, List<IMessageChecker>> m_Rules = new Dictionary<string, List<IMessageChecker>>();

        public virtual void CreateMessageCheckers()
        {
            if (m_Rules.Count > 0) return;
            m_Rules.Add("JSON", CreateJsonMessageCheckers());
            m_Rules.Add("XML", CreateXmlMessageCheckers());
        }

        protected virtual List<IMessageChecker> CreateJsonMessageCheckers()
        {
            return new List<IMessageChecker>();
        }

        protected virtual List<IMessageChecker> CreateXmlMessageCheckers()
        {
            return new List<IMessageChecker>();
        }

        public virtual List<IMessageChecker> GetMessageCheckers(string groupName)
        {
            if (m_Rules.ContainsKey(groupName.ToUpper())) return m_Rules[groupName.ToUpper()];
            else return null;
        }

        public void Send<TMessage>(Session session, TMessage obj) where TMessage : class
        {
            WebMessage.Send<TMessage>(session, obj);
        }

        public void SendString(Session session, string str)
        {
            WebMessage.SendString(session, str);
        }

        public void Handle(Session session, string msg)
        {
            T data = WebMessage.ToJsonObject<T>(msg);
            if (data != null)
            {
                ProcessMessage(session, data);
            }
        }

        public virtual void ProcessMessage(Session session, T msg)
        {
            // to be overridden ...
            return;
        }

    }

    
}
