# BlogGen.Imperative

A console application demonstrating the baseline **imperative multi-agent orchestration** pattern for blog post generation using Microsoft Agent Framework (`Microsoft.Agents.AI`).

## Architecture

This project coordinates multiple specialized agents sequentially through a central orchestrator service (`BlogGenerationService.cs`), invoking each agent imperatively with typed parameters and measuring elapsed time per step.

```
+--------------------+
| User: Topic/Prompt |
+---------+----------+
          |
          v
+--------------------+
|   ResearchAgent    | -> Researches topic, audience, and generates outline
+---------+----------+
          |
          v
+--------------------+
| ContentWriterAgent | -> Writes full narrative draft from the outline
+---------+----------+
          |
          v
+--------------------+
|    EditorAgent     | -> Edits draft for tone, readability, and clarity
+---------+----------+
          |
          v
+--------------------+
| MarkdownLinterAgent| -> Validates markdown syntax, headings, and formatting
+---------+----------+
          |
          v
+--------------------+
|      SEOAgent      | -> Optimizes keywords, meta description, and tags
+---------+----------+
          |
          v
+--------------------+
| Saved Output File  |
+--------------------+
```

## Features

- **Imperative Pipeline**: Step-by-step sequential coordination via `BlogGenerationService`.
- **Type-Safe Agent Registry**: `IAgentRegistry` manages dependency injection and retrieval of specialized agents (`ResearchAgent`, `ContentWriterAgent`, `EditorAgent`, `MarkdownLinterAgent`, `SEOAgent`).
- **Multi-Provider AI Client**: Supports Google Gemini (via `GeminiChatClient` with Polly resilience), Azure OpenAI (`DefaultAzureCredential` or API Key), OpenAI, and Local Models (Ollama/LM Studio).
- **Structured Request Input**: Supports reading requests from `blog-request.json` or interactive CLI parameters.

## Prerequisites

- .NET 10.0 SDK
- Credentials for Google Gemini, Azure OpenAI, OpenAI, or a running local Ollama instance.

## Configuration

Configure credentials in `appsettings.json`, environment variables, or user secrets:

```json
{
  "GoogleAI": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "ModelId": "gemini-3.5-flash-lite",
    "Resilience": {
      "MaxRetries": 3,
      "InitialBackoffSeconds": 2,
      "TimeoutSeconds": 120
    }
  }
}
```

## Running the Application

Execute from this directory:
```bash
dotnet run
```

Or pass input files:
```bash
dotnet run -- --request blog-request.json
```

## Sibling Workflow Patterns

For declarative Microsoft Agent Framework workflow orchestrations, see sibling folders:
- `../Sequential/`: Linear pipeline via `AgentWorkflowBuilder.BuildSequential`
- `../Concurrent/`: Fan-out/fan-in review via `AgentWorkflowBuilder.BuildConcurrent`
- `../WriterCritic/`: Evaluator-optimizer feedback loop via `WorkflowBuilder`
