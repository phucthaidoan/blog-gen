# Design: Migrate to .NET 10 and Microsoft Agent Framework

## Context

The application is a console-based multi-agent blog post generator built on .NET 9.0 using early preview packages (`Microsoft.Extensions.AI 9.0.1-preview.1`). The agents (`ResearchAgent`, `ContentWriterAgent`, `EditorAgent`, `MarkdownLinterAgent`, `SEOAgent`) derive from a custom `BaseAgent` that directly calls `IChatClient.CompleteAsync()`. Custom `IChatClient` implementations exist for Google Gemini (`GeminiChatClient`) and local OpenAI-compatible models (`LocalChatClient`).

See `proposal.md` for overall motivation.

## Goals / Non-Goals

**Goals:**
- Target `net10.0` and utilize stable .NET 10 packages (`10.0.14` runtime and `10.11.0` extensions).
- Integrate the official `Microsoft.Agents.AI` (version `1.28.0`) package.
- Wrap or back agent implementations using `AIAgent` / `ChatClientAgent` while preserving the existing `IAgentRegistry` and `BlogGenerationService` sequential orchestration.
- Update `GeminiChatClient` and `LocalChatClient` to adhere to the revised `IChatClient` interface contracts (`GetResponseAsync`, `GetStreamingResponseAsync`, `ChatResponse`, `ChatResponseUpdate`).

**Non-Goals:**
- Rewriting the orchestration layer into `Microsoft.Agents.AI.Workflows` graph structures (deferred to a future enhancement).
- Changing blog generation prompts, schemas, input templates, or command-line parameters.
- Modifying underlying model provider logic or secret storage configurations.

## Decisions

### Decision 1: Use `ChatClientAgent` inside `BaseAgent`
* **Approach**: Refactor `BaseAgent` to hold an instance of `AIAgent` created via `_chatClient.CreateAIAgent(...)` or `new ChatClientAgent(_chatClient, ...)`. Subclasses can provide their name and default instructions or pass them down.
* **Rationale**: This gives every agent full Microsoft Agent Framework capabilities (system instructions, future tool integration, agent context) while keeping the public interface of agents (`ExecutePromptAsync` / `RunAsync`) intact for `BlogGenerationService`.
* **Alternatives Considered**:
  - *Keep custom BaseAgent calling `IChatClient.GetResponseAsync()` directly*: Avoids using `Microsoft.Agents.AI` entirely, defeating the goal of migrating to the framework.
  - *Convert to full declarative DAG workflows (`Microsoft.Agents.AI.Workflows`)*: Considered during exploration, but rejected in favor of Option 1 to avoid unnecessary churn in the service layer and preserve step-by-step CLI output.

### Decision 2: Update `IChatClient` method signatures in Custom Clients
* **Approach**:
  - Rename `CompleteAsync` to `GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)` and return `Task<ChatResponse>`.
  - Rename `CompleteStreamingAsync` to `GetStreamingResponseAsync(...)` and yield `ChatResponseUpdate`.
  - Adjust response serialization mapping from `ChatCompletion` to `ChatResponse`.
* **Rationale**: Required by `Microsoft.Extensions.AI` GA interface specifications.

### Decision 3: Update .NET 10 Dependencies
* **Target Framework**: `net10.0`.
* **Packages**:
  - `Microsoft.Agents.AI`: `1.28.0`
  - `Microsoft.Extensions.AI`: `10.11.0`
  - `Microsoft.Extensions.AI.OpenAI`: `10.11.0`
  - `Microsoft.Extensions.Hosting`: `10.0.14`
  - `Microsoft.Extensions.Http.Resilience`: `10.11.0`
  - `Microsoft.Extensions.Configuration.*`, `Microsoft.Extensions.Logging.*`, `Microsoft.Extensions.DependencyInjection`, `System.Text.Json`: `10.0.14` / `10.11.0`.

## Risks / Trade-offs

- **[Risk] Breaking changes across MEAI types**: Types like `ChatCompletion`, `ChatMessage` properties, and extension methods changed between 9.0 preview and 10.11.0.
  - *Mitigation*: Comprehensive compile-time checks and updating mapping logic in `GeminiChatClient.cs` and `LocalChatClient.cs`.
- **[Risk] Agent instruction overrides**: `ChatClientAgent` treats instructions as system messages.
  - *Mitigation*: Ensure prompts passed to `ExecutePromptAsync` combine well with agent instructions without double-prompting.

## Migration Plan

1. Update `blog-gen.csproj` target framework and package references.
2. Update custom `IChatClient` implementations (`GeminiChatClient`, `LocalChatClient`).
3. Update `Program.cs` client initialization (`.AsIChatClient()`).
4. Update `BaseAgent` and concrete agents to use `Microsoft.Agents.AI`.
5. Build and execute verification tests with `blog-request.json`.
