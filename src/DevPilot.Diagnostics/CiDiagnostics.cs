using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevPilot.Diagnostics;

/// <summary>
/// Analyzes GitHub Actions CI logs for failures and common issues.
/// </summary>
public sealed class CiDiagnostics
{
    /// <summary>
    /// Runs CI diagnostics by fetching and analyzing latest GitHub Actions run.
    /// </summary>
    public static async Task<DiagnosticResult> RunAsync(string? workingDirectory = null)
    {
        var startTime = DateTime.UtcNow;
        workingDirectory ??= Directory.GetCurrentDirectory();

        var categories = new List<DiagnosticCategory>();

        // Check if gh CLI is available
        if (!await IsGitHubCliAvailableAsync())
        {
            return new DiagnosticResult(
                "CI Diagnostics",
                new List<DiagnosticCategory>
                {
                    new DiagnosticCategory(
                        "GitHub CLI Not Available",
                        "Cannot analyze CI logs without gh CLI tool",
                        DiagnosticSeverity.Warning,
                        new List<DiagnosticIssue>
                        {
                            new DiagnosticIssue("Install GitHub CLI: https://cli.github.com")
                        })
                },
                DateTime.UtcNow - startTime);
        }

        // Get latest CI run
        var (latestRun, error) = await GetLatestCiRunAsync(workingDirectory);

        if (error != null)
        {
            return new DiagnosticResult(
                "CI Diagnostics",
                new List<DiagnosticCategory>
                {
                    new DiagnosticCategory(
                        "CI Data Unavailable",
                        "Failed to fetch CI run data",
                        DiagnosticSeverity.Error,
                        new List<DiagnosticIssue>
                        {
                            new DiagnosticIssue(error)
                        })
                },
                DateTime.UtcNow - startTime);
        }

        if (latestRun == null)
        {
            return new DiagnosticResult(
                "CI Diagnostics",
                new List<DiagnosticCategory>
                {
                    new DiagnosticCategory(
                        "No CI Runs",
                        "No GitHub Actions runs found for this repository",
                        DiagnosticSeverity.Info,
                        new List<DiagnosticIssue>
                        {
                            new DiagnosticIssue("No CI runs to analyze")
                        })
                },
                DateTime.UtcNow - startTime);
        }

        // Parse run status
        var runNumber = latestRun.Value.GetProperty("number").GetInt32();
        var status = latestRun.Value.GetProperty("status").GetString();
        var conclusion = latestRun.Value.TryGetProperty("conclusion", out var conclusionProp)
            ? conclusionProp.GetString()
            : null;

        if (conclusion == "success")
        {
            return new DiagnosticResult(
                "CI Diagnostics",
                new List<DiagnosticCategory>
                {
                    new DiagnosticCategory(
                        $"CI Run #{runNumber} Passed",
                        "Latest CI run completed successfully",
                        DiagnosticSeverity.Info,
                        new List<DiagnosticIssue>
                        {
                            new DiagnosticIssue("All checks passed")
                        })
                },
                DateTime.UtcNow - startTime);
        }

        // Get failed jobs
        var jobs = await GetFailedJobsAsync(runNumber.ToString(), workingDirectory);

        if (jobs.Count == 0)
        {
            categories.Add(new DiagnosticCategory(
                $"CI Run #{runNumber} Status: {conclusion ?? status}",
                "No specific job failures identified",
                DiagnosticSeverity.Warning,
                new List<DiagnosticIssue>
                {
                    new DiagnosticIssue($"Status: {conclusion ?? status}")
                }));
        }
        else
        {
            foreach (var (jobName, jobConclusion, logs) in jobs)
            {
                var issues = ParseJobLogs(logs);

                categories.Add(new DiagnosticCategory(
                    $"Job: {jobName} ({jobConclusion})",
                    $"CI job failed in run #{runNumber}",
                    DiagnosticSeverity.Error,
                    issues)
                {
                    SuggestedFix = GetSuggestedFixFromLogs(logs)
                });
            }
        }

        return new DiagnosticResult("CI Diagnostics", categories, DateTime.UtcNow - startTime);
    }

