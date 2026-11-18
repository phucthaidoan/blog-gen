// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WorkflowInstance = Microsoft.Agents.AI.Workflows.Workflow;

namespace BlogGen.Workflow.Sequential;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("==========================================================");
        Console.WriteLine("  Microsoft Agent Framework: Sequential Blog Workflow");
        Console.WriteLine("==========================================================\n");

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddUserSecrets<BlogRequest>(optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var (chatClient, loggerFactory) = AgentClientFactory.CreateChatClient(config);
        var logger = loggerFactory.CreateLogger(nameof(Program));

        // Get blog input parameters
        var topic = config["topic"] ?? "Building Event-Driven Architectures with .NET 10";
        var audience = config["audience"] ?? "Senior .NET Engineers and Cloud Architects";
        var tone = config["tone"] ?? "Insightful, practical, and architecture-focused";
        var wordCount = config.GetValue<int>("wordCount", 800);

        Console.WriteLine($"Topic    : {topic}");
        Console.WriteLine($"Audience : {audience}");
        Console.WriteLine($"Tone     : {tone}");
        Console.WriteLine($"Target   : ~{wordCount} words\n");

        // 1. Create specialized agents
        AIAgent researcher = chatClient.AsAIAgent(
            name: "researcher",
            description: "Researches topic and generates structured outline",
            instructions: "You are an expert technical researcher. Analyze the requested topic and generate a structured " +
                          "outline featuring main sections, key technical takeaways, and 5 target SEO keywords.");

        AIAgent writer = chatClient.AsAIAgent(
            name: "writer",
            description: "Expands outline into full narrative draft",
            instructions: $"You are an expert technical blog writer. Expand the provided research outline into a comprehensive, " +
                          $"engaging draft targeting ~{wordCount} words in an {tone} tone for {audience}. " +
                          "Include clear H2/H3 subheadings and illustrative code or architectural examples.");

        AIAgent editor = chatClient.AsAIAgent(
            name: "editor",
            description: "Refines draft for clarity, tone, and technical precision",
            instructions: "You are a lead technical editor. Review the drafted article. Tighten prose, enhance transitions, " +
                          "fix awkward constructions, and ensure the concepts are crystal clear.");

        AIAgent formatter = chatClient.AsAIAgent(
            name: "formatter",
            description: "Formats publication-ready Markdown with YAML frontmatter",
            instructions: "You are a content publishing specialist. Convert the edited text into a complete, publication-ready " +
                          "Markdown document. At the top, include a YAML frontmatter block with: title, slug, description, " +
                          "date (YYYY-MM-DD), and tags. Output the entire article without omissions.");

        // 2. Build Sequential Workflow using AgentWorkflowBuilder
        logger.LogInformation("Building sequential workflow: Researcher -> Writer -> Editor -> Formatter");
        WorkflowInstance workflow = AgentWorkflowBuilder.BuildSequential([researcher, writer, editor, formatter]);

        // 3. Initial user message
        var initialPrompt = $"Please research, write, edit, and publish a technical blog post on: '{topic}'.\n" +
                            $"Target Audience: {audience}\nTone: {tone}\nTarget Length: ~{wordCount} words.";

        List<ChatMessage> messages = [new(ChatRole.User, initialPrompt)];

        Console.WriteLine("Executing sequential pipeline. Live progress stream:\n");
        var finalMarkdown = await RunSequentialWorkflowAsync(workflow, messages);

        // 4. Save result
        if (!string.IsNullOrWhiteSpace(finalMarkdown))
        {
            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
            Directory.CreateDirectory(outputDir);

            var safeSlug = Regex.Replace(topic.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            var filename = $"{DateTime.UtcNow:yyyy-MM-dd}-{safeSlug}.md";
            var filePath = Path.Combine(outputDir, filename);

            await File.WriteAllTextAsync(filePath, finalMarkdown, Encoding.UTF8);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[SUCCESS] Blog post saved to: {filePath}");
            Console.ResetColor();
        }
    }

    private static async Task<string> RunSequentialWorkflowAsync(WorkflowInstance workflow, List<ChatMessage> messages)
    {
        string? lastExecutorId = null;
        var stageOutputs = new Dictionary<string, StringBuilder>();
        string currentStage = "workflow";

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is AgentResponseUpdateEvent updateEvent)
            {
                if (updateEvent.ExecutorId != lastExecutorId)
                {
                    lastExecutorId = updateEvent.ExecutorId;
                    currentStage = updateEvent.ExecutorId ?? "stage";
                    if (!stageOutputs.ContainsKey(currentStage))
                    {
                        stageOutputs[currentStage] = new StringBuilder();
                    }

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n>>> Stage: [{currentStage.ToUpperInvariant()}]");
                    Console.ResetColor();
                }

                if (!string.IsNullOrEmpty(updateEvent.Update.Text))
                {
                    Console.Write(updateEvent.Update.Text);
                    if (stageOutputs.TryGetValue(currentStage, out var sb))
                    {
                        sb.Append(updateEvent.Update.Text);
                    }
                }
            }
            else if (evt is WorkflowOutputEvent outputEvent)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n>>> Sequential Workflow Completed.");
                Console.ResetColor();

                if (outputEvent.Data is IEnumerable<ChatMessage> outputMessages)
                {
                    var list = outputMessages.ToList();
                    if (list.Count > 0) return list.Last().Text ?? string.Empty;
                }
                if (outputEvent.Data is ChatMessage singleOutputMsg)
                {
                    return singleOutputMsg.Text ?? string.Empty;
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

        // Return formatter output if available, else last stage output
        if (stageOutputs.TryGetValue("formatter", out var formatterSb) && formatterSb.Length > 0)
        {
            return formatterSb.ToString();
        }

        return stageOutputs.Values.LastOrDefault()?.ToString() ?? string.Empty;
    }
}
