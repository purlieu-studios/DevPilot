namespace DevPilot.Diagnostics;

/// <summary>
/// Result of a diagnostic scan.
/// </summary>
public sealed class DiagnosticResult
{
    public DiagnosticResult(string title, List<DiagnosticCategory> categories, TimeSpan duration)
    {
        Title = title;
        Categories = categories;
        Duration = duration;
        TotalIssues = categories.Sum(c => c.Issues.Count);
    }

    /// <summary>
    /// Title of the diagnostic scan (e.g., "Test Diagnostics", "Build Diagnostics").
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Categories of issues found, grouped by type/pattern.
    /// </summary>
    public List<DiagnosticCategory> Categories { get; }

    /// <summary>
    /// Total number of issues across all categories.
    /// </summary>
    public int TotalIssues { get; }

    /// <summary>
    /// Time taken to run the diagnostic scan.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Whether any issues were found.
    /// </summary>
    public bool HasIssues => TotalIssues > 0;
}

/// <summary>
/// A category of related diagnostic issues.
/// </summary>
public sealed class DiagnosticCategory
{
    public DiagnosticCategory(string name, string description, DiagnosticSeverity severity, List<DiagnosticIssue> issues)
    {
        Name = name;
        Description = description;
        Severity = severity;
        Issues = issues;
    }

    /// <summary>
    /// Category name (e.g., "Missing Parameter", "Build Errors").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Human-readable description of the category.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Severity level of issues in this category.
    /// </summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>
    /// Individual issues in this category.
    /// </summary>
    public List<DiagnosticIssue> Issues { get; }

    /// <summary>
    /// Suggested fix for issues in this category.
    /// </summary>
    public string? SuggestedFix { get; init; }
}

/// <summary>
/// An individual diagnostic issue.
/// </summary>
public sealed class DiagnosticIssue
{
    public DiagnosticIssue(string message, string? file = null, int? line = null)
    {
        Message = message;
        File = file;
        Line = line;
    }

    /// <summary>
    /// Issue message (e.g., error text, description).
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// File where the issue was found (if applicable).
    /// </summary>
    public string? File { get; }

    /// <summary>
    /// Line number where the issue was found (if applicable).
    /// </summary>
    public int? Line { get; }

    /// <summary>
    /// Additional context or details.
    /// </summary>
    public string? Context { get; init; }
}

/// <summary>
/// Severity level of a diagnostic issue.
/// </summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
    Critical
}
