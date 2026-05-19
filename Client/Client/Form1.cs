using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Client
{

    public partial class Form1 : Form
    {
        private const string ServerIp = "127.0.0.1";
        private const int ServerPort = 8888;

        public Form1()
        {
            InitializeComponent();
            progressBar.Style = ProgressBarStyle.Blocks;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select File to Compress";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtFilePath.Text = ofd.FileName;
                }
            }
        }

        private async void btnCompress_Click(object sender, EventArgs e)
        {
            string inputPath = txtFilePath.Text;

            if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
            {
                MessageBox.Show("Please select a valid file first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outputPath;
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Save Compressed File As";
                sfd.Filter = "GZip Files (*.gz)|*.gz|All Files (*.*)|*.*";
                sfd.FileName = Path.GetFileName(inputPath) + ".gz";

                if (sfd.ShowDialog() != DialogResult.OK) return;
                outputPath = sfd.FileName;
            }

            ToggleControls(false);
            progressBar.Style = ProgressBarStyle.Marquee; 
            lblClientStatus.Text = "Connecting to compression server...";

            try
            {
                await Task.Run(async () =>
                {
                    using (TcpClient client = new TcpClient())
                    {
                        
                        await client.ConnectAsync(ServerIp, ServerPort);

                        UpdateStatus("Sending file data...");
                        using (NetworkStream stream = client.GetStream())
                        {
                            
                            byte[] fileBytes =  File.ReadAllBytes(inputPath);
                            long fileSize = fileBytes.Length;

                            byte[] sizeBuffer = BitConverter.GetBytes(fileSize);
                            await stream.WriteAsync(sizeBuffer, 0, 8);

                            await stream.WriteAsync(fileBytes, 0, fileBytes.Length);

                            UpdateStatus("Server is compressing... Waiting for response...");

                            byte[] compressedSizeBuffer = new byte[8];
                            await ReadExactAsync(stream, compressedSizeBuffer, 8);
                            long compressedSize = BitConverter.ToInt64(compressedSizeBuffer, 0);

                            byte[] compressedFileBytes = new byte[compressedSize];
                            await ReadExactAsync(stream, compressedFileBytes, compressedSize);

                            UpdateStatus("Saving compressed file...");
                             File.WriteAllBytes(outputPath, compressedFileBytes);
                        }
                    }
                });

                lblClientStatus.Text = "Success! File compressed and saved.";
                MessageBox.Show("File compressed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                lblClientStatus.Text = "Operation failed.";
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Style = ProgressBarStyle.Blocks;
                ToggleControls(true);
            }
        }

        private async Task ReadExactAsync(NetworkStream stream, byte[] buffer, long totalBytesToRead)
        {
            long bytesRead = 0;
            while (bytesRead < totalBytesToRead)
            {
                int read = await stream.ReadAsync(buffer, (int)bytesRead, (int)(totalBytesToRead - bytesRead));
                if (read == 0) throw new EndOfStreamException("Server closed connection prematurely.");
                bytesRead += read;
            }
        }

        private void ToggleControls(bool enabled)
        {
            btnBrowse.Enabled = enabled;
            btnCompress.Enabled = enabled;
            txtFilePath.Enabled = enabled;
        }

        private void UpdateStatus(string text)
        {
            if (lblClientStatus.InvokeRequired)
            {
                lblClientStatus.Invoke(new Action(() => UpdateStatus(text)));
            }
            else
            {
                lblClientStatus.Text = text;
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
