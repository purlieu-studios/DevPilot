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

            // Execute via Claude CLI using model alias from definition
            var response = await _client.ExecuteAsync(
                prompt: input,
                systemPrompt: Definition.SystemPrompt,
                model: Definition.Model,  // Direct use - "sonnet", "opus", or "haiku"
                mcpConfigPath: Definition.McpConfigPath, // Optional MCP config
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
}
