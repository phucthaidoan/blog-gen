# BlogGen.Workflow.WriterCritic

A standalone Microsoft Agent Framework workflow console application demonstrating the Evaluator-Optimizer (Writer-Critic) iterative feedback loop with conditional branching and safety bounds.

## Architecture

This workflow uses the low-level `WorkflowBuilder` from `Microsoft.Agents.AI.Workflows` with `.AddEdge()` and `.AddSwitch<CriticDecision>()` to orchestrate an editorial revision loop.

```
                    +--------------------+
                    | User: Topic/Prompt |
                    +---------+----------+
                              |
                              v
+-----------------> +--------------------+
|                   |   WriterExecutor   | <---------------+
|                   +---------+----------+                 |
|                             |                            |
|                             v                            |
|                   +--------------------+                 | Needs Revision
|                   |   CriticExecutor   |                 | (Approved == false)
|                   |  - Rubric Scoring  |                 | + Specific Feedback
|                   |  - Structured JSON |                 |
|                   +---------+----------+                 |
|                             |                            |
|                             v                            |
|                   [ Decision Switch ] -------------------+
|                             |
|                             | Approved == true (or Iteration >= MaxIterations)
|                             v
|                   +--------------------+
|                   | PublisherExecutor  |
|                   +---------+----------+
|                             |
|                             v
+------------------ +--------------------+
                    | ./output/*.md      |
                    +--------------------+
```

## Features

- **Iterative Refinement Loop**: Writer agent iteratively updates the draft based on explicit feedback points provided by Critic agent.
- **Structured Scoring**: Critic evaluates draft against rubrics and returns a structured `CriticDecision` (`Approved`, `QualityScore` 1-100, `Critiques`, `ActionableRecommendations`).
- **Loop Safety Bounds**: Enforces `MaxIterations` (default 3, configurable via `MAX_ITERATIONS` environment variable or `--maxIterations` command-line argument) to prevent infinite loops.
- **Workflow Graph Visualization**: Prints the Mermaid graph topology at startup using `workflow.ToMermaidString()`.
- **Live Event Streaming**: Streams token-by-token progress for each executor round.
- **Multi-Provider AI Factory**: Embedded `AgentClientFactory` supporting Google Gemini, Azure OpenAI, OpenAI, and Local Models (Ollama/LM Studio).
- **Automated Output**: Writes the finalized, approved markdown article with YAML frontmatter to `./output/{YYYY-MM-DD}-{slug}.md`.

## Prerequisites

- .NET 10.0 SDK
- Model credentials for Google Gemini, Azure OpenAI, OpenAI, or a running local Ollama instance.

## Configuration

Configure credentials in `appsettings.json`, environment variables, or user secrets:

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
dotnet run -- --topic "Fault-Tolerant Distributed Locks with Redis and .NET" --audience "Senior Backend Engineers" --tone "In-depth and practical" --wordCount 800 --maxIterations 3
```
