// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WorkflowInstance = Microsoft.Agents.AI.Workflows.Workflow;

namespace BlogGen.Workflow.WriterCritic;

public static class Program
{
    public const int DefaultMaxIterations = 3;
    public static int MaxIterations { get; private set; } = DefaultMaxIterations;

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("==========================================================");
        Console.WriteLine("  Microsoft Agent Framework: Writer-Critic Loop");
        Console.WriteLine("==========================================================\n");

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddUserSecrets<BlogRequest>(optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        if (int.TryParse(config["maxIterations"] ?? Environment.GetEnvironmentVariable("MAX_ITERATIONS"), out int configuredMax) && configuredMax > 0)
        {
            MaxIterations = configuredMax;
        }

        var (chatClient, loggerFactory) = AgentClientFactory.CreateChatClient(config);
        var logger = loggerFactory.CreateLogger(nameof(Program));

        var topic = config["topic"] ?? "Zero-Downtime Database Migrations in Kubernetes";
        var audience = config["audience"] ?? "Site Reliability Engineers & Platform Architects";
        var tone = config["tone"] ?? "Rigorous, production-ready, and practical";
        var wordCount = config.GetValue<int>("wordCount", 850);

        Console.WriteLine($"Topic          : {topic}");
        Console.WriteLine($"Audience       : {audience}");
        Console.WriteLine($"Tone           : {tone}");
        Console.WriteLine($"Target         : ~{wordCount} words");
        Console.WriteLine($"Max Iterations : {MaxIterations}\n");

        // 1. Create Executors
        var writer = new WriterExecutor(chatClient, topic, audience, tone, wordCount);
        var critic = new CriticExecutor(chatClient, audience, tone);
        var publisher = new PublisherExecutor(chatClient, topic);

        // 2. Build the workflow graph with conditional feedback loop
        var workflowBuilder = new WorkflowBuilder(writer)
            .AddEdge(writer, critic)
            .AddSwitch(critic, sw => sw
                .AddCase<CriticDecision>(cd => cd?.Approved == true, [publisher])
                .AddCase<CriticDecision>(cd => cd?.Approved == false, [writer]))
            .WithOutputFrom(publisher);

        WorkflowInstance workflow = workflowBuilder.Build();

        // 3. Display Mermaid graph topology
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("=== Workflow Graph Topology (Mermaid) ===");
        Console.WriteLine(workflow.ToMermaidString());
        Console.ResetColor();
        Console.WriteLine();

        // 4. Initial trigger message
        var initialPrompt = $"Please draft a technical blog post on: '{topic}'.\n" +
                            $"Target Audience: {audience}\nTone: {tone}\nTarget Length: ~{wordCount} words.";

        Console.WriteLine("Beginning Writer-Critic iteration cycles:\n");
        var finalMarkdown = await RunWriterCriticWorkflowAsync(workflow, initialPrompt);

        // 5. Save output
        if (!string.IsNullOrWhiteSpace(finalMarkdown))
        {
            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
            Directory.CreateDirectory(outputDir);

            var safeSlug = Regex.Replace(topic.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            var filename = $"{DateTime.UtcNow:yyyy-MM-dd}-{safeSlug}.md";
            var filePath = Path.Combine(outputDir, filename);

            await File.WriteAllTextAsync(filePath, finalMarkdown, Encoding.UTF8);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[SUCCESS] Final approved blog post saved to: {filePath}");
            Console.ResetColor();
        }
    }

    private static async Task<string> RunWriterCriticWorkflowAsync(WorkflowInstance workflow, string initialInput)
    {
        string finalContent = string.Empty;
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, initialInput);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is AgentResponseUpdateEvent updateEvent)
            {
                if (!string.IsNullOrEmpty(updateEvent.Update.Text))
                {
                    Console.Write(updateEvent.Update.Text);
                }
            }
            else if (evt is WorkflowOutputEvent outputEvent)
            {
                if (outputEvent.Data is string outputText)
                {
                    finalContent = outputText;
                }
                else if (outputEvent.Data is ChatMessage msg)
                {
                    finalContent = msg.Text ?? string.Empty;
                }
            }
            else if (evt is WorkflowErrorEvent errorEvent)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"\n[Workflow Error]: {errorEvent.Exception?.Message}");
                Console.ResetColor();
            }
            else if (evt is ExecutorFailedEvent failedEvent)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"\n[Executor '{failedEvent.ExecutorId}' Failed]: {failedEvent.Data}");
                Console.ResetColor();
            }
        }

        return finalContent;
    }
}

