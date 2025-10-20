using DevPilot.Core;
using System.Text.Json;

namespace DevPilot.Telemetry;

/// <summary>
/// Collects pipeline metrics and integrates with baseline tracking.
/// </summary>
public static class MetricsCollector
{
    /// <summary>
    /// Extracts metrics from a pipeline result.
    /// </summary>
    public static PipelineMetrics ExtractMetrics(PipelineResult result, bool ragEnabled, string? repositoryStructure = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        var (testsGenerated, testsPassed, testsFailed) = ExtractTestMetrics(result.Context.TestReport);
        var scores = ExtractScores(result.Context.Scores);

        return new PipelineMetrics
        {
            PipelineId = result.Context.PipelineId,
            Timestamp = result.Context.StartedAt,
            UserRequest = result.Context.UserRequest,
            Success = result.Success,
            OverallScore = scores.OverallScore,
            PlanQuality = scores.PlanQuality,
            CodeQuality = scores.CodeQuality,
            TestCoverage = scores.TestCoverage,
            Documentation = scores.Documentation,
            Maintainability = scores.Maintainability,
            TestsGenerated = testsGenerated,
            TestsPassed = testsPassed,
            TestsFailed = testsFailed,
            Duration = result.Duration,
            FinalStage = result.FinalStage.ToString(),
            RagEnabled = ragEnabled,
            FilesModified = result.Context.AppliedFiles?.Count ?? 0,
            RepositoryStructure = repositoryStructure
        };
    }

    /// <summary>
    /// Records metrics and checks for regressions, returning a report to display to the user.
    /// </summary>
    public static RegressionReport RecordAndCheck(PipelineMetrics metrics, BaselineTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(tracker);

        // Record metrics first
        tracker.RecordMetrics(metrics);

        // Compare against baseline
        return tracker.CompareAgainstBaseline(metrics);
    }

    private static (int Generated, int Passed, int Failed) ExtractTestMetrics(string? testReport)
    {
        if (string.IsNullOrWhiteSpace(testReport))
        {
            return (0, 0, 0);
        }

        try
        {
            // Parse test report JSON
            using var doc = JsonDocument.Parse(testReport);
            var root = doc.RootElement;

            if (root.TryGetProperty("total", out var total) &&
                root.TryGetProperty("passed", out var passed) &&
                root.TryGetProperty("failed", out var failed))
            {
                return (total.GetInt32(), passed.GetInt32(), failed.GetInt32());
            }
        }
        catch (JsonException)
        {
            // Fallback: parse text-based test report
            // Format: "Passed: X, Failed: Y, Total: Z"
            if (testReport.Contains("Total:"))
            {
                var parts = testReport.Split(',');
                var totalMatch = System.Text.RegularExpressions.Regex.Match(testReport, @"Total:\s*(\d+)");
                var passedMatch = System.Text.RegularExpressions.Regex.Match(testReport, @"Passed:\s*(\d+)");
                var failedMatch = System.Text.RegularExpressions.Regex.Match(testReport, @"Failed:\s*(\d+)");

                if (totalMatch.Success && passedMatch.Success && failedMatch.Success)
                {
                    return (
                        int.Parse(totalMatch.Groups[1].Value),
                        int.Parse(passedMatch.Groups[1].Value),
                        int.Parse(failedMatch.Groups[1].Value)
                    );
                }
            }
        }

        return (0, 0, 0);
    }

    private static ScoreBreakdown ExtractScores(string? scoresJson)
    {
        if (string.IsNullOrWhiteSpace(scoresJson))
        {
            return new ScoreBreakdown
            {
                OverallScore = 0,
                PlanQuality = 0,
                CodeQuality = 0,
                TestCoverage = 0,
                Documentation = 0,
                Maintainability = 0
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(scoresJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("evaluation", out var evaluation))
            {
                return new ScoreBreakdown
                {
                    OverallScore = evaluation.TryGetProperty("overall_score", out var overall) ? overall.GetDouble() : 0,
                    PlanQuality = GetScore(evaluation, "scores", "plan_quality"),
                    CodeQuality = GetScore(evaluation, "scores", "code_quality"),
                    TestCoverage = GetScore(evaluation, "scores", "test_coverage"),
                    Documentation = GetScore(evaluation, "scores", "documentation"),
                    Maintainability = GetScore(evaluation, "scores", "maintainability")
                };
            }
        }
        catch (JsonException)
        {
            // Fallback to zeros on parse error
        }

        return new ScoreBreakdown
        {
            OverallScore = 0,
            PlanQuality = 0,
            CodeQuality = 0,
            TestCoverage = 0,
            Documentation = 0,
            Maintainability = 0
        };
    }

    private static double GetScore(JsonElement element, string parentProperty, string scoreProperty)
    {
        if (element.TryGetProperty(parentProperty, out var parent) &&
            parent.TryGetProperty(scoreProperty, out var score))
        {
            return score.GetDouble();
        }

        return 0;
    }

    private sealed class ScoreBreakdown
    {
        public required double OverallScore { get; init; }
        public required double PlanQuality { get; init; }
        public required double CodeQuality { get; init; }
        public required double TestCoverage { get; init; }
        public required double Documentation { get; init; }
        public required double Maintainability { get; init; }
    }
}
