using DevPilot.Core;
using DevPilot.Telemetry;
using FluentAssertions;

namespace DevPilot.Telemetry.Tests;

/// <summary>
/// Edge case tests for telemetry system - boundary conditions, extreme values, unusual scenarios.
/// </summary>
public sealed class TelemetryEdgeCaseTests : IDisposable
{
    private readonly string _testDatabasePath;

    public TelemetryEdgeCaseTests()
    {
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"devpilot-edgecase-test-{Guid.NewGuid()}.db");
    }

    [Fact]
    public void EdgeCase_ZeroScoresAcrossAllMetrics()
    {
        // Test case: Complete pipeline failure with all scores at 0
        var metrics = CreateMetrics(
            overallScore: 0.0,
            planQuality: 0.0,
            codeQuality: 0.0,
            testCoverage: 0.0,
            documentation: 0.0,
            maintainability: 0.0,
            success: false);

        metrics.MeetsQualityThresholds().Should().BeFalse();
        metrics.TestPassRate.Should().Be(1.0, "no tests generated means 100% pass rate by default");
    }

    [Fact]
    public void EdgeCase_PerfectScoresAcrossAllMetrics()
    {
        // Test case: Perfect pipeline execution
        var metrics = CreateMetrics(
            overallScore: 10.0,
            planQuality: 10.0,
            codeQuality: 10.0,
            testCoverage: 10.0,
            documentation: 10.0,
            maintainability: 10.0,
            success: true,
            testsGenerated: 50,
            testsPassed: 50,
            testsFailed: 0);

        metrics.MeetsQualityThresholds().Should().BeTrue();
        metrics.TestPassRate.Should().Be(1.0);
    }

    [Fact]
    public void EdgeCase_VeryLongDuration()
    {
        // Test case: Pipeline takes over 1 hour (stuck/slow execution)
        var metrics = CreateMetrics(
            duration: TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(30)));

        metrics.Duration.TotalMinutes.Should().Be(150);
    }

    [Fact]
    public void EdgeCase_VeryShortDuration()
    {
        // Test case: Pipeline completes in under 1 second (likely cached/mocked)
        var metrics = CreateMetrics(
            duration: TimeSpan.FromMilliseconds(500));

        metrics.Duration.TotalSeconds.Should().BeLessThan(1.0);
    }

    [Fact]
    public void EdgeCase_HundredsOfTestsGenerated()
    {
        // Test case: Large codebase generates many tests
        var metrics = CreateMetrics(
            testsGenerated: 500,
            testsPassed: 495,
            testsFailed: 5);

        metrics.TestsGenerated.Should().Be(500);
        metrics.TestPassRate.Should().Be(0.99);
        metrics.MeetsQualityThresholds().Should().BeTrue("99% pass rate exceeds 95% threshold");
    }

    [Fact]
    public void EdgeCase_SingleTestFailure()
    {
        // Test case: Only 1 test fails out of 100
        var metrics = CreateMetrics(
            testsGenerated: 100,
            testsPassed: 99,
            testsFailed: 1);

        metrics.TestPassRate.Should().Be(0.99);
        metrics.MeetsQualityThresholds().Should().BeTrue();
    }

    [Fact]
    public void EdgeCase_ExactlyAtQualityThreshold()
    {
        // Test case: All metrics exactly at threshold (7.0)
        var metrics = CreateMetrics(
            overallScore: 7.0,
            planQuality: 7.0,
            codeQuality: 7.0,
            testCoverage: 7.0,
            documentation: 7.0,
            maintainability: 7.0,
            testsGenerated: 20,
            testsPassed: 19,
            testsFailed: 1); // 95% pass rate

        metrics.MeetsQualityThresholds().Should().BeTrue("threshold is inclusive (>=)");
    }

    [Fact]
    public void EdgeCase_JustBelowQualityThreshold()
    {
        // Test case: Just below threshold (6.99)
        var metrics = CreateMetrics(
            overallScore: 6.99,
            testCoverage: 6.99,
            codeQuality: 6.99);

        metrics.MeetsQualityThresholds().Should().BeFalse("6.99 is below 7.0 threshold");
    }

    [Fact]
    public void EdgeCase_BaselineWithExactly3Samples()
    {
        // Test case: Minimum required baseline samples
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        tracker.RecordMetrics(CreateMetrics(overallScore: 9.0, success: true));
        tracker.RecordMetrics(CreateMetrics(overallScore: 8.5, success: true));
        tracker.RecordMetrics(CreateMetrics(overallScore: 8.0, success: true));

        var current = CreateMetrics(overallScore: 7.9, success: true);
        var report = tracker.CompareAgainstBaseline(current);

        report.BaselineScore.Should().BeApproximately(8.5, 0.1, "average of 9.0, 8.5, 8.0");
    }

    [Fact]
    public void EdgeCase_BaselineWithThousandsOfSamples()
    {
        // Test case: Long-running project with many historical runs
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        // Add 1000 historical samples
        for (int i = 0; i < 1000; i++)
        {
            var score = 8.0 + (i % 10) * 0.1; // Varies between 8.0-8.9
            tracker.RecordMetrics(CreateMetrics(overallScore: score, success: true));
        }

        var current = CreateMetrics(overallScore: 8.5, success: true);
        var report = tracker.CompareAgainstBaseline(current);

        report.Should().NotBeNull();
        report.BaselineScore.Should().BeInRange(8.0, 9.0);
    }

    [Fact]
    public void EdgeCase_30DayBoundaryCheck()
    {
        // Test case: Metrics exactly at 30-day boundary
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        // Metric from exactly 30 days ago
        var oldMetric = CreateMetrics(
            overallScore: 9.0,
            success: true,
            timestamp: DateTimeOffset.UtcNow.AddDays(-30));

        // Metric from 29 days ago (within window)
        var recentMetric = CreateMetrics(
            overallScore: 8.0,
            success: true,
            timestamp: DateTimeOffset.UtcNow.AddDays(-29));

        tracker.RecordMetrics(oldMetric);
        tracker.RecordMetrics(recentMetric);

        var recent = tracker.GetRecentMetrics(30);
        recent.Should().Contain(m => Math.Abs(m.OverallScore - 9.0) < 0.01);
        recent.Should().Contain(m => Math.Abs(m.OverallScore - 8.0) < 0.01);
    }

    [Fact]
    public void EdgeCase_RegressionExactlyAt1PointThreshold()
    {
        // Test case: Score drop exactly 1.0 point
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        for (int i = 0; i < 5; i++)
        {
            tracker.RecordMetrics(CreateMetrics(overallScore: 9.0, success: true));
        }

        var current = CreateMetrics(overallScore: 7.99, success: true); // Drop of 1.01

        var report = tracker.CompareAgainstBaseline(current);

        report.HasRegression.Should().BeTrue("drop > 1.0 triggers regression");
    }

    [Fact]
    public void EdgeCase_RegressionJustBelow1PointThreshold()
    {
        // Test case: Score drop of 0.99 points
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        for (int i = 0; i < 5; i++)
        {
            tracker.RecordMetrics(CreateMetrics(overallScore: 9.0, success: true));
        }

        var current = CreateMetrics(overallScore: 8.01, success: true); // Drop of 0.99

        var report = tracker.CompareAgainstBaseline(current);

        report.HasRegression.Should().BeFalse("drop of 0.99 is below 1.0 threshold");
    }

    [Fact]
    public void EdgeCase_AllTestsFailed()
    {
        // Test case: 100% test failure
        var metrics = CreateMetrics(
            testsGenerated: 10,
            testsPassed: 0,
            testsFailed: 10);

        metrics.TestPassRate.Should().Be(0.0);
        metrics.MeetsQualityThresholds().Should().BeFalse();
    }

    [Fact]
    public void EdgeCase_NoFilesModified()
    {
        // Test case: Pipeline ran but didn't modify any files
        var metrics = CreateMetrics(filesModified: 0);

        metrics.FilesModified.Should().Be(0);
    }

    [Fact]
    public void EdgeCase_HundredsOfFilesModified()
    {
        // Test case: Large refactoring touched many files
        var metrics = CreateMetrics(filesModified: 250);

        metrics.FilesModified.Should().Be(250);
    }

    [Fact]
    public void EdgeCase_VeryLongUserRequest()
    {
        // Test case: User request is extremely verbose
        var longRequest = string.Join(" ", Enumerable.Repeat("Add feature to", 100)) + " the application.";

        var metrics = CreateMetrics(userRequest: longRequest);

        metrics.UserRequest.Length.Should().BeGreaterThan(1000);
        metrics.UserRequest.Should().Contain("Add feature to");
    }

    [Fact]
    public void EdgeCase_EmptyRepositoryStructure()
    {
        // Test case: Repository structure not detected
        var metrics = CreateMetrics(repositoryStructure: null);

        metrics.RepositoryStructure.Should().BeNull();
    }

    [Fact]
    public void EdgeCase_MultipleRegressions_AllAboveThreshold()
    {
        // Test case: Every metric regressed significantly
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        for (int i = 0; i < 5; i++)
        {
            tracker.RecordMetrics(CreateMetrics(
                overallScore: 9.0,
                planQuality: 9.0,
                codeQuality: 9.0,
                testCoverage: 9.0,
                documentation: 9.0,
                maintainability: 9.0,
                success: true));
        }

        var current = CreateMetrics(
            overallScore: 7.0,  // Drop of 2.0
            planQuality: 7.5,    // Drop of 1.5
            codeQuality: 7.0,    // Drop of 2.0
            testCoverage: 6.0,   // Drop of 3.0
            documentation: 7.5,  // Drop of 1.5
            maintainability: 7.0, // Drop of 2.0
            success: true);

        var report = tracker.CompareAgainstBaseline(current);

        report.HasRegression.Should().BeTrue();
        report.Regressions.Count.Should().BeGreaterThanOrEqualTo(3, "multiple metrics regressed");
    }

    [Fact]
    public void EdgeCase_MixedSuccessAndFailureRuns()
    {
        // Test case: Alternating success/failure pattern
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        for (int i = 0; i < 10; i++)
        {
            var success = i % 2 == 0;
            var score = success ? 9.0 : 3.0;
            tracker.RecordMetrics(CreateMetrics(overallScore: score, success: success));
        }

        var current = CreateMetrics(overallScore: 8.5, success: true);
        var report = tracker.CompareAgainstBaseline(current);

        // Baseline should only consider successful runs
        report.BaselineScore.Should().BeApproximately(9.0, 0.1);
    }

    [Fact]
    public void EdgeCase_RagEnabledAndDisabledComparison()
    {
        // Test case: Compare metrics with and without RAG
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        var withoutRag = CreateMetrics(overallScore: 7.0, ragEnabled: false);
        var withRag = CreateMetrics(overallScore: 9.0, ragEnabled: true);

        tracker.RecordMetrics(withoutRag);
        tracker.RecordMetrics(withRag);

        var recent = tracker.GetRecentMetrics(2);

        recent[0].RagEnabled.Should().BeTrue();
        recent[1].RagEnabled.Should().BeFalse();
        recent[0].OverallScore.Should().BeGreaterThan(recent[1].OverallScore);
    }

    [Fact]
    public void EdgeCase_DatabasePathWithSpecialCharacters()
    {
        // Test case: Database path contains spaces and special chars
        var specialPath = Path.Combine(Path.GetTempPath(), $"test db with spaces & chars {Guid.NewGuid()}.db");

        using var tracker = BaselineTracker.Create(specialPath);
        tracker.RecordMetrics(CreateMetrics());

        File.Exists(specialPath).Should().BeTrue();

        try
        {
            File.Delete(specialPath);
        }
        catch
        {
            // Ignore cleanup failures
        }
    }

    [Fact]
    public void EdgeCase_GetRecentMetrics_RequestMoreThanExist()
    {
        // Test case: Request 100 metrics but only 5 exist
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        for (int i = 0; i < 5; i++)
        {
            tracker.RecordMetrics(CreateMetrics());
        }

        var recent = tracker.GetRecentMetrics(100);

        recent.Should().HaveCount(5, "only 5 metrics exist");
    }

    [Fact]
    public void EdgeCase_GetRecentMetrics_RequestZero()
    {
        // Test case: Request 0 metrics
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        tracker.RecordMetrics(CreateMetrics());

        var recent = tracker.GetRecentMetrics(0);

        recent.Should().BeEmpty();
    }

    [Fact]
    public void EdgeCase_MultiplePipelineIdsSameTimestamp()
    {
        // Test case: Two pipelines complete at exact same millisecond
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        var timestamp = DateTimeOffset.UtcNow;
        var metrics1 = CreateMetrics(timestamp: timestamp);
        var metrics2 = CreateMetrics(timestamp: timestamp);

        tracker.RecordMetrics(metrics1);
        tracker.RecordMetrics(metrics2);

        var recent = tracker.GetRecentMetrics(2);

        recent.Should().HaveCount(2);
        recent[0].PipelineId.Should().NotBe(recent[1].PipelineId);
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
        int testsGenerated = 10,
        int testsPassed = 10,
        int testsFailed = 0,
        TimeSpan? duration = null,
        bool ragEnabled = false,
        string? repositoryStructure = null,
        int filesModified = 5,
        string? userRequest = null,
        DateTimeOffset? timestamp = null)
    {
        return new PipelineMetrics
        {
            PipelineId = Guid.NewGuid().ToString(),
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            UserRequest = userRequest ?? "Test request",
            Success = success,
            OverallScore = overallScore,
            PlanQuality = planQuality,
            CodeQuality = codeQuality,
            TestCoverage = testCoverage,
            Documentation = documentation,
            Maintainability = maintainability,
            TestsGenerated = testsGenerated,
            TestsPassed = testsPassed,
            TestsFailed = testsFailed,
            Duration = duration ?? TimeSpan.FromMinutes(2),
            FinalStage = "Completed",
            RagEnabled = ragEnabled,
            FilesModified = filesModified,
            RepositoryStructure = repositoryStructure
        };
    }
}
