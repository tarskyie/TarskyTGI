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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TarskyTGI
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class BasePage : Page
    {
        private Process? pythonProcess;
        private StreamWriter? pythonInput;
        private StreamReader? pythonOutput;
        private JsonService jsonService = new JsonService();
        private bool modelLoaded = false;

        public BasePage()
        {
            this.InitializeComponent();
            InitializePythonProcess();
            loadJson();
            if (App.m_window != null)
            {
                App.m_window.Closed += Window_Closed;
                this.Unloaded += BasePage_Unloaded;
            }
        }

        private void loadJson()
        {
            string jsonString = File.ReadAllText("basestuff.json");
            ChatClass? jsonToLoad = JsonSerializer.Deserialize<ChatClass?>(jsonString);

            if (jsonToLoad is null)
            {
                StatusTextBlock.Text = "Chat configuration is empty or invalid. Restoring default configuration.";
                jsonService.copyJsonToDocuments("chat.json");
                return;
            }

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

        private async void LoadModelButton_Click(object sender, RoutedEventArgs e)
        {
            //string modelPath = "C:/Users/ivany/Downloads/Phi-3.1-mini-4k-instruct-Q4_K_L.gguf";
            string modelPath = ModelBox.Text;
            await LoadModel(modelPath);
        }

        private async Task LoadModel(string modelPath)
        {
            if (pythonInput == null || pythonOutput == null)
            {
                StatusTextBlock.Text = "Error: Python process not initialized.";
                return;
            }

            await pythonInput.WriteLineAsync("load");
            await pythonInput.WriteLineAsync(modelPath);
            await pythonInput.FlushAsync();

            string ?response = await pythonOutput.ReadLineAsync();
            if (response == null)
            {
                StatusTextBlock.Text = "Error: No response from model.";
                return;
            }
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

        private void ClearFN(object sender, RoutedEventArgs e)
        {
            mainText.Text = string.Empty;
        }
        private async void SendFN(object sender, RoutedEventArgs e)
        {
            if (PromptBox.Text.Trim() != string.Empty)
            {
                mainText.Text=mainText.Text+PromptBox.Text.Trim();
                if (!modelLoaded)
                {
                    StatusTextBlock.Text = "Please load a model first.";
                    return;
                }

                string inputText = PromptBox.Text.Trim();
                string itemsAsString = mainText.Text;
                string generatedText = await GenerateText(itemsAsString + inputText);
                string outputString = generatedText.Replace("\\n", "\n");
                //string outputString = generatedText;
                mainText.Text += outputString;
            }
            PromptBox.Text = string.Empty;
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
            if (pythonInput == null || pythonOutput == null)
            {
                return "Error: Python process not initialized.";
            }
            await pythonInput.WriteLineAsync("chat");
            await pythonInput.WriteLineAsync(inputText);
            await pythonInput.FlushAsync();

            string? response = await pythonOutput.ReadLineAsync();
            if (response == null)
            {
                return "Error: No response from model.";
            }
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

        string GetListBoxItemsAsNewlineSeparatedString(ListBox listBox)
        {
            return string.Join("\\n", listBox.Items.Cast<object>().Select(item => item.ToString()));
        }

        private async void Window_Closed(object sender, WindowEventArgs args)
        {
            if (pythonProcess != null && !pythonProcess.HasExited && pythonInput != null)
            {
                await pythonInput.WriteLineAsync("exit");
                await pythonInput.FlushAsync();
                pythonProcess.WaitForExit(1000);
                if (!pythonProcess.HasExited)
                {
                    pythonProcess.Kill();
                }
                pythonProcess.Dispose();
            }
        }

        private void BasePage_Unloaded(object? sender, RoutedEventArgs e)
        {
            if (App.m_window != null)
            {
                App.m_window.Closed -= Window_Closed;
                this.Unloaded -= BasePage_Unloaded;
            }
        }

        //Additional SideBar stuff
        private void ModelBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var chatClass = new ChatClass(ModelBox.Text.Trim(), "chatml", int.Parse(ctxBox.Text), int.Parse(predictBox.Text), float.Parse(temperatureBox.Text), float.Parse(toppBox.Text), float.Parse(minpBox.Text), float.Parse(typicalpBox.Text), 35);

                string jsonString = JsonSerializer.Serialize(chatClass);

                File.WriteAllText("basestuff.json", jsonString);
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
    }
}
