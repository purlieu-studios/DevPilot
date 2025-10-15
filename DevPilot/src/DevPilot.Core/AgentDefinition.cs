namespace DevPilot.Core;

/// <summary>
/// Represents the complete definition of a MASAI agent loaded from .agents/ folder.
/// </summary>
public sealed class AgentDefinition
{
    /// <summary>
    /// Gets the unique name of the agent (e.g., "planner", "coder", "reviewer").
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
    /// Gets the model alias to use (e.g., "sonnet", "opus", "haiku").
    /// </summary>
    public required string Model { get; init; }
    /// <summary>
    /// Gets the optional MCP (Model Context Protocol) configuration file path.
    /// When set, Claude CLI will be invoked with --mcp-config flag.
    /// </summary>
    public string? McpConfigPath { get; init; }
}
