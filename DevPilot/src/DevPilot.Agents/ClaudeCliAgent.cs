using System.Diagnostics;
using DevPilot.Core;

namespace DevPilot.Agents;

/// <summary>
/// Agent implementation that executes using Claude CLI as a subprocess.
/// </summary>
public sealed class ClaudeCliAgent : IAgent
{
    private readonly ClaudeCliClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeCliAgent"/> class.
    /// </summary>
    /// <param name="definition">The agent definition loaded from .agents/ folder.</param>
    /// <param name="client">Optional ClaudeCliClient instance (creates default if not provided).</param>
    public ClaudeCliAgent(AgentDefinition definition, ClaudeCliClient? client = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Definition = definition;
        _client = client ?? new ClaudeCliClient();
    }

    /// <summary>
    /// Gets the agent definition.
    /// </summary>
    public AgentDefinition Definition { get; }

    /// <summary>
    /// Executes the agent with the given input and context.
    /// </summary>
    /// <param name="input">The input for the agent to process.</param>
    /// <param name="context">The shared context for agent communication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the agent execution.</returns>
    public async Task<AgentResult> ExecuteAsync(
        string input,
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Record input message in context
            context.AddMessage(new AgentMessage
            {
                AgentName = Definition.Name,
                Role = MessageRole.User,
                Content = input
            });

            // Extract model name - try to use alias if possible, otherwise full name
            var modelName = ExtractModelAlias(Definition.Model.ModelName);

            // Execute via Claude CLI
            var response = await _client.ExecuteAsync(
                prompt: input,
                systemPrompt: Definition.SystemPrompt,
                model: modelName,
                timeout: null, // Use ClaudeCliClient default
                cancellationToken: cancellationToken);

            stopwatch.Stop();

            // Handle response
            if (response.Success)
            {
                // Record output message in context
                context.AddMessage(new AgentMessage
                {
                    AgentName = Definition.Name,
                    Role = MessageRole.Assistant,
                    Content = response.Output
                });

                return AgentResult.CreateSuccess(
                    agentName: Definition.Name,
                    output: response.Output,
                    duration: stopwatch.Elapsed);
            }
            else
            {
                // Record error in context metadata
                context.AddMessage(new AgentMessage
                {
                    AgentName = Definition.Name,
                    Role = MessageRole.Assistant,
                    Content = $"[ERROR] {response.ErrorMessage}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["ExitCode"] = response.ExitCode,
                        ["Success"] = false
                    }
                });

                return AgentResult.CreateFailure(
                    agentName: Definition.Name,
                    errorMessage: response.ErrorMessage ?? "Unknown error",
                    duration: stopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record exception in context
            context.AddMessage(new AgentMessage
            {
                AgentName = Definition.Name,
                Role = MessageRole.System,
                Content = $"[EXCEPTION] {ex.Message}",
                Metadata = new Dictionary<string, object>
                {
                    ["ExceptionType"] = ex.GetType().Name,
                    ["StackTrace"] = ex.StackTrace ?? string.Empty
                }
            });

            return AgentResult.CreateFailure(
                agentName: Definition.Name,
                errorMessage: $"Agent execution failed: {ex.Message}",
                duration: stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Extracts a short model alias from a full model name.
    /// </summary>
    /// <param name="fullModelName">The full model name (e.g., "claude-sonnet-4-5-20250929").</param>
    /// <returns>The model alias (e.g., "sonnet") or the full name if extraction fails.</returns>
    private static string ExtractModelAlias(string fullModelName)
    {
        // Try to extract common aliases from full model names
        // Examples:
        //   "claude-sonnet-4-5-20250929" → "sonnet"
        //   "claude-opus-4-20250514" → "opus"
        //   "claude-haiku-4-20250703" → "haiku"

        var lowerName = fullModelName.ToLowerInvariant();

        if (lowerName.Contains("sonnet"))
        {
            return "sonnet";
        }

        if (lowerName.Contains("opus"))
        {
            return "opus";
        }

        if (lowerName.Contains("haiku"))
        {
            return "haiku";
        }

        // If no known alias found, return full name
        // Claude CLI should handle full model names too
        return fullModelName;
    }
}
