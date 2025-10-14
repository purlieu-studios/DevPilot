namespace DevPilot.Core;

/// <summary>
/// Represents the complete definition of a MASAI agent loaded from .agents/ folder.
/// </summary>
public sealed class AgentDefinition
{
    /// <summary>
    /// Gets the unique name of the agent (e.g., "orchestrator", "code-generator").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the agent version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the agent description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the system prompt loaded from system-prompt.md.
    /// </summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// Gets the model configuration for this agent.
    /// </summary>
    public required ModelConfiguration Model { get; init; }

    /// <summary>
    /// Gets the list of capabilities this agent supports.
    /// </summary>
    public required IReadOnlyList<string> Capabilities { get; init; }

    /// <summary>
    /// Gets the tools available to this agent.
    /// </summary>
    public IReadOnlyList<AgentTool>? Tools { get; init; }

    /// <summary>
    /// Gets the retry policy configuration.
    /// </summary>
    public RetryPolicy? RetryPolicy { get; init; }
}

/// <summary>
/// Represents model configuration for an agent.
/// </summary>
public sealed class ModelConfiguration
{
    /// <summary>
    /// Gets the model provider (e.g., "anthropic", "openai").
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Gets the specific model name.
    /// </summary>
    public required string ModelName { get; init; }

    /// <summary>
    /// Gets the temperature setting for randomness.
    /// </summary>
    public double Temperature { get; init; }

    /// <summary>
    /// Gets the maximum tokens to generate.
    /// </summary>
    public int MaxTokens { get; init; }

    /// <summary>
    /// Gets the reasoning configuration.
    /// </summary>
    public ReasoningConfiguration? Reasoning { get; init; }
}

/// <summary>
/// Represents reasoning configuration for extended thinking models.
/// </summary>
public sealed class ReasoningConfiguration
{
    /// <summary>
    /// Gets whether reasoning is enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the reasoning type (e.g., "extended").
    /// </summary>
    public string? Type { get; init; }
}

/// <summary>
/// Represents a tool available to an agent.
/// </summary>
public sealed class AgentTool
{
    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the tool parameters schema.
    /// </summary>
    public object? Parameters { get; init; }
}

/// <summary>
/// Represents retry policy configuration.
/// </summary>
public sealed class RetryPolicy
{
    /// <summary>
    /// Gets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; init; }

    /// <summary>
    /// Gets the delay between retries in milliseconds.
    /// </summary>
    public int RetryDelayMs { get; init; }

    /// <summary>
    /// Gets whether to use exponential backoff.
    /// </summary>
    public bool ExponentialBackoff { get; init; }
}
