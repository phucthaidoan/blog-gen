# Tasks: Migrate to .NET 10 and Microsoft Agent Framework

## 1. Project Dependencies and Runtime Upgrade

- [x] 1.1 Update `blog-gen.csproj` target framework from `net9.0` to `net10.0` and verify the project targets the .NET 10 TFM
- [x] 1.2 Update package references in `blog-gen.csproj` to include `Microsoft.Agents.AI` (1.28.0), `Microsoft.Extensions.AI` (10.11.0), `Microsoft.Extensions.AI.OpenAI` (10.11.0), and .NET 10 `Microsoft.Extensions.*` packages (10.0.14 / 10.11.0), and verify package restoration with `dotnet restore`

## 2. Custom IChatClient Implementation Updates

- [x] 2.1 Refactor `Services/Gemini/GeminiChatClient.cs` to implement the updated `IChatClient` interface (`GetResponseAsync`, `GetStreamingResponseAsync`, returning `ChatResponse` and `ChatResponseUpdate`) and verify compilation of the Gemini client
- [x] 2.2 Refactor `Services/LocalChatClient.cs` to implement the updated `IChatClient` interface (`GetResponseAsync`, `GetStreamingResponseAsync`, returning `ChatResponse` and `ChatResponseUpdate`) and verify compilation of the Local client
- [x] 2.3 Update `Program.cs` client registration and extension method calls (e.g., `.AsIChatClient()`) and verify client registration compiles

## 3. Microsoft Agent Framework Integration

- [x] 3.1 Refactor `Framework/BaseAgent.cs` to initialize and wrap an `AIAgent` / `ChatClientAgent` instance from `Microsoft.Agents.AI` and delegate `ExecutePromptAsync` to `agent.RunAsync`
- [x] 3.2 Update concrete agents (`ResearchAgent`, `ContentWriterAgent`, `EditorAgent`, `MarkdownLinterAgent`, `SEOAgent`) to leverage the updated `BaseAgent` and verify all agent classes build without errors
- [x] 3.3 Update `README.md` tech stack and documentation to reflect .NET 10 and Microsoft Agent Framework 1.28.0

## 4. Build and End-to-End Verification

- [x] 4.1 Build the entire solution using `dotnet build` and verify 0 errors and 0 warnings
- [x] 4.2 Run the application with `blog-request.json` in a smoke test run and verify complete blog generation output

## Workflow follow-up

- Archive the change after verification is complete.
