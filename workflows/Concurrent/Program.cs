// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WorkflowInstance = Microsoft.Agents.AI.Workflows.Workflow;

namespace BlogGen.Workflow.Concurrent;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("==========================================================");
        Console.WriteLine("  Microsoft Agent Framework: Concurrent Fan-Out/Fan-In");
        Console.WriteLine("==========================================================\n");

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddUserSecrets<BlogRequest>(optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var (chatClient, loggerFactory) = AgentClientFactory.CreateChatClient(config);
        // Also initialize the shared state so CreateIsolatedChatClient can be used for fan-out agents
        AgentClientFactory.Initialize(config);
        var logger = loggerFactory.CreateLogger(nameof(Program));

        var topic = config["topic"] ?? "Scaling Microservices with Azure Container Apps and Dapr";
        var audience = config["audience"] ?? "Cloud Architects & Distributed Systems Engineers";
        var tone = config["tone"] ?? "In-depth, hands-on, and authoritative";
        var wordCount = config.GetValue<int>("wordCount", 900);

        Console.WriteLine($"Topic    : {topic}");
        Console.WriteLine($"Audience : {audience}");
        Console.WriteLine($"Tone     : {tone}");
        Console.WriteLine($"Target   : ~{wordCount} words\n");

        // 1. Drafter Agent
        AIAgent drafter = chatClient.AsAIAgent(
            name: "drafter",
            description: "Produces initial blog draft",
            instructions: $"You are a seasoned technical writer. Produce a complete, high-quality blog draft (~{wordCount} words) " +
                          $"on the topic '{topic}' for {audience} in an {tone} tone. Use structured markdown subheadings.");

        // 2. Concurrent Critic Agents (Fan-Out) — each gets an isolated HttpClient/circuit breaker
        //    to prevent one agent's rate-limit failures from tripping others' circuits.
        AIAgent styleCritic = AgentClientFactory.CreateIsolatedChatClient("style_critic").AsAIAgent(
            name: "style_critic",
            description: "Audits prose flow, sentence variety, and reader engagement",
            instructions: "You are an expert editorial style critic. Review the provided draft specifically for narrative flow, " +
                          "clarity, conciseness, active voice, and reader engagement. Provide concise, bulleted feedback.");

        AIAgent seoCritic = AgentClientFactory.CreateIsolatedChatClient("seo_critic").AsAIAgent(
            name: "seo_critic",
            description: "Audits keyword density, heading structure, and search discoverability",
            instructions: "You are a senior SEO specialist. Evaluate the draft for SEO effectiveness: recommend 3 optimized titles, " +
                          "suggest high-volume keywords to emphasize, evaluate H2/H3 distribution, and craft a 150-character meta description.");

        AIAgent toneCritic = AgentClientFactory.CreateIsolatedChatClient("tone_critic").AsAIAgent(
            name: "tone_critic",
            description: "Audits audience alignment and technical precision",
            instructions: $"You are a technical peer reviewer. Verify that the technical depth is appropriate for {audience}, " +
                          $"the tone matches '{tone}', and claims are architecturally sound. Identify any confusing technical points.");

        // 3. Synthesizer Agent (Fan-In)
        AIAgent synthesizer = chatClient.AsAIAgent(
            name: "synthesizer",
            description: "Consolidates draft and all concurrent critiques into final publication",
            instructions: "You are a chief managing editor. You receive the original draft alongside three concurrent evaluations " +
                          "(Style, SEO, and Technical Tone). Synthesize everything into the definitive, publication-ready Markdown article. " +
                          "Incorporate the best feedback from all three critics and add a complete YAML frontmatter block at the top " +
                          "(title, slug, description, date, tags). Output the full article.");

        // Step 1: Generate initial draft
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(">>> Phase 1: Drafting Initial Article...");
        Console.ResetColor();

        var draftPrompt = $"Please write an initial blog draft on: '{topic}'.\nAudience: {audience}\nTone: {tone}";
        var draftMessages = new List<ChatMessage> { new(ChatRole.User, draftPrompt) };
        var draftResponse = await drafter.RunAsync(draftMessages);
        var initialDraftText = draftResponse.Text ?? string.Empty;

        Console.WriteLine("\n[Initial Draft Generated]");

        // Step 2: Fan-Out with AgentWorkflowBuilder.BuildConcurrent
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n>>> Phase 2: Fan-Out Concurrent Review (Style, SEO, Technical Tone)...");
        Console.ResetColor();

        WorkflowInstance concurrentWorkflow = AgentWorkflowBuilder.BuildConcurrent([styleCritic, seoCritic, toneCritic]);
        var reviewPrompt = $"Review this technical blog draft on '{topic}':\n\n{initialDraftText}";
        var reviewMessages = new List<ChatMessage> { new(ChatRole.User, reviewPrompt) };

        List<ChatMessage> critiqueResults = await RunConcurrentWorkflowAsync(concurrentWorkflow, reviewMessages);

        // Step 3: Fan-In with Synthesizer
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n>>> Phase 3: Fan-In Synthesis (Producing Final Optimized Post)...");
        Console.ResetColor();

        var synthesisPromptBuilder = new StringBuilder();
        synthesisPromptBuilder.AppendLine("ORIGINAL DRAFT:");
        synthesisPromptBuilder.AppendLine(initialDraftText);
        synthesisPromptBuilder.AppendLine("\nCRITIQUES RECEIVED FROM SPECIALISTS:");

        foreach (var critique in critiqueResults)
        {
            synthesisPromptBuilder.AppendLine($"\n--- [{critique.Role}] ---");
            synthesisPromptBuilder.AppendLine(critique.Text);
        }

        synthesisPromptBuilder.AppendLine("\nPlease synthesize the draft and all critiques into the final publication-ready markdown article.");

        var finalResponse = await synthesizer.RunAsync([new(ChatRole.User, synthesisPromptBuilder.ToString())]);
        var finalMarkdown = finalResponse.Text ?? string.Empty;

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(finalMarkdown);
        Console.ResetColor();

        // Step 4: Save output
        if (!string.IsNullOrWhiteSpace(finalMarkdown))
        {
            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
            Directory.CreateDirectory(outputDir);

            var safeSlug = Regex.Replace(topic.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            var filename = $"{DateTime.UtcNow:yyyy-MM-dd}-{safeSlug}.md";
            var filePath = Path.Combine(outputDir, filename);

            await File.WriteAllTextAsync(filePath, finalMarkdown, Encoding.UTF8);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[SUCCESS] Final synthesized blog post saved to: {filePath}");
            Console.ResetColor();
        }
    }

    private static async Task<List<ChatMessage>> RunConcurrentWorkflowAsync(WorkflowInstance workflow, List<ChatMessage> messages)
    {
        string? lastExecutorId = null;
        var agentOutputs = new Dictionary<string, StringBuilder>();
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is AgentResponseUpdateEvent updateEvent)
            {
                if (updateEvent.ExecutorId != lastExecutorId)
                {
                    lastExecutorId = updateEvent.ExecutorId;
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\n[Concurrent Agent: {updateEvent.ExecutorId?.ToUpperInvariant()}]");
                    Console.ResetColor();
                }

                if (!string.IsNullOrEmpty(updateEvent.Update.Text))
                {
                    Console.Write(updateEvent.Update.Text);
                    var id = updateEvent.ExecutorId ?? "agent";
                    if (!agentOutputs.TryGetValue(id, out var sb))
                    {
                        sb = new StringBuilder();
                        agentOutputs[id] = sb;
                    }
                    sb.Append(updateEvent.Update.Text);
                }
            }
            else if (evt is WorkflowOutputEvent outputEvent)
            {
                Console.WriteLine();
                if (outputEvent.Data is IEnumerable<ChatMessage> resultMessages)
                {
                    return resultMessages.ToList();
                }
                if (outputEvent.Data is ChatMessage singleMessage)
                {
                    return [singleMessage];
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

        if (agentOutputs.Count > 0)
        {
            return agentOutputs.Select(kvp => new ChatMessage(ChatRole.Assistant, kvp.Value.ToString()) { AuthorName = kvp.Key }).ToList();
        }

        return [];
    }
}
