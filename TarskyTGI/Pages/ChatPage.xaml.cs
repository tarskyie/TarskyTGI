using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Media.Devices;
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
        private string jsonPath;
        private JsonService jsonService = new JsonService();

        public ChatPage()
        {
            this.InitializeComponent();
            textGenerator = new TextGenerator();
            textGenerator.Initialize();

            jsonPath = jsonService.GetJsonFilePath("chat.json");
            LoadJson();
        }

        private void LoadJson()
        {
            try
            {
                jsonService.EnsureJsonExists("chat.json", "chat.json");

                if (!File.Exists(jsonPath))
                {
                    StatusTextBlock.Text = "Config file missing; created default.";
                    return;
                }

                string jsonString = File.ReadAllText(jsonPath);
                ChatClass? jsonToLoad = null;
                try
                {
                    jsonToLoad = JsonSerializer.Deserialize<ChatClass?>(jsonString);
                }
                catch (Exception ex)
                {
                    StatusTextBlock.Text = "Config invalid: " + ex.Message;
                }

                if (jsonToLoad is null)
                {
                    StatusTextBlock.Text = "Config invalid or empty; using defaults.";
                    return;
                }

                ModelBox.Text = jsonToLoad.model ?? string.Empty;
                ctxBox.Text = jsonToLoad.n_ctx.ToString();
                temperatureBox.Text = jsonToLoad.temperature.ToString();
                toppBox.Text = jsonToLoad.top_p.ToString();
                minpBox.Text = jsonToLoad.min_p.ToString();
                typicalpBox.Text = jsonToLoad.typical_p.ToString();
                // Restore other fields if present
                gpuLayers.Text = jsonToLoad.layers.ToString();
                // format could be used to set ChatFormatCombo
                if (!string.IsNullOrEmpty(jsonToLoad.format))
                {
                    // try to set ComboBox text or select matching item
                    bool matched = false;
                    for (int i = 0; i < ChatFormatCombo.Items.Count; i++)
                    {
                        if ((ChatFormatCombo.Items[i] as string) == jsonToLoad.format)
                        {
                            ChatFormatCombo.SelectedIndex = i;
                            matched = true;
                            break;
                        }
                    }
                    if (!matched)
                    {
                        ChatFormatCombo.Text = jsonToLoad.format;
                    }
                }

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
                var (success, message) = await textGenerator.LoadModelAsync(modelPath, gpuLayers, chatFormat: chatFormat, ctx: int.Parse(ctxBox.Text));
                modelLoaded = success;
                StatusTextBlock.Text = success ? "Model Ready." : message;
            }
        }

        private async void ClearFN(object sender, RoutedEventArgs e)
        {
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

            var messages = new List<TextGenerator.ChatMessage>
            {
                TextGenerator.CreateMessage(role: "system", textContent: systemPromptTextBox.Text)
            };

            for (int i = 0; i < ChatHistory.Items.Count; i++)
            {
                var item = ChatHistory.Items[i] as string;
                if (item != null)
                {
                    if (i % 2 == 0)
                    {
                        // Replace direct object initializer with CreateMessage helper
                        messages.Add(TextGenerator.CreateMessage("user", item, imagePath: currentImgPath));
                    
                    }
                    else
                    {
                        messages.Add(TextGenerator.CreateMessage("assistant", item));
                    }
                }
            }


            string generatedText = await GenerateChat(messages);

            // Post-process output
            string outputString = generatedText.Replace("/[newline]", "\r");

            StatusTextBlock.Text = "Ready.";
            ChatHistory.Items.Add(outputString);

            // Clear image after single turn use (standard behavior for many chat interfaces, 
            // comment out ClearImage() if you want image persistence across turns)
            ClearImage();
        }

        private async Task<string> GenerateChat(List<TextGenerator.ChatMessage> messages)
        {
            if (textGenerator == null) return "Error: Backend not initialized.";
            return await textGenerator.GenerateChatCompletionAsync(messages, temperature: double.Parse(temperatureBox.Text), topP: double.Parse(toppBox.Text),
                minP: double.Parse(minpBox.Text),
                typicalP: double.Parse(typicalpBox.Text),
                imagePath: currentImgPath);
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
                // ChatPage UI does not expose n_predict; use a sensible default
                int n_predict = 128;
                float.TryParse(temperatureBox.Text, out float temp);
                float.TryParse(toppBox.Text, out float top_p);
                float.TryParse(minpBox.Text, out float min_p);
                float.TryParse(typicalpBox.Text, out float typ_p);
                int.TryParse(gpuLayers.Text, out int gpu);

                var chatClass = new ChatClass(
                    ModelBox.Text.Trim(),
                    ChatFormatCombo.Text,
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
            catch (Exception ex)
            {
                StatusTextBlock.Text = ex.Message;
            }
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

        private void ChatHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ((ListBox)sender).SelectedIndex = -1;
        }

        private void ctxBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ctxSlider.Value = int.Parse(ctxBox.Text);
            } catch { }
        }

        private void temperatureBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                temperatureSlider.Value = float.Parse(temperatureBox.Text);
            } catch { }
        }

        private void toppBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                toppSlider.Value = float.Parse(toppBox.Text);
            } catch { }
        }

        private void minpBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                minpSlider.Value = float.Parse(minpBox.Text);
            } catch { }
        }

        private void typicalpBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                typicalpSlider.Value = float.Parse(typicalpBox.Text);
            } catch { }
        }

        private void temperatureSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            temperatureBox.Text = Math.Round(temperatureSlider.Value, 2).ToString();
        }

        private void ctxSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            ctxBox.Text = ctxSlider.Value.ToString();
        }

        private void toppSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            toppBox.Text = Math.Round(toppSlider.Value, 2).ToString();
        }

        private void minpSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            minpBox.Text = Math.Round(minpSlider.Value, 2).ToString();
        }

        private void typicalpSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            typicalpBox.Text = Math.Round(typicalpSlider.Value, 2).ToString();
        }
    }
}