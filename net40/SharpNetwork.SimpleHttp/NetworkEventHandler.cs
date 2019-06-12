using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharpNetwork.Core;

namespace SharpNetwork.SimpleHttp
{
    public class NetworkEventHandler : CommonNetworkEventHandler
    {
        HttpRouter m_MessageHandlerManager = null;

        public bool IsOrderlyProcess { get; set; }

        public NetworkEventHandler() : base()
        {
            m_MessageHandlerManager = null;
            IsOrderlyProcess = false;
        }

        public NetworkEventHandler(HttpRouter messageHandlerManager) : base()
        {
            m_MessageHandlerManager = messageHandlerManager;
            IsOrderlyProcess = false;
        }

        public NetworkEventHandler(HttpRouter messageHandlerManager, NetworkEventPackage events)
            : base(events)
        {
            m_MessageHandlerManager = messageHandlerManager;
            IsOrderlyProcess = false;
        }

        public HttpRouter GetHandlerManager()
        {
            return m_MessageHandlerManager;
        }

        protected virtual void ProcessMessage(Object task)
        {
            Session session = null;
            try
            {
                if (m_MessageHandlerManager == null || task == null) return;

                SessionContext ctx = (SessionContext)task;
                session = ctx.Session;
                HttpMessage msg = (HttpMessage)ctx.Data;

                IHandler handler = m_MessageHandlerManager.GetHandler(msg.RequestUrl);
                if (handler == null) return;

                // you may run some complex decode function here ...
                //MessageCodec.DecodeMessage(session, msg);

                handler.Handle(ctx);
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
            HttpMessage.GetSessionBuffer(session, true);
            HttpMessage.GetSessionData(session, true);
            HttpMessage.GetIncomingHeaders(session, true);

            // one factory for one session
            if (IsOrderlyProcess) HttpMessage.GetSingleTaskFactory(session, true);

            base.OnConnect(session);
        }

        public override int OnReceive(Session session, Object data)
        {
            if (data is HttpMessage) ProcessMessage(new SessionContext(session, data));
            return base.OnReceive(session, data);
        }

        protected virtual void ProcessMessage(SessionContext ctx)
        {
            if (ctx == null) return;

            TaskFactory factory = Task.Factory;

            if (IsOrderlyProcess && ctx.Session != null)
                factory = HttpMessage.GetSingleTaskFactory(ctx.Session);

            if (factory != null)
            {
                factory.StartNew((param) => DispatchMessage(param), ctx);
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
                HttpMessage msg = (HttpMessage)ctx.Data;

                IHandler handler = m_MessageHandlerManager.GetHandler(msg.RequestUrl);
                if (handler == null) return;

                // you may run some complex decode function here ...
                //MessageCodec.DecodeMessage(session, msg);

                handler.Handle(ctx);

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

    }
}
