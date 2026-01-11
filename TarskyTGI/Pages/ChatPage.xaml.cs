using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace TarskyTGI
{
    public sealed partial class ChatPage : Page
    {
        private TextGenerator? textGenerator;
        private bool modelLoaded = false;
        private string? currentImgPath = null; // Stores current image for the next prompt
        private string jsonPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\TarskyTGI\\chat.json";
        private JsonService jsonService = new JsonService();
        private string sysPrompt = "You are a helpful, respectful and honest assistant. Always answer as helpfully as possible, while being safe. Your answers should not include any harmful, unethical, racist, sexist, toxic, dangerous, or illegal content. Please ensure that your responses are socially unbiased and positive in nature. If a question does not make any sense, or is not factually coherent, explain why instead of answering something not correct. If you don't know the answer to a question, please don't share false information.";

        public ChatPage()
        {
            this.InitializeComponent();
            textGenerator = new TextGenerator();
            textGenerator.Initialize();
            _ = textGenerator.InsertSystemPromptAsync(sysPrompt);
            LoadJson();
        }

        private void LoadJson()
        {
            try
            {
                if (!File.Exists(jsonPath))
                {
                    jsonService.copyJsonToDocuments("chat.json");
                }

                string jsonString = File.ReadAllText(jsonPath);
                ChatClass? jsonToLoad = JsonSerializer.Deserialize<ChatClass>(jsonString);

                if (jsonToLoad is null)
                {
                    StatusTextBlock.Text = "Config invalid. Restoring defaults.";
                    jsonService.copyJsonToDocuments("chat.json");
                    return;
                }

                ModelBox.Text = jsonToLoad.model ?? "";
                ctxBox.Text = jsonToLoad.n_ctx.ToString();
                temperatureBox.Text = jsonToLoad.temperature.ToString();
                toppBox.Text = jsonToLoad.top_p.ToString();
                minpBox.Text = jsonToLoad.min_p.ToString();
                typicalpBox.Text = jsonToLoad.typical_p.ToString();
                // Attempt to restore format if possible, otherwise default to index 0
                // (ChatClass might need an update to store format, if not present we ignore)
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Config Load Error: " + ex.Message;
            }
        }

        private async void LoadModelButton_Click(object sender, RoutedEventArgs e)
        {
            int gpu_l = 0;
            if (!int.TryParse(gpuLayers.Text, out gpu_l))
            {
                StatusTextBlock.Text = "Invalid GPU layers number.";
                return;
            }

            string modelPath = ModelBox.Text;
            string mmprojPath = MmprojBox.Text; // Check if user provided a vision projector
            string selectedFormat = (ChatFormatCombo.SelectedItem as string) ?? ChatFormatCombo.Text;

            await LoadModelUnified(modelPath, mmprojPath, gpu_l, selectedFormat);
        }

        private async Task LoadModelUnified(string modelPath, string mmprojPath, int gpuLayers, string chatFormat)
        {
            StatusTextBlock.Text = "Loading model...";

            if (textGenerator == null)
            {
                StatusTextBlock.Text = "Backend not initialized.";
                return;
            }

            // Determine if we are loading standard LLM or Vision LLM
            bool isVision = !string.IsNullOrWhiteSpace(mmprojPath);

            if (isVision)
            {
                int ctx = int.TryParse(ctxBox.Text, out var c) ? c : 2048;
                // Call the Llava specific load
                var (success, message) = await textGenerator.LoadModelLlavaAsync(modelPath, mmprojPath, gpuLayers, ctx, chatFormat);
                modelLoaded = success;
                StatusTextBlock.Text = success ? "Vision Model Ready." : message;
            }
            else
            {
                // Call standard load
                var (success, message) = await textGenerator.LoadModelAsync(modelPath, gpuLayers, chatFormat);
                modelLoaded = success;
                StatusTextBlock.Text = success ? "Model Ready." : message;
            }
        }

        private async void ClearFN(object sender, RoutedEventArgs e)
        {
            if (textGenerator != null)
            {
                await textGenerator.ClearAsync(sysPrompt);
            }
            ChatHistory.Items.Clear();
            ClearImage();
        }

        private void ClearImage()
        {
            currentImgPath = null;
            imagePreview.Source = null;
            uploadStatus.Text = "No image selected.";
        }

        private async void SendFN(object sender, RoutedEventArgs e)
        {
            string rawInput = PromptBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(rawInput)) return;

            // Add user message to UI
            ChatHistory.Items.Add(rawInput + (currentImgPath != null ? " [Image Attached]" : ""));
            PromptBox.Text = string.Empty;

            if (!modelLoaded)
            {
                StatusTextBlock.Text = "Please load a model first.";
                return;
            }

            StatusTextBlock.Text = "Generating...";

            // Backend handles newline replacements if needed
            string preparedInput = rawInput.Replace("\r", "/[newline]");

            string generatedText = await GenerateText(preparedInput, currentImgPath);

            // Post-process output
            string outputString = generatedText.Replace("/[newline]", "\r");

            StatusTextBlock.Text = "Ready.";
            ChatHistory.Items.Add(outputString);

            // Clear image after single turn use (standard behavior for many chat interfaces, 
            // comment out ClearImage() if you want image persistence across turns)
            ClearImage();
        }

        private async Task<string> GenerateText(string inputText, string? imgPath)
        {
            if (textGenerator == null) return "Error: Backend not initialized.";
            return await textGenerator.GenerateTextAsync(inputText, imgPath);
        }

        private void PromptBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Enter sends, Shift+Enter adds newline
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
                if (!shiftState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                {
                    e.Handled = true;
                    SendFN(sender, e);
                }
            }
        }

        // --- Configuration & File Picking Logic ---

        private void ModelBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveJsonConfig();
        }

        private void SaveJsonConfig()
        {
            try
            {
                // Basic validation to prevent crash on empty strings
                int.TryParse(ctxBox.Text, out int n_ctx);
                int.TryParse("60", out int n_predict);
                float.TryParse(temperatureBox.Text, out float temp);
                float.TryParse(toppBox.Text, out float top_p);
                float.TryParse(minpBox.Text, out float min_p);
                float.TryParse(typicalpBox.Text, out float typ_p);
                int.TryParse(gpuLayers.Text, out int gpu); // saving gpu layers to min_p field in previous logic? 
                                                           // sticking to constructor signature provided in context:

                var chatClass = new ChatClass(
                    ModelBox.Text.Trim(),
                    "chatml", // Defaulting format in JSON for now
                    n_ctx,
                    n_predict,
                    temp,
                    top_p,
                    min_p,
                    typ_p,
                    gpu
                );

                string jsonString = JsonSerializer.Serialize(chatClass);
                File.WriteAllText(jsonPath, jsonString);
            }
            catch { /* Ignore serialization errors during typing */ }
        }

        private void TextBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            // Allow only digits
            args.Cancel = args.NewText.Any(c => !char.IsDigit(c));
        }

        private void TextBox2_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            // Allow digits and dots
            args.Cancel = args.NewText.Any(c => !char.IsDigit(c) && c != '.');
        }

        private async void selectModelButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializePicker(picker);
            picker.FileTypeFilter.Add(".gguf");
            picker.FileTypeFilter.Add(".bin");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null) ModelBox.Text = file.Path;
        }

        private async void selectMmprojButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializePicker(picker);
            picker.FileTypeFilter.Add(".gguf");
            picker.FileTypeFilter.Add(".bin");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null) MmprojBox.Text = file.Path;
        }

        private async void uploadButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializePicker(picker);
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                currentImgPath = file.Path;
                uploadStatus.Text = file.Name;
                imagePreview.Source = new BitmapImage(new Uri(file.Path));
            }
        }

        private void InitializePicker(FileOpenPicker picker)
        {
            var hwnd = WindowNative.GetWindowHandle(App.m_window);
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
        }

        private void priorityBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (textGenerator == null) return;

            // Map index to priority
            ProcessPriorityClass priority = priorityBox.SelectedIndex switch
            {
                0 => ProcessPriorityClass.Idle,
                1 => ProcessPriorityClass.BelowNormal,
                2 => ProcessPriorityClass.Normal,
                3 => ProcessPriorityClass.AboveNormal,
                4 => ProcessPriorityClass.High,
                5 => ProcessPriorityClass.RealTime,
                _ => ProcessPriorityClass.Normal
            };
            textGenerator.SetProcessPriority(priority);
        }
    }
}