using Microsoft.CodeAnalysis;

namespace DevPilot.Orchestrator.CodeAnalysis;

/// <summary>
/// Configuration options for code analysis.
/// </summary>
public sealed record AnalyzerOptions
{
    /// <summary>
    /// Gets whether code analysis is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the minimum diagnostic severity to include in results.
    /// Diagnostics below this severity will be filtered out.
    /// </summary>
    public DiagnosticSeverity MinimumSeverity { get; init; } = DiagnosticSeverity.Warning;

    /// <summary>
    /// Gets the list of diagnostic rule IDs to ignore.
    /// For example, ["CS1591"] to ignore missing XML documentation warnings.
    /// </summary>
    public List<string> IgnoredRuleIds { get; init; } = new();

    /// <summary>
    /// Gets the default analyzer options.
    /// </summary>
    public static AnalyzerOptions Default => new()
    {
        Enabled = true,
        MinimumSeverity = DiagnosticSeverity.Warning,
        IgnoredRuleIds = new List<string>()
    };
}
