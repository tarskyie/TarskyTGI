using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;

namespace TarskyTGI.Pages
{
    public sealed partial class HostPage : Page
    {
        private Process serverProcess;

        public HostPage()
        {
            this.InitializeComponent();
        }

        private async void StartServer_Click(object sender, RoutedEventArgs e)
        {
            if (serverProcess != null && !serverProcess.HasExited)
            {
                StatusTextBlock.Text = "Server already running.";
                return;
            }

            try
            {
                string modelPath = ModelBox.Text;
                string host = HostBox.Text;
                string port = PortBox.Text;

                StatusTextBlock.Text = "Starting server...";

                // Example: llama.cpp server mode (OpenAI API compatible)
                // Adjust args for your backend
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"server.py --model \"{modelPath}\" --ctx-size {ctxBox.Text} --n-gpu-layers {gpuLayers.Text} --host {host} --port {port}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                serverProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                serverProcess.OutputDataReceived += (s, ev) =>
                {
                    if (!string.IsNullOrEmpty(ev.Data))
                        DispatcherQueue.TryEnqueue(() => StatusTextBlock.Text = ev.Data);
                };
                serverProcess.ErrorDataReceived += (s, ev) =>
                {
                    if (!string.IsNullOrEmpty(ev.Data))
                        DispatcherQueue.TryEnqueue(() => StatusTextBlock.Text = "Error: " + ev.Data);
                };

                serverProcess.Start();
                serverProcess.BeginOutputReadLine();
                serverProcess.BeginErrorReadLine();

                StatusTextBlock.Text = $"Server running at http://{host}:{port}/v1";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Failed to start: " + ex.Message;
            }
        }

        private void StopServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (serverProcess != null && !serverProcess.HasExited)
                {
                    serverProcess.Kill();
                    serverProcess.Dispose();
                    serverProcess = null;
                    StatusTextBlock.Text = "Server stopped.";
                }
                else
                {
                    StatusTextBlock.Text = "Server not running.";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Error stopping server: " + ex.Message;
            }
        }

        private async void selectModelButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var hwnd = WindowNative.GetWindowHandle(App.m_window);
            InitializeWithWindow.Initialize(picker, hwnd);

            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.FileTypeFilter.Add(".gguf");
            picker.FileTypeFilter.Add(".bin");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                ModelBox.Text = file.Path;
            }
        }
    }
}
