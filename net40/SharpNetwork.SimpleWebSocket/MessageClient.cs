using System;

using SharpNetwork.Core;

namespace SharpNetwork.SimpleWebSocket
{
    public class MessageClient : Client
    {
        protected Uri m_Uri = null;

        protected WebSocketEventPackage m_Events = null;
        protected IMessageDispatcher m_Handlers = null;

        public WebSocketEventPackage Events { get { return m_Events; } }

        public IMessageDispatcher Handlers { get { return m_Handlers; } }

        public bool IsOrderlyProcess
        {
            get
            {
                NetworkEventHandler handler = GetIoHandler() as NetworkEventHandler;
                if (handler != null) return handler.IsOrderlyProcess;
                else return false;
            }

            set
            {
                NetworkEventHandler handler = GetIoHandler() as NetworkEventHandler;
                if (handler != null) handler.IsOrderlyProcess = value;
            }
        }

        public static MessageClient CreateNewClient(IMessageDispatcher dispatcher = null)
        {
            if (dispatcher != null)
            {
                return new MessageClient(new MessageCodec(),
                    new NetworkEventHandler(dispatcher, new WebSocketEventPackage()));
            }
            else
            {
                return new MessageClient(new MessageCodec(),
                    new NetworkEventHandler(new MessageDispatcher(), new WebSocketEventPackage()));
            }
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
                m_Events = ((handler as NetworkEventHandler).Events) as WebSocketEventPackage;
                m_Handlers = (handler as NetworkEventHandler).GetHandlerManager();
            }

            if (m_Events != null) m_Events.OnConnect += WhenTcpSocketConnected;
        }

        protected void WhenTcpSocketConnected(Session session)
        {
            if (m_Uri != null) WebMessage.SendHandshakeRequest(session, m_Uri);
        }

        public void Open(string url)
        {
            if (GetState() > 0) return;
            m_Uri = new Uri(url, UriKind.Absolute);
            Connect(m_Uri.Host, m_Uri.Port);
        }

        public void Close()
        {
            Disconnect();
        }

        public void Send<T>(T obj, bool needmask = true)
        {
            if (GetState() <= 0) return;
            WebMessage.Send<T>(GetSession(), obj, needmask);
        }

        public void SendString(string str, bool needmask = true)
        {
            if (GetState() <= 0) return;
            WebMessage.SendString(GetSession(), str, needmask);
        }

        public void SendByteArray(byte[] bytes, int length = -1, bool needmask = true)
        {
            if (GetState() <= 0) return;
            WebMessage.SendByteArray(GetSession(), bytes, length, needmask);
        }

    }

    
}
