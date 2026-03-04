using ABI.System;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

        public async Task<(bool success, string message)> LoadModelAsync(string modelPath, int gpuLayers, string chatFormat, System.TimeSpan? timeout = null)
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

        public async Task<string> GenerateChatCompletionAsync(List<ChatMessage> messages, System.TimeSpan? timeout = null)
        {
            if (!IsModelLoaded || serverProcess == null || serverProcess.HasExited)
            {
                return "Error: Model not loaded.";
            }

            var payload = new
            {
                messages = messages,
                n_predict = -1,
                temperature = 0.8,
                stream = false
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync($"http://localhost:{port}/chat/completions", content);
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync();
                var responseObject = JsonConvert.DeserializeObject<ChatCompletionResponse>(responseString);
                return responseObject?.choices[0]?.message?.content ?? "Error: Empty response from server.";
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
                    role = m.role.ToLowerInvariant(),
                    content = m.content
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

        private class CompletionResponse
        {
            public string? Content { get; set; }
        }

        public class ChatMessage
        {
            public string role { get; set; }
            public string content { get; set; }
        }

        private class ChatCompletionResponse
        {
            public List<Choice>? choices { get; set; }
        }

        private class Choice
        {
            public ChatMessage? message { get; set; }
        }
    }
}
