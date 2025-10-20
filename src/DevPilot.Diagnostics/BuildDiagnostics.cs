using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DevPilot.Diagnostics;

/// <summary>
/// Analyzes build errors and categorizes them by type and frequency.
/// </summary>
public sealed class BuildDiagnostics
{
    /// <summary>
    /// Runs build diagnostics on the current directory.
    /// </summary>
    public static async Task<DiagnosticResult> RunAsync(string? workingDirectory = null)
    {
        var startTime = DateTime.UtcNow;
        workingDirectory ??= Directory.GetCurrentDirectory();

        // Run dotnet build and capture output
        var (exitCode, output, errors) = await RunDotnetBuildAsync(workingDirectory);

        var categories = new List<DiagnosticCategory>();

        if (exitCode == 0)
        {
            // Build succeeded
            return new DiagnosticResult(
                "Build Diagnostics",
                new List<DiagnosticCategory>
                {
                    new DiagnosticCategory(
                        "Build Succeeded",
                        "Project compiled successfully with no errors",
                        DiagnosticSeverity.Info,
                        new List<DiagnosticIssue>
                        {
                            new DiagnosticIssue("Build completed successfully")
                        })
                },
                DateTime.UtcNow - startTime);
        }

        // Parse build output for errors and warnings
        var fullOutput = output + "\n" + errors;
        var issues = ParseBuildIssues(fullOutput);

        if (issues.Count == 0)
        {
            // Build failed but no parseable errors
            categories.Add(new DiagnosticCategory(
                "Unknown Build Failure",
                "Build failed but no specific errors were identified",
                DiagnosticSeverity.Critical,
                new List<DiagnosticIssue>
                {
                    new DiagnosticIssue("Check build output for details")
                    {
                        Context = fullOutput.Length > 500 ? fullOutput.Substring(0, 500) + "..." : fullOutput
                    }
                }));
        }
        else
        {
            // Group by error code
            var errorGroups = issues
                .Where(i => i.Context?.Contains("error") == true)
                .GroupBy(i => ExtractErrorCode(i.Message))
                .OrderByDescending(g => g.Count());

            foreach (var group in errorGroups)
            {
                var errorCode = group.Key;
                var errorIssues = group.ToList();
                var suggestedFix = GetSuggestedFixForErrorCode(errorCode, errorIssues);

                var severity = errorIssues.Count >= 10 ? DiagnosticSeverity.Critical : DiagnosticSeverity.Error;

                categories.Add(new DiagnosticCategory(
                    $"{errorCode} ({errorIssues.Count} occurrence{(errorIssues.Count > 1 ? "s" : "")})",
                    GetErrorCodeDescription(errorCode),
                    severity,
                    errorIssues)
                {
                    SuggestedFix = suggestedFix
                });
            }

            // Group warnings separately
            var warningGroups = issues
                .Where(i => i.Context?.Contains("warning") == true)
                .GroupBy(i => ExtractErrorCode(i.Message))
                .OrderByDescending(g => g.Count());

            foreach (var group in warningGroups.Take(5)) // Limit to top 5 warning types
            {
                var warningCode = group.Key;
                var warningIssues = group.ToList();

                categories.Add(new DiagnosticCategory(
                    $"{warningCode} ({warningIssues.Count} occurrence{(warningIssues.Count > 1 ? "s" : "")})",
                    GetErrorCodeDescription(warningCode),
                    DiagnosticSeverity.Warning,
                    warningIssues));
            }
        }

        return new DiagnosticResult("Build Diagnostics", categories, DateTime.UtcNow - startTime);
    }

    private static async Task<(int exitCode, string output, string errors)> RunDotnetBuildAsync(string workingDirectory)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build --no-restore",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }

    private static List<DiagnosticIssue> ParseBuildIssues(string buildOutput)
    {
        var issues = new List<DiagnosticIssue>();

        // Pattern to match MSBuild error/warning format:
        // Example: "C:\Path\File.cs(45,10): error CS1503: Argument 1: cannot convert from 'string' to 'int'"
        var pattern = new Regex(@"^(.+?)\((\d+),\d+\):\s+(error|warning)\s+(\w+):\s+(.+)$", RegexOptions.Multiline);

        var matches = pattern.Matches(buildOutput);

        foreach (Match match in matches)
        {
            var file = match.Groups[1].Value.Trim();
            var line = int.Parse(match.Groups[2].Value);
            var severity = match.Groups[3].Value; // "error" or "warning"
            var code = match.Groups[4].Value;
            var message = match.Groups[5].Value.Trim();

            issues.Add(new DiagnosticIssue(
                $"{code}: {message}",
                file,
                line)
            {
                Context = severity
            });
        }

        return issues;
    }

    private static string ExtractErrorCode(string message)
    {
        // Extract error code from message (e.g., "CS1503: ..." -> "CS1503")
        var match = Regex.Match(message, @"^(\w+):");
        return match.Success ? match.Groups[1].Value : "Unknown";
    }

    private static string GetErrorCodeDescription(string errorCode)
    {
        return errorCode switch
        {
            "CS1503" => "Argument type mismatch",
            "CS0246" => "Missing type or namespace",
            "CS1061" => "Member not found",
            "CS0103" => "Name does not exist in context",
            "CS0029" => "Cannot implicitly convert type",
            "CS1002" => "Syntax error (expected ;)",
            "CS1001" => "Syntax error (expected identifier)",
            "CS0161" => "Method must return a value",
            "CS8603" => "Possible null reference return",
            "CS8600" => "Possible null reference assignment",
            "CS8602" => "Dereference of possibly null reference",
            "CS0101" => "Duplicate member definition",
            "CS0111" => "Type already defines member with same parameters",
            _ => "Build issue"
        };
    }

    private static string? GetSuggestedFixForErrorCode(string errorCode, List<DiagnosticIssue> issues)
    {
        var firstIssue = issues.FirstOrDefault();
        var message = firstIssue?.Message ?? "";

        return errorCode switch
        {
            "CS1503" when message.Contains("WorkspaceType") || message.Contains("CancellationToken") =>
                "Add WorkspaceType parameter to CreateWorkspace() calls. Replace (id, dir, token) with (id, dir, WorkspaceType.Test)",

            "CS1503" =>
                $"Check parameter types in method calls. Ensure arguments match method signature.\nFirst occurrence: {firstIssue?.File}:{firstIssue?.Line}",

            "CS0246" =>
                "Add missing using directive or verify project references are correct",

            "CS1061" =>
                "Check method/property name spelling or verify the type has this member",

            "CS0103" =>
                "Variable may not be declared or is out of scope - check variable name and declaration",

            "CS8603" or "CS8600" or "CS8602" =>
                "Add null checks or use nullable reference types (Type?) where nulls are expected",

            "CS0101" or "CS0111" =>
                "Remove duplicate member definitions - same name/signature defined multiple times",

            _ => $"Review error details for pattern. Affected files:\n{string.Join("\n", issues.Take(3).Select(i => $"  - {i.File}:{i.Line}"))}"
        };
    }
}
