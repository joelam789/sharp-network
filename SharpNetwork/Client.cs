using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SharpNetwork
{
    public class Client
    {
        protected INetworkFilter m_IoFilter = null;
        protected INetworkEventHandler m_IoHandler = null;

        Session m_Session = null;

        Int32 m_ClientId = 1;
        Int32 m_ClientType = 1;

        Int32 m_State = -1;

        String m_SessionId = "0";

        String m_RemoteIp = "";
        Int32 m_RemotePort = 0;

        // default size is 8k
        Int32 m_IoBufferSize = 1024 * 8;

        SessionGroup m_SessionGroup = new SessionGroup();

        public INetworkEventHandler GetIoHandler()
        {
            return m_IoHandler;
        }

        public INetworkFilter GetIoFilter()
        {
            return m_IoFilter;
        }

        public virtual void SetIoHandler(INetworkEventHandler handler)
        {
            m_IoHandler = handler;
        }

        public virtual void SetIoFilter(INetworkFilter filter)
        {
            m_IoFilter = filter;
        }

        public string GetRemoteIp()
        {
            if (m_Session != null && m_Session.GetState() > 0) return m_Session.GetRemoteIp();
            else return m_RemoteIp;
        }

        public int GetRemotePort()
        {
            if (m_Session != null && m_Session.GetState() > 0) return m_Session.GetRemotePort();
            else return m_RemotePort;
        }

        public int GetClientId()
        {
            return m_ClientId;
        }

        public void SetClientId(int clientId)
        {
            m_ClientId = clientId;
        }

        public int GetClientType()
        {
            return m_ClientType;
        }

        public void SetClientType(int clientType)
        {
            m_ClientType = clientType;
        }

        public int GetState()
        {
            if (m_Session != null)
            {
                int sessionState = m_Session.GetState();
                if (m_State == 0 && sessionState < 0) return m_State;
                else return sessionState;
            }
            else return m_State;
        }

        public int GetIoBufferSize()
        {
            if (m_Session != null && m_Session.GetSocket() != null)
                return m_Session.GetSocket().ReceiveBufferSize;

            return m_IoBufferSize;
        }

        public void SetIoBufferSize(int value)
        {
            if (value <= 0) return;
            if (m_Session != null && m_Session.GetSocket() != null)
            {
                m_Session.GetSocket().ReceiveBufferSize = value;
                m_Session.GetSocket().SendBufferSize = value;
            }
            m_IoBufferSize = value;
        }

        public Session GetSession()
        {
            return m_Session;
        }

        public SessionGroup GetSessionGroup()
        {
            return m_SessionGroup;
        }

        public Dictionary<Int32, Object> GetAttributes()
        {
            return m_SessionGroup.GetAttributes();
        }

        public Dictionary<Int32, Object> GetSessionAttributes()
        {
            if (m_Session != null) return m_Session.GetAttributes();
            else return null;
        }
        public void Connect(string svrIp, int svrPort, int timeout = 0)
        {
            IPEndPoint remoteEP = null;
            IPAddress ipAddress = null;
            Socket socket = null;

            try
            {
                if (ipAddress == null)
                {
                    try
                    {
                        // first, we assume input is IP string
                        ipAddress = IPAddress.Parse(svrIp);
                    }
                    catch
                    {
                        ipAddress = null;
                    }
                }

                if (ipAddress == null)
                {
                    try
                    {
                        // and now, input string may be a domain name ...

                        IPHostEntry ipHostInfo = Dns.GetHostEntry(svrIp);
                        foreach (IPAddress addr in ipHostInfo.AddressList)
                        {
                            // try to accept IPv4 first ...
                            if (addr.AddressFamily == AddressFamily.InterNetwork)
                            {
                                ipAddress = addr;
                                break;
                            }
                        }

                        if (ipAddress == null)
                        {
                            // try to accept any available ...
                            foreach (IPAddress addr in ipHostInfo.AddressList)
                            {
                                ipAddress = addr;
                                break;
                            }
                        }
                    }
                    catch { }

                }

                if (ipAddress == null) return;

                Disconnect();

                remoteEP = new IPEndPoint(ipAddress, svrPort);

                // Create a TCP/IP socket.
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            }
            catch (Exception e)
            {
                if (m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnError(m_Session, Session.ERROR_CONNECT, e.Message);
                    }
                    catch { }
                }
                Disconnect();
            }

            if (socket == null || remoteEP == null || ipAddress == null) return;

            socket.ReceiveBufferSize = m_IoBufferSize;
            socket.SendBufferSize = m_IoBufferSize;

            // Connect to the remote endpoint.
            try
            {
                IAsyncResult result = null;
                bool success = false;

                lock (m_SessionGroup)
                {
                    m_State = 0;

                    m_RemoteIp = IPAddress.Parse(ipAddress.ToString()).ToString();
                    m_RemotePort = svrPort;

                    m_Session = new Session(m_ClientId, socket, m_IoHandler, m_IoFilter);
                    m_Session.SetSessionGroup(m_SessionGroup);
                    m_SessionId = Convert.ToString(m_ClientId);

                    result = socket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), this);
                }

                if (result != null && timeout > 0) success = result.AsyncWaitHandle.WaitOne(timeout * 1000, true);
                else success = result != null;

                if (!success)
                {
                    // NOTE, MUST CLOSE THE SOCKET
                    if (m_IoHandler != null)
                    {
                        try
                        {
                            m_IoHandler.OnError(m_Session, Session.ERROR_CONNECT, "Connection timeout");
                        }
                        catch { }
                    }

                    // close socket
                    Disconnect();
                }

            }
            catch (Exception e)
            {
                if (m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnError(m_Session, Session.ERROR_CONNECT, e.Message);
                    }
                    catch { }
                }

                // close socket
                Disconnect();
            }
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            Client client = (Client)ar.AsyncState;
            if (client != null)
            {
                // Complete the connection.
                try
                {
                    lock (client.m_SessionGroup)
                    {
                        if (client.m_Session != null)
                        {
                            client.m_Session.GetSocket().EndConnect(ar);

                            client.m_State = 1;

                            client.m_Session.Open();
                        }
                        client.m_SessionGroup.StartCheckingIdle();
                    }
                }
                catch (Exception e)
                {
                    if (client.m_IoHandler != null)
                    {
                        try
                        {
                            client.m_IoHandler.OnError(client.m_Session, Session.ERROR_CONNECT, e.Message);
                        }
                        catch { }
                    }
                    client.Disconnect();
                }
            }
        }

        public void Disconnect(bool rightNow = true)
        {
            lock (m_SessionGroup)
            {
                m_State = -1;
                if (m_Session != null) m_Session.Close(rightNow);
                m_SessionGroup.StopCheckingIdle();
                m_SessionGroup.Clear();
                //m_Session = null;
                m_SessionId = "0";
            }
        }

        public void Send(Object message)
        {
            if (m_State <= 0) return;
            if (m_Session != null) m_Session.Send(message);
        }

        public void SetIdleTime(Int32 opType, Int32 idleTime)
        {
            m_SessionGroup.SetIdleTime(opType, idleTime);
        }

    }
}
