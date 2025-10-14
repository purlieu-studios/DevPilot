using System.Text.Json;
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
    /// <param name="agentName">The agent name (e.g., "orchestrator").</param>
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
        var toolsPath = Path.Combine(agentPath, "tools.json");

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

        List<AgentTool>? tools = null;
        if (File.Exists(toolsPath))
        {
            var toolsJson = await File.ReadAllTextAsync(toolsPath, cancellationToken);
            var toolsDto = JsonSerializer.Deserialize<ToolsDto>(toolsJson, _jsonOptions);
            tools = toolsDto?.Tools?.Select(t => new AgentTool
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.Parameters
            }).ToList();
        }

        return new AgentDefinition
        {
            Name = config.AgentName,
            Version = config.Version,
            Description = config.Description,
            SystemPrompt = systemPrompt,
            Model = new ModelConfiguration
            {
                Provider = config.Model.Provider,
                ModelName = config.Model.ModelName,
                Temperature = config.Model.Temperature,
                MaxTokens = config.Model.MaxTokens,
                Reasoning = config.Model.Reasoning != null
                    ? new ReasoningConfiguration
                    {
                        Enabled = config.Model.Reasoning.Enabled,
                        Type = config.Model.Reasoning.Type
                    }
                    : null
            },
            Capabilities = config.Capabilities,
            Tools = tools?.AsReadOnly(),
            RetryPolicy = config.RetryPolicy != null
                ? new RetryPolicy
                {
                    MaxRetries = config.RetryPolicy.MaxRetries,
                    RetryDelayMs = config.RetryPolicy.RetryDelayMs,
                    ExponentialBackoff = config.RetryPolicy.ExponentialBackoff
                }
                : null
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

    // DTOs for deserialization
    private sealed class AgentConfigDto
    {
        public required string AgentName { get; init; }
        public required string Version { get; init; }
        public required string Description { get; init; }
        public required ModelDto Model { get; init; }
        public required List<string> Capabilities { get; init; }
        public RetryPolicyDto? RetryPolicy { get; init; }
    }

    private sealed class ModelDto
    {
        public required string Provider { get; init; }
        public required string ModelName { get; init; }
        public double Temperature { get; init; }
        public int MaxTokens { get; init; }
        public ReasoningDto? Reasoning { get; init; }
    }

    private sealed class ReasoningDto
    {
        public bool Enabled { get; init; }
        public string? Type { get; init; }
    }

    private sealed class RetryPolicyDto
    {
        public int MaxRetries { get; init; }
        public int RetryDelayMs { get; init; }
        public bool ExponentialBackoff { get; init; }
    }

    private sealed class ToolsDto
    {
        public List<ToolDto>? Tools { get; init; }
    }

    private sealed class ToolDto
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
        public object? Parameters { get; init; }
    }
}
