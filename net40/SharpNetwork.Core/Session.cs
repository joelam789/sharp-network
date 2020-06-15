using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;

namespace SharpNetwork.Core
{
    public class Session
    {
        public const int ERROR_LISTEN  = 0;
        public const int ERROR_CONNECT = 1;
        public const int ERROR_RECEIVE = 2;
        public const int ERROR_SEND    = 4;
        public const int ERROR_CODEC   = 8;
        public const int ERROR_PROCESS = 16;

        public const int IO_NONE    = 0;
        public const int IO_RECEIVE = 1;
        public const int IO_SEND    = 2;
        public const int IO_ANY     = 3;
        public const int IO_BOTH    = 4;

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

        public Int32 MaxReadQueueSize { get; set; }
        public Int32 MaxWriteQueueSize { get; set; }

        public Int32 FitReadQueueAction { get; set; }
        public Int32 FitWriteQueueAction { get; set; }

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

        public static bool IsNetworkError(int errortype)
        {
            return (errortype & Session.ERROR_PROCESS) == 0 // non in-process error
                && ((errortype & Session.ERROR_RECEIVE) != 0 || (errortype & Session.ERROR_SEND) != 0); // but io error
        }

        public static bool IsCodecError(int errortype)
        {
            return (errortype & Session.ERROR_CODEC) != 0;
        }

        public static bool IsProcessError(int errortype)
        {
            return (errortype & Session.ERROR_PROCESS) != 0;
        }

        public Session(int id, Socket socket, INetworkEventHandler handler, INetworkFilter filter, bool needSsl = false)
        {
            m_Id = id;

            m_Stream = new NetworkStream(socket, true);
            if (needSsl) m_Stream = new SslStream(m_Stream, false);

            m_IoHandler = handler;
            m_IoFilter = filter;

            MaxReadQueueSize = 1024;
            MaxWriteQueueSize = 0;

            FitReadQueueAction = ACT_KEEP_DEFAULT;
            FitWriteQueueAction = ACT_KEEP_DEFAULT;

            UserData = null;
        }

