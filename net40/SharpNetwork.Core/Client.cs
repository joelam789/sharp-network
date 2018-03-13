using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace SharpNetwork.Core
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

        String m_TargetServer = "";

        // default size is 8k
        Int32 m_IoBufferSize = 1024 * 8;

        Socket m_Socket = null;

        RemoteCertificateValidationCallback m_ValidationCallback = null;

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

        public virtual void SetValidationCallback(RemoteCertificateValidationCallback callback)
        {
            m_ValidationCallback = callback;
        }

        public virtual bool HasValidationCallback()
        {
            return m_ValidationCallback != null;
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
            return m_IoBufferSize;
        }

        public void SetIoBufferSize(int value)
        {
            if (value <= 0) return;
            if (m_Session != null)
            {
                m_Session.SetBufferSize(Session.IO_RECV_SEND, value);
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
                        // and now, input string might be a domain name ...

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

                m_TargetServer = svrIp;

                remoteEP = new IPEndPoint(ipAddress, svrPort);

                // Create a TCP/IP socket.
                m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            }
            catch (Exception e)
            {
                if (m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnError(null, Session.ERROR_CONNECT, e.Message);
                    }
                    catch { }
                }
                Disconnect();
            }

            if (m_Socket == null || remoteEP == null || ipAddress == null) return;

            // set session's io buffer size to a global value by default
            // but you still can change them with different values in session's OnConnect event function
            m_Socket.ReceiveBufferSize = m_IoBufferSize;
            m_Socket.SendBufferSize = m_IoBufferSize;

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

                    result = m_Socket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), this);
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
                            m_IoHandler.OnError(null, Session.ERROR_CONNECT, "Connection timeout");
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
                        m_IoHandler.OnError(null, Session.ERROR_CONNECT, e.Message);
                    }
                    catch { }
                }

                // close socket
                Disconnect();
            }
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            Client client = null;
            try
            {
                client = ar == null ? null : ar.AsyncState as Client;
            }
            catch { client = null; }

            if (client == null) return;

            // Complete the connection.
            try
            {
                lock (client.m_SessionGroup)
                {
                    if (client.m_Socket != null)
                    {
                        client.m_Socket.EndConnect(ar);

                        client.m_Session = client.m_ValidationCallback == null
                                ? new Session(client.m_ClientId, client.m_Socket, client.m_IoHandler, client.m_IoFilter)
                                : new Session(client.m_ClientId, client.m_Socket, client.m_IoHandler, client.m_IoFilter, client.m_ValidationCallback);
                        client.m_Session.SetSessionGroup(client.m_SessionGroup);
                        client.m_SessionId = Convert.ToString(client.m_ClientId);

                        if (client.m_Session != null)
                        {
                            Stream stream = client.m_Session.GetStream();
                            if (stream != null)
                            {
                                if (stream is SslStream)
                                {
                                    var result = (stream as SslStream).BeginAuthenticateAsClient(client.m_TargetServer, // it should be a domain name ...
                                                    new AsyncCallback(AuthenticateCallback), client);
                                    if (result == null) throw new Exception("Failed to run BeginAuthenticateAsClient()");
                                    else return;
                                }
                                else if (stream is NetworkStream)
                                {
                                    client.m_State = 1;
                                    client.m_Session.Open();
                                    client.m_SessionGroup.StartCheckingIdle();
                                    return;
                                }
                            }
                        }
                    }
                }

                client.Disconnect();
            }
            catch (Exception e)
            {
                if (client.m_IoHandler != null)
                {
                    try
                    {
                        client.m_IoHandler.OnError(null, Session.ERROR_CONNECT, e.Message);
                    }
                    catch { }
                }
                client.Disconnect();
            }
        }

        private static void AuthenticateCallback(IAsyncResult ar)
        {
            Client client = null;
            try
            {
                client = ar == null ? null : ar.AsyncState as Client;
            }
            catch { client = null; }

            if (client == null) return;

            // Complete the connection.
            try
            {
                lock (client.m_SessionGroup)
                {
                    if (client.m_Session != null)
                    {
                        Stream stream = client.m_Session.GetStream();
                        if (stream != null)
                        {
                            if (stream is SslStream)
                            {
                                (stream as SslStream).EndAuthenticateAsClient(ar);
                                client.m_State = 1;
                                client.m_Session.Open();
                                client.m_SessionGroup.StartCheckingIdle();
                                return;
                            }
                        }
                    }
                }

                client.Disconnect();
            }
            catch (Exception e)
            {
                if (client.m_IoHandler != null)
                {
                    try
                    {
                        client.m_IoHandler.OnError(null, Session.ERROR_CONNECT, e.Message);
                    }
                    catch { }
                }
                client.Disconnect();
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
                m_Socket = null;
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
