using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.TCP;
using NetworkCommsDotNet.Connections.UDP;
using NetworkCommsDotNet.DPSBase;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static NetworkCommsDotNet.Tools.StreamTools;

namespace CommsClient
{
    public partial class ClientForm : Form
    {
        string serverIp = string.Empty;
        int serverPort = 0;
        bool isReceivedReady = false;
        public static TextBox loggingTextBox = null;

        #region ClientForm
        public ClientForm()
        {
            InitializeComponent();
            loggingTextBox = this.logTextBox;

            //DataSerializer serializer = DPSManager.GetDataSerializer<NetworkCommsDotNet.DPSBase.DataSerializer>();
            // 지정된 serializer를 사용하도록 기본 보내기 수신 옵션을 설정합니다. 이전 기본값에서 DataProcessors 및 Options 유지
            //NetworkComms.DefaultSendReceiveOptions = new SendReceiveOptions(serializer, NetworkComms.DefaultSendReceiveOptions.DataProcessors, NetworkComms.DefaultSendReceiveOptions.Options);

            // 암호화를 전체적으로 (즉, 모든 연결에 대해) 사용하려면 먼저 암호화 비밀번호를 옵션으로 추가합니다
            //RijndaelPSKEncrypter.AddPasswordToOptions(NetworkComms.DefaultSendReceiveOptions.Options, "kjun");
            // 마지막으로 RijndaelPSKEncrypter 데이터 프로세서를 sendReceiveOptions에 추가합니다
            //NetworkComms.DefaultSendReceiveOptions.DataProcessors.Add(DPSManager.GetDataProcessor<RijndaelPSKEncrypter>());
        }
        #endregion

