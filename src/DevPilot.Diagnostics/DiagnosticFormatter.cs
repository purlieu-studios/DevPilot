using Spectre.Console;

namespace DevPilot.Diagnostics;

/// <summary>
/// Formats diagnostic results for console output using Spectre.Console.
/// </summary>
public static class DiagnosticFormatter
{
    /// <summary>
    /// Displays a diagnostic result to the console.
    /// </summary>
    public static void Display(DiagnosticResult result)
    {
        // Header
        AnsiConsole.Write(new Rule($"[bold]{result.Title}[/]"));
        AnsiConsole.WriteLine();

        // Summary
        var summaryColor = result.HasIssues ? "yellow" : "green";
        var summaryIcon = result.HasIssues ? "⚠" : "✓";

        AnsiConsole.MarkupLine(
            $"[{summaryColor}]{summaryIcon} Found {result.TotalIssues} issue(s) across {result.Categories.Count} category(ies)[/] [dim](scanned in {result.Duration.TotalSeconds:F1}s)[/]");
        AnsiConsole.WriteLine();

        // Categories
        foreach (var category in result.Categories)
        {
            DisplayCategory(category);
            AnsiConsole.WriteLine();
        }
    }

    private static void DisplayCategory(DiagnosticCategory category)
    {
        var severityColor = GetSeverityColor(category.Severity);
        var severityIcon = GetSeverityIcon(category.Severity);

        var panel = new Panel(BuildCategoryContent(category))
        {
            Header = new PanelHeader($" {severityIcon} {category.Name} ", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(severityColor)
        };

        AnsiConsole.Write(panel);
    }

    private static Markup BuildCategoryContent(DiagnosticCategory category)
    {
        var content = new System.Text.StringBuilder();

        // Description
        content.AppendLine($"[bold]{category.Description}[/]");
        content.AppendLine();

        // Issues (limit to 10 for readability)
        var displayedIssues = category.Issues.Take(10).ToList();

        foreach (var issue in displayedIssues)
        {
            var location = issue.File != null && issue.Line.HasValue
                ? $" [dim]({issue.File}:{issue.Line})[/]"
                : issue.File != null
                    ? $" [dim]({issue.File})[/]"
                    : "";

            content.AppendLine($"  • {Markup.Escape(issue.Message)}{location}");

            if (issue.Context != null && !string.IsNullOrWhiteSpace(issue.Context))
            {
                var context = issue.Context.Length > 200
                    ? issue.Context.Substring(0, 200) + "..."
                    : issue.Context;
                content.AppendLine($"    [dim]{Markup.Escape(context)}[/]");
            }
        }

        if (category.Issues.Count > 10)
        {
            content.AppendLine($"  [dim]... and {category.Issues.Count - 10} more[/]");
        }

        // Suggested fix
        if (!string.IsNullOrEmpty(category.SuggestedFix))
        {
            content.AppendLine();
            content.AppendLine("[bold yellow]💡 Suggested Fix:[/]");
            content.AppendLine($"[dim]{Markup.Escape(category.SuggestedFix)}[/]");
        }

        return new Markup(content.ToString().TrimEnd());
    }

    private static Color GetSeverityColor(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Info => Color.Blue,
            DiagnosticSeverity.Warning => Color.Yellow,
            DiagnosticSeverity.Error => Color.Red,
            DiagnosticSeverity.Critical => Color.Red1,
            _ => Color.Grey
        };
    }

    private static string GetSeverityIcon(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Info => "ℹ",
            DiagnosticSeverity.Warning => "⚠",
            DiagnosticSeverity.Error => "❌",
            DiagnosticSeverity.Critical => "🔴",
            _ => "•"
        };
    }
}
