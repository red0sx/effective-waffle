using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OllamaVision
{
    public class MainForm : Form
    {
        // P/Invoke for global hotkeys
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_ALT = 0x0001;
        private const int WM_HOTKEY = 0x0312;


        private Button captureButton;
        private Button typeButton;
        private TextBox outputTextBox;
        private static readonly HttpClient client = new HttpClient();

        public MainForm()
        {
            InitializeComponent();
            this.FormClosing += MainForm_FormClosing;
            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, (int)Keys.C);
        }

        private void InitializeComponent()
        {
            this.captureButton = new Button();
            this.typeButton = new Button();
            this.outputTextBox = new TextBox();
            this.SuspendLayout();

            // captureButton
            this.captureButton.Location = new Point(12, 12);
            this.captureButton.Name = "captureButton";
            this.captureButton.Size = new Size(150, 23);
            this.captureButton.Text = "Capture (Ctrl+Alt+C)";
            this.captureButton.UseVisualStyleBackColor = true;
            this.captureButton.Click += new EventHandler(this.captureButton_Click);

            // typeButton
            this.typeButton.Location = new Point(170, 12);
            this.typeButton.Name = "typeButton";
            this.typeButton.Size = new Size(150, 23);
            this.typeButton.Text = "Type Response";
            this.typeButton.UseVisualStyleBackColor = true;
            this.typeButton.Enabled = false;
            this.typeButton.Click += new EventHandler(this.typeButton_Click);

            // outputTextBox
            this.outputTextBox.Location = new Point(12, 41);
            this.outputTextBox.Multiline = true;
            this.outputTextBox.Name = "outputTextBox";
            this.outputTextBox.ReadOnly = true;
            this.outputTextBox.ScrollBars = ScrollBars.Vertical;
            this.outputTextBox.Size = new Size(760, 397);
            this.outputTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // MainForm
            this.ClientSize = new Size(784, 450);
            this.Controls.Add(this.outputTextBox);
            this.Controls.Add(this.captureButton);
            this.Controls.Add(this.typeButton);
            this.Name = "MainForm";
            this.Text = "Ollama Vision";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private async void captureButton_Click(object sender, EventArgs e)
        {
            if (!this.captureButton.Enabled) return; // Prevent re-entrancy

            try
            {
                this.outputTextBox.Text = "Capturing screen...";
                this.captureButton.Enabled = false;
                this.typeButton.Enabled = false;

                this.Hide();
                await Task.Delay(250);

                var base64Image = CaptureScreen();

                this.Show();
                this.Activate(); // Bring form to front
                this.outputTextBox.Text = "Screen captured. Sending to AI...";

                var requestData = new OllamaRequest
                {
                    Model = "llava",
                    Stream = false,
                    Messages = new[]
                    {
                        new Message
                        {
                            Role = "user",
                            Content = "Analyze the screenshot and describe the user interface. What can the user do here?",
                            Images = new[] { base64Image }
                        }
                    }
                };

                var jsonRequest = JsonSerializer.Serialize(requestData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("http://localhost:11434/api/chat", content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(jsonResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    this.outputTextBox.Text = "AI Response: " + ollamaResponse?.Message?.Content;
                    this.typeButton.Enabled = true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    this.outputTextBox.Text = $"Error: {response.StatusCode}\r\n{errorContent}";
                }
            }
            catch (Exception ex)
            {
                this.outputTextBox.Text = $"An exception occurred: {ex.Message}";
            }
            finally
            {
                this.captureButton.Enabled = true;
            }
        }

        private async void typeButton_Click(object sender, EventArgs e)
        {
            try
            {
                this.typeButton.Enabled = false;
                string textToType = this.outputTextBox.Text;
                if (textToType.StartsWith("AI Response: "))
                {
                    textToType = textToType.Substring("AI Response: ".Length);
                }

                if (string.IsNullOrEmpty(textToType))
                {
                    MessageBox.Show("There is no text to type.");
                    return;
                }

                this.WindowState = FormWindowState.Minimized;
                await Task.Delay(3000);

                InputSimulator.SendText(textToType);

                this.WindowState = FormWindowState.Normal;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during typing: {ex.Message}");
            }
            finally
            {
                this.typeButton.Enabled = true;
            }
        }

        private string CaptureScreen()
        {
            var bounds = Screen.PrimaryScreen.Bounds;
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                }

                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_HOTKEY)
            {
                if (m.WParam.ToInt32() == HOTKEY_ID)
                {
                    captureButton.PerformClick();
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
        }
    }

    // Data models
    public class OllamaRequest
    {
        public string Model { get; set; }
        public bool Stream { get; set; }
        public Message[] Messages { get; set; }
    }

    public class Message
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public string[] Images { get; set; }
    }

    public class OllamaResponse
    {
        public Message Message { get; set; }
    }
}
