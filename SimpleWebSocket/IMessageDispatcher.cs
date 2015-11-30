using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SharpNetwork.SimpleWebSocket
{
    public interface IMessageDispatcher
    {
        bool CanProcessBinary();
        bool Dispatch(Session session, object msg);
        void AddHandler(object handler);
        void LoadHandlers(string nameSpace = "", Assembly assembly = null);
    }
}
