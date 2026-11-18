// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace BlogGen.Workflow.WriterCritic;

/// <summary>
/// User request for blog post generation.
/// </summary>
public sealed class BlogRequest
{
    public string Topic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetAudience { get; set; } = "Software Developers";
    public string Tone { get; set; } = "Informative, Engaging, and Practical";
    public int WordCount { get; set; } = 800;
}

/// <summary>
/// Structured decision emitted by the CriticExecutor for workflow branching.
/// </summary>
public sealed class CriticDecision
{
    [JsonPropertyName("approved")]
    public bool Approved { get; set; }

    [JsonPropertyName("qualityScore")]
    public int QualityScore { get; set; }

    [JsonPropertyName("critiques")]
    public List<string> Critiques { get; set; } = [];

    [JsonPropertyName("actionableRecommendations")]
    public List<string> ActionableRecommendations { get; set; } = [];

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// State tracking for writer and critic iterations.
/// </summary>
public sealed class IterationState
{
    public int CurrentIteration { get; set; } = 1;
    public string LatestDraft { get; set; } = string.Empty;
    public CriticDecision? LatestDecision { get; set; }
}

/// <summary>
/// Final published blog post.
/// </summary>
public sealed class BlogPost
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int FinalQualityScore { get; set; }
    public int TotalIterations { get; set; }
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}