// ====================================
// Shared Flow State
// ====================================
internal sealed class LoopState
{
    public int Iteration { get; set; } = 1;
    public List<string> History { get; } = [];
}

internal static class LoopStateHelpers
{
    private const string Scope = "WriterCriticScope";
    private const string Key = "StateSingleton";

    public static async Task<LoopState> ReadStateAsync(IWorkflowContext context)
    {
        var state = await context.ReadStateAsync<LoopState>(Key, scopeName: Scope);
        return state ?? new LoopState();
    }

    public static ValueTask SaveStateAsync(IWorkflowContext context, LoopState state)
    {
        return context.QueueStateUpdateAsync(Key, state, scopeName: Scope);
    }
}

// ====================================
// Custom Executors
// ====================================

/// <summary>
/// Writer executor that creates the initial draft or revises it based on Critic feedback.
/// </summary>
internal sealed class WriterExecutor : Executor
{
    private readonly AIAgent _agent;
    private readonly string _topic;
    private readonly string _audience;
    private readonly string _tone;
    private readonly int _wordCount;

    public WriterExecutor(IChatClient client, string topic, string audience, string tone, int wordCount)
        : base("Writer")
    {
        _topic = topic;
        _audience = audience;
        _tone = tone;
        _wordCount = wordCount;

        _agent = client.AsAIAgent(
            name: "Writer",
            instructions: $"You are a technical writer drafting a ~{_wordCount}-word blog post on '{_topic}' for {_audience}. " +
                          $"Tone: {_tone}. Write high-value, practical content with markdown headings and architecture insights. " +
                          "When revising, address every critique raised by the reviewer thoroughly.");
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        return protocolBuilder.ConfigureRoutes(routeBuilder =>
        {
            routeBuilder
                .AddHandler<string, ChatMessage>(HandleInitialRequestAsync)
                .AddHandler<CriticDecision, ChatMessage>(HandleRevisionRequestAsync);
        });
    }

    [MessageHandler]
    public async ValueTask<ChatMessage> HandleInitialRequestAsync(
        string message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await LoopStateHelpers.ReadStateAsync(context);
        Console.WriteLine($"\n==================================================");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  [Writer] Round #{state.Iteration}: Generating Initial Draft");
        Console.ResetColor();
        Console.WriteLine($"==================================================\n");

        return await GenerateOrReviseAsync(new ChatMessage(ChatRole.User, message), context, cancellationToken);
    }

    [MessageHandler]
    public async ValueTask<ChatMessage> HandleRevisionRequestAsync(
        CriticDecision decision,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await LoopStateHelpers.ReadStateAsync(context);
        Console.WriteLine($"\n==================================================");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  [Writer] Round #{state.Iteration}: Revising Draft (Score: {decision.QualityScore}/100)");
        Console.ResetColor();
        Console.WriteLine($"==================================================\n");

        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine($"Please revise your previous draft based on this reviewer critique:");
        foreach (var c in decision.Critiques)
        {
            promptBuilder.AppendLine($"- {c}");
        }
        foreach (var r in decision.ActionableRecommendations)
        {
            promptBuilder.AppendLine($"Recommendation: {r}");
        }
        promptBuilder.AppendLine($"\nOriginal Draft Content:\n{decision.Summary}");

        return await GenerateOrReviseAsync(new ChatMessage(ChatRole.User, promptBuilder.ToString()), context, cancellationToken);
    }

    private async Task<ChatMessage> GenerateOrReviseAsync(
        ChatMessage prompt,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        await foreach (var update in _agent.RunStreamingAsync(prompt, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                sb.Append(update.Text);
                Console.Write(update.Text);
            }
        }
        Console.WriteLine("\n");

        var draftText = sb.ToString();
        var state = await LoopStateHelpers.ReadStateAsync(context);
        state.History.Add(draftText);
        await LoopStateHelpers.SaveStateAsync(context, state);

        return new ChatMessage(ChatRole.User, draftText);
    }
}

/// <summary>
/// Critic executor evaluating draft quality and providing structured CriticDecision.
/// </summary>
internal sealed class CriticExecutor : Executor<ChatMessage, CriticDecision>
{
    private readonly AIAgent _agent;

