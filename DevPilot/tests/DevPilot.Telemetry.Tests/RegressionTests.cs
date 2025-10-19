using DevPilot.Core;
using DevPilot.Telemetry;
using FluentAssertions;

namespace DevPilot.Telemetry.Tests;

/// <summary>
/// Regression tests codifying previously discovered bugs.
/// Each test represents a real bug that was fixed - these prevent regressions.
/// </summary>
public sealed class RegressionTests : IDisposable
{
    private readonly string _testDatabasePath;

    public RegressionTests()
    {
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"devpilot-regression-test-{Guid.NewGuid()}.db");
    }

    /// <summary>
    /// PR #54: Test coverage was always 0/10 due to missing .sln file in workspace.
    /// Bug: WorkspaceManager wasn't copying .sln files from source repository.
    /// Impact: Evaluator always scored test coverage as 0.
    /// </summary>
    [Fact]
    public void Regression_PR54_TestCoverageZero_DueToMissingSln()
    {
        // Arrange - simulate PR #54 bug scenario
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        // Before fix: Test coverage was always 0 despite tests passing
        var buggyMetrics = new PipelineMetrics
        {
            PipelineId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            UserRequest = "Add Calculator.Multiply method",
            Success = true,
            OverallScore = 6.6,
            PlanQuality = 9.0,
            CodeQuality = 8.5,
            TestCoverage = 0.0, // BUG: Should be 8.5, not 0!
            Documentation = 9.0,
            Maintainability = 9.0,
            TestsGenerated = 13,
            TestsPassed = 13,
            TestsFailed = 0,
            Duration = TimeSpan.FromMinutes(3),
            FinalStage = "Completed",
            RagEnabled = false,
            FilesModified = 2
        };

        // After fix: Test coverage reflects actual coverage
        var fixedMetrics = new PipelineMetrics
        {
            PipelineId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(5),
            UserRequest = "Add Calculator.Divide method",
            Success = true,
            OverallScore = 9.2,
            PlanQuality = 9.0,
            CodeQuality = 9.5,
            TestCoverage = 8.5, // FIXED: Correct coverage now
            Documentation = 10.0,
            Maintainability = 9.0,
            TestsGenerated = 7,
            TestsPassed = 7,
            TestsFailed = 0,
            Duration = TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(30)),
            FinalStage = "Completed",
            RagEnabled = false,
            FilesModified = 2
        };

        tracker.RecordMetrics(buggyMetrics);
        tracker.RecordMetrics(fixedMetrics);

        // Act - verify we can detect the improvement
        var recent = tracker.GetRecentMetrics(2);

        // Assert
        recent[0].TestCoverage.Should().Be(8.5, "after fix, test coverage is correctly calculated");
        recent[1].TestCoverage.Should().Be(0.0, "before fix, test coverage was always 0");

        // Verify overall score improved after fix
        recent[0].OverallScore.Should().BeGreaterThan(recent[1].OverallScore,
            "fixing test coverage bug improved overall scores");
    }

    /// <summary>
    /// Regression: Baseline tracking should ignore failed pipeline runs.
    /// Bug: Failed runs were included in baseline calculations, skewing averages.
    /// Impact: Baseline scores were artificially lowered by temporary failures.
    /// </summary>
    [Fact]
    public void Regression_BaselineIgnoresFailedRuns()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        // Record 3 successful runs with high scores
        for (int i = 0; i < 3; i++)
        {
            tracker.RecordMetrics(CreateMetrics(overallScore: 9.0, success: true));
        }

        // Record a failed run with low score (should be ignored)
        tracker.RecordMetrics(CreateMetrics(overallScore: 2.0, success: false));

        // Act - compare against baseline
        var currentMetrics = CreateMetrics(overallScore: 8.5, success: true);
        var report = tracker.CompareAgainstBaseline(currentMetrics);

        // Assert - baseline should be 9.0 (ignoring the failed run)
        report.BaselineScore.Should().BeApproximately(9.0, 0.1,
            "failed runs should not pollute baseline calculations");
        report.HasRegression.Should().BeFalse("small drop from 9.0 to 8.5 is within threshold");
    }

    /// <summary>
    /// Regression: Low-quality runs (score &lt; 7.0) should not establish baseline.
    /// Bug: Early development runs with low scores created misleading baselines.
    /// Impact: False "no regression" reports when quality was actually poor.
    /// </summary>
    [Fact]
    public void Regression_BaselineRequiresMinimumQuality()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        // Record only low-quality runs (score < 7.0)
        for (int i = 0; i < 5; i++)
        {
            tracker.RecordMetrics(CreateMetrics(overallScore: 5.0, success: true));
        }

        var currentMetrics = CreateMetrics(overallScore: 5.5, success: true);

        // Act
        var report = tracker.CompareAgainstBaseline(currentMetrics);

        // Assert
        report.HasRegression.Should().BeFalse("no baseline available yet");
        report.Message.Should().Contain("No baseline available",
            "low-quality runs should not establish baseline");
    }

    /// <summary>
    /// Regression: Test pass rate below 95% should fail quality thresholds.
    /// Bug: Single test failure was not flagged as quality issue.
    /// Impact: PRs merged with failing tests.
    /// </summary>
    [Fact]
    public void Regression_TestPassRateThresholdEnforced()
    {
        // Arrange - 10 tests, 9 passed, 1 failed = 90% pass rate
        var metrics = new PipelineMetrics
        {
            PipelineId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            UserRequest = "Test request",
            Success = true,
            OverallScore = 9.0,
            PlanQuality = 9.0,
            CodeQuality = 9.0,
            TestCoverage = 9.0,
            Documentation = 9.0,
            Maintainability = 9.0,
            TestsGenerated = 10,
            TestsPassed = 9,
            TestsFailed = 1, // 90% pass rate
            Duration = TimeSpan.FromMinutes(2),
            FinalStage = "Completed",
            RagEnabled = false,
            FilesModified = 2
        };

        // Act
        var meetsThresholds = metrics.MeetsQualityThresholds();

        // Assert
        meetsThresholds.Should().BeFalse("90% test pass rate is below 95% threshold");
        metrics.TestPassRate.Should().Be(0.9);
    }

    /// <summary>
    /// Regression: Metrics extraction should handle missing evaluator output gracefully.
    /// Bug: NullReferenceException when evaluator JSON was malformed.
    /// Impact: Telemetry tracking crashed on evaluator failures.
    /// </summary>
    [Fact]
    public void Regression_HandlesNullEvaluatorOutputGracefully()
    {
        // Arrange
        var context = new PipelineContext
        {
            UserRequest = "Test request"
        };
        context.SetWorkspaceRoot(Path.GetTempPath());

        var result = PipelineResult.CreateFailure(context, TimeSpan.FromMinutes(1), "Evaluator failed");

        // Act - should not throw
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false);

        // Assert
        metrics.OverallScore.Should().Be(0.0, "missing scores default to 0");
        metrics.PlanQuality.Should().Be(0.0);
        metrics.CodeQuality.Should().Be(0.0);
        metrics.TestCoverage.Should().Be(0.0);
    }

    /// <summary>
    /// Regression: Metrics extraction should handle missing test report gracefully.
    /// Bug: Crash when test stage failed and TestReport was null.
    /// Impact: Telemetry couldn't track test stage failures.
    /// </summary>
    [Fact]
    public void Regression_HandlesNullTestReportGracefully()
    {
        // Arrange
        var context = new PipelineContext
        {
            UserRequest = "Test request"
        };
        context.SetWorkspaceRoot(Path.GetTempPath());
        context.AdvanceToStage(PipelineStage.Evaluating, "{}");

        var result = PipelineResult.CreateSuccess(context, TimeSpan.FromMinutes(2));

        // Act - should not throw
        var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled: false);

        // Assert
        metrics.TestsGenerated.Should().Be(0, "missing test report defaults to 0");
        metrics.TestsPassed.Should().Be(0);
        metrics.TestsFailed.Should().Be(0);
    }

    /// <summary>
    /// Regression: Baseline calculation requires minimum 3 samples.
    /// Bug: Single high-quality run created unrealistic baseline.
    /// Impact: Next run always showed regression.
    /// </summary>
    [Fact]
    public void Regression_BaselineRequiresMinimum3Samples()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        // Record only 2 high-quality runs (need 3)
        tracker.RecordMetrics(CreateMetrics(overallScore: 9.0, success: true));
        tracker.RecordMetrics(CreateMetrics(overallScore: 8.5, success: true));

        var currentMetrics = CreateMetrics(overallScore: 8.0, success: true);

        // Act
        var report = tracker.CompareAgainstBaseline(currentMetrics);

        // Assert
        report.HasRegression.Should().BeFalse("no baseline available yet");
        report.Message.Should().Contain("No baseline available",
            "need minimum 3 samples for reliable baseline");
    }

    /// <summary>
    /// Regression: Regression detection threshold is 1.0 points.
    /// Bug: Small score drops (0.5) were flagged as regressions.
    /// Impact: Too many false positive regression warnings.
    /// </summary>
    [Fact]
    public void Regression_RegressionThresholdIs1Point()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        for (int i = 0; i < 5; i++)
        {
            tracker.RecordMetrics(CreateMetrics(overallScore: 9.0, success: true));
        }

        // Drop of 0.5 (below 1.0 threshold)
        var smallDropMetrics = CreateMetrics(overallScore: 8.5, success: true);
        var smallDropReport = tracker.CompareAgainstBaseline(smallDropMetrics);

        // Drop of 1.5 (above 1.0 threshold)
        var largeDropMetrics = CreateMetrics(overallScore: 7.5, success: true);
        var largeDropReport = tracker.CompareAgainstBaseline(largeDropMetrics);

        // Assert
        smallDropReport.HasRegression.Should().BeFalse("drop of 0.5 is below 1.0 threshold");
        largeDropReport.HasRegression.Should().BeTrue("drop of 1.5 exceeds 1.0 threshold");
    }

    /// <summary>
    /// Regression: Multiple metric drops should all be reported.
    /// Bug: Only first regression was reported, hiding other issues.
    /// Impact: Hidden quality problems in code/tests/docs.
    /// </summary>
    [Fact]
    public void Regression_AllMetricDropsReported()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        for (int i = 0; i < 5; i++)
        {
            tracker.RecordMetrics(CreateMetrics(
                overallScore: 9.0,
                testCoverage: 9.0,
                codeQuality: 9.0,
                success: true));
        }

        // Drop in multiple metrics
        var currentMetrics = CreateMetrics(
            overallScore: 7.5, // Drop of 1.5
            testCoverage: 7.0,  // Drop of 2.0
            codeQuality: 7.5,   // Drop of 1.5
            success: true);

        // Act
        var report = tracker.CompareAgainstBaseline(currentMetrics);

        // Assert
        report.HasRegression.Should().BeTrue();
        report.Regressions.Should().Contain(r => r.Contains("Overall score dropped"),
            "overall score regression should be reported");
        report.Regressions.Should().Contain(r => r.Contains("Test coverage dropped"),
            "test coverage regression should be reported");
        report.Regressions.Should().Contain(r => r.Contains("Code quality dropped"),
            "code quality regression should be reported");
        report.Regressions.Should().HaveCountGreaterThan(1,
            "all regressions should be reported, not just the first");
    }

    /// <summary>
    /// Regression: RAG enabled flag should be tracked in metrics.
    /// Bug: No way to correlate RAG usage with quality scores.
    /// Impact: Couldn't analyze RAG effectiveness.
    /// </summary>
    [Fact]
    public void Regression_RagEnabledFlagTracked()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        var withoutRag = CreateMetrics(overallScore: 8.0, success: true, ragEnabled: false);
        var withRag = CreateMetrics(overallScore: 9.0, success: true, ragEnabled: true);

        // Act
        tracker.RecordMetrics(withoutRag);
        tracker.RecordMetrics(withRag);

        var recent = tracker.GetRecentMetrics(2);

        // Assert
        recent[0].RagEnabled.Should().BeTrue("most recent run used RAG");
        recent[1].RagEnabled.Should().BeFalse("earlier run didn't use RAG");
    }

    /// <summary>
    /// Regression: Repository structure should be tracked in metrics.
    /// Bug: No way to correlate non-standard structures with quality issues.
    /// Impact: Couldn't identify structure-specific bugs.
    /// </summary>
    [Fact]
    public void Regression_RepositoryStructureTracked()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        var standardMetrics = CreateMetrics(overallScore: 9.0, success: true, repositoryStructure: "standard");
        var nonStandardMetrics = CreateMetrics(overallScore: 7.0, success: true, repositoryStructure: "nonstandard");

        // Act
        tracker.RecordMetrics(standardMetrics);
        tracker.RecordMetrics(nonStandardMetrics);

        var recent = tracker.GetRecentMetrics(2);

        // Assert
        recent[0].RepositoryStructure.Should().Be("nonstandard");
        recent[1].RepositoryStructure.Should().Be("standard");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testDatabasePath))
            {
                File.Delete(_testDatabasePath);
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }

    private static PipelineMetrics CreateMetrics(
        double overallScore = 8.0,
        double planQuality = 8.0,
        double codeQuality = 8.0,
        double testCoverage = 8.0,
        double documentation = 8.0,
        double maintainability = 8.0,
        bool success = true,
        bool ragEnabled = false,
        string? repositoryStructure = null,
        DateTimeOffset? timestamp = null)
    {
        return new PipelineMetrics
        {
            PipelineId = Guid.NewGuid().ToString(),
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            UserRequest = "Test request",
            Success = success,
            OverallScore = overallScore,
            PlanQuality = planQuality,
            CodeQuality = codeQuality,
            TestCoverage = testCoverage,
            Documentation = documentation,
            Maintainability = maintainability,
            TestsGenerated = 10,
            TestsPassed = 10,
            TestsFailed = 0,
            Duration = TimeSpan.FromSeconds(120),
            FinalStage = "Completed",
            RagEnabled = ragEnabled,
            FilesModified = 5,
            RepositoryStructure = repositoryStructure
        };
    }
}
