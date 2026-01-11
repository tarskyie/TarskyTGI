using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TarskyTGI
{
    public sealed class TextGenerator : IDisposable
    {
        private Process? pythonProcess;
        private StreamWriter? pythonInput;
        private StreamReader? pythonOutput;
        private readonly ConcurrentQueue<string> messages = new();
        private readonly SemaphoreSlim messageSignal = new(0);
        private readonly CancellationTokenSource readLoopCts = new();

        public bool IsModelLoaded { get; private set; } = false;

        public TextGenerator()
        {
        }

        public void Initialize()
        {
            if (pythonProcess != null) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = "-u -X utf8 textgenerator.py",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardInputEncoding = Encoding.UTF8;
            startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";

            pythonProcess = new Process { StartInfo = startInfo };
            pythonProcess.Start();

            pythonInput = pythonProcess.StandardInput;
            pythonOutput = pythonProcess.StandardOutput;

            // Start background read loop
            Task.Run(ReadLoopAsync, readLoopCts.Token);
        }

        private async Task ReadLoopAsync()
        {
            try
            {
                if (pythonOutput == null) return;

                while (!readLoopCts.IsCancellationRequested)
                {
                    string? line = await pythonOutput.ReadLineAsync();
                    if (line == null) break;
                    messages.Enqueue(line);
                    messageSignal.Release();
                }
            }
            catch
            {
                // swallow - Dispose will handle cleanup
            }
        }

        private async Task<string?> WaitForPrefixAsync(string[] prefixes, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                // wait until a message is available or timeout shorter
                var remaining = timeout - sw.Elapsed;
                if (remaining <= TimeSpan.Zero) break;
                try
                {
                    if (!await messageSignal.WaitAsync(remaining)) break;
                }
                catch (OperationCanceledException) { break; }

                if (messages.TryDequeue(out var msg))
                {
                    foreach (var p in prefixes)
                    {
                        if (msg.StartsWith(p)) return msg;
                    }
                }
            }
            return null;
        }

        public async Task InsertSystemPromptAsync(string sysPrompt)
        {
            if (pythonInput == null) return;
            await pythonInput.WriteLineAsync("system");
            await pythonInput.WriteLineAsync(sysPrompt);
            await pythonInput.FlushAsync();
        }

        public async Task<(bool success, string message)> LoadModelAsync(string modelPath, int gpuLayers, string chatFormat, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(60);
            if (pythonInput == null) return (false, "Backend not initialized");

            await pythonInput.WriteLineAsync("load");
            await pythonInput.WriteLineAsync(modelPath);
            await pythonInput.WriteLineAsync(gpuLayers.ToString());
            await pythonInput.WriteLineAsync(chatFormat);
            await pythonInput.FlushAsync();

            var response = await WaitForPrefixAsync(new[] {"$model_loaded$", "$model_load_error$"}, timeout.Value);
            if (response == null) return (false, "No response from backend.");

            if (response.StartsWith("$model_loaded$"))
            {
                IsModelLoaded = true;
                return (true, "Ready.");
            }
            else
            {
                IsModelLoaded = false;
                var idx = response.IndexOf(':');
                var msg = idx >= 0 ? response.Substring(idx + 1) : response;
                return (false, msg);
            }
        }

        // New LLava-specific load signature: modelPath, mmproj, layers, ctx, cformat
        public async Task<(bool success, string message)> LoadModelLlavaAsync(string modelPath, string mmproj, int gpuLayers, int ctx, string cformat, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(60);
            if (pythonInput == null) return (false, "Backend not initialized");

            await pythonInput.WriteLineAsync("load");
            await pythonInput.WriteLineAsync(modelPath);
            await pythonInput.WriteLineAsync(mmproj ?? string.Empty);
            await pythonInput.WriteLineAsync(gpuLayers.ToString());
            await pythonInput.WriteLineAsync(ctx.ToString());
            await pythonInput.WriteLineAsync(cformat ?? string.Empty);
            await pythonInput.FlushAsync();

            var response = await WaitForPrefixAsync(new[] {"$model_loaded$", "$model_load_error$"}, timeout.Value);
            if (response == null) return (false, "No response from backend.");

            if (response.StartsWith("$model_loaded$"))
            {
                IsModelLoaded = true;
                return (true, "Ready.");
            }
            else
            {
                IsModelLoaded = false;
                var idx = response.IndexOf(':');
                var msg = idx >= 0 ? response.Substring(idx + 1) : response;
                return (false, msg);
            }
        }

        public async Task ClearAsync(string sysPrompt)
        {
            if (pythonInput == null) return;
            await pythonInput.WriteLineAsync("clear");
            await pythonInput.WriteLineAsync("append");
            await pythonInput.WriteLineAsync("system");
            await pythonInput.WriteLineAsync(sysPrompt);
            await pythonInput.FlushAsync();
            // don't wait for any ack - backend may not send one for clear
        }

        // Overload: support optional image path
        public async Task<string> GenerateTextAsync(string inputText, string? imagePath = null, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(120);
            if (pythonInput == null) return "Error: Backend not initialized.";

            await pythonInput.WriteLineAsync("chat");
            await pythonInput.WriteLineAsync(inputText);
            // always send an image line to keep protocol consistent
            await pythonInput.WriteLineAsync(imagePath ?? "None");
            await pythonInput.FlushAsync();

            var response = await WaitForPrefixAsync(new[] {"$response$", "$not_loaded$", "$error$"}, timeout.Value);
            if (response == null) return "Error: No response from backend.";

            if (response.StartsWith("$not_loaded$"))
            {
                return "Error: Model not loaded.";
            }
            else if (response.StartsWith("$response$"))
            {
                var idx = response.IndexOf(':');
                var content = idx >= 0 ? response.Substring(idx + 1) : string.Empty;
                // replace any placeholder newline tokens with environment newlines
                content = content.Replace("/[newline]", Environment.NewLine);

                // Collect subsequent non-control lines (if the backend streams the rest as plain lines)
                // but stop if the next message starts with '$'
                while (true)
                {
                    // peek if there is a message already queued
                    if (messageSignal.CurrentCount == 0) break;
                    // wait a short time to see if there are more lines
                    if (!await messageSignal.WaitAsync(TimeSpan.FromMilliseconds(50))) break;
                    if (messages.TryDequeue(out var next))
                    {
                        if (next.StartsWith("$"))
                        {
                            // push it back by enqueuing and releasing
                            messages.Enqueue(next);
                            messageSignal.Release();
                            break;
                        }
                        content += Environment.NewLine + next;
                    }
                }

                return content;
            }
            else if (response.StartsWith("$error$"))
            {
                var idx = response.IndexOf(':');
                var msg = idx >= 0 ? response.Substring(idx + 1) : response;
                return $"Error: {msg}";
            }

            return response;
        }

        public void SetProcessPriority(ProcessPriorityClass priority)
        {
            if (pythonProcess != null)
            {
                try { pythonProcess.PriorityClass = priority; } catch { }
            }
        }

        public void Dispose()
        {
            try
            {
                readLoopCts.Cancel();
            }
            catch { }

            try
            {
                if (pythonProcess != null && !pythonProcess.HasExited)
                {
                    pythonProcess.Kill(true);
                }
            }
            catch { }

            try { pythonInput?.Dispose(); } catch { }
            try { pythonOutput?.Dispose(); } catch { }
            try { pythonProcess?.Dispose(); } catch { }
            try { messageSignal.Dispose(); } catch { }
            try { readLoopCts.Dispose(); } catch { }
        }
    }
}
