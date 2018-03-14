using System.Reflection;

using SharpNetwork.Core;

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
