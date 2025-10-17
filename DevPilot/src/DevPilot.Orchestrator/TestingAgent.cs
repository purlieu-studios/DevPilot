using DevPilot.Core;
using System.Diagnostics;
using System.Text.Json;

namespace DevPilot.Orchestrator;

/// <summary>
/// Agent implementation that executes real tests using TestRunner.
/// </summary>
public sealed class TestingAgent : IAgent
{
    /// <summary>
    /// Gets the agent definition.
    /// </summary>
    public AgentDefinition Definition { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestingAgent"/> class.
    /// </summary>
    public TestingAgent()
    {
        Definition = new AgentDefinition
        {
            Name = "tester",
            Version = "1.0.0",
            Description = "Executes tests in workspace and reports results",
            SystemPrompt = "Executes dotnet test and parses TRX results",
            Model = "native" // Not using LLM
        };
    }

    /// <summary>
    /// Executes tests in the workspace and returns structured JSON results.
    /// </summary>
    /// <param name="input">The input containing workspace path.</param>
    /// <param name="context">The agent context with pipeline metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Agent result with JSON test report.</returns>
    public async Task<AgentResult> ExecuteAsync(
        string input,
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Extract workspace path from input
            var workspacePath = ExtractWorkspacePath(input);
            if (string.IsNullOrWhiteSpace(workspacePath))
            {
                stopwatch.Stop();
                return AgentResult.CreateFailure(
                    Definition.Name,
                    "Could not extract workspace path from input",
                    stopwatch.Elapsed);
            }

            // Execute tests using TestRunner
            var testResult = await TestRunner.ExecuteTestsAsync(workspacePath);

            // Convert to JSON format matching Testing stage schema
            var json = ConvertToJson(testResult);

            stopwatch.Stop();

            if (!testResult.Pass)
            {
                return AgentResult.CreateFailure(
                    Definition.Name,
                    testResult.ErrorMessage ?? "Tests failed",
                    stopwatch.Elapsed);
            }

            return AgentResult.CreateSuccess(
                Definition.Name,
                json,
                stopwatch.Elapsed);
        }
        catch (System.Text.Json.JsonException ex)
        {
            stopwatch.Stop();
            return AgentResult.CreateFailure(
                Definition.Name,
                $"Failed to serialize test results: {ex.Message}",
                stopwatch.Elapsed);
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            return AgentResult.CreateFailure(
                Definition.Name,
                $"Test execution invalid operation: {ex.Message}",
                stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Extracts the workspace path from the input string.
    /// </summary>
    private static string? ExtractWorkspacePath(string input)
    {
        // Input format: "Workspace Path: /path/to/workspace\nApplied Files: ..."
        // Extract the path after "Workspace Path: "
        var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var workspaceLine = lines.FirstOrDefault(l => l.StartsWith("Workspace Path:", StringComparison.OrdinalIgnoreCase));

        if (workspaceLine == null)
        {
            return null;
        }

        var path = workspaceLine.Substring("Workspace Path:".Length).Trim();
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    /// <summary>
    /// Converts TestRunResult to JSON matching the Testing stage output schema.
    /// </summary>
    private static string ConvertToJson(TestRunResult result)
    {
        var jsonObject = new
        {
            pass = result.Pass,
            summary = result.Summary,
            test_results = result.TestResults.Select(tr => new
            {
                test_name = tr.TestName,
                status = tr.Status.ToString().ToLowerInvariant(),
                duration_ms = tr.DurationMs,
                message = tr.Message
            }).ToList(),
            coverage = result.Coverage != null ? new
            {
                line_coverage_percent = result.Coverage.LineCoveragePercent,
                branch_coverage_percent = result.Coverage.BranchCoveragePercent
            } : null,
            performance = new
            {
                total_duration_ms = result.Performance.TotalDurationMs,
                slowest_test = result.Performance.SlowestTest
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        return JsonSerializer.Serialize(jsonObject, options);
    }
}
