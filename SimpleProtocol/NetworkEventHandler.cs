using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;

namespace SharpNetwork.SimpleProtocol
{
    public class NetworkEventHandler : CommonNetworkEventHandler
    {
        HandlerManager m_MessageHandlerManager = null;

        public bool AllowOrderlyProcess { get; set; }

        public NetworkEventHandler(HandlerManager messageHandlerManager): base()
        {
            m_MessageHandlerManager = messageHandlerManager;
            AllowOrderlyProcess = false;
        }

        public NetworkEventHandler(HandlerManager messageHandlerManager, NetworkEventPackage events)
            : base(events)
        {
            m_MessageHandlerManager = messageHandlerManager;
            AllowOrderlyProcess = false;
        }

        public HandlerManager GetHandlerManager()
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
                NetMessage msg = (NetMessage)ctx.Data;
                int msgType = msg.MessageType;
                IHandler handler = m_MessageHandlerManager.GetHandler(msgType);
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
            NetMessage.GetSessionBuffer(session, true);
            NetMessage.GetSessionData(session, true);

            // one factory for one session
            if (AllowOrderlyProcess) NetMessage.GetSingleTaskFactory(session, true);

            base.OnConnect(session);
        }

        public override int OnReceive(Session session, Object data)
        {
            SessionContext ctx = new SessionContext(session, data);
            NetMessage msg = (NetMessage)ctx.Data;

            // run further decode process here (or may run it within the thread, see ProcessMessage())
            MessageCodec.DecodeMessage(session, msg);

            TaskFactory factory = Task.Factory;

            if (AllowOrderlyProcess && msg.IsOrdered() && ctx.Session != null)
                factory = NetMessage.GetSingleTaskFactory(ctx.Session);

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