    public CriticExecutor(IChatClient client, string audience, string tone) : base("Critic")
    {
        _agent = client.AsAIAgent(
            name: "Critic",
            instructions: $"You are a meticulous technical editor and reviewer for an audience of {audience} with a '{tone}' expectation.\n" +
                          "Evaluate the draft against: technical depth, clear actionable takeaways, flow, and absence of fluff.\n" +
                          "You MUST respond ONLY with a valid JSON object matching this schema:\n" +
                          "{\n" +
                          "  \"approved\": boolean (true if qualityScore >= 85, else false),\n" +
                          "  \"qualityScore\": integer (1 to 100),\n" +
                          "  \"critiques\": [ \"critique 1\", \"critique 2\" ],\n" +
                          "  \"actionableRecommendations\": [ \"recommendation 1\" ],\n" +
                          "  \"summary\": \"concise 1-sentence verdict\"\n" +
                          "}\n" +
                          "Do not include markdown backticks around the JSON.");
    }

    public override async ValueTask<CriticDecision> HandleAsync(
        ChatMessage message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await LoopStateHelpers.ReadStateAsync(context);
        Console.WriteLine($"\n==================================================");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  [Critic] Round #{state.Iteration}: Evaluating Draft Quality");
        Console.ResetColor();
        Console.WriteLine($"==================================================\n");

        var response = await _agent.RunAsync(message, cancellationToken: cancellationToken);
        var rawResponse = response.Text ?? "{}";

        CriticDecision decision;
        try
        {
            // Extract json if wrapped in markdown
            var match = Regex.Match(rawResponse, @"\{.*\}", RegexOptions.Singleline);
            var json = match.Success ? match.Value : rawResponse;
            decision = JsonSerializer.Deserialize<CriticDecision>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new CriticDecision { Approved = true, QualityScore = 85 };
        }
        catch
        {
            decision = new CriticDecision
            {
                Approved = state.Iteration >= Program.MaxIterations,
                QualityScore = 80,
                Critiques = ["Prose could be more concise."],
                Summary = "Auto-evaluated."
            };
        }

        // Display decision
        Console.ForegroundColor = decision.Approved ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine($"Quality Score: {decision.QualityScore}/100 | Status: {(decision.Approved ? "APPROVED" : "NEEDS REVISION")}");
        Console.ResetColor();

        foreach (var c in decision.Critiques)
        {
            Console.WriteLine($"  * Critique: {c}");
        }

        // Check max iteration boundary
        if (!decision.Approved && state.Iteration >= Program.MaxIterations)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n[Notice] Max iterations ({Program.MaxIterations}) reached. Auto-approving draft for publication.");
            Console.ResetColor();
            decision.Approved = true;
        }

        if (!decision.Approved)
        {
            state.Iteration++;
        }

        decision.Summary = message.Text ?? string.Empty;
        await LoopStateHelpers.SaveStateAsync(context, state);

        return decision;
    }
}

/// <summary>
/// Publisher executor that wraps the approved draft into publication-ready Markdown with frontmatter.
/// </summary>
internal sealed class PublisherExecutor : Executor<CriticDecision, string>
{
    private readonly AIAgent _agent;
    private readonly string _topic;

    public PublisherExecutor(IChatClient client, string topic) : base("Publisher")
    {
        _topic = topic;
        _agent = client.AsAIAgent(
            name: "Publisher",
            instructions: $"You are a publishing agent. Take the approved draft on '{_topic}' and emit the final publication-ready " +
                          "Markdown document with a complete YAML frontmatter at the top (title, slug, description, date, tags). " +
                          "Output the full article text.");
    }

    public override async ValueTask<string> HandleAsync(
        CriticDecision decision,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await LoopStateHelpers.ReadStateAsync(context);
        Console.WriteLine($"\n==================================================");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  [Publisher] Finalizing Approved Blog Post (Total Rounds: {state.Iteration})");
        Console.ResetColor();
        Console.WriteLine($"==================================================\n");

        var prompt = $"Finalize this approved blog post:\n\n{decision.Summary}";
        var sb = new StringBuilder();

        await foreach (var update in _agent.RunStreamingAsync(new ChatMessage(ChatRole.User, prompt), cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                sb.Append(update.Text);
                Console.Write(update.Text);
            }
        }
        Console.WriteLine("\n");

        return sb.ToString();
    }
}
