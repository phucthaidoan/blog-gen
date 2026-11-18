// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace BlogGen.Workflow.Sequential;

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
/// Intermediate structured outline produced by ResearchAgent.
/// </summary>
public sealed class BlogOutline
{
    public string Title { get; set; } = string.Empty;
    public List<string> TargetKeywords { get; set; } = [];
    public List<string> Sections { get; set; } = [];
    public List<string> KeyTakeaways { get; set; } = [];
}

/// <summary>
/// Final blog post output.
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
