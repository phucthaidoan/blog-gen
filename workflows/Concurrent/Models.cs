// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace BlogGen.Workflow.Concurrent;

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
/// Individual critique feedback from a concurrent review specialist.
/// </summary>
public sealed class ReviewFeedback
{
    public string CriticType { get; set; } = string.Empty;
    public int Score { get; set; }
    public List<string> Strengths { get; set; } = [];
    public List<string> Weaknesses { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
}

/// <summary>
/// Final synthesized blog post.
/// </summary>
public sealed class BlogPost
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}