        // Methods
        #region FileSend
        void FileSend()
        {
            //This is our progress percent, between 0 and 1 
            double progressPercentage = 0;

            //Initialise stream with 1MB
            byte[] buffer = new byte[1024 * 1024];
            ThreadSafeStream dataToSend = new ThreadSafeStream(new System.IO.MemoryStream(buffer));
            long totalBytesSent = 0;

            //We will send in chunks of 50KB
            int sendInChunksOfNBytes = 50 * 1024;

            //Get the connection to the target
            Connection connection = TCPConnection.GetConnection(new ConnectionInfo(this.serverIp, this.serverPort));

            do
            {
                //Determine the total number of bytes to send
                //We need to watch out for the end of the buffer when we send less than sendInChunksOfNBytes
                long bytesToSend = (buffer.Length - totalBytesSent > sendInChunksOfNBytes ? sendInChunksOfNBytes : buffer.Length - totalBytesSent);
                StreamSendWrapper streamWrapper = new StreamSendWrapper(dataToSend, totalBytesSent, bytesToSend);
                connection.SendObject("PartitionedSend", streamWrapper);
                totalBytesSent += bytesToSend;
                progressPercentage = ((double)totalBytesSent / buffer.Length);
            }
            while (totalBytesSent < buffer.Length);
        }
        #endregion
        #region OpenFileSend
        void OpenFileSend()
        {
            long totalBytesSent = 0;

            //Get the connection to the target
            Connection connection = TCPConnection.GetConnection(new ConnectionInfo(this.serverIp, this.serverPort));

            OpenFileDialog fd = new OpenFileDialog();
            if (fd.ShowDialog() == DialogResult.OK)
            {
                string filePath = fd.FileName;
                var fileName = System.IO.Path.GetFileName(filePath);

                using (var file = new FileStream(filePath, FileMode.Open))
                {
                    using (var safeStream = new ThreadSafeStream(file))
                    {
                        var sendChunkSizeBytes = (long)(file.Length / 20.0) + 1;

                        //Limit send chunk size to 500MB
                        const long maxChunkSizeBytes = 500L * 1024L * 1024L;
                        if (sendChunkSizeBytes > maxChunkSizeBytes)
                            sendChunkSizeBytes = maxChunkSizeBytes;

                        totalBytesSent = 0;
                        do
                        {
                            //Check the number of bytes to send as the last one may be smaller
                            var bytesToSend = (totalBytesSent + sendChunkSizeBytes < file.Length ? sendChunkSizeBytes : file.Length - totalBytesSent);

                            //Wrap the threadSafeStream in a StreamSendWrapper so that we can get NetworkComms.Net
                            //to only send part of the stream.
                            using (var streamWrapper = new StreamSendWrapper(safeStream, totalBytesSent, bytesToSend))
                            {
                                //We want to record the packetSequenceNumber
                                long packetSequenceNumber;

                                connection.SendObject("PartitionedSend", streamWrapper);
                                //Send the select data
                                //connection.SendObject("PartialFileData", streamWrapper, NetworkComms.DefaultSendReceiveOptions, out packetSequenceNumber);
                                //Send the associated SendInfo for this send so that the remote can correctly rebuild the data
                                //connection.SendObject("PartialFileDataInfo", new SendInfo(fileName, file.Length, totalBytesSent, packetSequenceNumber));
                                totalBytesSent += bytesToSend;
                            }

                        } while (totalBytesSent < file.Length);
                    }
                }
            }
        }
        #endregion
        #region ParallelTest
        void ParallelTest()
        {
            Parallel.For(0, 10, (counter) =>
            {
                this.logTextBox.Invoke(new MethodInvoker(
                    delegate ()
                    {
                        this.logTextBox.AppendText(Environment.NewLine + "▶ Send Message => Parallel Test" + counter.ToString());

                        // 스크롤 마지막으로 이동
                        this.logTextBox.SelectionStart = this.logTextBox.Text.Length;
                        this.logTextBox.ScrollToCaret();
                    }));
                NetworkComms.SendObject<string>("ChatMessage", this.serverIp, this.serverPort, "Parallel Test" + counter.ToString());
            });
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
                    loggingTextBox.AppendText(Environment.NewLine + message);

                    // 스크롤 마지막으로 이동
                    loggingTextBox.SelectionStart = loggingTextBox.Text.Length;
                    loggingTextBox.ScrollToCaret();
                }));
        }
        #endregion

        // Event Methods
        #region connectButton_Click
        private void connectButton_Click(object sender, EventArgs e)
        {
            if (this.connectButton.Text == "Connect")
            {
                this.connectButton.Text = "Stop";
                this.ipTextBox.ReadOnly = true;
                this.portTextBox.ReadOnly = true;
                this.userTextBox.ReadOnly = true;

                this.serverPort = int.Parse(this.portTextBox.Text);
                this.serverIp = this.ipTextBox.Text;
                try
                {
                    //UDPConnection.SendObject("Message", "Connect", this.serverIp, this.serverPort);
                    NetworkComms.SendObject<string>("ChatMessage", this.serverIp, this.serverPort, this.userTextBox.Text + "Connect");

                    if (!isReceivedReady)
                    {
                        isReceivedReady = true;
                        NetworkComms.AppendGlobalIncomingPacketHandler<string>("SendChatMessage", ReceiveChatMessage);
                    }
                    this.statusLabel.Text = "Connect!";
                }
                catch (Exception ex)
                {
                    this.statusLabel.Text = "Fail!";
                    this.logTextBox.AppendText(Environment.NewLine + ex.ToString());
                }
            }
            else
            {
                this.connectButton.Text = "Connect";
                this.ipTextBox.ReadOnly = false;
                this.portTextBox.ReadOnly = false;
                this.userTextBox.ReadOnly = false;
                this.statusLabel.Text = "Ready";
                try
                {
                    NetworkComms.SendObject<string>("ChatMessage", this.serverIp, this.serverPort, "Disconnect!");
                    NetworkComms.Shutdown();
                }
                catch (Exception ex)
                {

                }
            }
        }
        #endregion
        #region sendButton_Click
        private void sendButton_Click(object sender, EventArgs e)
        {
            this.logTextBox.AppendText(Environment.NewLine + this.userTextBox.Text + " : " + this.messageTextBox.Text);

            //NetworkComms.SendObject<string>("Message", this.serverIp, this.serverPort, this.messageTextBox.Text);
            NetworkComms.SendObject<string>("ChatMessage", this.serverIp, this.serverPort, this.userTextBox.Text + " : " + this.messageTextBox.Text);

            this.messageTextBox.Clear();
        }
        #endregion
        #region fileButton_Click
        private void fileButton_Click(object sender, EventArgs e)
        {
            FileSend();
        }
        #endregion
        #region parallelButton_Click
        private void parallelButton_Click(object sender, EventArgs e)
        {
            ParallelTest();
        }
        #endregion
        #region replyButton_Click
        private void replyButton_Click(object sender, EventArgs e)
        {
            this.Cursor = Cursors.WaitCursor;
            Stopwatch sw = new Stopwatch();
            sw.Start();

            string receivedData = NetworkComms.SendReceiveObject<string, string>("Message", this.serverIp, this.serverPort, "ReturnMessage", 6000, this.countTextBox.Text);
            StringReader sr = new StringReader(receivedData);
            DataSet ds = new DataSet();
            ds.ReadXml(sr);
            sw.Stop();
            this.timeLabel.Text = sw.ElapsedMilliseconds + " ms";

            this.replyDataDataGridView.DataSource = ds.Tables[0];
            this.Cursor = Cursors.Default;
        }
        #endregion
    }
}
