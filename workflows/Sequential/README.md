# BlogGen.Workflow.Sequential

A standalone Microsoft Agent Framework workflow console application implementing a 4-stage sequential pipeline for technical blog generation.

## Architecture

The workflow is constructed using `AgentWorkflowBuilder.BuildSequential()` from `Microsoft.Agents.AI.Workflows`. Each agent consumes the prior agent's response as conversational context, refining the draft stage-by-stage.

```
+--------------------+
| User: Topic/Prompt |
+---------+----------+
          |
          v
+--------------------+
|  ResearcherAgent   | -> Analyzes topic, identifies 5 target keywords, outlines H2/H3 sections
+---------+----------+
          |
          v
+--------------------+
|    WriterAgent     | -> Expands outline into a comprehensive long-form technical draft
+---------+----------+
          |
          v
+--------------------+
|    EditorAgent     | -> Polishes grammar, enhances flow, and aligns tone
+---------+----------+
          |
          v
+--------------------+
|   FormatterAgent   | -> Generates final Markdown with YAML frontmatter (title, slug, date, tags)
+---------+----------+
          |
          v
+--------------------+
| ./output/*.md      |
+--------------------+
```

## Features

- **Sequential Agent Chaining**: Clean linear multi-agent workflow using `AgentWorkflowBuilder.BuildSequential`.
- **Live Event Streaming**: Real-time console streaming of token updates via `InProcessExecution.RunStreamingAsync` and `run.WatchStreamAsync()`.
- **Multi-Provider AI Factory**: Embedded `AgentClientFactory` supporting:
  - Google Gemini (with Polly retry and exponential backoff)
  - Azure OpenAI (`DefaultAzureCredential` or API Key)
  - OpenAI (standard API key)
  - Local Models (Ollama or LM Studio via OpenAI-compatible endpoint)
- **Automatic Output Persistence**: Saves the resulting blog post into `./output/{YYYY-MM-DD}-{slug}.md`.

## Prerequisites

- .NET 10.0 SDK
- An API key for Google Gemini, Azure OpenAI, OpenAI, or a running local Ollama instance.

## Configuration

Set your model credentials in `appsettings.json`, via environment variables, or through user secrets:

### Option A: Google Gemini
```json
{
  "GoogleAI": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "ModelId": "gemini-2.5-flash"
  }
}
```

### Option B: Azure OpenAI
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<your-resource>.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini",
    "ApiKey": "YOUR_AZURE_KEY"
  }
}
```

### Option C: Local Model (e.g. Ollama)
```json
{
  "LocalModel": {
    "UseLocal": true,
    "Endpoint": "http://localhost:11434/v1",
    "ModelName": "llama3"
  }
}
```

## Running the Application

Run with default arguments:
```bash
dotnet run
```

Run with custom topic and options:
```bash
dotnet run -- --topic "Architecting High-Throughput Event Streams with Kafka and .NET 10" --audience "Cloud Solutions Architects" --tone "Authoritative, deep-dive" --wordCount 1200
```
