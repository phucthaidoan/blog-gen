# BlogGen.Workflow.Concurrent

A standalone Microsoft Agent Framework workflow console application demonstrating a Fan-Out / Fan-In pattern for multi-perspective blog generation and review.

## Architecture

This workflow uses `AgentWorkflowBuilder.BuildConcurrent()` from `Microsoft.Agents.AI.Workflows` to evaluate an initial draft simultaneously from three distinct domain perspectives (Style, SEO, and Technical Tone) before synthesizing them into the final publication.

```
                    +--------------------+
                    | User: Topic/Prompt |
                    +---------+----------+
                              |
                              v
                    +--------------------+
                    |    DrafterAgent    | -> Generates complete initial draft
                    +---------+----------+
                              |
              +---------------+---------------+
              |  Fan-Out (BuildConcurrent)    |
              v                               v
    +-------------------+           +-------------------+
    |    StyleCritic    |           |     SeoCritic     |
    | - Clarity & Flow  |           | - Keywords & Meta |
    | - Engagement      |           | - Headings        |
    +---------+---------+           +---------+---------+
              |                               |
              +---------------+---------------+
                              |
                              v (Also Concurrent)
                    +-------------------+
                    |    ToneCritic     |
                    | - Tech Precision  |
                    | - Audience Fit    |
                    +---------+---------+
                              |
                              v (Fan-In)
                    +--------------------+
                    |  SynthesizerAgent  | -> Integrates draft + all 3 critiques
                    +---------+----------+    into final polished article
                              |
                              v
                    +--------------------+
                    | ./output/*.md      |
                    +--------------------+
```

## Features

- **Fan-Out Parallel Evaluation**: Dispatches 3 specialist reviewers concurrently, minimizing total evaluation turnaround latency.
- **Fan-In Synthesis**: The `SynthesizerAgent` ingests all three streams of feedback simultaneously, resolving conflicts and applying enhancements.
- **Streaming Event Handling**: Real-time console visualization of each concurrent agent's progress via `run.WatchStreamAsync()`.
- **Multi-Provider AI Factory**: Embedded `AgentClientFactory` with support for Google Gemini, Azure OpenAI, OpenAI, and Local Models (Ollama/LM Studio).
- **Automated Output**: Saves output with full YAML frontmatter to `./output/{YYYY-MM-DD}-{slug}.md`.

## Prerequisites

- .NET 10.0 SDK
- Credentials for Google Gemini, Azure OpenAI, OpenAI, or a local Ollama instance.

## Configuration

Set model credentials in `appsettings.json`, environment variables, or user secrets:

```json
{
  "GoogleAI": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "ModelId": "gemini-2.5-flash"
  }
}
```

## Running the Application

Default execution:
```bash
dotnet run
```

Custom execution parameters:
```bash
dotnet run -- --topic "Zero Trust Security in Cloud Native Kubernetes" --audience "Platform Security Engineers" --tone "Technical and authoritative" --wordCount 1000
```
