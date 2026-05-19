using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Server
{
    public partial class Form1 : Form
    {
        private TcpListener _server;
        private CancellationTokenSource _cts;
        private bool _isRunning = false;
        private const int Port = 8888;

        public Form1()
        {
            InitializeComponent();
            lblStatus.Text = "Status: Stopped";
            txtLog.ReadOnly = true;
        }

        private async void btnStartStop_Click(object sender, EventArgs e)
        {
            if (!_isRunning)
            {
                
                _cts = new CancellationTokenSource();
                _server = new TcpListener(IPAddress.Any, Port);

                try
                {
                    _server.Start();
                    _isRunning = true;
                    btnStartStop.Text = "Stop Server";
                    lblStatus.Text = $"Status: Running on port {Port}";
                    LogMessage("[SERVER] Started and listening for connections...");

                    
                    _ = ListenForClientsAsync(_cts.Token);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to start server: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
               
                _cts?.Cancel();
                _server?.Stop();
                _isRunning = false;
                btnStartStop.Text = "Start Server";
                lblStatus.Text = "Status: Stopped";
                LogMessage("[SERVER] Stopped.");
            }
        }

        private async Task ListenForClientsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    
                    TcpClient client = await _server.AcceptTcpClientAsync();

                   
                    _ = Task.Run(() => HandleClientAsync(client, token), token);
                }
                catch (ObjectDisposedException)
                {
                    
                    break;
                }
                catch (Exception ex)
                {
                    LogMessage($"[ERROR] Error accepting client: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            string clientId = client.Client.RemoteEndPoint.ToString();
            LogMessage($"[CONNECTED] Client connected from: {clientId}");

            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    
                    byte[] sizeBuffer = new byte[8];
                    await ReadExactAsync(stream, sizeBuffer, 8, token);
                    long originalFileSize = BitConverter.ToInt64(sizeBuffer, 0);
                    LogMessage($"[{clientId}] Processing file. Original Size: {originalFileSize} bytes.");

                   
                    byte[] originalFileBytes = new byte[originalFileSize];
                    await ReadExactAsync(stream, originalFileBytes, originalFileSize, token);
                    LogMessage($"[{clientId}] Data received successfully. Compressing...");

                    
                    byte[] compressedBytes;
                    using (MemoryStream outputStream = new MemoryStream())
                    {
                        using (GZipStream gzip = new GZipStream(outputStream, CompressionMode.Compress))
                        {
                            await gzip.WriteAsync(originalFileBytes, 0, originalFileBytes.Length, token);
                        }
                        compressedBytes = outputStream.ToArray();
                    }

                    
                    byte[] compressedSizeBuffer = BitConverter.GetBytes((long)compressedBytes.Length);
                    await stream.WriteAsync(compressedSizeBuffer, 0, 8, token);

                    
                    await stream.WriteAsync(compressedBytes, 0, compressedBytes.Length, token);
                    LogMessage($"[{clientId}] Done. Compressed Size: {compressedBytes.Length} bytes.");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"[DISCONNECTED] Connection lost with {clientId}: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }

        private async Task ReadExactAsync(NetworkStream stream, byte[] buffer, long totalBytesToRead, CancellationToken token)
        {
            long bytesRead = 0;
            while (bytesRead < totalBytesToRead)
            {
                token.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(buffer, (int)bytesRead, (int)(totalBytesToRead - bytesRead), token);
                if (read == 0) throw new EndOfStreamException("Client disconnected prematurely.");
                bytesRead += read;
            }
        }

        
        private void LogMessage(string message)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => LogMessage(message)));
            }
            else
            {
                txtLog.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
                txtLog.ScrollToCaret();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();
            _server?.Stop();
            base.OnFormClosing(e);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

    }
}
