using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using SharpNetwork.Core;

namespace SharpNetwork.SimpleWebSocket
{
    public class MessageCodec : INetworkFilter
    {
        public const string HTTP_HEADER_SIGN = "HTTP/";
        public const string HTTP_SERVER_HEADER_SIGN = "Switching Protocols";

        public const string WEBSOCK_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public const string WEBSOCK_CLIENT_HEADER_SIGN = "Sec-WebSocket-Key";
        public const string WEBSOCK_SERVER_HEADER_SIGN = "Sec-WebSocket-Accept";

        public const string WEBSOCK_HANDSHAKE_REPLY_MSG = "HTTP/1.1 101 Switching Protocols" + "\r\n"
                                                        + "Upgrade: WebSocket" + "\r\n"
                                                        + "Connection: Upgrade" + "\r\n"
                                                        + "Sec-WebSocket-Accept: {0}" + "\r\n"
                                                        + "\r\n"
                                                        ;

        public static String ComputeWebSocketHandshakeSecurityHash09(String secWebSocketKey)
        {
            String secWebSocketAccept = String.Empty;
            // 1. Combine the request Sec-WebSocket-Key with magic key.
            String ret = secWebSocketKey + WEBSOCK_GUID;
            // 2. Compute the SHA1 hash
            SHA1 sha = new SHA1CryptoServiceProvider();
            byte[] sha1Hash = sha.ComputeHash(Encoding.UTF8.GetBytes(ret));
            // 3. Base64 encode the hash
            secWebSocketAccept = Convert.ToBase64String(sha1Hash);
            return secWebSocketAccept;
        }

        private int m_MaxMsgSize = 1024 * 256; // 256K

        public MessageCodec(int maxMsgSize = 0)
        {
            if (maxMsgSize > 0) m_MaxMsgSize = maxMsgSize;
        }

        public static void EncodeMessage(Session session, WebMessage msg)
        {
            // do some complex encode actions here ...

            Encoding encode = Encoding.UTF8;
            Byte[] bytes = null;

            if (msg.IsBinary()) bytes = msg.RawContent;
            else if (msg.MessageContent.Length > 0)
            {
                bytes = encode.GetBytes(msg.MessageContent);
                msg.ContentSize = bytes.Length;
            }

            if (bytes == null) bytes = new byte[0];

            if (msg.ContentSize <= 0) msg.ContentSize = bytes.Length; // refresh size
            if (msg.RawContent == null) msg.RawContent = bytes; // refresh content

            if (msg.RawContent != null && msg.ContentSize > msg.RawContent.Length)
                msg.ContentSize = msg.RawContent.Length;
        }

        public static void DecodeMessage(Session session, WebMessage msg)
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
            if (message is WebMessage)
            {
                WebMessage msg = (WebMessage)message;

                EncodeMessage(session, msg);

                Byte[] bytes = msg.RawContent;
                if (bytes == null) bytes = new byte[0];

                bool fin = true;
                bool rsv1 = false;
                bool rsv2 = false;
                bool rsv3 = false;
                byte opcode = 8; // if undefined then close it
                bool maskcode = msg.MaskFlag > 0;

                if (msg.IsString()) opcode = 0x01;
                else if (msg.IsBinary()) opcode = 0x02;
                else if (msg.IsPingFrame()) opcode = 0x09;
                else if (msg.IsPongFrame()) opcode = 0x0A;
                else if (msg.IsCloseFrame()) opcode = 0x08;

                byte payloadlength = 0;

                byte[] mask = maskcode ? new byte[4] {0, 0, 0, 0} : new byte[0];
                byte[] extend = new byte[0];

                int length = msg.ContentSize;

                if (length < 126)
                {
                    extend = new byte[0];
                    payloadlength = (byte)(length & 0xff);
                }
                else if (length < 65536)
                {
                    extend = new byte[2];
                    payloadlength = 126;
                    extend[0] = (byte)(length / 256);
                    extend[1] = (byte)(length % 256);
                }
                else
                {
                    extend = new byte[8];
                    payloadlength = 127;

                    int left = length;
                    int unit = 256;

                    for (int i = 7; i > 1; i--)
                    {
                        extend[i] = (byte)(left % unit);
                        left = left / unit;

                        if (left == 0)
                            break;
                    }
                }

                byte[] headerbuffer = new byte[2] { 0, 0 };

                if (fin) headerbuffer[0] |= 0x80;
                if (rsv1) headerbuffer[0] |= 0x40;
                if (rsv2) headerbuffer[0] |= 0x20;
                if (rsv3) headerbuffer[0] |= 0x10;

                headerbuffer[0] |= opcode;

                if (maskcode) headerbuffer[1] |= 0x80;

                headerbuffer[1] |= payloadlength;

                stream.Write(headerbuffer, 0, headerbuffer.Length);
                if (extend.Length > 0) stream.Write(extend, 0, extend.Length);
                if (mask.Length > 0) stream.Write(mask, 0, mask.Length);

                if (mask.Length > 0)
                {
                    int masklen = mask.Length;
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        bytes[i] = (byte)(bytes[i] ^ mask[i % masklen]);
                    }
                }

