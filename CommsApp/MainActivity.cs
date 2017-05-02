using Android.App;
using Android.Widget;
using Android.OS;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using System;

namespace CommsApp
{
    [Activity(Label = "CommsApp", MainLauncher = true, Icon = "@drawable/networkcomms")]
    public class MainActivity : Activity
    {
        EditText ipText;
        EditText messageText;
        EditText logText;
        Button connectButton;
        Button sendButton;
        string serverIp = string.Empty;
        int serverPort = 0;
        bool isReceivedReady = false;
        public static EditText loggingTextBox = null;
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView (Resource.Layout.Main);

            ipText = FindViewById<EditText>(Resource.Id.txtIP);
            // 아래 주소는 처리되지 않는다. 반드시 서버 실제 주로를 넣어야한다.
            ipText.Text = "127.0.0.1:2020";
            messageText = FindViewById<EditText>(Resource.Id.txtMessage);
            logText = FindViewById<EditText>(Resource.Id.txtLog);

            connectButton = FindViewById<Button>(Resource.Id.connectButton);
            connectButton.Click += ConnectButton_Click;
            sendButton = FindViewById<Button>(Resource.Id.sendButton);
            sendButton.Click += SendButton_Click;

        }

        #region ReceiveChatMessage
        /// <summary>
        /// Writes the provided message to the console window
        /// </summary>
        /// <param name="header">The packet header associated with the incoming message</param>
        /// <param name="connection">The connection used by the incoming message</param>
        /// <param name="message">The message to be printed to the console</param>
        private static void ReceiveChatMessage(PacketHeader header, Connection connection, string message)
        {
            try
            {
                loggingTextBox.Text += "\r\n" + message;
            }
            catch (Exception ex)
            {
                
            }
        }
        #endregion

        private void SendButton_Click(object sender, System.EventArgs e)
        {
            try
            {
                logText.Text += "\r\n" + "Android : " + this.messageText.Text;

                //NetworkComms.SendObject<string>("Message", this.serverIp, this.serverPort, this.messageTextBox.Text);
                NetworkComms.SendObject<string>("ChatMessage", this.serverIp, this.serverPort, "Android : " + this.messageText.Text);

                this.messageText.Text = "";
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, ex.ToString(), ToastLength.Long).Show();
            }
        }

        private void ConnectButton_Click(object sender, System.EventArgs e)
        {
            if (this.connectButton.Text == "Connect")
            {
                this.connectButton.Text = "Stop";
                string[] ipdata = this.ipText.Text.Split(':');
                this.serverPort = int.Parse(ipdata[1]);
                this.serverIp = ipdata[0];
                try
                {
                    //UDPConnection.SendObject("Message", "Connect", this.serverIp, this.serverPort);
                    NetworkComms.SendObject<string>("ChatMessage", this.serverIp, this.serverPort, "Android : Connect");

                    if (!isReceivedReady)
                    {
                        isReceivedReady = true;
                        NetworkComms.AppendGlobalIncomingPacketHandler<string>("SendChatMessage", ReceiveChatMessage);
                    }
                }
                catch (Exception ex)
                {
                    logText.Text +="\r\n" + ex.ToString();
                }
            }
            else
            {
                this.connectButton.Text = "Connect";
                NetworkComms.SendObject<string>("ChatMessage", this.serverIp, this.serverPort, "Disconnect!");
                NetworkComms.Shutdown();
            }
        }
    }
}

