using LLama;
using LLama.Abstractions;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using LLama.Batched;
using LLama.Extensions;
using Microsoft.UI.Content;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TarskyTGI
{
    public class TextGenerator : IDisposable
    {
        private readonly ConcurrentQueue<string> messages = new();
        private readonly SemaphoreSlim messageSignal = new(0);
        private readonly CancellationTokenSource readLoopCts = new();

        private LLamaWeights? model;
        private LLamaWeights? clipModel;
        private LLamaContext? context;
        private InteractiveExecutor? executor;
        private ChatSession? chatSession;
        private InferenceParams? inferenceParams;
        private bool isDisposed = false;

        public bool IsModelLoaded { get; private set; } = false;

        public async Task InsertSystemPromptAsync(string sysPrompt)
        {

        }

        public async Task<(bool success, string message)> LoadModelAsync(string modelPath, int gpuLayers = 0, int contextSize = 2048, TimeSpan? timeout = null)
        {
            try
            {
                var parameters = new ModelParams(modelPath)
                {
                    ContextSize = unchecked((uint)contextSize),
                    GpuLayerCount = gpuLayers
                };
                model = LLamaWeights.LoadFromFile(parameters);
                context = model.CreateContext(parameters);
                executor = new InteractiveExecutor(context);

                return (true, "Model loaded successfuly.");
            }
            catch (Exception ex)
            {
                return (false, $"I can't load. {ex.Message}");
            }

        }

        // New LLava-specific load signature: modelPath, mmproj, layers, ctx, cformat
        public async Task<(bool success, string message)> LoadModelLlavaAsync(string modelPath, string mmproj, int gpuLayers, int contextSize = 2048, TimeSpan? timeout = null)
        {
            try
            {
                var parameters = new ModelParams(modelPath)
                {
                    ContextSize = unchecked((uint)contextSize),
                    GpuLayerCount = gpuLayers
                };
                model = LLamaWeights.LoadFromFile(parameters);
                context = model.CreateContext(parameters);

                var clipModelNew = LLavaWeights.LoadFromFile(mmproj);

                executor = new InteractiveExecutor(context, clipModelNew);

                return (true, "Model loaded successfuly.");
            }
            catch (Exception ex)
            {
                return (false, $"I can't load. {ex.Message}");
            }
        }

        public async Task ClearAsync(string sysPrompt)
        {

        }

        // Overload: support optional image path
        public async Task<string> GenerateTextAsync(string inputText, string? imagePath = null, TimeSpan? timeout = null)
        {
            string answer = String.Empty;

            return answer;
        }

        public void SetProcessPriority(ProcessPriorityClass priority)
        {

        }

        public void Dispose()
        {
            model = null;
            context = null;
            executor = null;
        }
    }
}
