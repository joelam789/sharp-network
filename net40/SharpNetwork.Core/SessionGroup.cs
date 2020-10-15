using System;
using System.Collections.Generic;
using System.Threading;

namespace SharpNetwork.Core
{
    public class SessionGroup
    {
        private Dictionary<Int32, Session> m_Sessions = new Dictionary<Int32, Session>();
        private Dictionary<Int32, Object> m_Attributes = new Dictionary<Int32, Object>();

        private Int32 m_IdleType = -1;
        private Int32 m_IdleTime = 0;
        private Boolean m_NeedCheckIdle = false;

        protected Thread m_CheckIdleThread = null;

        public int GetSessionCount()
        {
            return m_Sessions.Count;
        }

        public Dictionary<Int32, Session> GetSessions()
        {
            Dictionary<Int32, Session> sessionMap = null;
            lock (m_Sessions)
            {
                sessionMap = new Dictionary<Int32, Session>(m_Sessions);
            }
            return sessionMap;
        }
        public Session GetSessionById(int sessionId)
        {
            lock (m_Sessions)
            {
                if (!m_Sessions.ContainsKey(sessionId)) return null;
                else return m_Sessions[sessionId];
            }
        }
        public void AddSession(int sessionId, Session session)
        {
            lock (m_Sessions)
            {
                if (!m_Sessions.ContainsKey(sessionId)) m_Sessions.Add(sessionId, session);
                else m_Sessions[sessionId] = session;
            }
        }
        public void RemoveSession(int sessionId)
        {
            lock (m_Sessions)
            {
                m_Sessions.Remove(sessionId);
            }
        }

        public void Clear()
        {
            lock (m_Sessions)
            {
                m_Sessions.Clear();
            }
        }

        public Dictionary<Int32, Object> GetAttributes()
        {
            return m_Attributes;
        }

        private void CheckIdleSessions()
        {
            while (m_IdleType >= 0 && m_IdleTime > 0 && m_NeedCheckIdle)
            {
                Dictionary<Int32, Session> sessions = GetSessions();
                if (sessions != null && sessions.Count > 0)
                {
                    foreach (KeyValuePair<Int32, Session> item in sessions)
                    {
                        try
                        {
                            Session session = item.Value;
                            session.TestIdle(m_IdleType, m_IdleTime);
                        }
                        catch { }
                    }
                }

                if (!m_NeedCheckIdle) break;

                try
                {
                    if (m_IdleTime > 0) Thread.Sleep(m_IdleTime * 1000);
                }
                catch
                {
                    break; // should be terminated by Interrupt or Abort ...
                }

                if (!m_NeedCheckIdle) break;
            }

        }

        public Int32 GetIdleType()
        {
            return m_IdleType;
        }

        public Int32 GetIdleTime()
        {
            return m_IdleTime;
        }

        public void SetIdleTime(Int32 opType, Int32 idleTime)
        {
            if (idleTime <= 0)
            {
                m_IdleType = -1;
                m_IdleTime = 0;

                m_NeedCheckIdle = false;

                if (m_CheckIdleThread != null)
                {
                    m_CheckIdleThread.Interrupt();
                    m_CheckIdleThread.Join();
                    m_CheckIdleThread = null;
                }
            }
            else if (opType >= 0)
            {
                m_NeedCheckIdle = false;

                if (m_IdleTime <= 0 || m_IdleType < 0)
                {
                    if (m_CheckIdleThread != null)
                    {
                        m_CheckIdleThread.Interrupt();
                        m_CheckIdleThread.Join();
                        m_CheckIdleThread = null;

                        m_NeedCheckIdle = true;
                    }
                }

                m_IdleType = opType;
                m_IdleTime = idleTime;

                if (m_NeedCheckIdle)
                {
                    if (m_CheckIdleThread == null)
                    {
                        m_CheckIdleThread = new Thread(new ThreadStart(CheckIdleSessions));
                        m_CheckIdleThread.Start();
                    }
                }
            }
        }

        public void StartCheckingIdle()
        {
            m_NeedCheckIdle = true;

            if (m_IdleType >= 0 && m_IdleTime > 0)
            {
                if (m_CheckIdleThread == null)
                {
                    m_CheckIdleThread = new Thread(new ThreadStart(CheckIdleSessions));
                    m_CheckIdleThread.Start();
                }
            }
            else
            {
                m_NeedCheckIdle = false;
            }
        }

        public void StopCheckingIdle()
        {
            m_NeedCheckIdle = false;

            if (m_CheckIdleThread != null)
            {
                m_CheckIdleThread.Interrupt();
                m_CheckIdleThread.Join();
                m_CheckIdleThread = null;
            }
        }
    }
}
