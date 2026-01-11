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
        private TextGenerator? textGenerator;
        private bool modelLoaded = false;
        private string? imgPath = null;
        private string jsonPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\TarskyTGI\\chat.json";
        private JsonService jsonService = new JsonService();
        private string sysPrompt = "You are a helpful, respectful and honest assistant. Always answer as helpfully as possible, while being safe.  Your answers should not include any harmful, unethical, racist, sexist, toxic, dangerous, or illegal content. Please ensure that your responses are socially unbiased and positive in nature. If a question does not make any sense, or is not factually coherent, explain why instead of answering something not correct. If you don't know the answer to a question, please don't share false information.";

        public LlavaPage()
        {
            this.InitializeComponent();
            textGenerator = new TextGenerator();
            textGenerator.Initialize();
            _ = textGenerator.InsertSystemPromptAsync(sysPrompt);
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

        // Python process handled by TextGenerator

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
            if (textGenerator == null)
            {
                StatusTextBlock.Text = "Backend not initialized.";
                return;
            }

            // Use LLava load signature: modelPath, mmproj, layers, ctx, cformat
            string mmproj = MmprojBox.Text ?? string.Empty;
            int ctx = int.TryParse(ctxBox.Text, out var ctxv) ? ctxv : 0;
            string cformat = (ChatFormatCombo.SelectedItem ?? ChatFormatCombo.SelectedValue ?? string.Empty).ToString();

            var (success, message) = await textGenerator.LoadModelLlavaAsync(modelPath, mmproj, gpu_l, ctx, cformat);
            modelLoaded = success;
            StatusTextBlock.Text = message;
        }

        private async void ClearFN(object sender, RoutedEventArgs e)
        {
            if (textGenerator != null)
            {
                await textGenerator.ClearAsync(sysPrompt);
            }
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
                string generatedText = await GenerateText(inputText, imgPath);
                string outputString = generatedText.Replace("/[newline]", "\n");
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

        private async Task<string> GenerateText(string inputText, string? imagePath = null)
        {
            if (textGenerator == null) return "Error: Backend not initialized.";

            return await textGenerator.GenerateTextAsync(inputText, imagePath);
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
            if (textGenerator == null) return;

            if (priorityBox.SelectedIndex == 0)
            {
                textGenerator.SetProcessPriority(ProcessPriorityClass.Idle);
            }
            else if (priorityBox.SelectedIndex == 1)
            {
                textGenerator.SetProcessPriority(ProcessPriorityClass.BelowNormal);
            }
            else if (priorityBox.SelectedIndex == 2)
            {
                textGenerator.SetProcessPriority(ProcessPriorityClass.Normal);
            }
            else if (priorityBox.SelectedIndex == 3)
            {
                textGenerator.SetProcessPriority(ProcessPriorityClass.AboveNormal);
            }
            else if (priorityBox.SelectedIndex == 4)
            {
                textGenerator.SetProcessPriority(ProcessPriorityClass.High);
            }
            else
            {
                textGenerator.SetProcessPriority(ProcessPriorityClass.RealTime);
            }
        }
    }
}
