# Proposal: Migrate to .NET 10 and Microsoft Agent Framework

## Why

The current codebase targets .NET 9.0 and uses an early preview version of `Microsoft.Extensions.AI` (9.0.1-preview.1) with custom agent wrappers. With the official release of .NET 10 and Microsoft Agent Framework (`Microsoft.Agents.AI` v1.28.0), migrating the project brings the system to supported, production-ready standards, resolves deprecated preview APIs, and aligns with Microsoft's official agent framework architecture.

## What Changes

- Upgrade project target framework from `net9.0` to `net10.0` in `blog-gen.csproj`.
- Update all `Microsoft.Extensions.*` packages to their .NET 10 GA versions (`10.0.14` / `10.11.0`).
- Add the official Microsoft Agent Framework package `Microsoft.Agents.AI` (version `1.28.0`).
- Update `Microsoft.Extensions.AI` and `Microsoft.Extensions.AI.OpenAI` to stable `10.11.0`.
- **BREAKING**: Adapt custom `IChatClient` implementations (`GeminiChatClient` and `LocalChatClient`) to match the latest `IChatClient` interface contracts (`GetResponseAsync`, `GetStreamingResponseAsync`, `ChatResponse`, `ChatResponseUpdate`).
- Refactor `BaseAgent` and concrete agents (`ResearchAgent`, `ContentWriterAgent`, `EditorAgent`, `MarkdownLinterAgent`, `SEOAgent`) to leverage `AIAgent` / `ChatClientAgent` from Microsoft Agent Framework while preserving the existing DI and orchestration structure.

## Capabilities

### New Capabilities
<!-- Pure refactor/tooling upgrade: no external functional capabilities added. -->

### Modified Capabilities
<!-- No functional requirements changed. -->

## Impact

- **Build & Runtime**: Requires .NET 10 SDK and runtime.
- **Dependencies**: Replaces preview `Microsoft.Extensions.AI` 9.0 preview packages with stable 10.x packages and introduces `Microsoft.Agents.AI` 1.28.0.
- **Internal APIs**: `IChatClient` implementors and `BaseAgent` consumers are refactored to use standard `AIAgent` and `GetResponseAsync` patterns.
- **External CLI Behavior**: Preserved without breaking changes to command-line arguments, JSON inputs, or blog output generation.
