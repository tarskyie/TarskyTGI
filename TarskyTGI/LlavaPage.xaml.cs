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
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using System.Text.Json;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;

namespace TarskyTGI
{
    public sealed partial class LlavaPage : Page
    {
        private Process pythonProcess;
        private StreamWriter pythonInput;
        private StreamReader pythonOutput;
        private bool modelLoaded = false;
        private string imgPath = null;

        public LlavaPage()
        {
            this.InitializeComponent();
            InitializePythonProcess();
            loadJson();
        }

        private void loadJson()
        {
            string jsonString = File.ReadAllText("chatstuff.json");
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
                    FileName = "python",
                    Arguments = "llavagenerator.py", // Обновлено для использования llavagenerator.py
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            pythonProcess.Start();
            pythonInput = pythonProcess.StandardInput;
            pythonOutput = pythonProcess.StandardOutput;
            pythonProcess.PriorityClass = ProcessPriorityClass.Normal;
        }

        private async void LoadModelButton_Click(object sender, RoutedEventArgs e)
        {
            int gpu_layers = 0;
            try
            {
                gpu_layers = int.Parse(gpuLayers.Text);
            }
            catch
            {
                StatusTextBlock.Text = "Please enter a valid number of GPU layers.";
                return;
            }

            string modelPath = ModelBox.Text;
            await LoadModel(modelPath, gpu_layers);
        }

        private async Task LoadModel(string modelPath, int gpu_l)
        {
            StatusTextBlock.Text = "Loading model...";
            //TODO exceptions
            await pythonInput.WriteLineAsync("load");
            await pythonInput.WriteLineAsync(modelPath);
            await pythonInput.WriteLineAsync(MmprojBox.Text);
            await pythonInput.WriteLineAsync(gpu_l.ToString());
            await pythonInput.WriteLineAsync(ctxBox.Text); // Передача контекста
            await pythonInput.WriteLineAsync(ChatFormatCombo.SelectedItem.ToString()); // Обновлено для использования выбранного формата чата
            await pythonInput.FlushAsync();

            string response = await pythonOutput.ReadLineAsync();

            if (response.StartsWith("$model_loaded$"))
            {
                modelLoaded = true;
                StatusTextBlock.Text = "Ready.";
            }
            else if (response.StartsWith("$model_load_error$"))
            {
                modelLoaded = false;
                StatusTextBlock.Text = $"Failed to load model: {response.Substring(response.IndexOf(':') + 1)}";
            }
        }

        private async void ClearFN(object sender, RoutedEventArgs e)
        {
            await pythonInput.WriteLineAsync("clear");
            await pythonInput.FlushAsync();

            ChatHistory.Items.Clear();
        }
        private async void SendFN(object sender, RoutedEventArgs e)
        {
            if (PromptBox.Text.Trim() != string.Empty)
            {
                ChatHistory.Items.Add(PromptBox.Text.Trim());
                StatusTextBlock.Text = "Generating response...";
                if (!modelLoaded)
                {
                    StatusTextBlock.Text = "Please load a model first.";
                    return;
                }

                string inputText = PromptBox.Text.Trim();
                PromptBox.Text = string.Empty;
                string generatedText = await GenerateText(inputText);
                string outputString = generatedText.Replace("\\n", "\n");
                StatusTextBlock.Text = "Ready.";
                ChatHistory.Items.Add(outputString);
            }
        }
        private void PromptBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && !Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            {
                e.Handled = true;
                SendFN(sender, e);
            }
        }

        private async Task<string> GenerateText(string inputText)
        {
            await pythonInput.WriteLineAsync("chat");
            await pythonInput.WriteLineAsync(inputText);
            await pythonInput.WriteLineAsync(imgPath ?? ""); // Передача пути к изображению, если он есть
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

        //Additional SideBar stuff
        private void ModelBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var chatClass = new ChatClass(ModelBox.Text.Trim(), "chatml", int.Parse(ctxBox.Text), int.Parse(predictBox.Text), float.Parse(temperatureBox.Text.Replace('.', ',')), float.Parse(toppBox.Text.Replace('.', ',')), float.Parse(minpBox.Text.Replace('.', ',')), float.Parse(typicalpBox.Text.Replace('.', ',')), 35);

                string jsonString = JsonSerializer.Serialize(chatClass);

                File.WriteAllText("chatstuff.json", jsonString);
            }
            catch { }
        }
        private void TextBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            args.Cancel = args.NewText.Any(c => !char.IsDigit(c));
        }
        private void TextBox2_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            args.Cancel = args.NewText.Any(c => !char.IsDigit(c) && c != '.');
        }
        //Select file
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

        private async void uploadButton_Click(object sender, RoutedEventArgs e)
        {
            // open a file selection dialog and get the path of the image
            var picker = new FileOpenPicker();
            var hwnd = WindowNative.GetWindowHandle(App.m_window);
            InitializeWithWindow.Initialize(picker, hwnd);

            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                uploadStatus.Text = "Picked " + file.Path;
                imgPath = file.Path;
            }
        }

        private void priorityBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pythonProcess != null)
            {
                if (priorityBox.SelectedIndex == 0)
                {
                    pythonProcess.PriorityClass = ProcessPriorityClass.Idle;
                }
                else if (priorityBox.SelectedIndex == 1)
                {
                    pythonProcess.PriorityClass = ProcessPriorityClass.BelowNormal;
                }
                else if (priorityBox.SelectedIndex == 2)
                {
                    pythonProcess.PriorityClass = ProcessPriorityClass.Normal;
                }
                else if (priorityBox.SelectedIndex == 3)
                {
                    pythonProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
                }
                else if (priorityBox.SelectedIndex == 4)
                {
                    pythonProcess.PriorityClass = ProcessPriorityClass.High;
                }
                else
                {
                    pythonProcess.PriorityClass = ProcessPriorityClass.RealTime;
                }
            }
        }
    }
}
