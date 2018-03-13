using System;
using System.Collections.Generic;
using System.IO;

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
        void OnError(Session session, Int32 errortype, String errormsg);
    }

    public delegate void NetworkEvent(Session session);
    public delegate int NetworkDataEvent(Session session, Object data);
    public delegate void NetworkOpEvent(Session session, Int32 optype);
    public delegate void NetworkErrorEvent(Session session, Int32 errortype, String errormsg);

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
            if (OnConnect != null) OnConnect(session);
        }

        public void FireDisconnectEvent(Session session)
        {
            if (OnDisconnect != null) OnDisconnect(session);
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

        public void FireErrorEvent(Session session, Int32 errortype, String errormsg)
        {
            if (OnError != null) OnError(session, errortype, errormsg);
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
            if (m_Events != null) m_Events.FireIdleEvent(session, optype);
        }

        public virtual void OnError(Session session, Int32 errortype, String errormsg)
        {
            try
            {
                if (m_Events != null) m_Events.FireErrorEvent(session, errortype, errormsg);
            }
            finally
            {
                if (session != null) session.Close();
            }
        }

    }
}
