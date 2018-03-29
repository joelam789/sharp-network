using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpNetwork.SimpleHttp
{
    public class HttpRouter
    {
        protected Dictionary<string, IHandler> m_HandlerMap = new Dictionary<string, IHandler>();

        public int GetHandlerCount()
        {
            return m_HandlerMap.Count;
        }

        public IHandler GetHandler(string url)
        {
            IHandler handler = null;
            if (m_HandlerMap.TryGetValue(url, out handler)) return handler;
            else return null;
        }

        public void AddHandler(string url, IHandler handler)
        {
            if (m_HandlerMap.ContainsKey(url)) m_HandlerMap.Remove(url);
            m_HandlerMap.Add(url, handler);
        }

        public void RemoveHandler(string url)
        {
            m_HandlerMap.Remove(url);
        }

        public void Clear()
        {
            m_HandlerMap.Clear();
        }
    }
}
