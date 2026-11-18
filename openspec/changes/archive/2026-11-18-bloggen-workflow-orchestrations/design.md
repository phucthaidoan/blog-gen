# Design

## Context

See `proposal.md` for motivation. The solution establishes four standalone console applications in `workflows/` inside the `blog-gen` workspace, unified by the root `blog-gen.sln` solution file. It includes the baseline imperative pipeline alongside the three Microsoft Agent Framework (`Microsoft.Agents.AI.Workflows`) orchestration patterns: Sequential Pipeline, Concurrent Fan-Out/Fan-In, and Writer-Critic Loop.

## Goals / Non-Goals

**Goals:**
- Provide four zero-dependency, self-contained projects at an equal hierarchy level: `workflows/Imperative`, `workflows/Sequential`, `workflows/Concurrent`, and `workflows/WriterCritic`.
- Relocate the existing root `blog-gen` project to `workflows/Imperative/` (named `BlogGen.Imperative.csproj`) to establish complete structural symmetry.
- Unify all four projects under `blog-gen.sln` so that `dotnet build blog-gen.sln` builds everything cleanly in a single command.
- Implement `AgentWorkflowBuilder.BuildSequential` for linear chaining (Research -> Writer -> Editor -> Formatter).
- Implement `AgentWorkflowBuilder.BuildConcurrent` for fan-out/fan-in reviews (Draft -> Style, SEO, Tone concurrently -> Synthesis).
- Implement `WorkflowBuilder` with `.AddEdge()` and `.AddSwitch<CriticDecision>()` for an evaluator-optimizer iterative loop with safe iteration boundaries.
- Embed a unified `AgentClientFactory` in each workflow project supporting Google Gemini (with Polly retry/backoff), Azure OpenAI, OpenAI, and Local models.
- Support real-time console streaming of `WorkflowEvent` objects (`AgentResponseUpdateEvent`, `WorkflowOutputEvent`, `ExecutorFailedEvent`).
- Include complete `README.md` and configuration files in each project.

**Non-Goals:**
- Introducing shared DLL/project references between the four projects (each remains 100% self-contained).
- Changing the internal implementation of the baseline imperative agents during relocation.
- Building a web UI or persistent database (all outputs save to local Markdown files).

## Decisions

### 1. Symmetrical Project Organization & Solution Consolidation
- **Decision**: Relocate the baseline `blog-gen` files from repository root into `workflows/Imperative/` (`BlogGen.Imperative.csproj`), and register all four projects in `blog-gen.sln`.
- **Directory Layout**:
  ```
  blog-gen/
  +-- blog-gen.sln
  +-- openspec/
  +-- workflows/
      +-- Imperative/     (Baseline imperative multi-agent loop)
      +-- Sequential/     (Linear pipeline via AgentWorkflowBuilder.BuildSequential)
      +-- Concurrent/     (Fan-out/fan-in via AgentWorkflowBuilder.BuildConcurrent)
      +-- WriterCritic/   (Evaluator-optimizer loop via WorkflowBuilder)
  ```
- **Rationale**: Keeps the repository root clean, puts all approaches on an equal architectural footing, and enables running both single-project builds and comprehensive solution builds (`dotnet build blog-gen.sln`).

### 2. Isolated Self-Contained Projects
- **Decision**: Each project contains its own models, client setup, `appsettings.json`, and `.csproj`.
- **Rationale**: Enables developers to clone, copy, or run each pattern completely independently without cross-project path coupling or build-order dependencies.

### 3. Multi-Provider Factory Architecture
- **Decision**: Provide `AgentClientFactory.CreateChatClient(IConfiguration, IHttpClientFactory)` in each workflow project with priority: Local Model -> Google Gemini -> Azure OpenAI -> OpenAI.
- **Resilience**: Google Gemini HTTP requests are wrapped with Polly exponential backoff and timeout handling.

### 4. Workflow Topologies
- **Pattern A (Imperative)**: Step-by-step orchestrator service (`BlogGenerationService`) invoking agents via typed registry.
- **Pattern B (Sequential)**: `AgentWorkflowBuilder.BuildSequential(agents)`.
- **Pattern C (Concurrent)**: Draft -> `AgentWorkflowBuilder.BuildConcurrent(critics)` -> `SynthesizerAgent`.
- **Pattern D (Writer-Critic Loop)**: `WorkflowBuilder` with `.AddEdge(writer, critic)` and `.AddSwitch(critic, ...)`.

## Risks / Trade-offs

- **[Risk] Path References after Relocation**: Relocating `blog-gen` to `workflows/Imperative/` could break paths if files assumed root execution.
  - *Mitigation*: Ensure `workflows/Imperative/BlogGen.Imperative.csproj` and `appsettings.json` use relative paths, and update `blog-gen.sln` project paths accordingly.
- **[Risk] Solution Build Failures**: Adding all four projects could introduce build conflicts if package versions diverge.
  - *Mitigation*: All projects target `net10.0` and use aligned `Microsoft.Agents.AI` and `Microsoft.Extensions.AI` packages.

## Migration / Setup Plan

1. Create `workflows/Imperative/` and move root project files (`Agents/`, `Services/`, `Models/`, `Framework/`, `Program.cs`, `blog-gen.csproj`, `appsettings.json`, `GEMINI_SETUP.md`) into it.
2. Rename `blog-gen.csproj` in `workflows/Imperative/` to `BlogGen.Imperative.csproj`.
3. Update `blog-gen.sln` with `dotnet sln remove blog-gen.csproj` and `dotnet sln add workflows/Imperative/... workflows/Sequential/... workflows/Concurrent/... workflows/WriterCritic/...`.
4. Verify with `dotnet build blog-gen.sln`.
