using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharpNetwork.Core;

namespace SharpNetwork.SimpleHttp
{
    public class MessageCodec : INetworkFilter
    {
        private int m_MaxMsgSize = 1024 * 256; // 256K

        public const string HTTP_CONTENT_TYPE = "Content-Type";
        public const string HTTP_CONTENT_LEN = "Content-Length";
        public const string HTTP_CONNECTION = "Connection";

        private Dictionary<string, string> m_CustomHeaders = null;

        public MessageCodec(int maxMsgSize = 0)
        {
            if (maxMsgSize > 0) m_MaxMsgSize = maxMsgSize;
        }

        public MessageCodec(IDictionary<string, string> customHeaders)
        {
            if (customHeaders != null) m_CustomHeaders = new Dictionary<string, string>(customHeaders);
        }

        public MessageCodec(int maxMsgSize, IDictionary<string, string> customHeaders)
        {
            if (maxMsgSize > 0) m_MaxMsgSize = maxMsgSize;
            if (customHeaders != null) m_CustomHeaders = new Dictionary<string, string>(customHeaders);
        }

        public void Encode(Session session, Object message, MemoryStream stream)
        {
            if (message is HttpMessage)
            {
                HttpMessage msg = message as HttpMessage;

                if (msg.IsString())
                {
                    string str = "";

                    if (m_CustomHeaders != null && m_CustomHeaders.Count > 0)
                    {
                        foreach(var item in m_CustomHeaders) msg.Headers[item.Key] = item.Value;
                    }

                    if (!msg.Headers.ContainsKey(HTTP_CONTENT_TYPE))
                        msg.Headers.Add(HTTP_CONTENT_TYPE, "text/plain; charset=utf-8");

                    if (!string.IsNullOrWhiteSpace(msg.MessageContent))
                    {
                        if (!msg.Headers.ContainsKey(HTTP_CONTENT_LEN))
                            msg.Headers.Add(HTTP_CONTENT_LEN, msg.MessageContent.Length.ToString());
                    }
                    else
                    {
                        if (!msg.Headers.ContainsKey(HTTP_CONTENT_LEN))
                            msg.Headers.Add(HTTP_CONTENT_LEN, "0");
                    }

                    if (msg.RequestMethod.Length > 0) // should be a request
                    {
                        str = string.Format("{0} {1} {2}\r\n{3}\r\n\r\n{4}", msg.RequestMethod, Uri.EscapeDataString(msg.RequestUrl), msg.ProtocolVersion,
                                            string.Join("\r\n", msg.Headers.Select(x => string.Format("{0}: {1}", x.Key, x.Value))), msg.MessageContent);
                    }
                    else // should be a response
                    {
                        if (!msg.Headers.ContainsKey(HTTP_CONNECTION))
                            msg.Headers.Add(HTTP_CONNECTION, "keep-alive");

                        str = string.Format("{0} {1} {2}\r\n{3}\r\n\r\n{4}", msg.ProtocolVersion, msg.StatusCode, msg.ReasonPhrase,
                                            string.Join("\r\n", msg.Headers.Select(x => string.Format("{0}: {1}", x.Key, x.Value))), msg.MessageContent);
                    }

                    Byte[] bytes = Encoding.UTF8.GetBytes(str);
                    stream.Write(bytes, 0, bytes.Length);
                }
            }

        }

        // please know that function Decode() should be called in single thread
        public bool Decode(Session session, MemoryStream stream, List<Object> output)
        {
            //bool isNew = false;
            HttpMessage netMsg = null;

            bool hasKey = false;
            Stack<Object> stack = HttpMessage.GetSessionBuffer(session);
            if (stack.Count > 0)
            {
                hasKey = true;
                netMsg = (HttpMessage)stack.Peek();
            }
            if (netMsg == null)
            {
                //isNew = true;
                netMsg = new HttpMessage();
                if (hasKey) stack.Pop();
                stack.Push(netMsg);
            }

            int total = 0;

            if (netMsg.ReceivingState == HttpMessage.STATE_WAIT_FOR_BODY)
            {
                if (stream.Length - stream.Position >= netMsg.ContentSize)
                {
                    Byte[] bytes = new Byte[netMsg.ContentSize];
                    stream.Read(bytes, 0, netMsg.ContentSize);

                    netMsg.RawContent = bytes;
                    netMsg.ReceivingState = HttpMessage.STATE_READY;

                    if (netMsg.IsString())
                        netMsg.MessageContent = Encoding.UTF8.GetString(bytes, 0, bytes.Length);

                    output.Add(netMsg);
                    total++;

                    netMsg = new HttpMessage();
                    if (stack.Count > 0) stack.Pop();
                    stack.Push(netMsg);
                }

            }

            while (netMsg.ReceivingState == HttpMessage.STATE_WAIT_FOR_HEADER 
                    && stream.Length - stream.Position > 0)
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
                    string headerContent = Encoding.UTF8.GetString(bytes, 0, checkedlen);

                    string[] headerLines = headerContent.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < headerLines.Length; i++)
                    {
                        var line = headerLines[i];
                        if (i == 0)
                        {
                            string[] tokens = line.Trim().Split(' ');
                            if (tokens.Length != 3)
                            {
                                //throw new Exception("invalid http request line");
                                session.Close();
                                return false;
                            }
                            netMsg.RequestMethod = tokens[0].ToUpper();
                            //netMsg.Url = new Uri(tokens[1], UriKind.Absolute);
                            //netMsg.RequestUrl = netMsg.Url.LocalPath;
                            netMsg.RequestUrl = Uri.UnescapeDataString(tokens[1]); // "HttpUtility.UrlDecode()" should be better... but need System.Web
                            netMsg.ProtocolVersion = tokens[2];
                        }
                        else
                        {
                            if (line.Equals("")) break;

                            int separator = line.IndexOf(':');
                            if (separator < 0)
                            {
                                //throw new Exception("invalid http header line: " + line);
                                session.Close();
                                return false;
                            }
                            string hname = line.Substring(0, separator);
                            int hpos = separator + 1;
                            while ((hpos < line.Length) && (line[hpos] == ' ')) hpos++;
                            string hvalue = line.Substring(hpos, line.Length - hpos);
                            netMsg.Headers.Add(hname, hvalue);
                        }
                    }

