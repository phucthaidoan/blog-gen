# Spec Delta

## Purpose

Enables automated, multi-agent blog post creation using distinct Microsoft Agent Framework workflow orchestration topologies (sequential, concurrent fan-out/fan-in, and iterative writer-critic loop) alongside a baseline imperative pattern, unified under a single solution structure.

## ADDED Requirements

### Requirement: Sequential Blog Generation Pipeline
The system SHALL provide a sequential multi-agent workflow that executes research, drafting, editing, and final markdown formatting in a strict linear dependency chain.

#### Scenario: End-to-end sequential generation
- **WHEN** a user initiates blog generation with a topic and target audience
- **THEN** the workflow executes ResearchAgent, ContentWriterAgent, EditorAgent, and FormatterAgent in sequence, outputting a complete Markdown article with YAML frontmatter.

#### Scenario: Streaming executor progress
- **WHEN** the sequential workflow executes
- **THEN** progress updates from each executing agent are streamed to the console with current stage indicators.

### Requirement: Concurrent Multi-Perspective Review Workflow
The system SHALL provide a fan-out and fan-in workflow that concurrently analyzes drafted blog content across distinct editorial, SEO, and tone perspectives before aggregating feedback into a synthesized publication.

#### Scenario: Parallel review execution
- **WHEN** an initial draft is produced
- **THEN** StyleCritic, SeoCritic, and ToneCritic agents run concurrently against the draft, returning independent critiques.

#### Scenario: Feedback synthesis
- **WHEN** all concurrent critique agents finish their evaluations
- **THEN** SynthesizerAgent aggregates the original draft and all critique findings to produce an optimized, publication-ready markdown post.

### Requirement: Iterative Writer-Critic Loop Workflow
The system SHALL provide an evaluator-optimizer workflow that iteratively critiques and revises blog drafts until a quality score threshold is met or maximum configured iterations are reached.

#### Scenario: Revision requested when score is below threshold
- **WHEN** CriticExecutor rates a draft with a quality score below the configured approval threshold (e.g., 85/100) and iteration count is below maximum
- **THEN** the workflow routes feedback back to WriterExecutor to generate a revised draft addressing specific critiques.

#### Scenario: Approval and publication when threshold is reached
- **WHEN** CriticExecutor rates a draft at or above the approval threshold
- **THEN** the workflow routes output to PublisherExecutor to finalize and save the markdown post.

#### Scenario: Maximum iteration safety cutoff
- **WHEN** the loop reaches the configured maximum iterations without reaching the score threshold
- **THEN** the workflow halts the feedback cycle and publishes the latest draft accompanied by warning logs.

### Requirement: Multi-Provider Model Configuration
The system SHALL support configuring language models across Google Gemini, Azure OpenAI, OpenAI, and local endpoints via application settings in each standalone project.

#### Scenario: Valid provider initialization
- **WHEN** a valid API key and model identifier are provided in configuration
- **THEN** AgentClientFactory successfully initializes the corresponding chat client with appropriate HTTP resilience handlers.

#### Scenario: Missing configuration reporting
- **WHEN** no supported provider configuration is supplied
- **THEN** the application throws an informative configuration error indicating missing configuration keys.

### Requirement: Unified Solution and Project Hierarchy
The system SHALL provide a single solution file (`blog-gen.sln`) that includes the baseline imperative project and all workflow projects organized at an equal directory hierarchy level.

#### Scenario: Unified solution build
- **WHEN** `dotnet build blog-gen.sln` is executed from the repository root
- **THEN** all four projects (Imperative, Sequential, Concurrent, WriterCritic) compile with 0 errors.

#### Scenario: Symmetrical project organization
- **WHEN** inspecting the `workflows/` directory
- **THEN** each approach (Imperative, Sequential, Concurrent, WriterCritic) resides in its own dedicated subdirectory containing its own project file, assets, and documentation.
