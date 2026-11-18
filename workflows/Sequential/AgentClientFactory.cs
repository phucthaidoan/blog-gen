// Copyright (c) Microsoft. All rights reserved.

using System.ClientModel;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using OpenAI;
using Polly;

namespace BlogGen.Workflow.Sequential;

/// <summary>
/// Factory for creating IChatClient instances across multiple AI providers.
/// </summary>
public static class AgentClientFactory
{
    public static (IChatClient Client, ILoggerFactory LoggerFactory) CreateChatClient(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        // Resilience handler for HTTP clients
        services.AddHttpClient("ResilientClient")
            .AddStandardResilienceHandler(options =>
            {
                var maxRetries = configuration.GetValue<int>("GoogleAI:Resilience:MaxRetries", 3);
                var initialBackoff = configuration.GetValue<int>("GoogleAI:Resilience:InitialBackoffSeconds", 2);
                var timeout = configuration.GetValue<int>("GoogleAI:Resilience:TimeoutSeconds", 30);

                options.Retry.MaxRetryAttempts = maxRetries;
                options.Retry.Delay = TimeSpan.FromSeconds(initialBackoff);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(timeout);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);

                // Circuit breaker configuration (SamplingDuration must be >= 2 * AttemptTimeout)
                // MinimumThroughput raised to 20 so transient 503 bursts don't trip the circuit.
                // BreakDuration set to 15s so recovery is fast if circuit does open.
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(15);
                options.CircuitBreaker.FailureRatio = 0.9;
                options.CircuitBreaker.MinimumThroughput = 20;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
            });

        var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(nameof(AgentClientFactory));
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        // 1. Local Model
        if (configuration.GetValue<bool>("LocalModel:UseLocal"))
        {
            var endpoint = configuration["LocalModel:Endpoint"] ?? "http://localhost:11434";
            var model = configuration["LocalModel:ModelName"] ?? "llama3";
            logger.LogInformation("Using Local Model at: {Endpoint} with model: {Model}", endpoint, model);

            var openAIClient = new OpenAIClient(
                new ApiKeyCredential("local-key"),
                new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

            return (openAIClient.GetChatClient(model).AsIChatClient(), loggerFactory);
        }

        // 2. Google Gemini
        var geminiKey = !string.IsNullOrWhiteSpace(configuration["GoogleAI:ApiKey"])
            ? configuration["GoogleAI:ApiKey"]
            : (Environment.GetEnvironmentVariable("GOOGLEAI__APIKEY") ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY"));

        if (!string.IsNullOrWhiteSpace(geminiKey))
        {
            var model = !string.IsNullOrWhiteSpace(configuration["GoogleAI:ModelId"])
                ? configuration["GoogleAI:ModelId"]!
                : "gemini-3.5-flash-lite";
            logger.LogInformation("Using Google Gemini model: {Model}", model);
            var httpClient = httpClientFactory.CreateClient("ResilientClient");
            return (new GeminiChatClient(httpClient, geminiKey, model), loggerFactory);
        }

        // 3. Azure OpenAI
        var azureEndpoint = !string.IsNullOrWhiteSpace(configuration["AzureOpenAI:Endpoint"])
            ? configuration["AzureOpenAI:Endpoint"]
            : Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var azureKey = !string.IsNullOrWhiteSpace(configuration["AzureOpenAI:ApiKey"])
            ? configuration["AzureOpenAI:ApiKey"]
            : Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var deployment = !string.IsNullOrWhiteSpace(configuration["AzureOpenAI:DeploymentName"])
            ? configuration["AzureOpenAI:DeploymentName"]!
            : "gpt-4o-mini";

        if (!string.IsNullOrWhiteSpace(azureEndpoint))
        {
            logger.LogInformation("Using Azure OpenAI endpoint: {Endpoint}, deployment: {Deployment}", azureEndpoint, deployment);
            if (!string.IsNullOrWhiteSpace(azureKey))
            {
                var azureClient = new AzureOpenAIClient(new Uri(azureEndpoint), new ApiKeyCredential(azureKey));
                return (azureClient.GetChatClient(deployment).AsIChatClient(), loggerFactory);
            }
            else
            {
                var azureClient = new AzureOpenAIClient(new Uri(azureEndpoint), new DefaultAzureCredential());
                return (azureClient.GetChatClient(deployment).AsIChatClient(), loggerFactory);
            }
        }

        // 4. OpenAI
        var openAIApiKey = !string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"])
            ? configuration["OpenAI:ApiKey"]
            : Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (!string.IsNullOrWhiteSpace(openAIApiKey))
        {
            var model = !string.IsNullOrWhiteSpace(configuration["OpenAI:ModelId"])
                ? configuration["OpenAI:ModelId"]!
                : "gpt-4o-mini";
            logger.LogInformation("Using OpenAI model: {Model}", model);
            var openAIClient = new OpenAIClient(openAIApiKey);
            return (openAIClient.GetChatClient(model).AsIChatClient(), loggerFactory);
        }

        throw new InvalidOperationException(
            "No valid AI configuration found. Please set credentials in appsettings.json or user-secrets for GoogleAI, AzureOpenAI, OpenAI, or LocalModel.");
    }
}

/// <summary>
/// Lightweight Google Gemini IChatClient implementation
/// </summary>
public sealed class GeminiChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly JsonSerializerOptions _jsonOptions;

