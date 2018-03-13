using System;
using System.Text;

using SharpNetwork.Core;

namespace SharpNetwork.SimpleProtocol
{
    public class ProcessResult
    {
        public const int SUCCESS = 0;
        public const int FAIL = -1;
    }

    public class MessageHandler<T> : IHandler
    {
        private int m_MessageType = 0;

        public MessageHandler(int msgType)
        {
            m_MessageType = msgType;
        }

        public void Send(Session session, T obj, int flag = 0)
        {
            NetMessage.Send<T>(session, m_MessageType, obj, flag);
        }

        public void Send<U>(Session session, int msgType, U obj, int flag = 0)
        {
            NetMessage.Send<U>(session, msgType, obj, flag);
        }

        public static U ToJsonObject<U>(String str)
        {
            return NetMessage.ToJsonObject<U>(str);
        }

        public static String ToJsonString<U>(U obj)
        {
            return NetMessage.ToJsonString<U>(obj);
        }

        public virtual Object Handle(Object data)
        {
            if (data is SessionContext)
            {
                SessionContext ctx = data as SessionContext;
                NetMessage msg = (NetMessage)ctx.Data;
                Session session = ctx.Session;
                if (msg.ContentSize > 0)
                {
                    T obj = msg.ToJsonObject<T>();
                    return Process(session, obj);
                }
                else
                {
                    return Process(session, default(T));
                }
            }
            else if (data is String)
            {
                T obj = default(T);
                if (obj is String)
                {
                    obj = (T)data;
                    return Process(null, obj);
                }
                else if (obj is Byte[])
                {
                    object bytes = Encoding.UTF8.GetBytes((String)data);
                    obj = (T)bytes;
                    return Process(null, obj);
                }
                else
                {
                    obj = ToJsonObject<T>((String)data);
                    return Process(null, obj);
                }
            }
            return ProcessResult.FAIL; // do not know the type of the input data, so return FAIL
        }

        public virtual Object Process(Session session, T obj)
        {
            ProcessMessage(session, obj);
            return ProcessResult.SUCCESS; // return SUCCESS by default
        }

        public virtual void ProcessMessage(Session session, T obj)
        {
            return;
        }
    }

    
}
