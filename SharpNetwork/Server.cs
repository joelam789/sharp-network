using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace SharpNetwork
{
    public class Server
    {
        protected INetworkFilter m_IoFilter = null;
        protected INetworkEventHandler m_IoHandler = null;

        Thread m_ListenThread = null;
        Socket m_ListenSocket = null;

        Boolean m_Acceptable = true;

        Int32 m_MaxClientCount = 0;

        Int32 m_ServerId = 1;
        Int32 m_ServerType = 1;

        String m_ServerName = "";
        String m_ServerTypeName = "";

        Int32 m_State = -1;

        Int32 m_ListeningPort = 0;
        String m_ListeningIp = "";

        Int32 m_NextSessionID = 0;

        Int32 m_IoBufferSize = 1024 * 8; // default size is 8k

        X509Certificate2 m_Cert = null;

        SessionGroup m_SessionGroup = new SessionGroup();

        ManualResetEvent m_ListenWatcher = new ManualResetEvent(false);

        public int GetServerId()
        {
            return m_ServerId;
        }
        public void SetServerId(int serverId)
        {
            m_ServerId = serverId;
        }

        public int GetServerType()
        {
            return m_ServerType;
        }
        public void SetServerType(int serverType)
        {
            m_ServerType = serverType;
        }

        public string GetServerName()
        {
            return m_ServerName;
        }
        public void SetServerName(string serverName)
        {
            m_ServerName = serverName;
        }

        public string GetServerTypeName()
        {
            return m_ServerTypeName;
        }
        public void SetServerTypeName(string serverTypeName)
        {
            m_ServerTypeName = serverTypeName;
        }

        public bool GetAcceptable()
        {
            return m_Acceptable;
        }
        public void SetAcceptable(bool value)
        {
            m_Acceptable = value;
        }

        public int GetMaxClientCount()
        {
            return m_MaxClientCount;
        }

        public void SetMaxClientCount(int value)
        {
            m_MaxClientCount = value;
        }

        public X509Certificate2 GetCert()
        {
            return m_Cert;
        }
        public void SetCert(X509Certificate2 cert)
        {
            m_Cert = cert;
        }

        public int GetState()
        {
            return m_State;
        }

        public int GetListeningPort()
        {
            if (m_State <= 0) return 0;
            else return m_ListeningPort;
        }

        public string GetListeningIp()
        {
            if (m_State <= 0) return "";
            else return m_ListeningIp;
        }

        public INetworkEventHandler GetIoHandler()
        {
            return m_IoHandler;
        }

        public virtual void SetIoHandler(INetworkEventHandler handler)
        {
            m_IoHandler = handler;
        }

        public INetworkFilter GetIoFilter()
        {
            return m_IoFilter;
        }

        public virtual void SetIoFilter(INetworkFilter filter)
        {
            m_IoFilter = filter;
        }

        public int GetIoBufferSize()
        {
            return m_IoBufferSize;
        }

        public void SetIoBufferSize(int value)
        {
            if (value <= 0) return;
            m_IoBufferSize = value;
        }

        private void AcceptLoop()
        {
            while (m_State > 0)
            {
                m_ListenWatcher.Reset();
                try
                {
                    lock (m_ListenWatcher)
                    {
                        if (m_ListenSocket != null)
                            m_ListenSocket.BeginAccept(new AsyncCallback(AcceptCallback), m_ListenSocket);
                    }

                    m_ListenWatcher.WaitOne();
                }
                catch (Exception ex)
                {
                    if (m_IoHandler != null)
                    {
                        try
                        {
                            m_IoHandler.OnError(null, Session.ERROR_CONNECT, ex.Message);
                        }
                        catch { }
                    }
                }
            }
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.

            Socket socket = null;

            lock (m_ListenWatcher)
            {
                try
                {
                    // Get the socket that handles the client request.
                    Socket listener = ar == null ? null : ar.AsyncState as Socket;
                    if (listener != null) socket = listener.EndAccept(ar);
                }
                catch { socket = null; }
            }

            // after unlock the watcher, re-activate it ... 
            m_ListenWatcher.Set();

            if (socket == null) return;

            if (!m_Acceptable || m_State <= 0)
            {
                try { socket.Close(); }
                catch { }
                return;
            }

            if (m_MaxClientCount > 0 && m_SessionGroup.GetSessionCount() >= m_MaxClientCount)
            {
                try { socket.Close(); }
                catch { }
                return;
            }

            // set every new session's io buffer size to a global value by default
            // but you still can change it in every session's OnConnect event function
            socket.ReceiveBufferSize = m_IoBufferSize;
            socket.SendBufferSize = m_IoBufferSize;

            // Create the session object.

            Session session = null;

            lock (m_SessionGroup)
            {
                m_NextSessionID++;
                session = new Session(m_NextSessionID, socket, m_IoHandler, m_IoFilter, m_Cert != null);
                session.SetSessionGroup(m_SessionGroup);
            }

            try
            {
                if (session != null)
                {
                    Stream stream = session.GetStream();
                    if (stream != null)
                    {
                        if (stream is SslStream && m_Cert != null)
                        {
                            var result = (stream as SslStream).BeginAuthenticateAsServer(m_Cert, new AsyncCallback(AuthenticateCallback), session);
                            if (result == null) throw new Exception("Failed to run BeginAuthenticateAsServer()");
                            else return;
                        }
                        else if (stream is NetworkStream)
                        {
                            if (session != null)
                            {
                                session.Open();
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnError(null, Session.ERROR_CONNECT, ex.Message);
                    }
                    catch { }
                }
            }

            if (session != null) session.Close();

        }

        public void AuthenticateCallback(IAsyncResult ar)
        {
            Session session = null;
            try
            {
                session = ar == null ? null : ar.AsyncState as Session;
            }
            catch { session = null; }

            if (session == null) return;

            try
            {
                Stream stream = session == null ? null : session.GetStream();
                if (ar != null && stream != null && stream is SslStream)
                {
                    (stream as SslStream).EndAuthenticateAsServer(ar);
                    if (session != null)
                    {
                        session.Open();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                if (m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnError(null, Session.ERROR_CONNECT, ex.Message);
                    }
                    catch { }
                }
            }

            if (session != null) session.Close();
        }

        protected bool Listen(IPEndPoint localEndPoint, int localPort)
        {
            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                m_State = 0;

                lock (m_ListenWatcher)
                {
                    m_ListenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    m_ListenSocket.ReceiveBufferSize = m_IoBufferSize;
                    m_ListenSocket.SendBufferSize = m_IoBufferSize;
                    m_ListenSocket.Bind(localEndPoint);
                    m_ListenSocket.Listen(100);
                }

                m_State = 1;

            }
            catch(Exception ex)
            {
                if (m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnError(null, Session.ERROR_CONNECT, ex.Message);
                    }
                    catch { }
                }

                Stop();
            }

            return m_State > 0;
        }

        public virtual bool Start(int port)
        {
            return Start("", port);
        }

        public bool Start(string ipstr, int port)
        {
            bool isWrongIp = false;
            string ipAddrStr = "";
            IPAddress ipAddress = null;

            if (ipstr != null) ipAddrStr = ipstr.Trim();
            if (ipAddrStr.Length > 0)
            {
                try
                {
                    ipAddress = IPAddress.Parse(ipAddrStr);
                }
                catch(Exception ex)
                {
                    isWrongIp = true;
                    ipAddress = null;

                    if (m_IoHandler != null)
                    {
                        try
                        {
                            m_IoHandler.OnError(null, Session.ERROR_CONNECT, ex.Message);
                        }
                        catch { }
                    }
                }
            }

            if (isWrongIp) return false;

            if (ipAddress == null) ipAddress = IPAddress.Any;

            Stop();

            bool result = false;

            try
            {
                IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

                if (localEndPoint != null) result = Listen(localEndPoint, port);

                if (result)
                {
                    if (ipAddrStr.Length > 0) m_ListeningIp = ipAddrStr;
                    m_ListeningPort = port;

                    m_ListenThread = new Thread(new ThreadStart(AcceptLoop));
                    m_ListenThread.Start();

                    lock (m_SessionGroup)
                    {
                        m_SessionGroup.StartCheckingIdle();
                    }
                }
                else
                {
                    m_ListeningIp = "";
                    m_ListeningPort = 0;
                }
            }
            catch (Exception ex)
            {
                if (m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnError(null, Session.ERROR_CONNECT, ex.Message);
                    }
                    catch { }
                }

                Stop();
            }

            return result;

        }

        public virtual void Stop()
        {
            m_State = -1;
            m_ListenWatcher.Set();

            lock (m_ListenWatcher)
            {
                if (m_ListenSocket != null)
                {
                    try
                    {
                        m_ListenSocket.Close();
                        m_ListenSocket.Dispose();
                    }
                    catch { }

                    m_ListenSocket = null;
                }
            }

            m_ListeningPort = 0;
            m_ListeningIp = "";

            Dictionary<Int32, Session> sessions = GetSessions();
            if (sessions != null && sessions.Count > 0)
            {
                foreach (KeyValuePair<Int32, Session> item in sessions)
                {
                    try
                    {
                        Session session = item.Value;
                        session.Close();
                    }
                    catch { }
                }
            }

            if (sessions != null) sessions.Clear();
            lock (m_SessionGroup)
            {
                m_SessionGroup.StopCheckingIdle();
                m_SessionGroup.Clear();
                m_NextSessionID = 0;
            }
        }

        public int GetSessionCount()
        {
            return m_SessionGroup.GetSessionCount();
        }

        public Dictionary<Int32, Session> GetSessions()
        {
            return m_SessionGroup.GetSessions();
        }

        public Session GetSessionById(int sessionId)
        {
            return m_SessionGroup.GetSessionById(sessionId);
        }

        public SessionGroup GetSessionGroup()
        {
            return m_SessionGroup;
        }

        public Dictionary<Int32, Object> GetAttributes()
        {
            return m_SessionGroup.GetAttributes();
        }
        
        public void Broadcast(Object msg)
        {
            Dictionary<Int32, Session> sessions = GetSessions();
            if (sessions != null && sessions.Count > 0)
            {
                foreach (KeyValuePair<Int32, Session> item in sessions)
                {
                    try
                    {
                        Session session = item.Value;
                        session.Send(msg);
                    }
                    catch { }
                }
            }
        }

        public void SetIdleTime(Int32 opType, Int32 idleTime)
        {
            m_SessionGroup.SetIdleTime(opType, idleTime);
        }

        
    }
}