    private static async Task<bool> IsGitHubCliAvailableAsync()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gh",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
#pragma warning disable CA1031 // Do not catch general exception types - diagnostics should be resilient
        catch
        {
            return false;
        }
#pragma warning restore CA1031
    }

    private static async Task<(JsonElement? run, string? error)> GetLatestCiRunAsync(string workingDirectory)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gh",
                    Arguments = "run list --limit 1 --json number,status,conclusion",
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var errors = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return (null, errors);
            }

            var runs = JsonSerializer.Deserialize<JsonElement>(output);
            if (runs.GetArrayLength() == 0)
            {
                return (null, null);
            }

            return (runs[0], null);
        }
#pragma warning disable CA1031 // Do not catch general exception types - diagnostics should be resilient
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
#pragma warning restore CA1031
    }

    private static async Task<List<(string jobName, string conclusion, string logs)>> GetFailedJobsAsync(string runNumber, string workingDirectory)
    {
        var failedJobs = new List<(string, string, string)>();

        try
        {
            // Get list of jobs for this run
            var jobsProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gh",
                    Arguments = $"run view {runNumber} --json jobs",
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            jobsProcess.Start();
            var jobsOutput = await jobsProcess.StandardOutput.ReadToEndAsync();
            await jobsProcess.WaitForExitAsync();

            var runData = JsonSerializer.Deserialize<JsonElement>(jobsOutput);
            var jobs = runData.GetProperty("jobs");

            foreach (var job in jobs.EnumerateArray())
            {
                var jobName = job.GetProperty("name").GetString() ?? "Unknown";
                var conclusion = job.GetProperty("conclusion").GetString() ?? "Unknown";

                if (conclusion != "success" && conclusion != "skipped")
                {
                    // Fetch logs for failed job
                    var logs = await GetJobLogsAsync(job.GetProperty("databaseId").GetInt64().ToString(), workingDirectory);
                    failedJobs.Add((jobName, conclusion, logs));
                }
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types - diagnostics should be resilient
        catch
        {
            // Failed to parse jobs - diagnostics should be resilient
        }
#pragma warning restore CA1031

        return failedJobs;
    }

    private static async Task<string> GetJobLogsAsync(string jobId, string workingDirectory)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gh",
                    Arguments = $"run view --job {jobId} --log-failed",
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var logs = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return logs;
        }
#pragma warning disable CA1031 // Do not catch general exception types - diagnostics should be resilient
        catch
        {
            return string.Empty;
        }
#pragma warning restore CA1031
    }

    private static List<DiagnosticIssue> ParseJobLogs(string logs)
    {
        var issues = new List<DiagnosticIssue>();

        // Extract error patterns from logs
        var errorPatterns = new[]
        {
            (@"##\[error\](.+)", "Error"),
            (@"Error:\s+(.+)", "Error"),
            (@"FAILED:\s+(.+)", "Test Failure"),
            (@"The directory name is invalid", "Path Error"),
            (@"Process completed with exit code (\d+)", "Non-zero Exit Code")
        };

        foreach (var (pattern, category) in errorPatterns)
        {
            var matches = Regex.Matches(logs, pattern, RegexOptions.Multiline);
            foreach (Match match in matches.Take(5)) // Limit to 5 per pattern
            {
                var message = match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : match.Value.Trim();
                issues.Add(new DiagnosticIssue(message) { Context = category });
            }
        }

        if (issues.Count == 0)
        {
            // No specific errors found, show generic message
            issues.Add(new DiagnosticIssue("Job failed - see full logs for details")
            {
                Context = logs.Length > 500 ? logs.Substring(0, 500) + "..." : logs
            });
        }

        return issues;
    }

    private static string? GetSuggestedFixFromLogs(string logs)
    {
        if (logs.Contains("The directory name is invalid"))
        {
            if (logs.Contains("DevPilot\\DevPilot") || logs.Contains("DevPilot/DevPilot"))
            {
                return "Remove 'working-directory: DevPilot' from .github/workflows/*.yml - path is already in DevPilot root after repository flattening";
            }
            return "Check working-directory configuration in .github/workflows/*.yml files";
        }

        if (logs.Contains("npm") && logs.Contains("404"))
        {
            return "Check npm package name - package may not exist or repository may need authentication";
        }

        if (logs.Contains("dotnet") && logs.Contains("error"))
        {
            return "Run 'devpilot diagnose build' locally to debug build errors";
        }

        if (logs.Contains("test") && logs.Contains("FAILED"))
        {
            return "Run 'devpilot diagnose tests' locally to debug test failures";
        }

        return "Review full CI logs with: gh run view <run-number> --log";
    }
}
