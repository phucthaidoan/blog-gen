# Tasks

## 1. Project Scaffolding

- [x] 1.1 Create `workflows/` directory structure containing `Sequential`, `Concurrent`, and `WriterCritic` subdirectories, and verify the directory layout exists on disk.
- [x] 1.2 Create `BlogGen.Workflow.Sequential.csproj`, `BlogGen.Workflow.Concurrent.csproj`, and `BlogGen.Workflow.WriterCritic.csproj` targeting `net10.0` with `Microsoft.Agents.AI.Workflows` (v1.28.0+), `Microsoft.Agents.AI`, `Microsoft.Extensions.AI`, and configuration dependencies, and verify all three projects restore via `dotnet restore`.

## 2. Common Infrastructure & Client Factories

- [x] 2.1 Implement `AgentClientFactory.cs` and `appsettings.json` supporting Google Gemini (with Polly resilience), Azure OpenAI, OpenAI, and Local models in `workflows/Sequential/`, and verify compilation with `dotnet build`.
- [x] 2.2 Replicate `AgentClientFactory.cs` and `appsettings.json` in `workflows/Concurrent/` and `workflows/WriterCritic/`, and verify compilation with `dotnet build`.
- [x] 2.3 Implement domain models in each project (`BlogRequest`, `BlogOutline`, `BlogPost`, and structured `CriticDecision`), and verify compilation with `dotnet build`.

## 3. Sequential Workflow Implementation

- [x] 3.1 Implement `Program.cs` in `workflows/Sequential/` composing Researcher, Writer, Editor, and Formatter agents using `AgentWorkflowBuilder.BuildSequential()`, and verify compilation with `dotnet build`.
- [x] 3.2 Add streaming event handler loop in `workflows/Sequential/` processing `AgentResponseUpdateEvent` and `WorkflowOutputEvent`, and verify successful execution and output markdown generation.
- [x] 3.3 Create `workflows/Sequential/README.md` documenting pipeline architecture, configuration instructions, and sample execution output, and verify documentation file exists and renders correctly.

## 4. Concurrent Fan-Out/Fan-In Workflow Implementation

- [x] 4.1 Implement `Program.cs` in `workflows/Concurrent/` composing drafting, concurrent StyleCritic/SeoCritic/ToneCritic agents via `AgentWorkflowBuilder.BuildConcurrent()`, and Synthesizer aggregation, and verify compilation with `dotnet build`.
- [x] 4.2 Add streaming run loop and aggregated output handling in `workflows/Concurrent/`, and verify runtime execution and synthesized markdown generation.
- [x] 4.3 Create `workflows/Concurrent/README.md` documenting the fan-out/fan-in topology, configuration, and sample execution output, and verify documentation file exists and renders correctly.

## 5. Writer-Critic Iterative Loop Implementation

- [x] 5.1 Implement `WriterExecutor`, `CriticExecutor`, and `PublisherExecutor` with strongly-typed `CriticDecision` structured output handling in `workflows/WriterCritic/`, and verify compilation with `dotnet build`.
- [x] 5.2 Build workflow graph using `WorkflowBuilder` with `.AddEdge(writer, critic)` and `.AddSwitch<CriticDecision>()` enforcing score thresholds and `MaxIterations` boundaries, and verify compilation with `dotnet build`.
- [x] 5.3 Add streaming event processing, Mermaid diagram printing (`workflow.ToMermaidString()`), and iteration log tracking in `workflows/WriterCritic/`, and verify runtime execution and output markdown generation.
- [x] 5.4 Create `workflows/WriterCritic/README.md` documenting evaluator-optimizer loop mechanics, rubric criteria, configuration, and sample execution output, and verify documentation file exists and renders correctly.

## 6. End-to-End Verification

- [x] 6.1 Build all three standalone projects using `dotnet build` from their respective directories, and verify zero compilation errors.
- [x] 6.2 Verify each standalone project has its own independent configuration, models, client factory, and `README.md` ready for isolated execution.

## 7. Solution Consolidation & Project Reorganization

- [x] 7.1 Relocate existing root project files (`Agents/`, `Services/`, `Models/`, `Framework/`, `Program.cs`, `blog-gen.csproj`, `appsettings.json`, and `GEMINI_SETUP.md`) into `workflows/Imperative/`, rename project to `BlogGen.Imperative.csproj`, and verify compilation with `dotnet build`.
- [x] 7.2 Update `blog-gen.sln` to include all four projects (`workflows/Imperative/BlogGen.Imperative.csproj`, `workflows/Sequential/BlogGen.Workflow.Sequential.csproj`, `workflows/Concurrent/BlogGen.Workflow.Concurrent.csproj`, and `workflows/WriterCritic/BlogGen.Workflow.WriterCritic.csproj`) using `dotnet sln`, and verify with `dotnet sln list`.
- [x] 7.3 Run `dotnet build blog-gen.sln` from the repository root and verify that all 4 projects build cleanly with 0 errors and 0 warnings.
- [x] 7.4 Create `workflows/Imperative/README.md` documenting the baseline imperative multi-agent pattern and verify the file exists and renders correctly.
