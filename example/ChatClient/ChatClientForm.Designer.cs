namespace ChatClient
{
    partial class ChatClientForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.mmChat = new System.Windows.Forms.RichTextBox();
            this.edtServerIp = new System.Windows.Forms.TextBox();
            this.edtServerPort = new System.Windows.Forms.TextBox();
            this.btnConnect = new System.Windows.Forms.Button();
            this.edtUser = new System.Windows.Forms.TextBox();
            this.btnJoin = new System.Windows.Forms.Button();
            this.edtMsg = new System.Windows.Forms.TextBox();
            this.btnChat = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // mmChat
            // 
            this.mmChat.Location = new System.Drawing.Point(111, 50);
            this.mmChat.Name = "mmChat";
            this.mmChat.Size = new System.Drawing.Size(579, 346);
            this.mmChat.TabIndex = 2;
            this.mmChat.Text = "";
            // 
            // edtServerIp
            // 
            this.edtServerIp.Location = new System.Drawing.Point(111, 24);
            this.edtServerIp.Name = "edtServerIp";
            this.edtServerIp.Size = new System.Drawing.Size(103, 20);
            this.edtServerIp.TabIndex = 3;
            this.edtServerIp.Text = "127.0.0.1";
            // 
            // edtServerPort
            // 
            this.edtServerPort.Location = new System.Drawing.Point(220, 24);
            this.edtServerPort.Name = "edtServerPort";
            this.edtServerPort.Size = new System.Drawing.Size(54, 20);
            this.edtServerPort.TabIndex = 4;
            this.edtServerPort.Text = "9090";
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(280, 22);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(75, 23);
            this.btnConnect.TabIndex = 5;
            this.btnConnect.Text = "Connect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // edtUser
            // 
            this.edtUser.Location = new System.Drawing.Point(506, 24);
            this.edtUser.Name = "edtUser";
            this.edtUser.Size = new System.Drawing.Size(100, 20);
            this.edtUser.TabIndex = 6;
            this.edtUser.Text = "Tommy";
            // 
            // btnJoin
            // 
            this.btnJoin.Location = new System.Drawing.Point(612, 22);
            this.btnJoin.Name = "btnJoin";
            this.btnJoin.Size = new System.Drawing.Size(75, 23);
            this.btnJoin.TabIndex = 7;
            this.btnJoin.Text = "Join";
            this.btnJoin.UseVisualStyleBackColor = true;
            this.btnJoin.Click += new System.EventHandler(this.btnJoin_Click);
            // 
            // edtMsg
            // 
            this.edtMsg.Location = new System.Drawing.Point(111, 402);
            this.edtMsg.Name = "edtMsg";
            this.edtMsg.Size = new System.Drawing.Size(495, 20);
            this.edtMsg.TabIndex = 8;
            // 
            // btnChat
            // 
            this.btnChat.Location = new System.Drawing.Point(612, 400);
            this.btnChat.Name = "btnChat";
            this.btnChat.Size = new System.Drawing.Size(75, 23);
            this.btnChat.TabIndex = 9;
            this.btnChat.Text = "Chat";
            this.btnChat.UseVisualStyleBackColor = true;
            this.btnChat.Click += new System.EventHandler(this.btnChat_Click);
            // 
            // ChatClientForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.btnChat);
            this.Controls.Add(this.edtMsg);
            this.Controls.Add(this.btnJoin);
            this.Controls.Add(this.edtUser);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.edtServerPort);
            this.Controls.Add(this.edtServerIp);
            this.Controls.Add(this.mmChat);
            this.Name = "ChatClientForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Chat Client";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ChatClientForm_FormClosing);
            this.Load += new System.EventHandler(this.ChatClientForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RichTextBox mmChat;
        private System.Windows.Forms.TextBox edtServerIp;
        private System.Windows.Forms.TextBox edtServerPort;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.TextBox edtUser;
        private System.Windows.Forms.Button btnJoin;
        private System.Windows.Forms.TextBox edtMsg;
        private System.Windows.Forms.Button btnChat;
    }
}

