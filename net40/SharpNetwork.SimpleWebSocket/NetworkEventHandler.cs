using System;
using System.Threading.Tasks;

using SharpNetwork.Core;

namespace SharpNetwork.SimpleWebSocket
{
    public class NetworkEventHandler : CommonNetworkEventHandler
    {
        IMessageDispatcher m_MessageHandlerManager = null;

        public bool IsOrderlyProcess { get; set; }

        public NetworkEventHandler(): base()
        {
            m_MessageHandlerManager = null;
            IsOrderlyProcess = false;
        }

        public NetworkEventHandler(IMessageDispatcher messageHandlerManager): base()
        {
            m_MessageHandlerManager = messageHandlerManager;
            IsOrderlyProcess = false;
        }

        public NetworkEventHandler(IMessageDispatcher messageHandlerManager, NetworkEventPackage events)
            : base(events)
        {
            m_MessageHandlerManager = messageHandlerManager;
            IsOrderlyProcess = false;
        }

        public IMessageDispatcher GetHandlerManager()
        {
            return m_MessageHandlerManager;
        }

        protected virtual void ProcessMessage(SessionContext ctx)
        {
            if (ctx == null) return;

            TaskFactory factory = Task.Factory;

            if (IsOrderlyProcess && ctx.Session != null)
                factory = WebMessage.GetSingleTaskFactory(ctx.Session);

            if (factory != null)
            {
                factory.StartNew((Action<object>)((param) =>
                {
                    DispatchMessage(param);
                }), ctx);
            }
            else DispatchMessage(ctx);

        }

        protected virtual void DispatchMessage(object data)
        {
            Session session = null;
            try
            {
                if (m_MessageHandlerManager == null || data == null) return;

                SessionContext ctx = (SessionContext)data;
                session = ctx.Session;
                WebMessage msg = (WebMessage)ctx.Data;

                if (msg.IsString()) m_MessageHandlerManager.Dispatch(session, msg.MessageContent);
                else if (msg.IsBinary())
                {
                    if (m_MessageHandlerManager.CanProcessBinary()) 
                        m_MessageHandlerManager.Dispatch(session, msg.RawContent);
                }

            }
            catch (Exception ex)
            {
                try
                {
                    OnError(session, Session.ERROR_RECEIVE, ex);
                }
                catch { }
            }

        }

        public override void OnConnect(Session session)
        {
            WebMessage.GetSessionBuffer(session, true);
            WebMessage.GetSessionData(session, true);
            //WebMessage.GetIncomingHeaders(session, true);

            if (IsOrderlyProcess) WebMessage.GetSingleTaskFactory(session, true);

            base.OnConnect(session);
        }

        public virtual void OnHandshake(Session session)
        {
            WebSocketEventPackage events = Events as WebSocketEventPackage;
            if (events != null) events.FireHandshakeEvent(session);
        }

        public virtual void OnPing(Session session, byte[] data)
        {
            WebSocketEventPackage events = Events as WebSocketEventPackage;
            if (events != null) events.FirePingEvent(session, data);

            session.Send(WebMessage.CreatePongMessage(data));

        }

        public virtual void OnPong(Session session, byte[] data)
        {
            WebSocketEventPackage events = Events as WebSocketEventPackage;
            if (events != null) events.FirePongEvent(session, data);
        }

        public override int OnReceive(Session session, Object data)
        {
            if (data is WebMessage)
            {
                WebMessage msg = data as WebMessage;

                if (msg.MessageType == WebMessage.MSG_TYPE_HANDSHAKE)
                {
                    Task.Factory.StartNew(() => { OnHandshake(session); });
                }
                else if (msg.MessageType == WebMessage.MSG_TYPE_PING)
                {
                    Task.Factory.StartNew(() => { OnPing(session, msg.RawContent); });
                }
                else if (msg.MessageType == WebMessage.MSG_TYPE_PONG)
                {
                    Task.Factory.StartNew(() => { OnPong(session, msg.RawContent); });
                }
                else
                {
                    // run further decode process here (or may run it within the thread, see ProcessMessage())
                    MessageCodec.DecodeMessage(session, msg);

                    ProcessMessage(new SessionContext(session, data));

                }
            }
            return base.OnReceive(session, data);
        }
   
    }

    public class WebSocketEventPackage : NetworkEventPackage
    {
        public event NetworkEvent OnHandshake;
        public event NetworkDataEvent OnPing;
        public event NetworkDataEvent OnPong;

        public WebSocketEventPackage(): base()
        {
            OnHandshake = null;
            OnPing = null;
            OnPong = null;
        }

        public void FireHandshakeEvent(Session session)
        {
            if (OnHandshake != null) OnHandshake(session);
        }

        public void FirePingEvent(Session session, Object data)
        {
            if (OnPing != null) OnPing(session, data);
        }

        public void FirePongEvent(Session session, Object data)
        {
            if (OnPong != null) OnPong(session, data);
        }
    }

    

}
