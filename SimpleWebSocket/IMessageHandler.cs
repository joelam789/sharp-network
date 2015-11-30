using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpNetwork.SimpleWebSocket
{
    public interface IMessageHandler
    {
        void Handle(object data);
        Type GetMessageType();
    }

    public interface IGenericMessageHandler
    {
        void Handle(Session session, string msg);
        List<IMessageChecker> GetMessageCheckers(string groupName);
        void CreateMessageCheckers();
    }

}
