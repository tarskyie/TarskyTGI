using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;

namespace TarskyTGI
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            this.InitializeComponent();
        }

        private void CheckPython_Click(object sender, RoutedEventArgs e)
        {
            CheckDependency("python --version");
        }

        private void CheckLlamaCppPython_Click(object sender, RoutedEventArgs e)
        {
            CheckDependency("pip show llama-cpp-python");
        }

        private void InstallLlamaCppPython_Click(object sender, RoutedEventArgs e)
        {
            InstallDependency("pip install llama-cpp-python");
        }

        private void InstallLlamaCppPythonVulkan_Click(object sender, RoutedEventArgs e)
        {
            InstallDependency("pip install llama-cpp-python -C cmake.args=\"-DGGML_VULKAN=on\"");
        }
        private void InstallLlamaCppPythonServer_Click(object sender, RoutedEventArgs e)
        {
            InstallDependency("pip install 'llama-cpp-python[server]'");
        }

        private void NavigateToChatPage_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ChatPage));
        }

        private void NavigateToInstructPage_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(InstructPage));
        }

        private void CheckDependency(string command)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    ShowMessage(output);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Error: {ex.Message}");
            }
        }

        private void InstallDependency(string command)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    ShowMessage(output);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Error: {ex.Message}");
            }
        }

        private async void ShowMessage(string message)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "Dependency Check",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot // Set the XamlRoot property
            };

            await dialog.ShowAsync();
        }

    }
}
