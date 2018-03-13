using SharpNetwork.Core;

namespace SharpNetwork.SimpleProtocol
{
    public class MessageClient : Client
    {
        protected NetworkEventPackage m_Events = null;
        protected HandlerManager m_Handlers = null;

        public NetworkEventPackage Events { get { return m_Events; } }

        public HandlerManager Handlers { get { return m_Handlers; } }

        public bool AllowOrderlyProcess
        {
            get
            {
                NetworkEventHandler handler = GetIoHandler() as NetworkEventHandler;
                if (handler != null) return handler.AllowOrderlyProcess;
                else return false;
            }

            set
            {
                NetworkEventHandler handler = GetIoHandler() as NetworkEventHandler;
                if (handler != null) handler.AllowOrderlyProcess = value;
            }
        }

        public static MessageClient CreateNewClient()
        {
            return new MessageClient(new MessageCodec(),
                new NetworkEventHandler(new HandlerManager(), new NetworkEventPackage()));
        }

        public MessageClient() : base() { }

        public MessageClient(int clientId)
            : this()
        {
            SetClientId(clientId);
        }

        public MessageClient(int clientId, int clientType)
            : this()
        {
            SetClientId(clientId);
            SetClientType(clientType);
        }

        public MessageClient(INetworkFilter filter, INetworkEventHandler handler)
            : this()
        {
            SetIoFilter(filter);
            SetIoHandler(handler);
        }

        public MessageClient(int clientId, int clientType, INetworkFilter filter, INetworkEventHandler handler)
            : this()
        {
            SetClientId(clientId);
            SetClientType(clientType);

            SetIoFilter(filter);
            SetIoHandler(handler);
        }

        public override void SetIoHandler(INetworkEventHandler handler)
        {
            base.SetIoHandler(handler);
            if (handler is NetworkEventHandler)
            {
                m_Events = (handler as NetworkEventHandler).Events;
                m_Handlers = (handler as NetworkEventHandler).GetHandlerManager();
            }

        }

        public void Send<T>(int msgType, T obj, int flag = 0)
        {
            if (GetState() <= 0) return;
            NetMessage.Send<T>(GetSession(), msgType, obj, flag);
        }

    }
}
