# Proposal

## Why

The current `blog-gen` system orchestrates multiple specialized agents imperatively within a monolithic service loop. To unlock the full potential of Microsoft Agent Framework (`Microsoft.Agents.AI.Workflows`), we need dedicated, reference-grade implementations of the three foundational workflow patterns: Sequential Pipeline, Concurrent Fan-Out/Fan-In, and Writer-Critic Evaluator-Optimizer loop. Creating these as three self-contained, standalone projects in a dedicated `workflows/` directory provides modular, reproducible templates for AI-assisted blog content generation with zero cross-project dependencies. Additionally, organizing the existing imperative blog-gen implementation into the same `workflows/` directory structure and consolidating all four projects under `blog-gen.sln` creates a unified, symmetrical solution.

## What Changes

- Create a `workflows/` directory hosting four independent .NET 10 console application projects at the same hierarchy level:
  - `workflows/Imperative`: Relocates the baseline imperative multi-agent orchestration project into its own folder (`BlogGen.Imperative.csproj`) with its agents, services, and configuration.
  - `workflows/Sequential`: Implements a 4-stage linear workflow (`AgentWorkflowBuilder.BuildSequential`) passing artifacts from Research to Writing to Editing to Final Markdown/SEO formatting.
  - `workflows/Concurrent`: Implements a fan-out/fan-in workflow (`AgentWorkflowBuilder.BuildConcurrent`) where a drafted article is reviewed simultaneously across Style, SEO, and Tone/Fact dimensions before synthesis into a final blog post.
  - `workflows/WriterCritic`: Implements an evaluator-optimizer feedback loop (`WorkflowBuilder` with `.AddSwitch` and iteration thresholds) where a Writer agent revises drafts based on structured rubric evaluations from a Critic agent.
- Implement an embedded `AgentClientFactory` inside each workflow project to support multi-provider AI configurations (Google Gemini, Azure OpenAI, OpenAI, and Local models) via `appsettings.json` and user secrets without shared library dependencies.
- Consolidate all four projects under the root `blog-gen.sln` solution file.
- Provide comprehensive `README.md` documentation for each project describing architecture, configuration, execution instructions, and expected outputs.

## Capabilities

### New Capabilities
- `workflow-orchestrations`: Standalone workflow orchestration engines for blog generation supporting sequential chaining, concurrent multi-agent review, iterative evaluator-critic loops, and streaming workflow events.

### Modified Capabilities
<!-- None: existing blog-gen logic is preserved and organized under workflows/Imperative -->

## Impact

- **Projects in Solution (`blog-gen.sln`)**:
  - `workflows/Imperative/BlogGen.Imperative.csproj`
  - `workflows/Sequential/BlogGen.Workflow.Sequential.csproj`
  - `workflows/Concurrent/BlogGen.Workflow.Concurrent.csproj`
  - `workflows/WriterCritic/BlogGen.Workflow.WriterCritic.csproj`
- **Dependencies**: `Microsoft.Agents.AI.Workflows`, `Microsoft.Agents.AI`, `Microsoft.Extensions.AI`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.Http.Resilience`.
- **Existing System**: Root directory becomes clean, hosting `blog-gen.sln`, `openspec/`, and `workflows/`. All projects build together via `dotnet build blog-gen.sln`.
