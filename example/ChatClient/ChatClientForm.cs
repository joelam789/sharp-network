using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using SharpNetwork.Core;
using SharpNetwork.SimpleWebSocket;

namespace ChatClient
{
    public partial class ChatClientForm : Form
    {
        Client m_ChatClient = new Client();
        Uri m_ServerUrl = null;

        public ChatClientForm()
        {
            InitializeComponent();
        }

        public Uri GetServerAddress()
        {
            return m_ServerUrl;
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

        }

        private void ChatClientForm_Load(object sender, EventArgs e)
        {
            m_ChatClient.SetIoFilter(new MessageCodec(1024 * 1024 * 2)); // set max buffer size to 2m ...
            m_ChatClient.SetIoHandler(new ClientNetworkEventHandler(this));
        }

        private void ChatClientForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (m_ChatClient.GetState() > 0)
            {
                WebMessage.SendString(m_ChatClient.GetSession(), "LEAVE|" + edtUser.Text);
                Thread.Sleep(200);
            }
            m_ChatClient.Disconnect();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            m_ServerUrl = new Uri("ws://" + edtServerIp.Text + ":" + edtServerPort.Text + "/chat", UriKind.Absolute);
            m_ChatClient.Connect(edtServerIp.Text, Convert.ToInt32(edtServerPort.Text));
        }

        private void btnJoin_Click(object sender, EventArgs e)
        {
            WebMessage.SendString(m_ChatClient.GetSession(), "JOIN|" + edtUser.Text);
        }

        private void btnChat_Click(object sender, EventArgs e)
        {
            WebMessage.SendString(m_ChatClient.GetSession(), "CHAT|" + edtUser.Text + "|" + edtMsg.Text);
            edtMsg.Text = "";
        }
    }

    public class ClientNetworkEventHandler : NetworkEventHandler
    {
        ChatClientForm m_MainForm = null;

        public ClientNetworkEventHandler(ChatClientForm mainForm) : base()
        {
            IsOrderlyProcess = true;
            m_MainForm = mainForm;
        }

        public override void OnConnect(Session session)
        {
            base.OnConnect(session);
            WebMessage.SendHandshakeRequest(session, m_MainForm.GetServerAddress());
        }

        public override void OnHandshake(Session session)
        {
            base.OnHandshake(session);
            m_MainForm.LogMsg("connected to server");
        }

        public override void OnDisconnect(Session session)
        {
            base.OnDisconnect(session);
            m_MainForm.LogMsg("disconnected from server");
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
