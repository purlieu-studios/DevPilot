using DevPilot.Core;
using System.Diagnostics;
using System.Xml.Linq;

namespace DevPilot.Orchestrator;

/// <summary>
/// Executes dotnet test commands and parses TRX test results.
/// </summary>
public static class TestRunner
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Executes tests in the specified workspace directory.
    /// </summary>
    /// <param name="workspaceRoot">The root directory of the workspace.</param>
    /// <param name="cancellationToken">Cancellation token to abort test execution.</param>
    /// <returns>The test run result with all test outcomes.</returns>
    public static async Task<TestRunResult> ExecuteTestsAsync(string workspaceRoot, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        if (!Directory.Exists(workspaceRoot))
        {
            return TestRunResultExtensions.CreateFailure($"Workspace directory does not exist: {workspaceRoot}");
        }

        // Step 1: Find solution file to build and test
        // The Coder agent may create DevPilot.sln, so we need to find the original solution file
        var solutionFiles = Directory.GetFiles(workspaceRoot, "*.sln");
        var solutionFile = solutionFiles.FirstOrDefault(f => !Path.GetFileName(f).Equals("DevPilot.sln", StringComparison.OrdinalIgnoreCase))
                          ?? solutionFiles.FirstOrDefault();

        if (solutionFile == null)
        {
            return TestRunResultExtensions.CreateFailure($"No solution file found in workspace: {workspaceRoot}");
        }

        // Step 2: Build the solution
        var buildResult = await RunDotnetCommandAsync(workspaceRoot, $"build \"{solutionFile}\"", BuildTimeout, cancellationToken);
        if (!buildResult.Success)
        {
            // Combine both stdout and stderr for complete diagnostics
            var errorDetails = string.IsNullOrWhiteSpace(buildResult.ErrorOutput)
                ? buildResult.Output
                : $"{buildResult.Output}\n{buildResult.ErrorOutput}";
            return TestRunResultExtensions.CreateFailure($"Build failed:\n{errorDetails}");
        }

        // Step 3: Run tests with TRX logger and code coverage
        // Use simple "TestResults" since we're already in the workspace directory
        const string testResultsDir = "TestResults";
        var testResult = await RunDotnetCommandAsync(workspaceRoot, $"test \"{solutionFile}\" --logger \"trx\" --collect:\"XPlat Code Coverage\" --results-directory \"{testResultsDir}\"", TestTimeout, cancellationToken);

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

        // Step 4: Parse TRX file
        var testRunResult = ParseTrxFile(trxFile);

        // Step 5: Find and parse coverage file
        var coverageFile = FindLatestCoverageFile(testResultsFullPath);
        if (coverageFile != null)
        {
            var coverageData = ParseCoverageFile(coverageFile);
            if (coverageData != null)
            {
                // Create new TestRunResult with coverage data
                testRunResult = new TestRunResult
                {
                    Pass = testRunResult.Pass,
                    Summary = testRunResult.Summary,
                    TestResults = testRunResult.TestResults,
                    Coverage = coverageData,
                    Performance = testRunResult.Performance,
                    ErrorMessage = testRunResult.ErrorMessage
                };
            }
        }

        return testRunResult;
    }

    /// <summary>
    /// Runs a dotnet command in the specified directory.
    /// </summary>
    private static async Task<CommandResult> RunDotnetCommandAsync(string workingDirectory, string arguments, TimeSpan timeout, CancellationToken cancellationToken)
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

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        // Wait for process to exit with timeout
        var completed = await WaitForExitAsync(process, timeout, cancellationToken);

        if (!completed)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited, ignore
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Cannot terminate process, ignore
            }

            return new CommandResult
            {
                Success = false,
                Output = $"Command timed out after {timeout.TotalSeconds} seconds",
                ErrorOutput = string.Empty,
                ExitCode = -1
            };
        }

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
    /// Waits for a process to exit with timeout support.
    /// </summary>
    /// <param name="process">The process to wait for.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the process exited within the timeout; otherwise, false.</returns>
    private static async Task<bool> WaitForExitAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Finds the most recently created coverage.cobertura.xml file in the test results directory.
    /// </summary>
    private static string? FindLatestCoverageFile(string testResultsDir)
    {
        if (!Directory.Exists(testResultsDir))
        {
            return null;
        }

        var coverageFiles = Directory.GetFiles(testResultsDir, "coverage.cobertura.xml", SearchOption.AllDirectories);
        if (coverageFiles.Length == 0)
        {
            return null;
        }

        // Return the most recently created coverage file
        return coverageFiles
            .Select(f => new FileInfo(f))
            .OrderByDescending(fi => fi.CreationTimeUtc)
            .First()
            .FullName;
    }

    /// <summary>
    /// Parses a Cobertura XML coverage file and extracts line and branch coverage.
    /// </summary>
    private static CoverageInfo? ParseCoverageFile(string coverageFilePath)
    {
        try
        {
            var doc = XDocument.Load(coverageFilePath);
            var coverageElement = doc.Root;

            if (coverageElement == null)
            {
                return null;
            }

            // Extract line-rate and branch-rate attributes
            var lineRateStr = coverageElement.Attribute("line-rate")?.Value;
            var branchRateStr = coverageElement.Attribute("branch-rate")?.Value;

            if (!double.TryParse(lineRateStr, out var lineRate) ||
                !double.TryParse(branchRateStr, out var branchRate))
            {
                return null;
            }

            // Convert rates (0.0-1.0) to percentages (0-100)
            var linePercentage = lineRate * 100;
            var branchPercentage = branchRate * 100;

            return new CoverageInfo
            {
                LineCoveragePercent = linePercentage,
                BranchCoveragePercent = branchPercentage
            };
        }
        catch
        {
            // If coverage parsing fails, return null (coverage is optional)
            return null;
        }
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
