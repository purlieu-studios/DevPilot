namespace DevPilot.Core;

/// <summary>
/// Defines the contract for a MASAI agent that can execute tasks.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Gets the agent definition.
    /// </summary>
    AgentDefinition Definition { get; }

    /// <summary>
    /// Executes the agent with the given input and context.
    /// </summary>
    /// <param name="input">The input for the agent to process.</param>
    /// <param name="context">The shared context for agent communication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the agent execution.</returns>
    Task<AgentResult> ExecuteAsync(
        string input,
        AgentContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of an agent execution.
/// </summary>
public sealed class AgentResult
{
    /// <summary>
    /// Gets whether the execution was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the output from the agent.
    /// </summary>
    public required string Output { get; init; }

    /// <summary>
    /// Gets the agent that produced this result.
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// Gets the error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets optional metadata about the execution.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Gets the execution duration.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="agentName">The agent name.</param>
    /// <param name="output">The output.</param>
    /// <param name="duration">The execution duration.</param>
    /// <returns>A successful agent result.</returns>
    public static AgentResult CreateSuccess(
        string agentName,
        string output,
        TimeSpan? duration = null)
    {
        return new AgentResult
        {
            Success = true,
            AgentName = agentName,
            Output = output,
            Duration = duration
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="agentName">The agent name.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="duration">The execution duration.</param>
    /// <returns>A failed agent result.</returns>
    public static AgentResult CreateFailure(
        string agentName,
        string errorMessage,
        TimeSpan? duration = null)
    {
        return new AgentResult
        {
            Success = false,
            AgentName = agentName,
            Output = string.Empty,
            ErrorMessage = errorMessage,
            Duration = duration
        };
    }
}
