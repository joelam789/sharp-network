using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using SharpNetwork.Core;
using SharpNetwork.SimpleWebSocket;

namespace ChatServer
{
    public partial class ChatServerForm : Form
    {
        Server m_ChatServer = new Server();

        public ChatServerForm()
        {
            InitializeComponent();
        }

        public void LogMsg(string msg)
        {
            BeginInvoke((Action)(() =>
            {
                if (mmChat.Lines.Length > 1024)
                {
                    List<string> finalLines = mmChat.Lines.ToList();
                    finalLines.RemoveRange(0, 512);
                    mmChat.Lines = finalLines.ToArray();
                }

                mmChat.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\n");
                mmChat.SelectionStart = mmChat.Text.Length;
                mmChat.ScrollToCaret();

            }));
        }

        public void ProcessMsg(string msg)
        {
            var parts = msg.Split('|');
            var act = parts[0];
            var user = parts[1];
            var chat = parts.Length >= 3 ? parts[2] : "";
            if (act == "JOIN") LogMsg(user + " has joined chatroom");
            else if (act == "LEAVE") LogMsg(user + " has left chatroom");
            else if (act == "CHAT") LogMsg(user + ": " + chat);

            WebMessage webmsg = new WebMessage(msg);
            webmsg.MaskFlag = (byte)1;
            m_ChatServer.Broadcast(webmsg);
        }

        private void ChatServerForm_Load(object sender, EventArgs e)
        {
            m_ChatServer.SetIoFilter(new MessageCodec(1024 * 1024 * 2)); // set max buffer size to 2m ...
            m_ChatServer.SetIoHandler(new ServerNetworkEventHandler(this));

            //m_ChatServer.SetCert(new X509Certificate2(certFilepath, certKey));

            m_ChatServer.Start(9090);
            LogMsg("Server is listening on 9090...");
        }

        private void ChatServerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            m_ChatServer.Stop();
        }
    }

    public class ServerNetworkEventHandler : NetworkEventHandler
    {
        ChatServerForm m_MainForm = null;

        public ServerNetworkEventHandler(ChatServerForm mainForm) : base()
        {
            IsOrderlyProcess = true;
            m_MainForm = mainForm;
        }

        public override void OnConnect(Session session)
        {
            base.OnConnect(session);
        }

        public override void OnHandshake(Session session)
        {
            base.OnHandshake(session);
            m_MainForm.LogMsg("new client connected");
        }

        public override void OnDisconnect(Session session)
        {
            base.OnDisconnect(session);
            m_MainForm.LogMsg("a client disconnected");
        }

        public override void OnError(Session session, int errortype, Exception error)
        {
            base.OnError(session, errortype, error);
            m_MainForm.LogMsg("socket error: " + error.Message);
        }

        protected override void ProcessMessage(SessionContext ctx)
        {
            Session session = ctx.Session;
            WebMessage msg = (WebMessage)ctx.Data;

            m_MainForm.ProcessMsg(msg.MessageContent);
        }
    }
}
