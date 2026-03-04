using ABI.System;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Protection.PlayReady;

namespace TarskyTGI
{
    public sealed class TextGenerator : IDisposable
    {
        private Process? serverProcess;
        private HttpClient httpClient = new HttpClient();
        private int port = 8080;

        public bool IsModelLoaded { get; private set; } = false;

        public TextGenerator()
        {
        }

        public void Initialize()
        {
            // The server is started when a model is loaded
        }

        public async Task<(bool success, string message)> LoadModelAsync(string modelPath, int gpuLayers, int ctx, string? chatFormat = null, System.TimeSpan? timeout = null)
        {
            if (serverProcess != null && !serverProcess.HasExited)
            {
                serverProcess.Kill(true);
            }

            var arguments = new List<string>
            {
                $"-m \"{modelPath}\"",
                $"--port {port}",
                $"-np 1",
                $"--metrics",
                $"-c {ctx}",
                $"-ngl {gpuLayers}"
            };
            
             if(!string.IsNullOrEmpty(chatFormat) && chatFormat != "default")
            {
                arguments.Add($"--chat-template {chatFormat}");
            }



            var startInfo = new ProcessStartInfo
            {
                FileName = "CompiledLlama\\llama-server.exe",
                Arguments = string.Join(" ", arguments),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            serverProcess = new Process { StartInfo = startInfo };
            serverProcess.Start();

            // ping address until 404
            string address = $"http://localhost:{port}/nonexistentpage";
            int attempt = 0;
            int maxattempts = 10;
            while (attempt <= maxattempts)
            {
                attempt++;
                try
                {
                    HttpResponseMessage response = await httpClient.GetAsync(address);
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Console.WriteLine($"✅ Success on attempt #{attempt}: Received 404 Not Found!");
                        IsModelLoaded = true;
                        return (true, "Ready.");
                    }
                }
                catch (System.Exception ex)
                {
                    if (attempt >= 30)
                    {
                        return (false, ex.Message);
                    }
                }
            }
            return (false, $"Ran out of attempts to connect. {maxattempts} retries.");
        }

        public async Task<(bool success, string message)> LoadModelLlavaAsync(string modelPath, string mmproj, int gpuLayers, int ctx, string chatFormat, System.TimeSpan? timeout = null)
        {
            if (serverProcess != null && !serverProcess.HasExited)
            {
                serverProcess.Kill(true);
            }
            Thread.Sleep(10);

            var arguments = new List<string>
            {
                $"-m \"{modelPath}\"",
                $"--port {port}",
                $"-np 1",
                $"-c {ctx}",
                $"--metrics",
                $"-ngl {gpuLayers}"
            };

            if (!string.IsNullOrEmpty(mmproj))
            {
                arguments.Add($"--mmproj \"{mmproj}\"");
            }
            if (!string.IsNullOrEmpty(chatFormat) && chatFormat != "default")
            {
                arguments.Add($"--chat-template {chatFormat}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "CompiledLlama\\llama-server.exe",
                Arguments = string.Join(" ", arguments),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            serverProcess = new Process { StartInfo = startInfo };
            serverProcess.Start();

            // ping address until 404
            string address = $"http://localhost:{port}/nonexistentpage";
            int attempt = 0;
            int maxattempts = 10;
            while (attempt <= maxattempts)
            {
                attempt++;
                try
                {
                    HttpResponseMessage response = await httpClient.GetAsync(address);
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Console.WriteLine($"✅ Success on attempt #{attempt}: Received 404 Not Found!");
                        IsModelLoaded = true;
                        return (true, "Ready.");
                    }
                }
                catch (System.Exception ex)
                {
                    if (attempt >= 30)
                    {
                        return (false, ex.Message);
                    }
                }
            }
            return (false, $"Ran out of attempts to connect. {maxattempts} retries.");
        }

        public async Task<string> GenerateTextAsync(string inputText, string? imagePath = null, int? num_predict = -1, System.TimeSpan? timeout = null)
        {
            if (!IsModelLoaded || serverProcess == null || serverProcess.HasExited)
            {
                return "Error: Model not loaded.";
            }

            var payload = new
            {
                prompt = inputText,
                n_predict = num_predict,
                temperature = 0.8,
                stream = false
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync($"http://localhost:{port}/completion", content);
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync();
                var responseObject = JsonConvert.DeserializeObject<CompletionResponse>(responseString);
                return responseObject?.Content ?? "Error: Empty response from server.";
            }
            catch (HttpRequestException e)
            {
                return $"Error: {e.Message}";
            }
        }

        public async Task<string> GenerateChatCompletionAsync(List<ChatMessage> messages, double temperature = 0.8, 
            double topP = 0.95, 
            double minP = 0.05, 
            double typicalP = 1.0,
            string? imagePath = null,
            System.TimeSpan? timeout = null)
        {
            if (!IsModelLoaded || serverProcess == null || serverProcess.HasExited)
            {
                return "Error: Model not loaded.";
            }

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            var payload = new
            {
                messages = messages,
                n_predict = -1,
                temperature = temperature,
                top_p = topP,
                min_p = minP,
                typical_p = typicalP,
                stream = false
            };

            var jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload, options);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync($"http://localhost:{port}/chat/completions", content);
                response.EnsureSuccessStatusCode(); // fails here if image included
                var responseString = await response.Content.ReadAsStringAsync();

                var responseObject = System.Text.Json.JsonSerializer.Deserialize<ChatCompletionResponse>(responseString, options);

                var choice = responseObject?.Choices?.FirstOrDefault();
                string reply = choice?.Message?.Content ?? "Error: Empty response from server.";
                return reply;
            }
            catch (HttpRequestException e)
            {
                return $"Error: {e.Message}";
            }
        }

