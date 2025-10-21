using Microsoft.CodeAnalysis;

namespace DevPilot.Orchestrator.CodeAnalysis;

/// <summary>
/// Represents the result of code analysis using Roslyn analyzers.
/// </summary>
public sealed record AnalysisResult
{
    /// <summary>
    /// Gets the list of diagnostics found during analysis.
    /// </summary>
    public required List<CodeDiagnostic> Diagnostics { get; init; }

    /// <summary>
    /// Gets whether the analysis found any error-level diagnostics.
    /// </summary>
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// Gets whether the analysis found any warning-level diagnostics.
    /// </summary>
    public bool HasWarnings => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);

    /// <summary>
    /// Gets the total count of diagnostics.
    /// </summary>
    public int TotalCount => Diagnostics.Count;

    /// <summary>
    /// Groups diagnostics by severity for reporting.
    /// </summary>
    /// <returns>Dictionary mapping severity to diagnostic count.</returns>
    public Dictionary<DiagnosticSeverity, int> GetCountsBySeverity()
    {
        return Diagnostics
            .GroupBy(d => d.Severity)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Creates an empty analysis result (no diagnostics found).
    /// </summary>
    public static AnalysisResult Empty => new() { Diagnostics = new List<CodeDiagnostic>() };
}

/// <summary>
/// Represents a single diagnostic (error, warning, or info) from code analysis.
/// </summary>
public sealed record CodeDiagnostic
{
    /// <summary>
    /// Gets the file path where the diagnostic was found.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the line number (1-based) where the diagnostic occurs.
    /// </summary>
    public required int LineNumber { get; init; }

    /// <summary>
    /// Gets the diagnostic rule ID (e.g., CS1591, CA1062).
    /// </summary>
    public required string RuleId { get; init; }

    /// <summary>
    /// Gets the severity of the diagnostic.
    /// </summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the diagnostic category (e.g., "Compiler", "Style", "Design").
    /// </summary>
    public string? Category { get; init; }
}
