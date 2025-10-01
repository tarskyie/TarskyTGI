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
        private string jsonPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\TarskyTGI\\chat.json";
        private JsonService jsonService = new JsonService();
        private string sysPrompt = "You are a helpful, respectful and honest assistant. Always answer as helpfully as possible, while being safe.  Your answers should not include any harmful, unethical, racist, sexist, toxic, dangerous, or illegal content. Please ensure that your responses are socially unbiased and positive in nature. If a question does not make any sense, or is not factually coherent, explain why instead of answering something not correct. If you don't know the answer to a question, please don't share false information.";

        public LlavaPage()
        {
            this.InitializeComponent();
            InitializePythonProcess();
            loadJson();
        }

        private void loadJson()
        {
            try
            {
                string jsonString = File.ReadAllText(jsonPath);
                ChatClass jsonToLoad = JsonSerializer.Deserialize<ChatClass>(jsonString);
                ModelBox.Text = jsonToLoad.model;
                ctxBox.Text = jsonToLoad.n_ctx.ToString();
                predictBox.Text = jsonToLoad.n_predict.ToString();
                temperatureBox.Text = jsonToLoad.temperature.ToString();
                toppBox.Text = jsonToLoad.top_p.ToString();
                minpBox.Text = jsonToLoad.min_p.ToString();
                typicalpBox.Text = jsonToLoad.typical_p.ToString();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = ex.Message;
                jsonService.copyJsonToDocuments("chat.json");
            }
        }

        private void InitializePythonProcess()
        {
            pythonProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "llavagenerator.py", 
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
            await pythonInput.WriteLineAsync(ctxBox.Text);
            await pythonInput.WriteLineAsync(ChatFormatCombo.SelectedItem.ToString());
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
                imgPath = null;
                imagePreview.Source = null;
                uploadStatus.Text = "No image uploaded.";
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
            await pythonInput.WriteLineAsync(imgPath ?? "None"); 
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
                var chatClass = new ChatClass(ModelBox.Text.Trim(), "chatml", int.Parse(ctxBox.Text), int.Parse(predictBox.Text), float.Parse(temperatureBox.Text), float.Parse(toppBox.Text), float.Parse(minpBox.Text), float.Parse(typicalpBox.Text), int.Parse(ctxBox.Text));

                string jsonString = JsonSerializer.Serialize(chatClass);

                File.WriteAllText(jsonPath, jsonString);
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

        private async void selectMmprojButton_Click(object sender, RoutedEventArgs e)
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
                MmprojBox.Text = file.Path;
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
                imagePreview.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(file.Path));
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
