using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharpNetwork.Core;

namespace SharpNetwork.SimpleHttp
{
    public interface IHandler
    {
        void Handle(Object data);
    }

    public class MessageHandler<T> : IHandler where T : class
    {
        public virtual void Handle(object data)
        {
            if (data is SessionContext)
            {
                SessionContext ctx = data as SessionContext;
                HttpMessage msg = (HttpMessage)ctx.Data;
                Session session = ctx.Session;
                if (msg.ContentSize > 0)
                {
                    T obj = msg.ToJsonObject<T>();
                    ProcessMessage(session, obj);
                }
                else
                {
                    ProcessMessage(session, default(T));
                }
            }
        }

        public void Send<TMessage>(Session session, TMessage obj, Dictionary<string, string> headers = null) where TMessage : class
        {
            var msg = new HttpMessage(HttpMessage.ToJsonString<TMessage>(obj));
            if (headers != null) msg.SetHeaders(headers);
            session.Send(msg);
        }

        public void Send(Session session, string str, Dictionary<string, string> headers = null)
        {
            var msg = new HttpMessage(str);
            if (headers != null) msg.SetHeaders(headers);
            session.Send(msg);
        }

        public virtual void ProcessMessage(Session session, T msg)
        {
            // to be overridden ...
            return;
        }

    }
}
