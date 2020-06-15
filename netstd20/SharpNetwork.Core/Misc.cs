using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace SharpNetwork.Core
{
    public interface INetworkFilter
    {
        void Encode(Session session, Object message, MemoryStream stream);
        bool Decode(Session session, MemoryStream stream, List<Object> output);
    }

    public interface INetworkEventHandler
    {
        void OnConnect(Session session);
        void OnDisconnect(Session session);

        int OnSend(Session session, Object data);
        int OnReceive(Session session, Object data);

        void OnIdle(Session session, Int32 optype);
        void OnError(Session session, Int32 errortype, Exception error);
    }

    public delegate void NetworkEvent(Session session);
    public delegate int NetworkDataEvent(Session session, Object data);
    public delegate void NetworkOpEvent(Session session, Int32 optype);
    public delegate void NetworkErrorEvent(Session session, Int32 errortype, Exception error);

    public class NetworkEventPackage
    {
        public event NetworkEvent OnConnect;
        public event NetworkEvent OnDisconnect;
        public event NetworkDataEvent OnSend;
        public event NetworkDataEvent OnReceive;
        public event NetworkOpEvent OnIdle;
        public event NetworkErrorEvent OnError;

        public NetworkEventPackage()
        {
            OnConnect = null;
            OnDisconnect = null;
            OnSend = null;
            OnReceive = null;
            OnIdle = null;
            OnError = null;
        }

        public void FireConnectEvent(Session session)
        {
            OnConnect?.Invoke(session);
        }

        public void FireDisconnectEvent(Session session)
        {
            OnDisconnect?.Invoke(session);
        }

        public int FireSendEvent(Session session, Object data)
        {
            if (OnSend != null) return OnSend(session, data);
            else return 0;
        }

        public int FireReceiveEvent(Session session, Object data)
        {
            if (OnReceive != null) return OnReceive(session, data);
            else return 0;
        }

        public void FireIdleEvent(Session session, Int32 optype)
        {
            if (OnIdle != null) OnIdle(session, optype);
        }

        public void FireErrorEvent(Session session, Int32 errortype, Exception error)
        {
            if (OnError != null) OnError(session, errortype, error);
        }
    }

    public class CommonNetworkEventHandler : INetworkEventHandler
    {
        NetworkEventPackage m_Events = null;

        public NetworkEventPackage Events
        {
            get
            {
                return m_Events;
            }
        }

        public CommonNetworkEventHandler()
        {
            m_Events = null;
        }

        public CommonNetworkEventHandler(NetworkEventPackage events)
        {
            m_Events = events;
        }

        public virtual void OnConnect(Session session)
        {
            if (m_Events != null) m_Events.FireConnectEvent(session);
        }

        public virtual void OnDisconnect(Session session)
        {
            if (m_Events != null) m_Events.FireDisconnectEvent(session);
        }

        public virtual int OnSend(Session session, Object data)
        {
            if (m_Events != null) return m_Events.FireSendEvent(session, data);
            else return 0;
        }

        public virtual int OnReceive(Session session, Object data)
        {
            if (m_Events != null) return m_Events.FireReceiveEvent(session, data);
            else return 0;
        }

        public virtual void OnIdle(Session session, Int32 optype)
        {
            //if (m_Events != null) m_Events.FireIdleEvent(session, optype);

            try
            {
                if (m_Events != null) m_Events.FireIdleEvent(session, optype);
            }
            finally
            {
                if (session != null) session.Close(); // we should close idle session by default
            }
        }

        public virtual void OnError(Session session, Int32 errortype, Exception error)
        {
            try
            {
                if (m_Events != null) m_Events.FireErrorEvent(session, errortype, error);
            }
            finally
            {
                if (session != null && Session.IsNetworkError(errortype))
                {
                    session.Close();
                }
            }
        }

    }

    public interface ICommonJsonCodec
    {
        string ToJsonString(object obj);
        T ToJsonObject<T>(string str) where T : class;
    }

    public class SimpleJsonCodec : ICommonJsonCodec
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