        public static async Task<string> GetChatCompletionAsync(
            List<ChatMessage> messages,
            string apiKey,
            string model,
            double? temperature = 0.8,
            double? topP = 0.95,
            int? maxTokens = null,
            string baseUrl = "https://api.openai.com/v1",
            CancellationToken cancellationToken = default)
        {
            using var openAiClient = new HttpClient();
            openAiClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            openAiClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            var requestBody = new
            {
                model,
                messages = messages.ConvertAll(m => new
                {
                    role = m.Role.ToLowerInvariant(),
                    content = string.Join("\n", m.Content.ConvertAll(cp => cp.Text ?? (cp.ImageUrl != null ? cp.ImageUrl.Url : "")))
                }),
                temperature,
                top_p = topP,
                max_tokens = maxTokens,
                stream = false
            };

            var json = System.Text.Json.JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition =
                    System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            string endpoint = $"{baseUrl.TrimEnd('/')}/chat/completions";

            HttpResponseMessage response;

            try
            {
                response = await openAiClient
                    .PostAsync(endpoint, content, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new System.Exception($"Failed to connect to API: {ex.Message}", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                string errorBody =
                    await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"API error {response.StatusCode}: {errorBody}");
            }

            string responseJson =
                await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(responseJson);

            var choices = doc.RootElement.GetProperty("choices");

            if (choices.GetArrayLength() == 0)
                throw new System.Exception("No choices returned from API");

            var message = choices[0].GetProperty("message");

            string assistantReply =
                message.GetProperty("content").GetString() ?? string.Empty;

            return assistantReply.Trim();

            // Ensures compiler sees all paths returning
            throw new InvalidOperationException("Unexpected execution path.");
        }

        public void SetProcessPriority(ProcessPriorityClass priority)
        {
            if (serverProcess != null)
            {
                try { serverProcess.PriorityClass = priority; } catch { }
            }
        }

        public void Dispose()
        {
            try
            {
                if (serverProcess != null && !serverProcess.HasExited)
                {
                    serverProcess.Kill(true);
                }
            }
            catch { }
            serverProcess?.Dispose();
            httpClient?.Dispose();
        }

        public static ChatMessage CreateMessage(string role, string textContent, string? imagePath = null)
        {
            if (imagePath == null)
            {
                return new ChatMessage
                {
                    Role = role,
                    Content = new List<ContentPart>
                    {
                        new ContentPart
                        {
                            Type = "text",
                            Text = textContent
                        }
                    }
                };
            }

            if (string.IsNullOrWhiteSpace(imagePath))
                throw new ArgumentException("Image path cannot be empty.", nameof(imagePath));

            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Image file not found.", imagePath);

            // Read and convert image to base64
            byte[] imageBytes = File.ReadAllBytes(imagePath);
            string base64 = Convert.ToBase64String(imageBytes);

            // Auto-detect correct MIME type
            string mimeType = GetImageMimeType(imagePath);
            string dataUrl = $"data:{mimeType};base64,{base64}";

            return new ChatMessage
            {
                Role = role,
                Content = new List<ContentPart>
                {
                    new ContentPart
                    {
                        Type = "text",
                        Text = textContent
                    },
                    new ContentPart
                    {
                        Type = "image_url",
                        ImageUrl = new ImageUrl { Url = dataUrl }
                    }
                }
            };
        }

        private static string GetImageMimeType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "image/jpeg" // fallback
            };
        }

        private class CompletionResponse
        {
            public string? Content { get; set; }
        }

        public class ChatMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("content")]
            public List<ContentPart> Content { get; set; } = new();
        }

        public class ContentPart
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;

            [JsonPropertyName("text")]
            public string? Text { get; set; }

            [JsonPropertyName("image_url")]
            public ImageUrl? ImageUrl { get; set; }
        }

        public class ImageUrl
        {
            [JsonPropertyName("url")]
            public string Url { get; set; } = string.Empty;
        }
        private class ChatCompletionResponse
        {
            public List<Choice>? Choices { get; set; }   // note capital C
        }

        private class Choice
        {
            public ResponseMessage? Message { get; set; }
        }

        private class ResponseMessage
        {
            public string? Role { get; set; }
            public string? Content { get; set; }   // ← string, not List!
        }
    }
}
