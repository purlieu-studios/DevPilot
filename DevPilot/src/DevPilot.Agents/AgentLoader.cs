using System.Text.Json;
using System.Text.Json.Serialization;
using DevPilot.Core;

namespace DevPilot.Agents;

/// <summary>
/// Loads agent definitions from the .agents/ directory.
/// </summary>
public sealed class AgentLoader
{
    private readonly string _agentsDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentLoader"/> class.
    /// </summary>
    /// <param name="agentsDirectory">The path to the .agents/ directory.</param>
    public AgentLoader(string? agentsDirectory = null)
    {
        _agentsDirectory = agentsDirectory ?? Path.Combine(
            Directory.GetCurrentDirectory(),
            ".agents");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }

    /// <summary>
    /// Loads an agent definition by name.
    /// </summary>
    /// <param name="agentName">The agent name (e.g., "planner").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded agent definition.</returns>
    /// <exception cref="FileNotFoundException">When agent directory or required files not found.</exception>
    public async Task<AgentDefinition> LoadAgentAsync(
        string agentName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        var agentPath = Path.Combine(_agentsDirectory, agentName);
        if (!Directory.Exists(agentPath))
        {
            throw new FileNotFoundException(
                $"Agent directory not found: {agentPath}");
        }

        var configPath = Path.Combine(agentPath, "config.json");
        var systemPromptPath = Path.Combine(agentPath, "system-prompt.md");

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                $"Agent config not found: {configPath}");
        }

        if (!File.Exists(systemPromptPath))
        {
            throw new FileNotFoundException(
                $"Agent system prompt not found: {systemPromptPath}");
        }

        var configJson = await File.ReadAllTextAsync(configPath, cancellationToken);
        var config = JsonSerializer.Deserialize<AgentConfigDto>(configJson, _jsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize config for agent: {agentName}");

        var systemPrompt = await File.ReadAllTextAsync(systemPromptPath, cancellationToken);

        return new AgentDefinition
        {
            Name = config.AgentName,
            Version = config.Version,
            Description = config.Description,
            SystemPrompt = systemPrompt,
            Model = config.Model
        };
    }

    /// <summary>
    /// Discovers all available agents in the .agents/ directory.
    /// </summary>
    /// <returns>A list of available agent names.</returns>
    public IReadOnlyList<string> DiscoverAgents()
    {
        if (!Directory.Exists(_agentsDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory
            .GetDirectories(_agentsDirectory)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToList()
            .AsReadOnly();
    }

    // DTO for deserialization
    private sealed class AgentConfigDto
    {
        [JsonPropertyName("agent_name")]
        public required string AgentName { get; init; }

        [JsonPropertyName("version")]
        public required string Version { get; init; }

        [JsonPropertyName("description")]
        public required string Description { get; init; }

        [JsonPropertyName("model")]
        public required string Model { get; init; }
    }
}