    public GeminiChatClient(HttpClient httpClient, string apiKey, string model = "gemini-3.5-flash-lite")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _model = model;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public ChatClientMetadata Metadata => new("Gemini", new Uri("https://generativelanguage.googleapis.com"), _model);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = CreateGeminiRequest(chatMessages, options);
        var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);

        using var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url) { Content = httpContent };
        httpRequest.Headers.Add("x-goog-api-key", _apiKey);

        var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Gemini API request failed with status {(int)httpResponse.StatusCode} ({httpResponse.StatusCode}). Details: {errorContent}",
                null,
                httpResponse.StatusCode);
        }

        var responseContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent, _jsonOptions);

        return ConvertToChatResponse(geminiResponse, options);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(chatMessages, options, cancellationToken);
        yield return new ChatResponseUpdate
        {
            ResponseId = response.ResponseId,
            CreatedAt = response.CreatedAt,
            FinishReason = response.FinishReason,
            Contents = response.Messages.FirstOrDefault()?.Contents ?? [],
            Role = response.Messages.FirstOrDefault()?.Role,
            ModelId = response.ModelId,
            RawRepresentation = response.RawRepresentation
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(HttpClient) ? _httpClient : null;

    public void Dispose() { }

    private GeminiRequest CreateGeminiRequest(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var contents = new List<GeminiContent>();
        foreach (var message in messages)
        {
            var parts = new List<GeminiPart>();
            foreach (var content in message.Contents)
            {
                if (content is TextContent textContent)
                {
                    parts.Add(new GeminiPart { Text = textContent.Text });
                }
            }

            if (parts.Count > 0)
            {
                contents.Add(new GeminiContent { Parts = parts });
            }
        }

        var request = new GeminiRequest { Contents = contents };
        if (options != null)
        {
            var config = new GeminiGenerationConfig();
            if (options.Temperature.HasValue) config.Temperature = (float)options.Temperature.Value;
            if (options.MaxOutputTokens.HasValue) config.MaxOutputTokens = options.MaxOutputTokens.Value;
            if (options.TopP.HasValue) config.TopP = (float)options.TopP.Value;
            if (options.TopK.HasValue) config.TopK = options.TopK.Value;
            request.GenerationConfig = config;
        }

        return request;
    }

    private ChatResponse ConvertToChatResponse(GeminiResponse? geminiResponse, ChatOptions? options)
    {
        var candidate = geminiResponse?.Candidates?.FirstOrDefault();
        var text = string.Concat(candidate?.Content?.Parts?.Where(p => !string.IsNullOrEmpty(p.Text)).Select(p => p.Text) ?? []);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"Invalid or empty response from Gemini API. FinishReason: {candidate?.FinishReason}");
        }

        var message = new ChatMessage(ChatRole.Assistant, text);
        var usage = new UsageDetails
        {
            InputTokenCount = geminiResponse?.UsageMetadata?.PromptTokenCount,
            OutputTokenCount = geminiResponse?.UsageMetadata?.CandidatesTokenCount,
            TotalTokenCount = geminiResponse?.UsageMetadata?.TotalTokenCount
        };

        return new ChatResponse(message)
        {
            ResponseId = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            ModelId = _model,
            FinishReason = ChatFinishReason.Stop,
            Usage = usage,
            RawRepresentation = geminiResponse
        };
    }
}

public sealed class GeminiRequest
{
    public List<GeminiContent> Contents { get; set; } = [];
    public GeminiGenerationConfig? GenerationConfig { get; set; }
}

public sealed class GeminiContent
{
    public List<GeminiPart> Parts { get; set; } = [];
}

public sealed class GeminiPart
{
    public string? Text { get; set; }
}

public sealed class GeminiGenerationConfig
{
    public float? Temperature { get; set; }
    public int? MaxOutputTokens { get; set; }
    public float? TopP { get; set; }
    public int? TopK { get; set; }
}

public sealed class GeminiResponse
{
    public List<GeminiCandidate>? Candidates { get; set; }
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

public sealed class GeminiCandidate
{
    public GeminiContent? Content { get; set; }
    public string? FinishReason { get; set; }
}

public sealed class GeminiUsageMetadata
{
    public int? PromptTokenCount { get; set; }
    public int? CandidatesTokenCount { get; set; }
    public int? TotalTokenCount { get; set; }
}
