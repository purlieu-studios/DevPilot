using System.Text.Json.Serialization;

namespace DevPilot.Core;

/// <summary>
/// Represents the configuration for DevPilot loaded from devpilot.json in the target repository.
/// This configuration allows target repositories to customize workspace file copying behavior.
/// </summary>
public sealed class DevPilotConfig
{
    /// <summary>
    /// Gets the additional folders to copy from the target repository to the workspace.
    /// These folders are copied in addition to the default set (src/, tests/, docs/).
    /// Example: ["migrations/", "config/", "scripts/"]
    /// </summary>
    [JsonPropertyName("folders")]
    public string[]? Folders { get; init; }

    /// <summary>
    /// Gets whether to copy all files from the target repository to the workspace.
    /// When true, overrides selective copying and copies the entire repository.
    /// Default: false (selective copying)
    /// </summary>
    [JsonPropertyName("copyAllFiles")]
    public bool? CopyAllFiles { get; init; }

    /// <summary>
    /// Creates a default configuration with no additional folders and selective copying enabled.
    /// </summary>
    public static DevPilotConfig Default => new DevPilotConfig
    {
        Folders = null,
        CopyAllFiles = false
    };
}
