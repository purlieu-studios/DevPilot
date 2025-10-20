using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DevPilot.Diagnostics;

/// <summary>
/// Analyzes test failures and identifies patterns.
/// </summary>
public sealed class TestDiagnostics
{
    /// <summary>
    /// Runs test diagnostics on the current directory.
    /// </summary>
    public static async Task<DiagnosticResult> RunAsync(string? workingDirectory = null)
    {
        var startTime = DateTime.UtcNow;
        workingDirectory ??= Directory.GetCurrentDirectory();

        // Run dotnet test and capture output
        var (exitCode, output, errors) = await RunDotnetTestAsync(workingDirectory);

        var categories = new List<DiagnosticCategory>();

        if (exitCode == 0)
        {
            // All tests passed
            return new DiagnosticResult(
                "Test Diagnostics",
                new List<DiagnosticCategory>
                {
                    new DiagnosticCategory(
                        "All Tests Passed",
                        "All tests executed successfully",
                        DiagnosticSeverity.Info,
                        new List<DiagnosticIssue>
                        {
                            new DiagnosticIssue("No test failures detected")
                        })
                },
                DateTime.UtcNow - startTime);
        }

        // Parse test output for failures
        var fullOutput = output + "\n" + errors;
        var failures = ParseTestFailures(fullOutput);

        if (failures.Count == 0)
        {
            // Build failed before tests could run
            categories.Add(new DiagnosticCategory(
                "Build Failed",
                "Tests could not run due to build errors",
                DiagnosticSeverity.Critical,
                new List<DiagnosticIssue>
                {
                    new DiagnosticIssue("Build failed - run 'devpilot diagnose build' for details")
                }));
        }
        else
        {
            // Group failures by pattern
            var groupedFailures = GroupFailuresByPattern(failures);

            foreach (var group in groupedFailures.OrderByDescending(g => g.Value.Count))
            {
                var (pattern, issues) = group;
                var suggestedFix = GetSuggestedFix(pattern, issues);

                categories.Add(new DiagnosticCategory(
                    pattern,
                    $"{issues.Count} test(s) failed with this pattern",
                    DiagnosticSeverity.Error,
                    issues)
                {
                    SuggestedFix = suggestedFix
                });
            }
        }

        return new DiagnosticResult("Test Diagnostics", categories, DateTime.UtcNow - startTime);
    }

    private static async Task<(int exitCode, string output, string errors)> RunDotnetTestAsync(string workingDirectory)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "test --no-build --verbosity detailed",
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

    private static List<DiagnosticIssue> ParseTestFailures(string testOutput)
    {
        var failures = new List<DiagnosticIssue>();

        // Pattern to match test failures (xUnit format)
        // Example: "X DevPilot.Tests.PipelineTests.Test_Method [FAIL]"
        var failurePattern = new Regex(@"^\s*X\s+([\w\.]+)\s+\[FAIL\]", RegexOptions.Multiline);

        // Pattern to match error details
        var errorPattern = new Regex(@"Error Message:\s*(.+?)(?=\s*Stack Trace:|$)", RegexOptions.Singleline);

        // Pattern to match file locations in stack traces
        var filePattern = new Regex(@"in\s+(.+?):line\s+(\d+)", RegexOptions.Multiline);

        var failureMatches = failurePattern.Matches(testOutput);

        foreach (Match match in failureMatches)
        {
            var testName = match.Groups[1].Value;
            var startIndex = match.Index;

            // Find error message after this failure
            var errorMatch = errorPattern.Match(testOutput, startIndex);
            var errorMessage = errorMatch.Success ? errorMatch.Groups[1].Value.Trim() : "Unknown error";

            // Find file location
            var fileMatch = filePattern.Match(testOutput, startIndex);
            var file = fileMatch.Success ? fileMatch.Groups[1].Value : null;
            var line = fileMatch.Success && int.TryParse(fileMatch.Groups[2].Value, out var l) ? l : (int?)null;

            failures.Add(new DiagnosticIssue(
                testName,
                file,
                line)
            {
                Context = errorMessage
            });
        }

        return failures;
    }

    private static Dictionary<string, List<DiagnosticIssue>> GroupFailuresByPattern(List<DiagnosticIssue> failures)
    {
        var groups = new Dictionary<string, List<DiagnosticIssue>>();

        foreach (var failure in failures)
        {
            var pattern = IdentifyPattern(failure);

            if (!groups.ContainsKey(pattern))
            {
                groups[pattern] = new List<DiagnosticIssue>();
            }

            groups[pattern].Add(failure);
        }

        return groups;
    }

    private static string IdentifyPattern(DiagnosticIssue failure)
    {
        var context = failure.Context ?? "";

        // Check for common error patterns
        if (context.Contains("CS1503") || context.Contains("cannot convert") || context.Contains("Argument"))
        {
            if (context.Contains("WorkspaceType") || context.Contains("CancellationToken"))
            {
                return "Missing WorkspaceType Parameter";
            }
            return "Type Conversion Error";
        }

        if (context.Contains("CS0246") || context.Contains("type or namespace"))
        {
            return "Missing Type/Namespace";
        }

        if (context.Contains("Expected:") && context.Contains("Actual:"))
        {
            return "Assertion Failure";
        }

        if (context.Contains("NullReferenceException"))
        {
            return "Null Reference";
        }

        if (context.Contains("FileNotFoundException") || context.Contains("DirectoryNotFoundException"))
        {
            return "File/Directory Not Found";
        }

        if (context.Contains("IOException") || context.Contains("locked") || context.Contains("in use"))
        {
            return "File Lock/IO Error";
        }

        if (context.Contains("TimeoutException") || context.Contains("timed out"))
        {
            return "Timeout";
        }

        return "Other Error";
    }

    private static string? GetSuggestedFix(string pattern, List<DiagnosticIssue> issues)
    {
        return pattern switch
        {
            "Missing WorkspaceType Parameter" =>
                "Add WorkspaceType.Test parameter to WorkspaceManager.CreateWorkspace() calls in test files",

            "Type Conversion Error" =>
                "Check parameter types and order in method calls - ensure arguments match the method signature",

            "Missing Type/Namespace" =>
                "Add missing using directives or verify project references are correct",

            "File/Directory Not Found" =>
                "Verify file paths are correct and files exist - check for typos or incorrect relative paths",

            "File Lock/IO Error" =>
                "Run 'devpilot diagnose workspace' to identify locked files and processes",

            "Assertion Failure" =>
                "Review test expectations - actual values don't match expected values",

            "Null Reference" =>
                "Add null checks or ensure objects are properly initialized before use",

            "Timeout" =>
                "Increase timeout values or investigate performance issues causing slow operations",

            _ => null
        };
    }
}
