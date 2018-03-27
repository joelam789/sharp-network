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

        public bool AllowOrderlyProcess { get; set; }

        public NetworkEventHandler(HttpRouter messageHandlerManager) : base()
        {
            m_MessageHandlerManager = messageHandlerManager;
            AllowOrderlyProcess = false;
        }

        public NetworkEventHandler(HttpRouter messageHandlerManager, NetworkEventPackage events)
            : base(events)
        {
            m_MessageHandlerManager = messageHandlerManager;
            AllowOrderlyProcess = false;
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
                    OnError(session, Session.ERROR_RECEIVE, ex.Message);
                }
                catch { }
            }

        }

        public override void OnConnect(Session session)
        {
            HttpMessage.GetSessionBuffer(session, true);
            HttpMessage.GetSessionData(session, true);

            // one factory for one session
            if (AllowOrderlyProcess) HttpMessage.GetSingleTaskFactory(session, true);

            base.OnConnect(session);
        }

        public override int OnReceive(Session session, Object data)
        {
            SessionContext ctx = new SessionContext(session, data);
            HttpMessage msg = (HttpMessage)ctx.Data;

            // run further decode process here (or may run it within the thread, see ProcessMessage())
            //MessageCodec.DecodeMessage(session, msg);

            TaskFactory factory = Task.Factory;

            if (AllowOrderlyProcess && ctx.Session != null)
                factory = HttpMessage.GetSingleTaskFactory(ctx.Session);

            if (factory != null)
            {
                factory.StartNew((Action<object>)((param) =>
                {
                    ProcessMessage(param);
                }), ctx);
            }
            else ProcessMessage(ctx);

            return base.OnReceive(session, data);
        }

    }
}