                stream.Write(bytes, 0, length);

            }
            else if (message is byte[])
            {
                Encode(session, new WebMessage(message as byte[]), stream);
            }
            else if (message is string)
            {
                String msg = (String)message;
                Encoding encode = Encoding.UTF8;
                Byte[] bytes = encode.GetBytes(msg);
                stream.Write(bytes, 0, bytes.Length);
            }

        }

        // please know that function Decode() should be called in single thread
        public bool Decode(Session session, MemoryStream stream, List<Object> output)
        {
            bool isNew = false;
            WebMessage netMsg = null;

            bool hasKey = false;
            Stack<Object> stack = WebMessage.GetSessionBuffer(session);
            if (stack.Count > 0)
            {
                hasKey = true;
                netMsg = (WebMessage)stack.Peek();
            }
            if (netMsg == null)
            {
                isNew = true;
                netMsg = new WebMessage();
                if (hasKey) stack.Pop();
                stack.Push(netMsg);
            }

            if (isNew)
            {
                if (netMsg != null)
                {
                    netMsg.ReceivingState = WebMessage.STATE_WAIT_FOR_BODY;
                    netMsg.MessageType = WebMessage.MSG_TYPE_HANDSHAKE;
                }
            }

            int total = 0;

            if (netMsg.ReceivingState == WebMessage.STATE_WAIT_FOR_BODY)
            {
                if (netMsg.MessageType == WebMessage.MSG_TYPE_HANDSHAKE)
                {
                    long orgpos = stream.Position;
                    long msglen = stream.Length - stream.Position;

                    Byte[] bytes = new Byte[msglen];
                    stream.Read(bytes, 0, bytes.Length);

                    bool found = false;

                    int curpos = 0;
                    int maxpos = bytes.Length - 1;
                    int checkedlen = 0;
                    while (curpos <= maxpos && !found)
                    {
                        if (bytes[curpos] == '\r')
                        {
                            if (curpos + 1 <= maxpos && bytes[curpos + 1] == '\n')
                            {
                                if (curpos + 2 <= maxpos && bytes[curpos + 2] == '\r')
                                {
                                    if (curpos + 3 <= maxpos && bytes[curpos + 3] == '\n')
                                    {
                                        found = true;
                                        checkedlen = curpos + 3 + 1;
                                    }
                                }
                            }
                        }

                        curpos++;
                    }

                    if (found)
                    {
                        Encoding encode = Encoding.UTF8;
                        string headerContent = encode.GetString(bytes, 0, checkedlen);

                        string[] rawClientHandshakeLines = headerContent.Split(new string[] { "\r\n" },
                                                                System.StringSplitOptions.RemoveEmptyEntries);
                        string acceptKey = "";
                        bool foundKey = false;
                        bool foundUrl = false;
                        bool foundServerSign = false;

                        string handshakeMsg = "";

                        foreach (string oneline in rawClientHandshakeLines)
                        {
                            string line = String.Copy(oneline);

                            if (!foundUrl && oneline.Contains(HTTP_HEADER_SIGN))
                            {
                                if (oneline.Contains(HTTP_SERVER_HEADER_SIGN))
                                {
                                    foundServerSign = true;
                                }
                                else
                                {
                                    string reqline = oneline.Trim();
                                    int beginPos = reqline.IndexOf(' ') + 1;
                                    int endPos = reqline.LastIndexOf(' ');
                                    if (beginPos > 0 && endPos > beginPos)
                                    {
                                        WebMessage.SetSessionData(session, "Path", reqline.Substring(beginPos, endPos - beginPos).Trim());
                                    }
                                }
                                foundUrl = true;
                            }

                            if (!foundKey && !foundServerSign && oneline.Contains(WEBSOCK_CLIENT_HEADER_SIGN + ":"))
                            {
                                acceptKey = ComputeWebSocketHandshakeSecurityHash09(oneline.Substring(oneline.IndexOf(":") + 2));
                                foundKey = true;
                            }

                            int separator = line.IndexOf(':');
                            if (separator > 0)
                            {
                                string hname = line.Substring(0, separator);
                                int hpos = separator + 1;
                                while ((hpos < line.Length) && (line[hpos] == ' ')) hpos++;
                                string hvalue = line.Substring(hpos, line.Length - hpos);
                                netMsg.Headers.Add(hname, hvalue);
                            }
                        }

                        //var incomingHeaders = WebMessage.GetIncomingHeaders(session);
                        //incomingHeaders.Clear();
                        //foreach (var headerItem in netMsg.Headers)
                        //    incomingHeaders.Add(headerItem.Key, headerItem.Value);

                        //var incomingHeaders = new Dictionary<string, string>(netMsg.Headers);
                        //WebMessage.SetIncomingHeaders(session, incomingHeaders);

                        if (acceptKey != null && acceptKey.Length > 0) 
                            handshakeMsg = String.Format(WEBSOCK_HANDSHAKE_REPLY_MSG, acceptKey);

                        stream.Position = orgpos + checkedlen;

                        netMsg.ReceivingState = WebMessage.STATE_READY;
                        if (stack.Count > 0) stack.Pop();

                        netMsg.MessageContent = headerContent;
                        netMsg.MessageType = WebMessage.MSG_TYPE_HANDSHAKE;
                        output.Add(netMsg);
                        total++;

                        netMsg = new WebMessage();
                        netMsg.VirtualHeaderSize = 2;
                        stack.Push(netMsg);

                        if (handshakeMsg != null && handshakeMsg.Length > 0)
                            session.Send(handshakeMsg);
                    }
                    else
                    {
                        if (curpos > m_MaxMsgSize) session.Close();
                        else stream.Position = orgpos;
                        return false;
                    }
                }

                if (netMsg.ReceivingState == WebMessage.STATE_WAIT_FOR_BODY
                    && stream.Length - stream.Position >= netMsg.ContentSize)
                {
                    Byte[] bytes = new Byte[netMsg.ContentSize];
                    stream.Read(bytes, 0, netMsg.ContentSize);

                    if (netMsg.MaskFlag > 0 && netMsg.MaskBytes != null)
                    {
                        int masklen = netMsg.MaskBytes.Length;
                        for (int i = 0; i < bytes.Length; i++)
                        {
                            bytes[i] = (byte)(bytes[i] ^ netMsg.MaskBytes[i % masklen]);
                        }
                    }

                    if (netMsg.MessageType == WebMessage.MSG_TYPE_STRING)
                    {
                        netMsg.MessageContent = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
                    }

                    if (netMsg.MessageType == WebMessage.MSG_TYPE_BINARY
                        || netMsg.MessageType == WebMessage.MSG_TYPE_PING
                        || netMsg.MessageType == WebMessage.MSG_TYPE_PONG)
                    {
                        netMsg.RawContent = bytes;
                        netMsg.ContentSize = bytes.Length;
                    }

                    output.Add(netMsg);
                    total++;

                    netMsg.ReceivingState = WebMessage.STATE_READY;
                    if (stack.Count > 0) stack.Pop();

                    netMsg = new WebMessage();
                    netMsg.VirtualHeaderSize = 2;
                    stack.Push(netMsg);
                }

            }

            while (netMsg.ReceivingState == WebMessage.STATE_WAIT_FOR_HEADER
                    && stream.Length - stream.Position >= netMsg.VirtualHeaderSize)
            {
                if (netMsg.ReceivingState == WebMessage.STATE_WAIT_FOR_HEADER)
                {
                    if (stream.Length - stream.Position >= netMsg.VirtualHeaderSize)
                    {
                        Byte[] bytes = new Byte[netMsg.VirtualHeaderSize];
                        stream.Read(bytes, 0, netMsg.VirtualHeaderSize);

                        if (netMsg.VirtualHeaderSize == 2)
                        {
                            // first byte ...
                            sbyte opcode = (sbyte)(bytes[0] & 0x0f);

                            switch (opcode) // not support 0x00 for now ...
                            {
                                case 0x08:
                                    session.Close();
                                    return false;
                                case 0x09:
                                    netMsg.MessageType = WebMessage.MSG_TYPE_PING;
                                    break;
                                case 0x0A:
                                    netMsg.MessageType = WebMessage.MSG_TYPE_PONG;
                                    break;
                                case 0x01:
                                    netMsg.MessageType = WebMessage.MSG_TYPE_STRING;
                                    break;
                                case 0x02:
                                    netMsg.MessageType = WebMessage.MSG_TYPE_BINARY;
                                    break;
                                default:
                                    session.Close(); // just close it if undefined op code found
                                    return false;
                            }

                            bool needmask = (bytes[1] & 0x80) == 0x80;

                            if (needmask)
                            {
                                netMsg.VirtualHeaderSize += 4;
                                netMsg.MaskFlag = 1;
                            }
                            else
                            {
                                netMsg.MaskFlag = 0;
                            }

                            sbyte payloadlen = (sbyte)(bytes[1] & 0x7f);

                            if (payloadlen == 126)
                            {
                                netMsg.VirtualHeaderSize += 2;
                                netMsg.HeaderFlag = 1;
                            }
                            else if (payloadlen == 127)
                            {
                                netMsg.VirtualHeaderSize += 8;
                                netMsg.HeaderFlag = 2;
                            }
                            else
                            {
                                netMsg.HeaderFlag = 0;
                            }
                        }

                        if (netMsg.VirtualHeaderSize > bytes.Length)
                        {
                            stream.Position = stream.Position - bytes.Length;
                            continue;
                        }

                        if (netMsg.MaskFlag > 0)
                        {
                            netMsg.MaskBytes = new byte[4];
                            Buffer.BlockCopy(bytes, netMsg.VirtualHeaderSize - 4, netMsg.MaskBytes, 0, 4);
                        }

                        if (netMsg.HeaderFlag == 1)
                        {
                            netMsg.ContentSize = (int)bytes[2] * 256 + (int)bytes[3];
                        }
                        else if (netMsg.HeaderFlag == 2)
                        {
                            long len = 0;
                            int n = 1;
                            for (int i = 7; i >= 0; i--)
                            {
                                len += (int)bytes[i + 2] * n;
                                n *= 256;
                            }

                            netMsg.ContentSize = (int)len;
                        }
                        else
                        {
                            netMsg.ContentSize = (sbyte)(bytes[1] & 0x7f);
                        }

                        if (netMsg.ContentSize > m_MaxMsgSize) netMsg.ContentSize = 0;
                        if (netMsg.ContentSize < 0) netMsg.ContentSize = 0;

                        if (netMsg.ContentSize > 0)
                        {
                            netMsg.ReceivingState = WebMessage.STATE_WAIT_FOR_BODY;
                        }
                        else
                        {
                            netMsg.ReceivingState = WebMessage.STATE_READY;
                            if (stack.Count > 0) stack.Pop();

                            netMsg = new WebMessage();
                            netMsg.VirtualHeaderSize = 2;
                            stack.Push(netMsg);

                            continue;
                        }

                    }

                }

                if (netMsg.ReceivingState == WebMessage.STATE_WAIT_FOR_BODY)
                {

                    if (stream.Length - stream.Position >= netMsg.ContentSize)
                    {
                        Byte[] bytes = new Byte[netMsg.ContentSize];
                        stream.Read(bytes, 0, netMsg.ContentSize);

                        if (netMsg.MaskFlag > 0 && netMsg.MaskBytes != null)
                        {
                            int masklen = netMsg.MaskBytes.Length;
                            for (int i = 0; i < bytes.Length; i++)
                            {
                                bytes[i] = (byte)(bytes[i] ^ netMsg.MaskBytes[i % masklen]);
                            }
                        }

                        if (netMsg.MessageType == WebMessage.MSG_TYPE_STRING)
                        {
                            netMsg.MessageContent = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
                        }

                        if (netMsg.MessageType == WebMessage.MSG_TYPE_BINARY
                            || netMsg.MessageType == WebMessage.MSG_TYPE_PING
                            || netMsg.MessageType == WebMessage.MSG_TYPE_PONG)
                        {
                            netMsg.RawContent = bytes;
                            netMsg.ContentSize = bytes.Length;
                        }

                        output.Add(netMsg);
                        total++;

                        netMsg.ReceivingState = WebMessage.STATE_READY;
                        if (stack.Count > 0) stack.Pop();

                        netMsg = new WebMessage();
                        netMsg.VirtualHeaderSize = 2;
                        stack.Push(netMsg);
                    }

                }

            }

            if (total > 0 && stream.Length - stream.Position <= 0) return true;

            if (netMsg.ReceivingState != WebMessage.STATE_WAIT_FOR_HEADER
                    && netMsg.ReceivingState != WebMessage.STATE_WAIT_FOR_BODY
                    && netMsg.ReceivingState != WebMessage.STATE_READY)
            {
                session.Close();
            }

            return false;
        }
    }
}

