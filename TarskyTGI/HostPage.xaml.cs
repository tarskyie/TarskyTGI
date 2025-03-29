using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Text.Json;
using System.Diagnostics;

namespace TarskyTGI
{
    public sealed partial class HostPage : Page
    {
        private string model;
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

        private const int Port = 5000; 
        private TcpListener _listener;
        public HostPage()
        {
            this.InitializeComponent();
            InitializePythonProcess();
        }

        private async void StartServer()
        {
            loadmodel1();

            if (_listener == null || !_listener.Server.IsBound)
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                try
                {
                    _listener.Start();
                    StatusTextBlock.Text = $"Server started on port {Port}...";
                    await AcceptClients();
                }
                catch (SocketException ex)
                {
                    StatusTextBlock.Text = $"Error: {ex.Message}";
                }
            }
            else
            {
                StatusTextBlock.Text = "Server is already running.";
            }
        }

        private async Task AcceptClients()
        {
            try
            {
                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client);
                }
            }
            catch (ObjectDisposedException)
            {
                StatusTextBlock.Text = "Server stopped.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error: {ex.Message}";
            }
        }


        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                var stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    string input = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    StatusTextBlock.Text = $"Received: {input}";

                    // Process input and create a response
                    string generatedText = await GenerateText(input);

                    string response = $"{generatedText}";
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                }
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
            if (_listener != null)
            {
                _listener.Stop();
                _listener.Server.Dispose();
                StatusTextBlock.Text = "Server stopped.";
                _listener = null;
            }
        }

        // Text gen below

        private void InitializePythonProcess()
        {
            pythonProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "py",
                    Arguments = "basegenerator.py",
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
            string jsonString = File.ReadAllText("basestuff.json");
            ChatClass jsonToLoad = JsonSerializer.Deserialize<ChatClass>(jsonString);
            model = jsonToLoad.model;
            ctxBox = jsonToLoad.n_ctx;
            predictBox = jsonToLoad.n_predict;
            temperatureBox = jsonToLoad.temperature;
            toppBox = jsonToLoad.top_p;
            minpBox = jsonToLoad.min_p;
            typicalpBox = jsonToLoad.typical_p;
        }

        private async void loadmodel1()
        {
            loadJson();
            string modelPath = model;
            await LoadModel(modelPath);
        }
        private async Task LoadModel(string modelPath)
        {
            await pythonInput.WriteLineAsync("load");
            await pythonInput.WriteLineAsync(modelPath);
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
                //StopServer();
            }
        }
        private async Task<string> GenerateText(string inputText)
        {
            await pythonInput.WriteLineAsync("chat");
            await pythonInput.WriteLineAsync(inputText);
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
