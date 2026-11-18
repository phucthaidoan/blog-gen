using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace BlogPostGenerator.Framework;

/// <summary>
/// Base class for all agents in the Microsoft Agent Framework implementation
/// </summary>
public abstract class BaseAgent
{
    protected readonly IChatClient _chatClient;
    protected readonly ILogger _logger;
    protected readonly AIAgent _agent;

    protected BaseAgent(IChatClient chatClient, ILogger logger, string? name = null, string? instructions = null, string? description = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agent = new ChatClientAgent(
            chatClient: chatClient,
            instructions: instructions,
            name: name ?? GetType().Name,
            description: description);
    }

    protected async Task<string> ExecutePromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
            return response.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing prompt for {AgentType}", GetType().Name);
            throw;
        }
    }
}