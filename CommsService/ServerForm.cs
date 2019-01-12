using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.UDP;
using NetworkCommsDotNet.DPSBase;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommsService
{
    public partial class ServerForm : Form
    {
        public static TextBox loggingTextBox = null;

        #region ServerForm
        public ServerForm()
        {
            InitializeComponent();
            this.ipTextBox.Text = LocalIPAddress().ToString();
            loggingTextBox = this.logTextBox;
            
            //DataSerializer serializer = DPSManager.GetDataSerializer<JsonSerializer>();
            // 지정된 serializer를 사용하도록 기본 보내기 수신 옵션을 설정합니다. 이전 기본값에서 DataProcessors 및 Options 유지
            //NetworkComms.DefaultSendReceiveOptions = new SendReceiveOptions(serializer, NetworkComms.DefaultSendReceiveOptions.DataProcessors, NetworkComms.DefaultSendReceiveOptions.Options);

            // 암호화를 전체적으로 (즉, 모든 연결에 대해) 사용하려면 먼저 암호화 비밀번호를 옵션으로 추가합니다
            //RijndaelPSKEncrypter.AddPasswordToOptions(NetworkComms.DefaultSendReceiveOptions.Options, "kjun");
            // 마지막으로 RijndaelPSKEncrypter 데이터 프로세서를 sendReceiveOptions에 추가합니다
            //NetworkComms.DefaultSendReceiveOptions.DataProcessors.Add(DPSManager.GetDataProcessor<RijndaelPSKEncrypter>());
        }
        #endregion

        // Method
        #region ReceiveReplyMessage
        /// <summary>
        /// Writes the provided message to the console window
        /// </summary>
        /// <param name="header">The packet header associated with the incoming message</param>
        /// <param name="connection">The connection used by the incoming message</param>
        /// <param name="message">The message to be printed to the console</param>
        private static void ReceiveReplyMessage(PacketHeader header, Connection connection, string message)
        {
            loggingTextBox.Invoke(new MethodInvoker(
                delegate ()
                {
                    loggingTextBox.AppendText(Environment.NewLine + "▶ A message was received" + Environment.NewLine + connection.ToString() + " => '" + message);

                    // 스크롤 마지막으로 이동
                    loggingTextBox.SelectionStart = loggingTextBox.Text.Length;
                    loggingTextBox.ScrollToCaret();
                }));

            //UDPConnection.SendObject("Message", "Broadcast Message", new IPEndPoint(IPAddress.Broadcast, 1000));
            DataTable dt = new DataTable();
            dt.Columns.Add("Key");
            dt.Columns.Add("Value");
            dt.Columns.Add("Data");

            int count = int.Parse(message);
            for (int i = 0; i < count; i++)
            {
                dt.Rows.Add(i.ToString(), "Value" + i.ToString(), "Data" + i.ToString());
            }
            dt.AcceptChanges();
            dt.TableName = "Test";
            StringWriter writer = new StringWriter();
            dt.WriteXml(writer, XmlWriteMode.IgnoreSchema, false);
            connection.SendObject<string>("ReturnMessage", writer.ToString());

            //UDPConnection.SendObject("BroadMessage", "Broad Message", new IPEndPoint(IPAddress.Broadcast, 1000));
        }
        #endregion
        #region ReceiveChatMessage
        /// <summary>
        /// Writes the provided message to the console window
        /// </summary>
        /// <param name="header">The packet header associated with the incoming message</param>
        /// <param name="connection">The connection used by the incoming message</param>
        /// <param name="message">The message to be printed to the console</param>
        private static void ReceiveChatMessage(PacketHeader header, Connection connection, string message)
        {
            loggingTextBox.Invoke(new MethodInvoker(
                delegate ()
                {
                    loggingTextBox.AppendText(Environment.NewLine + "▶ A message was received" + connection.ToString() + " => " + message);

                    // 스크롤 마지막으로 이동
                    loggingTextBox.SelectionStart = loggingTextBox.Text.Length;
                    loggingTextBox.ScrollToCaret();
                }));

            //UDPConnection.SendObject("Message", "Broadcast Message", new IPEndPoint(IPAddress.Broadcast, 1000));

            var allRelayConnections = (from current in NetworkComms.GetExistingConnection() where current != connection select current).ToArray();

            foreach (var relayConnection in allRelayConnections)
            {
                try { relayConnection.SendObject("SendChatMessage", message); }
                catch (CommsException) { /* Catch the general comms exception, ignore and continue */ }
            }

            //UDPConnection.SendObject("BroadMessage", "Broad Message", new IPEndPoint(IPAddress.Broadcast, 1000));
        }
        #endregion
        #region LocalIPAddress
        /// <summary>
        /// IP 주소를 가져옵니다.
        /// </summary>
        /// <returns></returns>
        private IPAddress LocalIPAddress()
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return null;

            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

            return host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        }
        #endregion

        // Event Method
        #region startButton_Click
        private void startButton_Click(object sender, EventArgs e)
        {
            try
            {
                this.logTextBox.Clear();

                // 들어올 메세지 형식을 정의합니다.
                NetworkComms.AppendGlobalIncomingPacketHandler<string>("Message", ReceiveReplyMessage);
                NetworkComms.AppendGlobalIncomingPacketHandler<string>("ChatMessage", ReceiveChatMessage);

                // port 를 정의합니다.
                int inputport = 0;

                if (!string.IsNullOrEmpty(this.portTextBox.Text))
                {
                    string[] ports = this.portTextBox.Text.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string port in ports)
                    {
                        inputport = int.Parse(port);
                        // 통신방식을 지정하고 서버를 시작합니다.
                        Connection.StartListening(ConnectionType.TCP, new System.Net.IPEndPoint(System.Net.IPAddress.Any, inputport));
                        Connection.StartListening(ConnectionType.UDP, new System.Net.IPEndPoint(System.Net.IPAddress.Any, inputport));

                        // 파일을 수신합니다.
                        System.IO.Stream recievedData = new System.IO.MemoryStream();
                        NetworkComms.AppendGlobalIncomingPacketHandler<byte[]>("PartitionedSend", (packetHeader, connection, incomingBytes) =>
                        {
                            this.logTextBox.Invoke(new MethodInvoker(
                                                    delegate ()
                                                    {
                                                        this.logTextBox.AppendText(Environment.NewLine + "  ... Incoming data from " + connection.ToString());

                                                        // 스크롤 마지막으로 이동
                                                        this.logTextBox.SelectionStart = loggingTextBox.Text.Length;
                                                        this.logTextBox.ScrollToCaret();
                                                    }));

                            recievedData.Write(incomingBytes, 0, incomingBytes.Length);
                        });
                    }
                }

                foreach (System.Net.IPEndPoint localEndPoint in Connection.ExistingLocalListenEndPoints(ConnectionType.TCP))
                {
                    this.logTextBox.AppendText($"{Environment.NewLine} TCP => {localEndPoint.Address}:{localEndPoint.Port}");
                }

                foreach (System.Net.IPEndPoint localEndPoint in Connection.ExistingLocalListenEndPoints(ConnectionType.UDP))
                {
                    this.logTextBox.AppendText($"{Environment.NewLine} UDP => {localEndPoint.Address}:{localEndPoint.Port}");
                }

                this.statusToolStripStatusLabel.Text = $"Start Server - Sucess ({DateTime.Now.ToString()})";
            }
            catch (Exception ex)
            {
                this.logTextBox.AppendText(ex.ToString());
                this.statusToolStripStatusLabel.Text = "Start Server - Failed";
            }
        }
        #endregion
        #region stopButton_Click
        private void stopButton_Click(object sender, EventArgs e)
        {
            this.statusToolStripStatusLabel.Text = "Stop Server";
            // 서버를 중단합니다.
            NetworkComms.Shutdown();
        }

        private void fireButton_Click(object sender, EventArgs e)
        {
            string exeName = System.Reflection.Assembly.GetExecutingAssembly().Location;
            FireWallManager.AllowThisProgram("CommsService", "TCP", this.portTextBox.Text, "", "IN");
            FireWallManager.AllowThisProgram("CommsService", "UPD", this.portTextBox.Text, "", "IN");
        }
        #endregion
    }
}
