using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpNetwork.SimpleProtocol
{
    public class HandlerManager
    {
        protected Dictionary<Int32, IHandler> m_HandlerMap = new Dictionary<Int32, IHandler>();

        public int GetHandlerCount()
        {
            return m_HandlerMap.Count;
        }

        public IHandler GetHandler(Int32 code)
        {
            if (m_HandlerMap.ContainsKey(code)) return m_HandlerMap[code];
            else return null;
        }

        public void AddHandler(Int32 code, IHandler handler)
        {
            if (!m_HandlerMap.ContainsKey(code))
            {
                m_HandlerMap.Add(code, handler);
            }
        }

        public void AddHandlers(HandlerManager mgr)
        {
            if (mgr == null || mgr.m_HandlerMap.Count <= 0) return;
            foreach (KeyValuePair<Int32, IHandler> item in mgr.m_HandlerMap)
            {
                Int32 code = item.Key;
                IHandler handler = item.Value;
                if (!m_HandlerMap.ContainsKey(code))
                {
                    m_HandlerMap.Add(code, handler);
                }
            }
        }

        public void RemoveHandler(Int32 code)
        {
            if (m_HandlerMap.ContainsKey(code))
            {
                m_HandlerMap.Remove(code);
            }
        }

        public void Clear()
        {
            m_HandlerMap.Clear();
        }

    }
}
