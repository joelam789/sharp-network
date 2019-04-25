using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using SharpNetwork.Core;

namespace SharpNetwork.SimpleProtocol
{
    public class MessageCodec : INetworkFilter
    {
        private int m_MaxMsgSize = 1024 * 128; // 128K

        public MessageCodec(int maxMsgSize = 0)
        {
            if (maxMsgSize > 0) m_MaxMsgSize = maxMsgSize;
        }

        public static void EncodeMessage(Session session, NetMessage msg)
        {
            // do some complex encode actions here ...

            Encoding encode = Encoding.UTF8;
            Byte[] bytes = null;

            if (!msg.IsString()) 
                bytes = msg.RawContent;
            else if (msg.MessageContent.Length > 0) 
                bytes = encode.GetBytes(msg.MessageContent);

            if (bytes == null) bytes = new byte[0];

            msg.ContentSize = bytes.Length; // refresh size
            msg.RawContent = bytes; // refresh content
        }

        public static void DecodeMessage(Session session, NetMessage msg)
        {
            // do some complex decode actions here ...

            Byte[] bytes = msg.RawContent;
            if (bytes == null || bytes.Length <= 0) return; // nothing needs to decode

            if (msg.IsString())
            {
                Encoding encode = Encoding.UTF8;
                if (msg.MessageContent.Length == 0) msg.MessageContent = encode.GetString(bytes, 0, bytes.Length);
                else msg.MessageContent += encode.GetString(bytes, 0, bytes.Length);
            }
            else
            {
                msg.RawContent = bytes;
            }
        }

        public void Encode(Session session, Object message, MemoryStream stream)
        {
            if (message is NetMessage)
            {
                NetMessage msg = (NetMessage)message;

                msg.IoFlag = Session.IO_SEND; // encode msg for sending ... 

                EncodeMessage(session, msg);

                Byte[] bytes = msg.RawContent;
                if (bytes == null) bytes = new byte[0];

                BinaryWriter writer = new BinaryWriter(stream);

                writer.Write(NetMessage.HEADER_SIGN);
                writer.Write(msg.MessageType);
                writer.Write(msg.MessageFlag);
                writer.Write(bytes.Length);

                stream.Write(bytes, 0, bytes.Length);
            }
            else if (message is String) // also support raw string ...
            {
                String msg = (String)message;
                Encoding encode = Encoding.UTF8;
                Byte[] bytes = encode.GetBytes(msg);
                stream.Write(bytes, 0, bytes.Length);
                stream.WriteByte(0); // let it end with NULL ('\0')
            }

        }

        // please know that function Decode() should be called in single thread
        public bool Decode(Session session, MemoryStream stream, List<Object> output)
        {
            NetMessage netMsg = null;

            bool hasKey = false;
            Stack<Object> stack = NetMessage.GetSessionBuffer(session);
            if (stack.Count > 0)
            {
                hasKey = true;
                netMsg = (NetMessage)stack.Peek();
            }
            if (netMsg == null)
            {
                netMsg = new NetMessage();
                if (hasKey) stack.Pop();
                stack.Push(netMsg);
            }

            int total = 0;

            if (netMsg.ReceivingState == NetMessage.STATE_WAIT_FOR_BODY)
            {
                if (stream.Length - stream.Position >= netMsg.ContentSize)
                {
                    Byte[] bytes = new Byte[netMsg.ContentSize];
                    stream.Read(bytes, 0, netMsg.ContentSize);
 
                    netMsg.RawContent = bytes;
                    netMsg.ReceivingState = NetMessage.STATE_READY;

                    netMsg.IoFlag = Session.IO_RECEIVE; // receiving ...
                    output.Add(netMsg);
                    total++;

                    netMsg = new NetMessage();
                    if (stack.Count > 0) stack.Pop();
                    stack.Push(netMsg);
                }

            }

            while (netMsg.ReceivingState == NetMessage.STATE_WAIT_FOR_HEADER
                    && stream.Length - stream.Position >= NetMessage.HEADER_SIZE)
            {
                if (netMsg.ReceivingState == NetMessage.STATE_WAIT_FOR_HEADER)
                {
                    if (stream.Length - stream.Position >= NetMessage.HEADER_SIZE)
                    {
                        BinaryReader reader = new BinaryReader(stream);
                        int netMsgSign = reader.ReadInt32();
                        if (netMsgSign != NetMessage.HEADER_SIGN)
                        {
                            netMsg.ContentSize = -1;
                        }
                        else
                        {
                            netMsg.MessageType = reader.ReadInt32();
                            netMsg.MessageFlag = reader.ReadInt32();
                            netMsg.ContentSize = reader.ReadInt32();

                            if (netMsg.ContentSize > m_MaxMsgSize) netMsg.ContentSize = 0;
                        }

                        if (netMsg.ContentSize > 0)
                        {
                            netMsg.ReceivingState = NetMessage.STATE_WAIT_FOR_BODY;
                        }
                        else
                        {
                            netMsg.ReceivingState = NetMessage.STATE_READY;
                            netMsg.IoFlag = Session.IO_RECEIVE; // receiving ...
                            output.Add(netMsg);
                            total++;
                            netMsg = new NetMessage();
                            if (stack.Count > 0) stack.Pop();
                            stack.Push(netMsg);
                            continue;
                        }

                    }

                }

                if (netMsg.ReceivingState == NetMessage.STATE_WAIT_FOR_BODY)
                {
                    if (stream.Length - stream.Position >= netMsg.ContentSize)
                    {
                        Byte[] bytes = new Byte[netMsg.ContentSize];
                        stream.Read(bytes, 0, netMsg.ContentSize);

                        netMsg.RawContent = bytes;
                        netMsg.ReceivingState = NetMessage.STATE_READY;

                        netMsg.IoFlag = Session.IO_RECEIVE; // receiving ...
                        output.Add(netMsg);
                        total++;
                        netMsg = new NetMessage();
                        if (stack.Count > 0) stack.Pop();
                        stack.Push(netMsg);
                        continue;
                    }

                }

            }

            if (total > 0 && stream.Length - stream.Position <= 0) return true;

            if (netMsg.ReceivingState != NetMessage.STATE_WAIT_FOR_HEADER
                    && netMsg.ReceivingState != NetMessage.STATE_WAIT_FOR_BODY
                    && netMsg.ReceivingState != NetMessage.STATE_READY)
            {
                session.Close();
            }

            return false;
        }
    }
}

