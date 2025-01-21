using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using Windows.ApplicationModel.DataTransfer;

namespace TarskyTGI
{
    public sealed partial class InstructPage : Page
    {
        private Process pythonProcess;
        private StreamWriter pythonInput;
        private StreamReader pythonOutput;
        private bool modelLoaded = false;
        public InstructPage()
        {
            this.InitializeComponent();
            InitializePythonProcess();
            loadJson();
        }

        private void loadJson()
        {
            string jsonString = File.ReadAllText("instruct.json");
            ChatClass jsonToLoad = JsonSerializer.Deserialize<ChatClass>(jsonString);
            ModelBox.Text = jsonToLoad.model;
            ctxBox.Text = jsonToLoad.n_ctx.ToString();
            predictBox.Text = jsonToLoad.n_predict.ToString();
            temperatureBox.Text = jsonToLoad.temperature.ToString();
            toppBox.Text = jsonToLoad.top_p.ToString();
            minpBox.Text = jsonToLoad.min_p.ToString();
            typicalpBox.Text = jsonToLoad.typical_p.ToString();
        }

        private void InitializePythonProcess()
        {
            pythonProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "py",
                    Arguments = "instruct.py",
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


        private void PromptBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && !Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            {
                e.Handled = true;
                SendFN(sender, e);
            }
        }

        private async void SendFN(object sender, RoutedEventArgs e)
        {
            if (PromptBox.Text.Trim() != string.Empty)
            {
                //outputBox.Text = outputBox.Text + PromptBox.Text.Trim();
                if (!modelLoaded)
                {
                    StatusTextBlock.Text = "Please load a model first.";
                    return;
                }

                string inputText = PromptBox.Text.Trim();
                string itemsAsString = outputBox.Text;
                string generatedText = await GenerateText(itemsAsString + inputText);
                outputBox.Text = string.Empty;
                string outputString = generatedText.Replace("\\n", "\n");
                //string outputString = generatedText;
                outputBox.Text += outputString;
            }
            PromptBox.Text = string.Empty;
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
            //return "Unknown error occurred.";
            return response;
        }

        private async void LoadModelButton_Click(object sender, RoutedEventArgs e)
        {
            //string modelPath = "C:/Users/ivany/Downloads/Phi-3.1-mini-4k-instruct-Q4_K_L.gguf";
            string modelPath = ModelBox.Text;
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
            }
        }

        private void ModelBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                //float check = float.Parse(temperatureBox.Text.Replace('.', ','));
                var chatClass = new ChatClass(ModelBox.Text.Trim(), "chatml", int.Parse(ctxBox.Text), int.Parse(predictBox.Text), float.Parse(temperatureBox.Text.Replace('.', ',')), float.Parse(toppBox.Text.Replace('.', ',')), float.Parse(minpBox.Text.Replace('.', ',')), float.Parse(typicalpBox.Text.Replace('.', ',')), 35);
                //ChatClass chatClass = new ChatClass("aaaa", 1028, 128);

                string jsonString = JsonSerializer.Serialize(chatClass);

                File.WriteAllText("instruct.json", jsonString);
            }
            catch { }
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
        private void TextBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            args.Cancel = args.NewText.Any(c => !char.IsDigit(c));
        }
        private void TextBox2_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            args.Cancel = args.NewText.Any(c => !char.IsDigit(c) && c != '.');
        }

        private void copyButton_Click(object sender, RoutedEventArgs e)
        {
            DataPackage dataPackage = new();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            dataPackage.SetText(outputBox.Text);
            Clipboard.SetContent(dataPackage);
        }
    }
}