                    //var incomingHeaders = HttpMessage.GetIncomingHeaders(session);
                    //incomingHeaders.Clear();
                    //foreach (var headerItem in netMsg.Headers)
                    //    incomingHeaders.Add(headerItem.Key, headerItem.Value);

                    //var incomingHeaders = new Dictionary<string, string>(netMsg.Headers);
                    //HttpMessage.SetIncomingHeaders(session, incomingHeaders);

                    //var reqUrlInfo = new Dictionary<string, string>();
                    //reqUrlInfo.Add("Method", netMsg.RequestMethod);
                    //reqUrlInfo.Add("Path", netMsg.RequestUrl);
                    //reqUrlInfo.Add("Version", netMsg.ProtocolVersion);
                    //HttpMessage.SetRequestUrlInfo(session, reqUrlInfo);

                    HttpMessage.SetSessionData(session, "Path", netMsg.RequestUrl);

                    if (netMsg.Headers.ContainsKey(HTTP_CONTENT_LEN))
                    {
                        netMsg.ContentSize = Convert.ToInt32(netMsg.Headers[HTTP_CONTENT_LEN]);
                        if (netMsg.ContentSize > m_MaxMsgSize) netMsg.ContentSize = 0;
                    }
                    if (netMsg.Headers.ContainsKey(HTTP_CONTENT_TYPE))
                    {
                        var ctype = netMsg.Headers[HTTP_CONTENT_TYPE].ToLower();
                        if (ctype.Contains("text")) netMsg.MessageType = HttpMessage.MSG_TYPE_STRING;
                        else if (ctype.Contains("stream")) netMsg.MessageType = HttpMessage.MSG_TYPE_BINARY;
                    }

                    stream.Position = orgpos + checkedlen;

                    if (netMsg.ContentSize > 0)
                    {
                        netMsg.ReceivingState = HttpMessage.STATE_WAIT_FOR_BODY;
                    }
                    else
                    {
                        netMsg.ReceivingState = HttpMessage.STATE_READY;
                        output.Add(netMsg);
                        total++;
                        netMsg = new HttpMessage();
                        if (stack.Count > 0) stack.Pop();
                        stack.Push(netMsg);
                        continue;
                    }

                }
                else
                {
                    if (curpos > m_MaxMsgSize)
                    {
                        session.Close();
                        return false;
                    }
                    stream.Position = orgpos;
                    break;
                }

                if (netMsg.ReceivingState == HttpMessage.STATE_WAIT_FOR_BODY)
                {
                    if (stream.Length - stream.Position >= netMsg.ContentSize)
                    {
                        Byte[] bodybytes = new Byte[netMsg.ContentSize];
                        stream.Read(bodybytes, 0, netMsg.ContentSize);

                        netMsg.RawContent = bodybytes;
                        netMsg.ReceivingState = HttpMessage.STATE_READY;

                        if (netMsg.IsString())
                            netMsg.MessageContent = Encoding.UTF8.GetString(bodybytes, 0, bodybytes.Length);

                        output.Add(netMsg);
                        total++;

                        netMsg = new HttpMessage();
                        if (stack.Count > 0) stack.Pop();
                        stack.Push(netMsg);
                        continue;
                    }

                }

            }

            if (total > 0 && stream.Length - stream.Position <= 0) return true;

            if (netMsg.ReceivingState != HttpMessage.STATE_WAIT_FOR_HEADER
                    && netMsg.ReceivingState != HttpMessage.STATE_WAIT_FOR_BODY
                    && netMsg.ReceivingState != HttpMessage.STATE_READY)
            {
                session.Close();
            }

            return false;
        }
    }
}