        public Session(int id, Socket socket, INetworkEventHandler handler, INetworkFilter filter,
                        RemoteCertificateValidationCallback validationCallback)
        {
            m_Id = id;

            m_Stream = new SslStream(new NetworkStream(socket, true), false, validationCallback);

            m_IoHandler = handler;
            m_IoFilter = filter;

            MaxReadQueueSize = 1024;
            MaxWriteQueueSize = 0;

            FitReadQueueAction = ACT_KEEP_DEFAULT;
            FitWriteQueueAction = ACT_KEEP_DEFAULT;

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

            try
            {
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
            }
            catch { }

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

        public int GetReceiveBufferSize()
        {
            Socket socket = GetSocket();
            if (socket != null) return socket.ReceiveBufferSize;
            return 0;
        }

        public int GetSendBufferSize()
        {
            Socket socket = GetSocket();
            if (socket != null) return socket.SendBufferSize;
            return 0;
        }

        public void SetIoBufferSize(int value)
        {
            if (value <= 0) return;
            Socket socket = GetSocket();
            if (socket != null)
            {
                socket.ReceiveBufferSize = value;
                socket.SendBufferSize = value;
            }
        }

        public void SetReceiveBufferSize(int value)
        {
            if (value <= 0) return;
            Socket socket = GetSocket();
            if (socket != null) socket.ReceiveBufferSize = value;
        }

        public void SetSendBufferSize(int value)
        {
            if (value <= 0) return;
            Socket socket = GetSocket();
            if (socket != null) socket.SendBufferSize = value;
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

            if (m_IoHandler != null)
            {
                try
                {
                    m_IoHandler.OnConnect(this);
                }
                catch { }
            }

            if (m_Stream == null) socket = null;

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
                        m_IoHandler.OnError(this, ERROR_RECEIVE, ex);
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
            catch (Exception ex)
            {
                if (session != null && session.m_IoHandler != null)
                {
                    try
                    {
                        session.m_IoHandler.OnError(session, ERROR_RECEIVE, ex);
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
                                m_IoHandler.OnError(this, Session.ERROR_CODEC | Session.ERROR_RECEIVE, ex);
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
                        if (MaxReadQueueSize > 0)
                        {
                            if (queueSize >= MaxReadQueueSize)
                            {
                                full = true;

                                if (FitReadQueueAction == ACT_KEEP_NEW)
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
                        if (FitReadQueueAction == ACT_KEEP_DEFAULT && m_IoHandler != null)
                        {
                            try
                            {
                                m_IoHandler.OnError(this, Session.ERROR_RECEIVE, new Exception("Incoming queue is full"));
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
                if (m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnDisconnect(this);
                    }
                    catch { }
                }
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
            if (MaxWriteQueueSize > 0 && m_OutgoingMessageQueue.Count >= MaxWriteQueueSize)
            {
                if (FitWriteQueueAction == ACT_KEEP_DEFAULT && m_IoHandler != null)
                {
                    try
                    {
                        m_IoHandler.OnError(this, Session.ERROR_SEND, new Exception("Outgoing queue is full"));
                    }
                    catch { }
                }
                if (FitWriteQueueAction == ACT_KEEP_DEFAULT || FitWriteQueueAction == ACT_KEEP_OLD) return;
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
                    if (MaxWriteQueueSize > 0
                        && m_OutgoingMessageQueue.Count >= MaxWriteQueueSize
                        && FitWriteQueueAction == ACT_KEEP_NEW)
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
                        m_IoHandler.OnError(this, encodeOK ? Session.ERROR_SEND : (Session.ERROR_CODEC | Session.ERROR_SEND), ex);
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
            catch (Exception ex)
            {
                if (session != null && session.m_IoHandler != null)
                {
                    try
                    {
                        session.m_IoHandler.OnError(session, ERROR_SEND | ERROR_PROCESS, ex);
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
                    catch (Exception ex)
                    {
                        if (m_IoHandler != null)
                        {
                            try
                            {
                                m_IoHandler.OnError(this, ERROR_RECEIVE | ERROR_PROCESS, ex);
                            }
                            catch { }
                        }
                    }
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

        public int TestIdle(int opType, int idleTime)
        {
            if (m_State <= 0 || m_IsGoingToClose) return 0;
            if (opType < 0 || idleTime <= 0) return 0;

            DateTime currentTime = DateTime.Now;

            double readIdleTime = (currentTime - m_LastReadTime).TotalSeconds;
            double writeIdleTime = (currentTime - m_LastWriteTime).TotalSeconds;

            bool isIdle = false;
            int idleFlag = 0;

            if (opType == IO_ANY)
            {
                if (readIdleTime > idleTime) idleFlag |= IO_RECEIVE;
                if (writeIdleTime > idleTime) idleFlag |= IO_SEND;
                isIdle = idleFlag > 0;
            }
            else if (opType == IO_BOTH)
            {
                isIdle = readIdleTime > idleTime && writeIdleTime > idleTime;
                if (isIdle) idleFlag = IO_BOTH;
            }
            else if (opType == IO_RECEIVE)
            {
                isIdle = readIdleTime > idleTime;
                if (isIdle) idleFlag = IO_RECEIVE;
            }
            else if (opType == IO_SEND)
            {
                isIdle = writeIdleTime > idleTime;
                if (isIdle) idleFlag = IO_SEND;
            }

            if (isIdle)
            {
                if (m_IoHandler != null)
                {
                    try
                    {
                        // isIdle is true, so idleFlag > 0 (IO_RECEIVE/IO_SEND/IO_ANY/IO_BOTH)
                        m_IoHandler.OnIdle(this, idleFlag);
                    }
                    catch { }
                }
                return idleFlag;
            }

            return 0;
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
