using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace TarskyTGI
{
    public sealed partial class HostPage : Page
    {
        private string model;
        private string chatFormat;
        private int gpuLayers;
        private int ctxBox;
        private int predictBox;
        private float temperatureBox;
        private float toppBox;
        private float minpBox;
        private float typicalpBox;

        private Process pythonProcess;
        private StreamWriter pythonInput;
        private StreamReader pythonOutput;
        private bool modelLoaded = false;

        private HttpListener _listener;

        public HostPage()
        {
            this.InitializeComponent();
            InitializePythonProcess();
        }

        private async void StartServer()
        {
            loadmodel1();

            if (_listener == null || !_listener.IsListening)
            {
                string portText = PortTextBox.Text;
                if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535)
                {
                    StatusTextBlock.Text = "Invalid port number. Please enter a value between 1 and 65535.";
                    return;
                }

                string url = $"http://localhost:{port}/";
                _listener = new HttpListener();
                _listener.Prefixes.Add(url);

                try
                {
                    _listener.Start();
                    StatusTextBlock.Text = $"Server started at {url}...";
                    await AcceptRequests();
                }
                catch (HttpListenerException ex)
                {
                    StatusTextBlock.Text = $"Error: {ex.Message}";
                }
            }
            else
            {
                StatusTextBlock.Text = "Server is already running.";
            }
        }

        private async Task AcceptRequests()
        {
            try
            {
                while (_listener.IsListening)
                {
                    var context = await _listener.GetContextAsync();
                    _ = HandleRequestAsync(context);
                }
            }
            catch (HttpListenerException)
            {
                StatusTextBlock.Text = "Server stopped.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error: {ex.Message}";
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string input = await reader.ReadToEndAsync();
                    StatusTextBlock.Text = $"Received: {input}";

                    // Process input and create a response
                    string generatedText = await GenerateText(input.Replace("\r", "/[newline]"));
                    generatedText = generatedText.Replace("/[newline]", "\r");
                    string responseString = $"{generatedText}";
                    byte[] responseBytes = Encoding.UTF8.GetBytes(responseString);

                    response.ContentType = "text/plain";
                    response.ContentEncoding = Encoding.UTF8;
                    response.ContentLength64 = responseBytes.Length;

                    await response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error: {ex.Message}";
            }
            finally
            {
                response.Close();
            }
        }

        private void Button_StartServer_Click(object sender, RoutedEventArgs e)
        {
            StartServer();
        }

        private void Button_StopServer_Click(object sender, RoutedEventArgs e)
        {
            StopServer();
        }

        private void StopServer()
        {
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
                StatusTextBlock.Text = "Server stopped.";
                _listener = null;
            }
        }

        // Text generation and model loading methods
        private void InitializePythonProcess()
        {
            pythonProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "textgenerator.py",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            pythonProcess.Start();
            pythonInput = pythonProcess.StandardInput;
            pythonOutput = pythonProcess.StandardOutput;
        }

        private void loadJson()
        {
            string jsonString = File.ReadAllText("chatstuff.json");
            ChatClass jsonToLoad = JsonSerializer.Deserialize<ChatClass>(jsonString);
            model = jsonToLoad.model;
            ctxBox = jsonToLoad.n_ctx;
            predictBox = jsonToLoad.n_predict;
            temperatureBox = jsonToLoad.temperature;
            toppBox = jsonToLoad.top_p;
            minpBox = jsonToLoad.min_p;
            typicalpBox = jsonToLoad.typical_p;
            gpuLayers = jsonToLoad.layers;
            chatFormat = jsonToLoad.format;
        }

        private async void loadmodel1()
        {
            loadJson();
            string modelPath = model;
            await LoadModel(modelPath);
        }

        private async Task LoadModel(string modelPath)
        {
            StatusTextBlock.Text = "Loading model...";

            await pythonInput.WriteLineAsync("load");
            await pythonInput.WriteLineAsync(modelPath);
            await pythonInput.WriteLineAsync(gpuLayers.ToString());
            await pythonInput.WriteLineAsync(chatFormat);
            await pythonInput.FlushAsync();

            string response = await pythonOutput.ReadLineAsync();
            if (response.StartsWith("$model_loaded$"))
            {
                modelLoaded = true;
                StatusTextBlock.Text = "LOADED.";
            }
            else if (response.StartsWith("$model_load_error$"))
            {
                modelLoaded = false;
                StatusTextBlock.Text = $"Failed to load model: {response.Substring(response.IndexOf(':') + 1)}";
                StopServer();
            }
        }

        private async Task<string> GenerateText(string inputText)
        {
            await pythonInput.WriteLineAsync("chat_server");
            await pythonInput.WriteLineAsync(inputText);
            await pythonInput.WriteLineAsync(SystemPromptBox.Text);
            await pythonInput.FlushAsync();

            string response = await pythonOutput.ReadLineAsync();
            if (response.StartsWith("$not_loaded$"))
            {
                return "Error: Model not loaded.";
            }
            else if (response.StartsWith("$response$"))
            {
                return response.Substring(response.IndexOf(':') + 1);
            }
            else if (response.StartsWith("$error$"))
            {
                return $"Error: {response.Substring(response.IndexOf(':') + 1)}";
            }
            return response;
        }
    }
}
