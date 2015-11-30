using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SharpNetwork
{
    public class Session
    {
        public const int ERROR_CONNECT  =  0;
        public const int ERROR_RECEIVE  =  1;
        public const int ERROR_SEND     =  2;

        public const int IO_RECV_SEND   =  0;
        public const int IO_RECV        =  1;
        public const int IO_SEND        =  2;

        public const int OP_ASYNC       =  1;
        public const int OP_CONCURRENT  =  2;

        public const int PROTOCOL_TYPE_UNKNOWN = 0;
        public const int PROTOCOL_TYPE_NORMAL = 1;
        //public const int PROTOCOL_TYPE_HTTP = 2;
        //public const int PROTOCOL_TYPE_WEBSOCK = 3;

        private Socket m_Socket = null;

        private INetworkFilter m_IoFilter = null;
        private INetworkEventHandler m_IoHandler = null;

        private Int32 m_Id = 0;

        private String m_RemoteIp = "";
        private Int32 m_RemotePort = 0;

        private Int32 m_State = -1;

        private Int32 m_ProtocolType = 0;

        private Boolean m_IsGoingToClose = false;

        private Int32 m_MaxReadQueueSize = 1024;
        private Int32 m_MaxWriteQueueSize = 0;

        private Int32 m_ReadSize = 0;
        private Byte[] m_ReadBuffer = null;

        private MemoryStream m_ReadStream = new MemoryStream();
        private MemoryStream m_IncomingStream = new MemoryStream();

        private DateTime m_LastReadTime = DateTime.MinValue;
        private DateTime m_LastWriteTime = DateTime.MinValue;

        private Dictionary<Int32, Object> m_Attributes = new Dictionary<Int32, Object>();

        private Queue<IoDataStream> m_OutgoingMessageQueue = new Queue<IoDataStream>();
        private Queue<Object> m_IncomingMessageQueue = new Queue<Object>();

        private SessionGroup m_SessionGroup = null;

        
        public Session(int id, Socket socket, INetworkEventHandler handler, INetworkFilter filter)
        {
            m_Id = id;
            m_Socket = socket;
            m_IoHandler = handler;
            m_IoFilter = filter;
        }

        public int GetId()
        {
            return m_Id;
        }

        public int GetState()
        {
            return m_State;
        }

        public Socket GetSocket()
        {
            return m_Socket;
        }

        public INetworkEventHandler GetIoHandler()
        {
            return m_IoHandler;
        }

        public INetworkFilter GetIoFilter()
        {
            return m_IoFilter;
        }

        public string GetRemoteIp()
        {
            return m_RemoteIp;
        }

        public int GetRemotePort()
        {
            return m_RemotePort;
        }

        public int GetProtocolType()
        {
            return m_ProtocolType;
        }

        public void SetProtocolType(int flag)
        {
            m_ProtocolType = flag;
        }

        public int GetMaxMessageQueueSize(int optype)
        {
            if (optype == Session.IO_RECV) return m_MaxReadQueueSize;
            else if (optype == Session.IO_SEND) return m_MaxWriteQueueSize;

            return 0;
        }

        public void SetMaxMessageQueueSize(int optype, int value)
        {
            if(optype == 0)
            {
                m_MaxReadQueueSize = value;
                m_MaxWriteQueueSize = value;
            }
            else if (optype == Session.IO_RECV) m_MaxReadQueueSize = value;
            else if (optype == Session.IO_SEND) m_MaxWriteQueueSize = value;
        }

        public int GetSessionCount()
        {
            if (m_SessionGroup == null) return 0;
            else return m_SessionGroup.GetSessionCount();
        }
        public Dictionary<Int32, Session> GetSessions()
        {
            if (m_SessionGroup == null) return null;
            else return m_SessionGroup.GetSessions();
        }
        public SessionGroup GetSessionGroup()
        {
            return m_SessionGroup;
        }
        public void SetSessionGroup(SessionGroup sessionGroup)
        {
            m_SessionGroup = sessionGroup;
        }
        public Session GetSessionById(int sessionId)
        {
            if (m_SessionGroup == null) return null;
            else return m_SessionGroup.GetSessionById(sessionId);
        }

        public Dictionary<Int32, Object> GetAttributes()
        {
            return m_Attributes;
        }

        public void Open()
        {
            if (m_State >= 0) return; // do nothing if the session is already in "connected" or "connecting" state

            if (m_SessionGroup != null)
            {
                // first, add it to group ...
                m_SessionGroup.AddSession(m_Id, this);
            }

            m_LastReadTime = DateTime.Now;
            m_LastWriteTime = DateTime.Now;

            if (m_Socket != null)
            {
                try
                {
                    m_State = 1;
                    m_RemoteIp = IPAddress.Parse(((IPEndPoint)m_Socket.RemoteEndPoint).Address.ToString()).ToString();
                    m_RemotePort = ((IPEndPoint)m_Socket.RemoteEndPoint).Port;
                }
                catch { }
            }

            // make sure the queue is clean
            if (m_OutgoingMessageQueue.Count > 0)
            {
                m_OutgoingMessageQueue.Clear();
            }

            // make sure the queue is clean
            if (m_IncomingMessageQueue.Count > 0)
            {
                m_IncomingMessageQueue.Clear();
            }

            if (m_IoHandler != null) { try { m_IoHandler.OnConnect(this); } catch { } }

            try
            {
                if (m_Socket != null)
                {
                    if (m_ReadBuffer == null || m_ReadBuffer.Length <= 0) m_ReadBuffer = new Byte[m_Socket.ReceiveBufferSize];
                    m_Socket.BeginReceive(m_ReadBuffer, 0, m_ReadBuffer.Length, 0, new AsyncCallback(ReceiveCallback), this);
                }
            }
            catch (Exception ex)
            {
                if (m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnError(this, ERROR_RECEIVE, ex.Message);
                    }
                    catch { }
                }
            }
        }

        private static void ReceiveCallback(IAsyncResult arg)
        {
            Session session = (Session)arg.AsyncState;

            try
            {
                bool needClose = false;

                session.m_LastReadTime = DateTime.Now;

                if (session.m_Socket != null && session.m_State > 0)
                {
                    // Read data from the remote device.
                    session.m_ReadSize = session.m_Socket.EndReceive(arg);
                    if (session.m_ReadSize <= 0) needClose = true;
                }

                if (needClose) session.Close();
                else if (session.m_ReadSize > 0 && session.m_State > 0) session.Receive();

            }
            catch (Exception e)
            {
                if (session.m_IoHandler != null)
                {
                    try
                    {
                        session.m_IoHandler.OnError(session, ERROR_RECEIVE, e.Message);
                    }
                    catch { }
                }
            }
        }

        private void Receive()
        {
            try
            {
                List<Object> outputList = new List<Object>();

                int lastsize = Convert.ToInt32(m_IncomingStream.Length);
                int totalsize = m_ReadSize + lastsize;

                if (totalsize > 0)
                {
                    m_ReadStream.Position = 0;
                    m_ReadStream.SetLength(totalsize);

                    if (m_IncomingStream.Length > 0)
                    {
                        m_IncomingStream.Position = 0;
                        m_IncomingStream.WriteTo(m_ReadStream);
                    }

                    m_ReadStream.Position = lastsize;
                    m_ReadStream.Write(m_ReadBuffer, 0, m_ReadSize);

                    m_ReadStream.Position = 0;

                    bool noRemains = true;

                    if (m_IoFilter != null)
                    {
                        try
                        {
                            noRemains = m_IoFilter.Decode(this, m_ReadStream, outputList);
                        }
                        catch { }
                    }

                    if (!noRemains && m_ReadStream.Position != totalsize)
                    {
                        int remain = totalsize - Convert.ToInt32(m_ReadStream.Position);
                        if (remain > 0)
                        {
                            Byte[] tempbytes = new Byte[remain];
                            m_ReadStream.Read(tempbytes, 0, remain);

                            m_IncomingStream.Position = 0;
                            m_IncomingStream.SetLength(remain);

                            m_IncomingStream.Write(tempbytes, 0, remain);
                            m_IncomingStream.Position = 0;

                        }
                    }
                    else
                    {
                        m_IncomingStream.Position = 0;
                        m_IncomingStream.SetLength(0);
                    }

                    if (outputList.Count > 0 && m_IoHandler != null)
                    {
                        bool full = false;

                        int queueSize = m_IncomingMessageQueue.Count;
                        foreach (Object obj in outputList)
                        {
                            if (m_MaxReadQueueSize > 0)
                            {
                                if (queueSize >= m_MaxReadQueueSize)
                                {
                                    full = true;
                                    break;
                                }
                            }
                            m_IncomingMessageQueue.Enqueue(obj);
                            queueSize++;
                        }

                        if (full)
                        {
                            if (m_IoHandler != null)
                            {
                                try
                                {
                                    m_IoHandler.OnError(this, Session.ERROR_RECEIVE, "Incoming queue is full");
                                }
                                catch { }
                            }
                        }

                        ProcessIncomingData(); // process
                    }

                    // next round
                    if (m_Socket != null && m_State > 0)
                    {
                        m_Socket.BeginReceive(m_ReadBuffer, 0, m_ReadBuffer.Length, 0, new AsyncCallback(ReceiveCallback), this);
                    }

                }
                else
                {
                    // next round
                    if (m_Socket != null && m_State > 0)
                    {
                        m_Socket.BeginReceive(m_ReadBuffer, 0, m_ReadBuffer.Length, 0, new AsyncCallback(ReceiveCallback), this);
                    }
                }
               
            }
            catch (Exception ex)
            {
                if (m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnError(this, ERROR_RECEIVE, ex.Message);
                    }
                    catch { }
                }
            }
        }

        public void Close(bool rightNow = true)
        {
            if (!rightNow)
            {
                // if there are some messages still waiting for sending, delay the closing action
                if (m_OutgoingMessageQueue.Count > 0 || m_IncomingMessageQueue.Count > 0)
                {
                    m_IsGoingToClose = true;
                    return;
                }
            }

            if (m_State > 0)
            {
                // when state is changed from "connected" to "disconnected", fire the event.
                // please note that now the socket is (probably) still connecting and the session is still in group.
                if (m_IoHandler != null) { try { m_IoHandler.OnDisconnect(this); } catch { } }
            }

            if (m_Socket != null)
            {
                m_State = 0;
                try
                {
                    try { m_Socket.Shutdown(SocketShutdown.Both); }
                    catch { }
                    try { m_Socket.Close(); }
                    catch { }
                    try { m_Socket.Dispose(); }
                    catch { }
                }
                catch { }
                m_State = -1;
                m_Socket = null;
            }
            else
            {
                m_State = -1;
                m_Socket = null;
            }

            // clean up the queue
            if (m_OutgoingMessageQueue.Count > 0)
            {
                m_OutgoingMessageQueue.Clear();
            }

            // clean up the queue
            if (m_IncomingMessageQueue.Count > 0)
            {
                m_IncomingMessageQueue.Clear();
            }

            m_IsGoingToClose = false;

            m_Attributes.Clear();

            if (m_SessionGroup != null)
            {
                // finally, remove it from group ...
                m_SessionGroup.RemoveSession(m_Id);
            }
        }

        public void Send(Object message)
        {
            if (m_Socket == null || m_State <= 0 || m_IsGoingToClose || message == null) return;

            int queueSize = 0;
            if (m_MaxWriteQueueSize > 0) // need to check queue size
            {
                queueSize = m_OutgoingMessageQueue.Count;
            }
            if (m_MaxWriteQueueSize > 0 && queueSize >= m_MaxWriteQueueSize)
            {
                if (m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnError(this, Session.ERROR_SEND, "Outgoing queue is full");
                    }
                    catch { }
                }
                return;
            }

            MemoryStream stream = new MemoryStream();

            stream.SetLength(0);
            stream.Position = 0;

            bool encodeOK = true;

            if (m_IoFilter != null)
            {
                try
                {
                    m_IoFilter.Encode(this, message, stream);
                }
                catch (Exception ex)
                {
                    encodeOK = false;
                    if (m_IoHandler != null)
                    {
                        try
                        {
                            m_IoHandler.OnError(this, Session.ERROR_SEND, ex.Message);
                        }
                        catch { }
                    }
                }
            }

            if (!encodeOK) return;

            IoDataStream msg = null;
            lock (m_OutgoingMessageQueue)
            {
                bool sending = m_OutgoingMessageQueue.Count > 0;
                m_OutgoingMessageQueue.Enqueue(new IoDataStream(message, stream));
                if (!sending) msg = m_OutgoingMessageQueue.Peek();
            }

            if (msg != null) DoSend(msg);

        }

        private void DoSend(IoDataStream message)
        {
            if (m_Socket == null || m_State <= 0 || message == null) return;

            Object msg = message.IoData;
            MemoryStream stream = message.IoStream;

            if (stream.Length > 0)
            {
                stream.Position = 0;

                try
                {
                    if (m_Socket != null)
                    {
                        SessionContext info = new SessionContext(this, msg);
                        m_Socket.BeginSend(stream.ToArray(),
                            0, Convert.ToInt32(stream.Length), 0,
                                new AsyncCallback(SendCallback), info);
                    }
                }
                catch (Exception ex)
                {
                    if (m_IoHandler != null)
                    {
                        try
                        {
                            m_IoHandler.OnError(this, ERROR_SEND, ex.Message);
                        }
                        catch { }
                    }
                }
            }

        }

        private static void SendCallback(IAsyncResult arg)
        {
            SessionContext info = (SessionContext)arg.AsyncState;
            Session session = info.Session;
            Object data = info.Data;
            try
            {
                int bytesSent = 0;

                session.m_LastWriteTime = DateTime.Now;

                // Complete sending the data to the remote device.
                if (session.m_Socket != null) bytesSent = session.m_Socket.EndSend(arg);

                if (bytesSent > 0 && session.m_IoHandler != null)
                {
                    if (session.m_State > 0) session.m_IoHandler.OnSend(session, data);
                }

            }
            catch (Exception e)
            {
                if (session.m_IoHandler != null)
                {
                    try
                    {
                        session.m_IoHandler.OnError(session, ERROR_SEND, e.Message);
                    }
                    catch { }
                }
            }

            // continue to process the rest
            session.ProcessOutgoingData();

        }

        private void ProcessOutgoingData()
        {
            bool noRest = false;
            if (m_State > 0)
            {
                IoDataStream msg = null;

                lock (m_OutgoingMessageQueue)
                {
                    // before send out a new message, we must remove the old one first
                    m_OutgoingMessageQueue.Dequeue();

                    // try to find some messages which are still waiting
                    if (m_OutgoingMessageQueue.Count > 0) msg = m_OutgoingMessageQueue.Peek();
                }

                if (msg != null) DoSend(msg); // if found someone still waiting then send it
                else // else check whether need to close the session 
                {
                    if (m_IsGoingToClose) // need to check unhandled messages only when closing
                    {
                        if (m_IncomingMessageQueue.Count <= 0) noRest = true;
                    }
                }

            }

            if (noRest && m_IsGoingToClose) Close(true);

        }

        private void ProcessIncomingData()
        {
            bool noRest = false;

            while (m_State > 0)
            {
                Object msg = null;

                if (m_IncomingMessageQueue.Count > 0) msg = m_IncomingMessageQueue.Dequeue();

                if (msg != null)
                {
                    try
                    {
                        if (m_State > 0 && m_IoHandler != null) m_IoHandler.OnReceive(this, msg);
                    }
                    catch { }
                }
                else
                {
                    if (m_IsGoingToClose) // need to check unhandled messages only when closing
                    {
                        if (m_OutgoingMessageQueue.Count <= 0) noRest = true;
                    }
                    break;
                }
            }

            if (noRest && m_IsGoingToClose) Close(true);
        }

        public bool TestIdle(int opType, int idleTime)
        {
            if (m_State <= 0 || m_IsGoingToClose) return false;
            if (opType < 0 || opType > 2 || idleTime <= 0) return false;

            DateTime currentTime = DateTime.Now;

            double readIdleTime = (currentTime - m_LastReadTime).TotalSeconds;
            double writeIdleTime = (currentTime - m_LastWriteTime).TotalSeconds;

            bool isIdle = false;

            if(opType == IO_RECV_SEND)
            {
                isIdle = readIdleTime > idleTime && writeIdleTime > idleTime;
            }
            else if(opType == IO_RECV)
            {
                isIdle = readIdleTime > idleTime;
            }
            else if(opType == IO_SEND)
            {
                isIdle = writeIdleTime > idleTime;
            }

            if(isIdle)
            {
                if(m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnIdle(this, opType);
                    }
                    catch { }
                }
                return true;
            }

            return false;
        }

    }


    public class SessionContext
    {
        public Session Session { get; set; }
        public Object Data { get; set; }

        public SessionContext()
        {
            Session = null;
            Data = null;
        }

        public SessionContext(Session session, Object data)
        {
            Session = session;
            Data = data;
        }
    }

    public class IoDataStream
    {
        public Object IoData { get; set; }
        public MemoryStream IoStream { get; set; }

        public IoDataStream()
        {
            IoData = null;
            IoStream = null;
        }

        public IoDataStream(Object data, MemoryStream stream)
        {
            IoData = data;
            IoStream = stream;
        }
    }
}
