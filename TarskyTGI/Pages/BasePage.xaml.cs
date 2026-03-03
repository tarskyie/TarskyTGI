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
        private TextGenerator textGenerator = new TextGenerator();
        private JsonService jsonService = new JsonService();
        private bool modelLoaded = false;

        public BasePage()
        {
            this.InitializeComponent();
            textGenerator.Initialize();
            loadJson();
            if (App.m_window != null)
            {
                App.m_window.Closed += Window_Closed;
                this.Unloaded += BasePage_Unloaded;
            }
        }

        private void loadJson()
        {
            try
            {
                // Ensure the basestuff.json exists in Documents; if not copy default from app folder
                jsonService.EnsureJsonExists("basestuff.json", "basestuff.json");
                string path = jsonService.GetJsonFilePath("basestuff.json");
                if (!File.Exists(path))
                {
                    StatusTextBlock.Text = "Configuration file not found; using defaults.";
                    return;
                }

                string jsonString = File.ReadAllText(path);
                ChatClass? jsonToLoad = null;
                try
                {
                    jsonToLoad = JsonSerializer.Deserialize<ChatClass?>(jsonString);
                }
                catch (Exception ex)
                {
                    StatusTextBlock.Text = "Chat configuration is invalid: " + ex.Message;
                }

                if (jsonToLoad is null)
                {
                    StatusTextBlock.Text = "Chat configuration is empty or invalid. Restoring default configuration.";
                    // ensure default exists in Documents
                    jsonService.EnsureJsonExists("basestuff.json", "basestuff.json");
                    return;
                }

                ModelBox.Text = jsonToLoad.model ?? string.Empty;
                ctxBox.Text = jsonToLoad.n_ctx.ToString();
                predictBox.Text = jsonToLoad.n_predict.ToString();
                temperatureBox.Text = jsonToLoad.temperature.ToString();
                toppBox.Text = jsonToLoad.top_p.ToString();
                minpBox.Text = jsonToLoad.min_p.ToString();
                typicalpBox.Text = jsonToLoad.typical_p.ToString();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Config Load Error: " + ex.Message;
            }
        }

        private async void LoadModelButton_Click(object sender, RoutedEventArgs e)
        {
            //string modelPath = "C:/Users/ivany/Downloads/Phi-3.1-mini-4k-instruct-Q4_K_L.gguf";
            string modelPath = ModelBox.Text;
            await LoadModel(modelPath);
        }

        private async Task LoadModel(string modelPath)
        {
            StatusTextBlock.Text = "Loading.";
            var (success, message) = await textGenerator.LoadModelAsync(modelPath, 0, "chatml");
            if (success)
            {
                modelLoaded = true;
                StatusTextBlock.Text = "Ready.";
            }
            else
            {
                modelLoaded = false;
                StatusTextBlock.Text = $"Failed to load model: {message}";
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
                PromptBox.Text = string.Empty;
                mainText.Text=mainText.Text+PromptBox.Text.Trim();
                if (!modelLoaded)
                {
                    StatusTextBlock.Text = "Please load a model first.";
                    return;
                }

                StatusTextBlock.Text = "Generating the text";

                string inputText = PromptBox.Text.Trim();
                string itemsAsString = mainText.Text;
                string generatedText = await textGenerator.GenerateTextAsync(itemsAsString + inputText);
                string outputString = generatedText.Replace("\\n", "\n");
                //string outputString = generatedText;
                mainText.Text += outputString;

                StatusTextBlock.Text = "Ready.";
            }
            else
            {
                StatusTextBlock.Text = "Empty prompt";
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

        string GetListBoxItemsAsNewlineSeparatedString(ListBox listBox)
        {
            return string.Join("\\n", listBox.Items.Cast<object>().Select(item => item.ToString()));
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            textGenerator.Dispose();
        }

        private void BasePage_Unloaded(object? sender, RoutedEventArgs e)
        {
            if (App.m_window != null)
            {
                App.m_window.Closed -= Window_Closed;
                this.Unloaded -= BasePage_Unloaded;
            }
            textGenerator.Dispose();
        }

        //Additional SideBar stuff
        private void ModelBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                int n_ctx = int.TryParse(ctxBox.Text, out var tmpCtx) ? tmpCtx : 1024;
                int n_predict = int.TryParse(predictBox.Text, out var tmpPredict) ? tmpPredict : 128;
                float temperature = float.TryParse(temperatureBox.Text, out var tmpTemp) ? tmpTemp : 0.8f;
                float top_p = float.TryParse(toppBox.Text, out var tmpTopP) ? tmpTopP : 0.95f;
                float min_p = float.TryParse(minpBox.Text, out var tmpMinP) ? tmpMinP : 0.05f;
                float typical_p = float.TryParse(typicalpBox.Text, out var tmpTypicalP) ? tmpTypicalP : 1.0f;

                var chatClass = new ChatClass(ModelBox.Text.Trim(), "chatml", n_ctx, n_predict, temperature, top_p, min_p, typical_p, 35);

                string jsonString = JsonSerializer.Serialize(chatClass);

                string path = jsonService.GetJsonFilePath("basestuff.json");
                File.WriteAllText(path, jsonString);
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
