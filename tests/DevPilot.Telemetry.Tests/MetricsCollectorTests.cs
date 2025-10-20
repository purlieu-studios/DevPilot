using DevPilot.Core;
using DevPilot.Telemetry;
using FluentAssertions;
using System.Text.Json;

namespace DevPilot.Telemetry.Tests;

/// <summary>
/// Tests for MetricsCollector class - extracting metrics from PipelineResult.
/// </summary>
public sealed class MetricsCollectorTests
{
    [Fact]
    public void ExtractMetrics_WithValidEvaluatorJson_ParsesScoresCorrectly()
    {
        // Arrange
        var evaluatorOutput = """
            {
              "evaluation": {
                "overall_score": 8.5,
                "scores": {
                  "plan_quality": 9.0,
                  "code_quality": 8.0,
                  "test_coverage": 8.5,
                  "documentation": 8.0,
                  "maintainability": 9.0
                },
                "final_verdict": "ACCEPT"
              }
            }
            """;

        var result = CreatePipelineResult(
            success: true,
            evaluatorOutput: evaluatorOutput,
            testReport: null);

        // Act
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false);

        // Assert
        metrics.OverallScore.Should().Be(8.5);
        metrics.PlanQuality.Should().Be(9.0);
        metrics.CodeQuality.Should().Be(8.0);
        metrics.TestCoverage.Should().Be(8.5);
        metrics.Documentation.Should().Be(8.0);
        metrics.Maintainability.Should().Be(9.0);
    }

    [Fact]
    public void ExtractMetrics_WithInvalidEvaluatorJson_DefaultsToZero()
    {
        // Arrange
        var result = CreatePipelineResult(
            success: true,
            evaluatorOutput: "Invalid JSON",
            testReport: null);

        // Act
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false);

        // Assert
        metrics.OverallScore.Should().Be(0.0);
        metrics.PlanQuality.Should().Be(0.0);
        metrics.CodeQuality.Should().Be(0.0);
        metrics.TestCoverage.Should().Be(0.0);
        metrics.Documentation.Should().Be(0.0);
        metrics.Maintainability.Should().Be(0.0);
    }

    [Fact]
    public void ExtractMetrics_WithValidTestReport_ParsesTestMetrics()
    {
        // Arrange
        var testReport = """
            {
              "total": 15,
              "passed": 14,
              "failed": 1
            }
            """;

        var result = CreatePipelineResult(
            success: true,
            evaluatorOutput: CreateDefaultEvaluatorJson(),
            testReport: testReport);

        // Act
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false);

        // Assert
        metrics.TestsGenerated.Should().Be(15);
        metrics.TestsPassed.Should().Be(14);
        metrics.TestsFailed.Should().Be(1);
        metrics.TestPassRate.Should().BeApproximately(0.933, 0.001);
    }

    [Fact]
    public void ExtractMetrics_WithTextTestReport_ParsesUsingRegex()
    {
        // Arrange
        var testReport = """
            Test run completed
            Total: 20, Passed: 18, Failed: 2, Skipped: 0
            """;

        var result = CreatePipelineResult(
            success: true,
            evaluatorOutput: CreateDefaultEvaluatorJson(),
            testReport: testReport);

        // Act
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false);

        // Assert
        metrics.TestsGenerated.Should().Be(20);
        metrics.TestsPassed.Should().Be(18);
        metrics.TestsFailed.Should().Be(2);
    }

    [Fact]
    public void ExtractMetrics_WithMissingTestReport_DefaultsToZero()
    {
        // Arrange
        var result = CreatePipelineResult(
            success: true,
            evaluatorOutput: CreateDefaultEvaluatorJson(),
            testReport: null);

        // Act
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false);

        // Assert
        metrics.TestsGenerated.Should().Be(0);
        metrics.TestsPassed.Should().Be(0);
        metrics.TestsFailed.Should().Be(0);
    }

    [Fact]
    public void ExtractMetrics_GeneratesPipelineId()
    {
        // Arrange
        var result = CreatePipelineResult(
            success: true,
            evaluatorOutput: CreateDefaultEvaluatorJson(),
            testReport: null);

        // Act
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false);

        // Assert
        metrics.PipelineId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(metrics.PipelineId, out _).Should().BeTrue("Pipeline ID should be a valid GUID");
    }

    [Fact]
    public void ExtractMetrics_CapturesUserRequest()
    {
        // Arrange
        var userRequest = "Add authentication to User model";
        var result = CreatePipelineResult(
            success: true,
            evaluatorOutput: CreateDefaultEvaluatorJson(),
            testReport: null,
            userRequest: userRequest);

        // Act
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false);

        // Assert
        metrics.UserRequest.Should().Be(userRequest);
    }

    [Fact]
    public void ExtractMetrics_CapturesSuccessFlag()
    {
        // Arrange
        var result = CreatePipelineResult(
            success: false,
            evaluatorOutput: CreateDefaultEvaluatorJson(),
            testReport: null);

        // Act
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false);

        // Assert
        metrics.Success.Should().BeFalse();
    }

    [Fact]
    public void ExtractMetrics_CapturesFinalStage()
    {
        // Arrange
        var result = CreatePipelineResult(
            success: true,
            evaluatorOutput: CreateDefaultEvaluatorJson(),
            testReport: null);

        // Act
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false);

        // Assert
        metrics.FinalStage.Should().Be("Evaluating", "because we advanced to Evaluating stage");
    }

    [Fact]
    public void ExtractMetrics_CapturesDuration()
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(30));
        var result = CreatePipelineResult(
            success: true,
            evaluatorOutput: CreateDefaultEvaluatorJson(),
            testReport: null,
            duration: duration);

        // Act
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false);

        // Assert
        metrics.Duration.Should().Be(duration);
    }

    [Fact]
    public void ExtractMetrics_CapturesRagEnabledFlag()
    {
        // Arrange
        var result = CreatePipelineResult(
            success: true,
            evaluatorOutput: CreateDefaultEvaluatorJson(),
            testReport: null);

        // Act
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: true);

        // Assert
        metrics.RagEnabled.Should().BeTrue();
    }

    [Fact]
    public void ExtractMetrics_CapturesRepositoryStructure()
    {
        // Arrange
        var result = CreatePipelineResult(
            success: true,
            evaluatorOutput: CreateDefaultEvaluatorJson(),
            testReport: null);

        // Act
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false, repositoryStructure: "nonstandard");

        // Assert
        metrics.RepositoryStructure.Should().Be("nonstandard");
    }

    [Fact]
    public void ExtractMetrics_CapturesTimestamp()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;
        var result = CreatePipelineResult(
            success: true,
            evaluatorOutput: CreateDefaultEvaluatorJson(),
            testReport: null);

        // Act
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false);
        var after = DateTimeOffset.UtcNow;

        // Assert
        metrics.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void RecordAndCheck_RecordsMetrics_AndReturnsReport()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(Path.GetTempFileName());
        var result = CreatePipelineResult(
            success: true,
            evaluatorOutput: CreateDefaultEvaluatorJson(overallScore: 9.0),
            testReport: null);

        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false);

        // Act
        var report = MetricsCollector.RecordAndCheck(metrics, tracker);

        // Assert
        var recent = tracker.GetRecentMetrics(1);
        recent.Should().HaveCount(1);
        recent[0].PipelineId.Should().Be(metrics.PipelineId);
        report.Should().NotBeNull();
    }

    // Helper methods

    private static string CreateDefaultEvaluatorJson(double overallScore = 8.0)
    {
        return $$"""
            {
              "evaluation": {
                "overall_score": {{overallScore}},
                "scores": {
                  "plan_quality": {{overallScore}},
                  "code_quality": {{overallScore}},
                  "test_coverage": {{overallScore}},
                  "documentation": {{overallScore}},
                  "maintainability": {{overallScore}}
                },
                "final_verdict": "ACCEPT"
              }
            }
            """;
    }

    private static PipelineResult CreatePipelineResult(
        bool success,
        string evaluatorOutput,
        string? testReport,
        string? pipelineId = null,
        string? userRequest = null,
        PipelineStage? finalStage = null,
        TimeSpan? duration = null)
    {
        userRequest ??= "Test request";
        duration ??= TimeSpan.FromMinutes(2);

        var context = new PipelineContext
        {
            UserRequest = userRequest
        };

        // Set workspace root
        context.SetWorkspaceRoot(Path.GetTempPath());

        // Advance through stages to populate outputs
        if (testReport != null)
        {
            context.AdvanceToStage(PipelineStage.Testing, testReport);
        }

        if (evaluatorOutput != null)
        {
            context.AdvanceToStage(PipelineStage.Evaluating, evaluatorOutput);
        }

        return success
            ? PipelineResult.CreateSuccess(context, duration.Value)
            : PipelineResult.CreateFailure(context, duration.Value, "Test failure");
    }
}
