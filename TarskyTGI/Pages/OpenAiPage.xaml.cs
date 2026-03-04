using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.Chat;

namespace TarskyTGI.Pages
{
    public sealed partial class OpenAiPage : Page
    {
        private string apiKey = string.Empty;
        private string baseUrl = "https://api.openai.com/v1";
        private string model = "gpt-4o-mini";
        private string systemPrompt = "You are a helpful assistant.";

        private readonly List<TextGenerator.ChatMessage> conversationHistory = new();
        private readonly string jsonPath;
        private readonly JsonService jsonService = new JsonService();

        public OpenAiPage()
        {
            this.InitializeComponent();
            jsonPath = jsonService.GetJsonFilePath("chat.json");
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                jsonService.EnsureJsonExists("chat.json", "chat.json");
                if (!File.Exists(jsonPath)) return;

                string json = File.ReadAllText(jsonPath);
                var config = JsonSerializer.Deserialize<OpenAIConfig>(json);

                if (config is null) return;

                apiKey = config.ApiKey ?? "";
                baseUrl = config.BaseUrl ?? "https://api.openai.com/v1";
                model = config.Model ?? "gpt-4o-mini";
                systemPrompt = config.SystemPrompt ?? "You are a helpful assistant.";

                ApiKeyBox.Password = apiKey;
                BaseUrlBox.Text = baseUrl;
                ModelBox.Text = model;
                systemPromptTextBox.Text = systemPrompt;
                temperatureBox.Text = config.Temperature.ToString("0.##");
                toppBox.Text = config.TopP.ToString("0.##");
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Load error: {ex.Message}";
            }
        }

        private void SaveSettings()
        {
            try
            {
                var config = new OpenAIConfig
                {
                    ApiKey = apiKey,
                    BaseUrl = baseUrl,
                    Model = model,
                    SystemPrompt = systemPrompt,
                    Temperature = double.TryParse(temperatureBox.Text, out var t) ? t : 0.8,
                    TopP = double.TryParse(toppBox.Text, out var p) ? p : 0.95
                };

                File.WriteAllText(jsonPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* silent fail is fine for settings */ }
        }

        private async void SendFN(object sender, RoutedEventArgs e)
        {
            string input = PromptBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(input)) return;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                StatusTextBlock.Text = "⚠️ Please enter your API Key";
                return;
            }

            // Add user message to UI + history
            ChatHistory.Items.Add($"You: {input}");
            conversationHistory.Add(new TextGenerator.ChatMessage { role = "user", content=input });
            PromptBox.Text = string.Empty;

            StatusTextBlock.Text = "Thinking...";

            try
            {
                var messages = new List<TextGenerator.ChatMessage>
            {
                new TextGenerator.ChatMessage { role = "system", content = systemPromptTextBox.Text }
            };
                messages.AddRange(conversationHistory);

                string reply = await TextGenerator.GetChatCompletionAsync(
                    messages: messages,
                    apiKey: apiKey,
                    model: model,
                    temperature: double.TryParse(temperatureBox.Text, out var temp) ? temp : 0.8,
                    topP: double.TryParse(toppBox.Text, out var tp) ? tp : 0.95,
                    baseUrl: baseUrl);

                ChatHistory.Items.Add($"Assistant: {reply}");
                conversationHistory.Add(new TextGenerator.ChatMessage { role = "assistant", content = reply });

                StatusTextBlock.Text = "Ready";
            }
            catch (Exception ex)
            {
                ChatHistory.Items.Add($"Error: {ex.Message}");
                StatusTextBlock.Text = "Error";
            }
        }

        private void ClearFN(object sender, RoutedEventArgs e)
        {
            ChatHistory.Items.Clear();
            conversationHistory.Clear();
            StatusTextBlock.Text = "Conversation cleared";
        }

        // ── Settings changed handlers (auto-save) ──
        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            apiKey = ApiKeyBox.Password;
            SaveSettings();
        }

        private void BaseUrlBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            baseUrl = BaseUrlBox.Text.Trim();
            SaveSettings();
        }

        private void ModelBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            model = ModelBox.Text.Trim();
            SaveSettings();
        }

        private void systemPromptTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            systemPrompt = systemPromptTextBox.Text;
            SaveSettings();
        }

        private void PromptBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter &&
                !Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                    .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            {
                e.Handled = true;
                SendFN(sender, null!);
            }
        }

        private void TextBox2_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            args.Cancel = args.NewText.Any(c => !char.IsDigit(c) && c != '.');
        }

        // Remove blue selection background
        private void ChatHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ((ListBox)sender).SelectedIndex = -1;
        }
    }

    // ── Config saved to chat.json ──
    public class OpenAIConfig
    {
        public string? ApiKey { get; set; }
        public string? BaseUrl { get; set; } = "https://api.openai.com/v1";
        public string? Model { get; set; } = "gpt-4o-mini";
        public string? SystemPrompt { get; set; }
        public double Temperature { get; set; } = 0.8;
        public double TopP { get; set; } = 0.95;
    }
}