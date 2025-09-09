using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OllamaVision
{
    public class MainForm : Form
    {
        // P/Invoke for global hotkeys
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int EXECUTE_HOTKEY_ID = 9000;
        private const int STOP_HOTKEY_ID = 9001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_ALT = 0x0001;
        private const int WM_HOTKEY = 0x0312;

        private Button executeButton;
        private TextBox outputTextBox;
        private TextBox instructionTextBox;
        private Label instructionLabel;

        private static readonly HttpClient client = new HttpClient();
        private CancellationTokenSource _executionCts;
        private List<AIAction> _actionHistory;

        public MainForm()
        {
            InitializeComponent();
            this.FormClosing += MainForm_FormClosing;
            RegisterHotKey(this.Handle, EXECUTE_HOTKEY_ID, MOD_CONTROL | MOD_ALT, (int)Keys.C);
            RegisterHotKey(this.Handle, STOP_HOTKEY_ID, MOD_CONTROL | MOD_ALT, (int)Keys.S);
        }

        private void InitializeComponent()
        {
            this.executeButton = new Button();
            this.outputTextBox = new TextBox();
            this.instructionTextBox = new TextBox();
            this.instructionLabel = new Label();
            this.SuspendLayout();

            this.instructionLabel.AutoSize = true;
            this.instructionLabel.Location = new Point(12, 9);
            this.instructionLabel.Text = "Instruction:";

            this.instructionTextBox.Location = new Point(12, 27);
            this.instructionTextBox.Name = "instructionTextBox";
            this.instructionTextBox.Size = new Size(760, 20);
            this.instructionTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.instructionTextBox.Text = "Open notepad and type 'Hello from the AI'.";

            this.executeButton.Location = new Point(12, 55);
            this.executeButton.Name = "executeButton";
            this.executeButton.Size = new Size(200, 23);
            this.executeButton.Text = "Execute (Ctrl+Alt+C)";
            this.executeButton.UseVisualStyleBackColor = true;
            this.executeButton.Click += new EventHandler(this.executeButton_Click);

            var stopLabel = new Label();
            stopLabel.Text = "Stop Hotkey: Ctrl+Alt+S";
            stopLabel.Location = new Point(220, 60);
            stopLabel.AutoSize = true;
            this.Controls.Add(stopLabel);


            this.outputTextBox.Location = new Point(12, 84);
            this.outputTextBox.Multiline = true;
            this.outputTextBox.Name = "outputTextBox";
            this.outputTextBox.ReadOnly = true;
            this.outputTextBox.ScrollBars = ScrollBars.Vertical;
            this.outputTextBox.Size = new Size(760, 354);
            this.outputTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.outputTextBox.Font = new Font("Consolas", 8.25F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));

            this.ClientSize = new Size(784, 450);
            this.Controls.Add(this.instructionLabel);
            this.Controls.Add(this.instructionTextBox);
            this.Controls.Add(this.outputTextBox);
            this.Controls.Add(this.executeButton);
            this.Name = "MainForm";
            this.Text = "Ollama Vision Autopilot";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private async void executeButton_Click(object sender, EventArgs e)
        {
            if (_executionCts != null && !_executionCts.IsCancellationRequested)
            {
                _executionCts.Cancel();
                outputTextBox.AppendText("\r\n--- EXECUTION CANCELED BY USER ---");
                return;
            }

            string instruction = instructionTextBox.Text;
            if (string.IsNullOrWhiteSpace(instruction))
            {
                MessageBox.Show("Please enter an instruction.", "Instruction Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _executionCts = new CancellationTokenSource();
            _actionHistory = new List<AIAction>();

            executeButton.Text = "Stop Execution";

            try
            {
                await ExecutionLoop(instruction, _executionCts.Token);
            }
            catch (TaskCanceledException)
            {
                // This is expected
            }
            catch (Exception ex)
            {
                outputTextBox.AppendText($"\r\nAn unexpected error occurred: {ex.Message}\r\n{ex.StackTrace}");
            }
            finally
            {
                executeButton.Text = "Execute (Ctrl+Alt+C)";
                _executionCts.Dispose();
                _executionCts = null;
            }
        }

        private async Task ExecutionLoop(string instruction, CancellationToken token)
        {
            outputTextBox.Text = $"Starting execution for: {instruction}\r\n";

            for (int i = 0; i < 10; i++) // Safety break after 10 steps
            {
                token.ThrowIfCancellationRequested();
                outputTextBox.AppendText($"\r\n--- Step {i + 1} ---\r\n");

                this.Hide();
                await Task.Delay(250, token);
                var base64Image = CaptureScreen();
                this.Show();
                this.Activate();

                string prompt = BuildPrompt(instruction, _actionHistory);

                outputTextBox.AppendText("Asking AI for next action...\r\n");
                var ollamaResponse = await GetAIResponse(prompt, base64Image, token);

                if (ollamaResponse?.Message?.Content == null) { outputTextBox.AppendText("Error: Received empty response from AI.\r\n"); break; }

                AIAction aiAction;
                try
                {
                    aiAction = JsonSerializer.Deserialize<AIAction>(ollamaResponse.Message.Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException jsonEx)
                {
                    outputTextBox.AppendText($"Error parsing AI response: {jsonEx.Message}\r\nAI response was: {ollamaResponse.Message.Content}\r\n");
                    break;
                }

                if (aiAction == null) { outputTextBox.AppendText("Error: Failed to deserialize AI action.\r\n"); break; }

                _actionHistory.Add(aiAction);
                outputTextBox.AppendText($"AI action: {aiAction.Action} {aiAction.Text} {(aiAction.X.HasValue ? $"({aiAction.X.Value},{aiAction.Y.Value})" : "")}\r\n");

                if (aiAction.Action.ToUpper() == "DONE") { outputTextBox.AppendText("\r\n--- EXECUTION COMPLETE ---"); break; }

                ActionExecutor.Execute(aiAction);
                await Task.Delay(1000, token);
            }
        }

        private string BuildPrompt(string userInstruction, List<AIAction> history)
        {
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("You are an AI assistant controlling a computer. Your goal is to complete the user's instruction.");
            promptBuilder.AppendLine($"The user's instruction is: '{userInstruction}'.");
            promptBuilder.AppendLine("You will be given a screenshot of the current screen. Based on the screenshot and the instruction, decide the next single action to perform.");

            if (history.Count > 0)
            {
                promptBuilder.AppendLine("So far, you have performed these actions:");
                foreach(var action in history) { promptBuilder.AppendLine($"- {action.Action} {action.Text} {(action.X.HasValue ? $"({action.X.Value},{action.Y.Value})" : "")}"); }
            }

            promptBuilder.AppendLine("Respond with a JSON object describing the next single action. The possible actions are:");
            promptBuilder.AppendLine("- `TYPE`: type a string of text. `text` property is required.");
            promptBuilder.AppendLine("- `CLICK`: click the mouse at a specific coordinate. `x` and `y` properties are required.");
            promptBuilder.AppendLine("- `DONE`: the instruction is complete.");
            promptBuilder.AppendLine("Example for typing: {\"action\": \"TYPE\", \"text\": \"hello world\"}");
            promptBuilder.AppendLine("Example for clicking: {\"action\": \"CLICK\", \"x\": 123, \"y\": 456}");
            promptBuilder.AppendLine("Example for finishing: {\"action\": \"DONE\"}");
            promptBuilder.AppendLine("Analyze the image and provide the JSON for the very next action to take.");

            return promptBuilder.ToString();
        }

        private async Task<OllamaResponse> GetAIResponse(string prompt, string base64Image, CancellationToken token)
        {
            var requestData = new OllamaRequest { Model = "llava", Stream = false, Messages = new[] { new Message { Role = "user", Content = prompt, Images = new[] { base64Image } } } };
            var jsonRequest = JsonSerializer.Serialize(requestData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("http://localhost:11434/api/chat", content, token);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<OllamaResponse>(jsonResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        private string CaptureScreen()
        {
            var bounds = Screen.PrimaryScreen.Bounds;
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap)) { graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy); }
                using (var ms = new MemoryStream()) { bitmap.Save(ms, ImageFormat.Png); return Convert.ToBase64String(ms.ToArray()); }
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_HOTKEY)
            {
                if (m.WParam.ToInt32() == EXECUTE_HOTKEY_ID)
                {
                    executeButton.PerformClick();
                }
                else if (m.WParam.ToInt32() == STOP_HOTKEY_ID)
                {
                    if (_executionCts != null && !_executionCts.IsCancellationRequested)
                    {
                        _executionCts.Cancel();
                        outputTextBox.AppendText("\r\n--- EXECUTION CANCELED BY HOTKEY ---");
                    }
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, EXECUTE_HOTKEY_ID);
            UnregisterHotKey(this.Handle, STOP_HOTKEY_ID);
            _executionCts?.Dispose();
        }
    }

    // Data models
    public class OllamaRequest { public string Model { get; set; } public bool Stream { get; set; } public Message[] Messages { get; set; } }
    public class Message { public string Role { get; set; } public string Content { get; set; } public string[] Images { get; set; } }
    public class OllamaResponse { public Message Message { get; set; } }
}
