using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SimpleLauncher.ResourceTranslator.Models;

namespace SimpleLauncher.ResourceTranslator.Services;

/// <summary>
///     Provides translation services using the OpenRouter API (OpenAI-compatible).
/// </summary>
public class OpenRouterTranslationService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    private readonly string _apiKey;
    private readonly string _modelId;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OpenRouterTranslationService" /> class.
    /// </summary>
    /// <param name="apiKey">The OpenRouter API key.</param>
    /// <param name="modelId">The model identifier to use for translations.</param>
    public OpenRouterTranslationService(string apiKey, string modelId)
    {
        _apiKey = apiKey;
        _modelId = modelId;
    }

    /// <summary>
    ///     Returns the list of available OpenRouter models for translation.
    /// </summary>
    /// <returns>A list of available model information.</returns>
    public static IList<OpenRouterModelInfo> GetAvailableModels()
    {
        return
        [
            new OpenRouterModelInfo
            {
                Id = "z-ai/glm-5.3-flash",
                Name = "z-ai/glm-5.3-flash",
                Description = "GLM-5.3-Flash. $0.07 Input. $0.25 Output. 1.3M Context",
                ContextLength = 1310720
            },
            new OpenRouterModelInfo
            {
                Id = "deepseek/deepseek-v4-flash",
                Name = "deepseek/deepseek-v4-flash",
                Description = "DeepSeek-V4-Flash. $0.09 Input. $0.18 Output. 1M Context",
                ContextLength = 1048576
            },
            new OpenRouterModelInfo
            {
                Id = "qwen/qwen3.7-flash",
                Name = "qwen/qwen3.7-flash",
                Description = "Qwen3.7-Flash. $0.03 Input. $0.13 Output. 1M Context",
                ContextLength = 1000000
            },
            new OpenRouterModelInfo
            {
                Id = "qwen/qwen3.8-flash",
                Name = "qwen/qwen3.8-flash",
                Description = "Qwen3.8-Flash. $0.15 Input. $0.47 Output. 1M Context",
                ContextLength = 1000000
            },
            new OpenRouterModelInfo
            {
                Id = "deepseek/deepseek-v4-pro-0813",
                Name = "deepseek/deepseek-v4-pro-0813",
                Description = "DeepSeek-V4-Pro. $0.66 Input. $1.98 Output. 1M Context",
                ContextLength = 1048576
            }
        ];
    }

    /// <summary>
    ///     Translates a batch of key-value pairs to the target language using the OpenRouter API.
    /// </summary>
    /// <param name="targetLanguageName">The name of the target language.</param>
    /// <param name="entries">The list of key-value pairs to translate.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A dictionary of translated key-value pairs.</returns>
    public async Task<IDictionary<string, string>> TranslateBatchAsync(
        string targetLanguageName,
        IList<KeyValuePair<string, string>> entries,
        CancellationToken cancellationToken = default)
    {
        try
        {
            const string apiUrl = "https://openrouter.ai/api/v1/chat/completions";

            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"You are a professional UI translator. Translate each English string into {targetLanguageName}.");
            sb.AppendLine("Preserve UI context, keep placeholders like {0}, {1}, etc. intact.");
            sb.AppendLine("Do NOT add explanations, markdown, or any extra text.");
            sb.AppendLine("Return EXACTLY one line per item in this strict format:");
            sb.AppendLine("Key|TranslatedValue");
            sb.AppendLine();
            sb.AppendLine("English strings:");
            foreach (var entry in entries)
            {
                // Escape newlines (as literal \n markers) and pipes so every entry
                // stays on a single prompt line; ParseTranslations reverses both.
                var escapedValue = entry.Value
                    .Replace("\r\n", "\\n")
                    .Replace("\n", "\\n")
                    .Replace("|", "\\|");
                sb.AppendLine(CultureInfo.InvariantCulture, $"{entry.Key}|{escapedValue}");
            }

            var requestData = new
            {
                model = _modelId,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = sb.ToString()
                    }
                },
                temperature = 0.2,
                top_p = 0.95

                // NOTE: no `reasoning` parameter. Some endpoints (e.g. z-ai/glm-5.3-flash)
                // mandate reasoning and reject `reasoning: { enabled: false }` with a 400.
                // Thinking output arrives in a separate `message.reasoning` field which is
                // ignored here; ParseTranslations only reads "Key|Value" lines, so any
                // reasoning noise in the content is harmless.
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            // Optional OpenRouter attribution headers
            request.Headers.Add("HTTP-Referer", "https://github.com/purelogiccode/SimpleLauncher");
            request.Headers.Add("X-Title", "Simple Launcher Resource Translator");
            request.Content = JsonContent.Create(requestData);

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"OpenRouter API error ({response.StatusCode}): {responseJson}");

            var text = ExtractTextFromResponse(responseJson);
            return ParseTranslations(text, entries.Select(static e => e.Key).ToList());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error(ex, "Translation batch failed for {TargetLanguage}", targetLanguageName);
            throw;
        }
    }

    private static string ExtractTextFromResponse(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);

        if (doc.RootElement.TryGetProperty("error", out var errorElement))
        {
            var errorMessage = errorElement.TryGetProperty("message", out var msgProp)
                ? msgProp.GetString()
                : "Unknown error";
            throw new InvalidOperationException($"OpenRouter API error: {errorMessage}");
        }

        if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("No choices in OpenRouter response.");
        }

        var first = choices[0];
        if (first.TryGetProperty("finish_reason", out var finishReason))
        {
            var reason = finishReason.GetString();
            if (!string.Equals(reason, "stop", StringComparison.Ordinal))
                throw new InvalidOperationException($"Generation stopped. Reason: {reason}");
        }

        if (!first.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var contentElement))
        {
            throw new InvalidOperationException("Unable to extract content from OpenRouter response.");
        }

        return contentElement.GetString() ?? "";
    }

    private static Dictionary<string, string> ParseTranslations(string text, List<string> expectedKeys)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var remainingKeys = new HashSet<string>(expectedKeys, StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var pipeIndex = line.IndexOf('|');
            if (pipeIndex <= 0) continue;

            var key = line[..pipeIndex].Trim();
            var value = line[(pipeIndex + 1)..].Trim();

            // Unescape pipes and newline markers
            value = value.Replace("\\|", "|").Replace("\\n", "\n");

            if (remainingKeys.Contains(key) && !string.IsNullOrEmpty(value))
            {
                result[key] = value;
                remainingKeys.Remove(key);
            }
        }

        // Keys the model omitted (e.g. truncated output reported as "stop") are simply
        // absent: the analyzer treats them as missing and the next run retries them.
        // Filling "" here used to make the blank permanent.
        return result;
    }
}