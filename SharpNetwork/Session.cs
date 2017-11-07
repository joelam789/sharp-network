using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
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

        public const int ACT_KEEP_DEFAULT = 0;
        public const int ACT_KEEP_OLD     = 1;
        public const int ACT_KEEP_NEW     = 2;

        public Object UserData { get; set; }

        private Stream m_Stream = null;

        private INetworkFilter m_IoFilter = null;
        private INetworkEventHandler m_IoHandler = null;

        private Int32 m_Id = 0;

        private String m_RemoteIp = "";
        private Int32 m_RemotePort = 0;

        private Int32 m_State = -1;

        private Boolean m_IsGoingToClose = false;

        private Int32 m_MaxReadQueueSize = 1024;
        private Int32 m_MaxWriteQueueSize = 0;

        private Int32 m_FitReadQueueAction = ACT_KEEP_DEFAULT;
        private Int32 m_FitWriteQueueAction = ACT_KEEP_DEFAULT;

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

        
        public Session(int id, Socket socket, INetworkEventHandler handler, INetworkFilter filter, bool needSsl = false)
        {
            m_Id = id;

            m_Stream = new NetworkStream(socket, true);
            if (needSsl) m_Stream = new SslStream(m_Stream, false);

            m_IoHandler = handler;
            m_IoFilter = filter;

            UserData = null;
        }

        public Session(int id, Socket socket, INetworkEventHandler handler, INetworkFilter filter, 
                        RemoteCertificateValidationCallback validationCallback)
        {
            m_Id = id;

            m_Stream = new SslStream(new NetworkStream(socket, true), false, validationCallback);

            m_IoHandler = handler;
            m_IoFilter = filter;

            UserData = null;
        }

        public int GetId()
        {
            return m_Id;
        }

        public int GetState()
        {
            return m_State;
        }

        public Stream GetStream()
        {
            return m_Stream;
        }

        public Socket GetSocket()
        {
            Socket socket = null;

            if (m_Stream != null)
            {
                if (m_Stream is NetworkStream)
                {
                    socket = m_Stream.GetType()
                                            .GetProperty("Socket", BindingFlags.NonPublic | BindingFlags.Instance)
                                            .GetValue(m_Stream, null) as Socket;
                }
                else if (m_Stream is SslStream)
                {
                    var stream = m_Stream.GetType()
                                            .GetProperty("InnerStream", BindingFlags.NonPublic | BindingFlags.Instance)
                                            .GetValue(m_Stream, null);
                    if (stream != null)
                        socket = stream.GetType()
                                            .GetProperty("Socket", BindingFlags.NonPublic | BindingFlags.Instance)
                                            .GetValue(stream, null) as Socket;
                }
            }

            return socket;
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

        public int GetBufferSize(int iotype)
        {
            Socket socket = GetSocket();

            if (socket != null)
            {
                if (iotype == Session.IO_RECV) return socket.ReceiveBufferSize;
                else if (iotype == Session.IO_SEND) return socket.SendBufferSize;
            }

            return 0;
        }

        public void SetBufferSize(int iotype, int value)
        {
            if (value <= 0) return;

            Socket socket = GetSocket();

            if (socket != null)
            {
                if (iotype == Session.IO_RECV) socket.ReceiveBufferSize = value;
                else if (iotype == Session.IO_SEND) socket.SendBufferSize = value;
                else if (iotype == Session.IO_RECV_SEND)
                {
                    socket.ReceiveBufferSize = value;
                    socket.SendBufferSize = value;
                }
            }
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

        public int GetQueueOverflowAction(int optype)
        {
            if (optype == Session.IO_RECV) return m_FitReadQueueAction;
            else if (optype == Session.IO_SEND) return m_FitWriteQueueAction;

            return 0;
        }

        public void SetQueueOverflowAction(int optype, int value)
        {
            if (optype == 0)
            {
                m_FitReadQueueAction = value;
                m_FitWriteQueueAction = value;
            }
            else if (optype == Session.IO_RECV) m_FitReadQueueAction = value;
            else if (optype == Session.IO_SEND) m_FitWriteQueueAction = value;
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

            Socket socket = GetSocket();

            if (socket != null)
            {
                try
                {
                    m_State = 1;
                    m_RemoteIp = IPAddress.Parse(((IPEndPoint)socket.RemoteEndPoint).Address.ToString()).ToString();
                    m_RemotePort = ((IPEndPoint)socket.RemoteEndPoint).Port;
                }
                catch { }
            }

            try
            {
                // make sure the queue is clean
                if (m_OutgoingMessageQueue.Count > 0)
                {
                    lock (m_OutgoingMessageQueue) m_OutgoingMessageQueue.Clear();
                }
                // make sure the queue is clean
                if (m_IncomingMessageQueue.Count > 0)
                {
                    m_IncomingMessageQueue.Clear(); // incoming data should be processed in single thread
                }
            }
            catch { }

            if (m_IoHandler != null) { try { m_IoHandler.OnConnect(this); } catch { } }

            try
            {
                if (socket != null)
                {
                    if (m_ReadBuffer == null || m_ReadBuffer.Length <= 0) m_ReadBuffer = new Byte[socket.ReceiveBufferSize];
                }

                if (m_Stream != null)
                {
                    m_Stream.BeginRead(m_ReadBuffer, 0, m_ReadBuffer.Length, new AsyncCallback(ReceiveCallback), this);
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

                if (session.m_State > 0)
                {
                    if (session.m_Stream != null)
                    {
                        session.m_ReadSize = session.m_Stream.EndRead(arg);
                    }
                    if (session.m_ReadSize <= 0) needClose = true;
                }

                if (needClose) session.Close();
                else if (session.m_ReadSize > 0 && session.m_State > 0) session.Receive();

            }
            catch (Exception e)
            {
                if (session != null && session.m_IoHandler != null)
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
                    catch (Exception ex)
                    {
                        try
                        {
                            if (m_IoHandler != null)
                                m_IoHandler.OnError(this, Session.ERROR_RECEIVE, "Decode Error: " + ex.Message);
                        }
                        catch { }
                    }
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

                                if (m_FitReadQueueAction == ACT_KEEP_NEW)
                                {
                                    // give up the old one ...
                                    if (m_IncomingMessageQueue.Count > 0) m_IncomingMessageQueue.Dequeue();
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        m_IncomingMessageQueue.Enqueue(obj);
                        queueSize++;
                    }

                    if (full)
                    {
                        if (m_FitReadQueueAction == ACT_KEEP_DEFAULT && m_IoHandler != null)
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
                if (m_State > 0)
                {
                    if (m_Stream != null)
                    {
                        m_Stream.BeginRead(m_ReadBuffer, 0, m_ReadBuffer.Length, new AsyncCallback(ReceiveCallback), this);
                    }
                }

            }
            else
            {
                // next round
                if (m_State > 0)
                {
                    if (m_Stream != null)
                    {
                        m_Stream.BeginRead(m_ReadBuffer, 0, m_ReadBuffer.Length, new AsyncCallback(ReceiveCallback), this);
                    }
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

            if (m_Stream != null)
            {
                m_State = 0;
                try { m_Stream.Close(); } catch { }
                try { m_Stream.Dispose(); } catch { }
                m_State = -1;
                m_Stream = null;
            }
            else
            {
                m_State = -1;
                m_Stream = null;
            }

            try
            {
                // clean up the queue
                if (m_OutgoingMessageQueue.Count > 0)
                {
                    lock (m_OutgoingMessageQueue) m_OutgoingMessageQueue.Clear();
                }

                // clean up the queue
                if (m_IncomingMessageQueue.Count > 0)
                {
                    m_IncomingMessageQueue.Clear(); // incoming data should be processed in single thread
                }

                m_Attributes.Clear();
            }
            catch { }

            m_IsGoingToClose = false;

            if (m_SessionGroup != null)
            {
                // finally, remove it from group ...
                m_SessionGroup.RemoveSession(m_Id);
            }
        }

        public void Send(Object message)
        {
            if (m_Stream == null || m_State <= 0 || m_IsGoingToClose || message == null) return;
            if (m_MaxWriteQueueSize > 0 && m_OutgoingMessageQueue.Count >= m_MaxWriteQueueSize)
            {
                if (m_FitWriteQueueAction == ACT_KEEP_DEFAULT && m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnError(this, Session.ERROR_SEND, "Outgoing queue is full");
                    }
                    catch { }
                }
                if (m_FitWriteQueueAction == ACT_KEEP_DEFAULT || m_FitWriteQueueAction == ACT_KEEP_OLD) return;
            }

            MemoryStream stream = new MemoryStream();

            stream.SetLength(0);
            stream.Position = 0;

            bool encodeOK = false;

            try
            {
                m_IoFilter.Encode(this, message, stream);
                encodeOK = true;

                IoDataStream msg = null;
                lock (m_OutgoingMessageQueue)
                {
                    bool sending = m_OutgoingMessageQueue.Count > 0;
                    if (m_MaxWriteQueueSize > 0
                        && m_OutgoingMessageQueue.Count >= m_MaxWriteQueueSize
                        && m_FitWriteQueueAction == ACT_KEEP_NEW)
                    {
                        m_OutgoingMessageQueue.Dequeue(); // just remove one old packet, even it's being sent
                    }
                    m_OutgoingMessageQueue.Enqueue(new IoDataStream(message, stream));
                    if (!sending) msg = m_OutgoingMessageQueue.Peek();
                }
                if (msg != null) DoSend(msg);
            }
            catch (Exception ex)
            {
                if (m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnError(this, Session.ERROR_SEND, (encodeOK ? "" : "Encode Error: ") + ex.Message);
                    }
                    catch { }
                }
            }

        }

        private void DoSend(IoDataStream message)
        {
            if (m_Stream == null || m_State <= 0 || message == null) return;

            Object msg = message.IoData;
            MemoryStream stream = message.IoStream;

            if (stream.Length > 0)
            {
                stream.Position = 0;

                if (m_Stream != null)
                {
                    SessionContext info = new SessionContext(this, msg);
                    m_Stream.BeginWrite(stream.ToArray(),
                        0, Convert.ToInt32(stream.Length),
                            new AsyncCallback(SendCallback), info);
                }
            }

        }

        private static void SendCallback(IAsyncResult arg)
        {
            Session session = null;
            Object data = null;
            try
            {
                SessionContext info = (SessionContext)arg.AsyncState;
                session = info.Session;
                data = info.Data;

                session.m_LastWriteTime = DateTime.Now;

                // Complete sending the data to the remote device.
                if (session.m_Stream != null) session.m_Stream.EndWrite(arg);
                if (session.m_IoHandler != null && session.m_State > 0) session.m_IoHandler.OnSend(session, data);

                // continue to process the rest
                session.ProcessOutgoingData();

            }
            catch (Exception e)
            {
                if (session != null && session.m_IoHandler != null)
                {
                    try
                    {
                        session.m_IoHandler.OnError(session, ERROR_SEND, e.Message);
                    }
                    catch { }
                }
            }
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
                    if (m_OutgoingMessageQueue.Count > 0) m_OutgoingMessageQueue.Dequeue();

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
