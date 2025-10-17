using DevPilot.Core;
using System.Diagnostics;
using System.Xml.Linq;

namespace DevPilot.Orchestrator;

/// <summary>
/// Executes dotnet test commands and parses TRX test results.
/// </summary>
public static class TestRunner
{
    /// <summary>
    /// Executes tests in the specified workspace directory.
    /// </summary>
    /// <param name="workspaceRoot">The root directory of the workspace.</param>
    /// <returns>The test run result with all test outcomes.</returns>
    public static async Task<TestRunResult> ExecuteTestsAsync(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        if (!Directory.Exists(workspaceRoot))
        {
            return TestRunResultExtensions.CreateFailure($"Workspace directory does not exist: {workspaceRoot}");
        }

        // Step 1: Build the solution
        var buildResult = await RunDotnetCommandAsync(workspaceRoot, "build");
        if (!buildResult.Success)
        {
            // Combine both stdout and stderr for complete diagnostics
            var errorDetails = string.IsNullOrWhiteSpace(buildResult.ErrorOutput)
                ? buildResult.Output
                : $"{buildResult.Output}\n{buildResult.ErrorOutput}";
            return TestRunResultExtensions.CreateFailure($"Build failed:\n{errorDetails}");
        }

        // Step 2: Run tests with TRX logger
        // Use simple "TestResults" since we're already in the workspace directory
        const string testResultsDir = "TestResults";
        var testResult = await RunDotnetCommandAsync(workspaceRoot, $"test --logger \"trx\" --results-directory \"{testResultsDir}\"");

        // Check if test command failed
        if (!testResult.Success)
        {
            // Combine both stdout and stderr for complete diagnostics
            var errorDetails = string.IsNullOrWhiteSpace(testResult.ErrorOutput)
                ? testResult.Output
                : $"{testResult.Output}\n{testResult.ErrorOutput}";
            return TestRunResultExtensions.CreateFailure($"Test execution failed:\n{errorDetails}");
        }

        // Step 3: Find and parse TRX file
        var testResultsFullPath = Path.Combine(workspaceRoot, testResultsDir);
        var trxFile = FindLatestTrxFile(testResultsFullPath);
        if (trxFile == null)
        {
            // Include test output to help diagnose why no TRX was generated
            var diagnosticInfo = $"Test command output:\n{testResult.Output}";
            if (!string.IsNullOrWhiteSpace(testResult.ErrorOutput))
            {
                diagnosticInfo += $"\nError output:\n{testResult.ErrorOutput}";
            }
            return TestRunResultExtensions.CreateFailure($"No TRX file found after test execution.\n{diagnosticInfo}");
        }

        // Step 4: Parse TRX and return results
        return ParseTrxFile(trxFile);
    }

    /// <summary>
    /// Runs a dotnet command in the specified directory.
    /// </summary>
    private static async Task<CommandResult> RunDotnetCommandAsync(string workingDirectory, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        return new CommandResult
        {
            Success = process.ExitCode == 0,
            Output = output,
            ErrorOutput = error,
            ExitCode = process.ExitCode
        };
    }

    /// <summary>
    /// Finds the most recently created TRX file in the test results directory.
    /// </summary>
    private static string? FindLatestTrxFile(string testResultsDir)
    {
        if (!Directory.Exists(testResultsDir))
        {
            return null;
        }

        var trxFiles = Directory.GetFiles(testResultsDir, "*.trx", SearchOption.AllDirectories);
        if (trxFiles.Length == 0)
        {
            return null;
        }

        // Return the most recently created TRX file
        return trxFiles
            .Select(f => new FileInfo(f))
            .OrderByDescending(fi => fi.CreationTimeUtc)
            .First()
            .FullName;
    }

    /// <summary>
    /// Parses a TRX XML file and extracts test results.
    /// </summary>
    private static TestRunResult ParseTrxFile(string trxFilePath)
    {
        try
        {
            var doc = XDocument.Load(trxFilePath);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

            // Extract test results
            var results = doc.Descendants(ns + "UnitTestResult")
                .Select(result => ParseTestResult(result, ns))
                .ToList();

            // Extract summary counters
            var counters = doc.Descendants(ns + "Counters").FirstOrDefault();
            var total = int.Parse(counters?.Attribute("total")?.Value ?? "0");
            var passed = int.Parse(counters?.Attribute("passed")?.Value ?? "0");
            var failed = int.Parse(counters?.Attribute("failed")?.Value ?? "0");
            var skipped = int.Parse(counters?.Attribute("notExecuted")?.Value ?? "0");

            // Calculate total duration
            var totalDuration = results.Sum(r => r.DurationMs);

            // Find slowest test
            var slowestTest = results.OrderByDescending(r => r.DurationMs).FirstOrDefault();

            return new TestRunResult
            {
                Pass = failed == 0,
                Summary = $"{passed} passed, {failed} failed, {skipped} skipped (total: {total})",
                TestResults = results,
                Coverage = null, // Coverage requires additional tooling
                Performance = new PerformanceInfo
                {
                    TotalDurationMs = totalDuration,
                    SlowestTest = slowestTest?.TestName
                }
            };
        }
        catch (IOException ex)
        {
            return TestRunResultExtensions.CreateFailure($"Failed to read TRX file: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return TestRunResultExtensions.CreateFailure($"Access denied to TRX file: {ex.Message}");
        }
        catch (System.Xml.XmlException ex)
        {
            return TestRunResultExtensions.CreateFailure($"Malformed TRX XML: {ex.Message}");
        }
        catch (FormatException ex)
        {
            return TestRunResultExtensions.CreateFailure($"Invalid TRX format (failed to parse numbers): {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return TestRunResultExtensions.CreateFailure($"TRX parsing error (missing required elements): {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a single UnitTestResult element from the TRX file.
    /// </summary>
    private static TestResult ParseTestResult(XElement result, XNamespace ns)
    {
        var testName = result.Attribute("testName")?.Value ?? "Unknown";
        var outcome = result.Attribute("outcome")?.Value ?? "Unknown";
        var duration = result.Attribute("duration")?.Value ?? "00:00:00";

        // Parse duration (format: HH:MM:SS.mmmmmmm)
        var durationMs = TimeSpan.TryParse(duration, out var ts) ? ts.TotalMilliseconds : 0;

        // Extract failure/skip message if present
        var message = result.Descendants(ns + "Message").FirstOrDefault()?.Value?.Trim();

        var status = outcome.ToLowerInvariant() switch
        {
            "passed" => TestStatus.Passed,
            "failed" => TestStatus.Failed,
            "notexecuted" => TestStatus.Skipped,
            _ => TestStatus.Skipped
        };

        return new TestResult
        {
            TestName = testName,
            Status = status,
            DurationMs = durationMs,
            Message = message
        };
    }

    /// <summary>
    /// Represents the result of running a command.
    /// </summary>
    private sealed class CommandResult
    {
        public required bool Success { get; init; }
        public required string Output { get; init; }
        public required string ErrorOutput { get; init; }
        public required int ExitCode { get; init; }
    }
}

/// <summary>
/// Extension methods for TestRunResult.
/// </summary>
public static class TestRunResultExtensions
{
    /// <summary>
    /// Creates a failed TestRunResult with an error message.
    /// </summary>
    public static TestRunResult CreateFailure(string errorMessage)
    {
        return new TestRunResult
        {
            Pass = false,
            Summary = "Test execution failed",
            TestResults = Array.Empty<TestResult>(),
            Coverage = null,
            Performance = new PerformanceInfo
            {
                TotalDurationMs = 0,
                SlowestTest = null
            },
            ErrorMessage = errorMessage
        };
    }
}
