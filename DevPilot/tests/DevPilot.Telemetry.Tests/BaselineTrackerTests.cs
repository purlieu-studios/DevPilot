using DevPilot.Telemetry;
using FluentAssertions;

namespace DevPilot.Telemetry.Tests;

/// <summary>
/// Tests for BaselineTracker class.
/// </summary>
public sealed class BaselineTrackerTests : IDisposable
{
    private readonly string _testDatabasePath;
    private readonly List<string> _databaseFilesToCleanup;

    public BaselineTrackerTests()
    {
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"devpilot-telemetry-test-{Guid.NewGuid()}.db");
        _databaseFilesToCleanup = new List<string> { _testDatabasePath };
    }

    [Fact]
    public void Create_CreatesDatabase_AtSpecifiedPath()
    {
        // Act
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        // Assert
        File.Exists(_testDatabasePath).Should().BeTrue();
    }

    [Fact]
    public void RecordMetrics_StoresMetrics_InDatabase()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);
        var metrics = CreateMetrics(overallScore: 9.0, testCoverage: 9.0);

        // Act
        tracker.RecordMetrics(metrics);

        // Assert - Verify by retrieving recent metrics
        var recent = tracker.GetRecentMetrics(1);
        recent.Should().HaveCount(1);
        recent[0].PipelineId.Should().Be(metrics.PipelineId);
        recent[0].OverallScore.Should().Be(9.0);
    }

    [Fact]
    public void GetRecentMetrics_ReturnsMetrics_InDescendingOrder()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        var metrics1 = CreateMetrics(overallScore: 8.0, timestamp: DateTimeOffset.UtcNow.AddDays(-2));
        var metrics2 = CreateMetrics(overallScore: 9.0, timestamp: DateTimeOffset.UtcNow.AddDays(-1));
        var metrics3 = CreateMetrics(overallScore: 7.5, timestamp: DateTimeOffset.UtcNow);

        tracker.RecordMetrics(metrics1);
        tracker.RecordMetrics(metrics2);
        tracker.RecordMetrics(metrics3);

        // Act
        var recent = tracker.GetRecentMetrics(3);

        // Assert
        recent.Should().HaveCount(3);
        recent[0].OverallScore.Should().Be(7.5); // Most recent
        recent[1].OverallScore.Should().Be(9.0);
        recent[2].OverallScore.Should().Be(8.0); // Oldest
    }

    [Fact]
    public void GetRecentMetrics_LimitsResults_ToRequestedCount()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        for (int i = 0; i < 20; i++)
        {
            tracker.RecordMetrics(CreateMetrics(overallScore: 8.0 + i * 0.1));
        }

        // Act
        var recent = tracker.GetRecentMetrics(5);

        // Assert
        recent.Should().HaveCount(5);
    }

    [Fact]
    public void CompareAgainstBaseline_WithNoBaseline_ReportsNoBaseline()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);
        var metrics = CreateMetrics(overallScore: 9.0);

        // Act
        var report = tracker.CompareAgainstBaseline(metrics);

        // Assert
        report.HasRegression.Should().BeFalse();
        report.Message.Should().Contain("No baseline available");
    }

    [Fact]
    public void CompareAgainstBaseline_WithInsufficientSamples_ReportsNoBaseline()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        // Record only 2 metrics (need 3 for reliable baseline)
        tracker.RecordMetrics(CreateMetrics(overallScore: 9.0, success: true));
        tracker.RecordMetrics(CreateMetrics(overallScore: 8.5, success: true));

        var currentMetrics = CreateMetrics(overallScore: 7.0);

        // Act
        var report = tracker.CompareAgainstBaseline(currentMetrics);

        // Assert
        report.HasRegression.Should().BeFalse();
        report.Message.Should().Contain("No baseline available");
    }

    [Fact]
    public void CompareAgainstBaseline_WithGoodBaseline_AndNoDrop_ReportsNoRegression()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        // Create solid baseline (3 runs with score >= 7.0)
        for (int i = 0; i < 5; i++)
        {
            tracker.RecordMetrics(CreateMetrics(overallScore: 9.0, testCoverage: 9.0, codeQuality: 9.0, success: true));
        }

        var currentMetrics = CreateMetrics(overallScore: 8.5, testCoverage: 8.5, codeQuality: 8.5);

        // Act
        var report = tracker.CompareAgainstBaseline(currentMetrics);

        // Assert
        report.HasRegression.Should().BeFalse();
        report.Message.Should().Contain("No regressions detected");
    }

    [Fact]
    public void CompareAgainstBaseline_WithOverallScoreDrop_ReportsRegression()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        // Baseline: 5 runs with 9.0 score
        for (int i = 0; i < 5; i++)
        {
            tracker.RecordMetrics(CreateMetrics(overallScore: 9.0, success: true));
        }

        // Current: score dropped to 7.5 (drop of 1.5)
        var currentMetrics = CreateMetrics(overallScore: 7.5);

        // Act
        var report = tracker.CompareAgainstBaseline(currentMetrics);

        // Assert
        report.HasRegression.Should().BeTrue();
        report.Regressions.Should().Contain(r => r.Contains("Overall score dropped"));
    }

    [Fact]
    public void CompareAgainstBaseline_WithTestCoverageDrop_ReportsRegression()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        for (int i = 0; i < 5; i++)
        {
            tracker.RecordMetrics(CreateMetrics(testCoverage: 9.0, success: true));
        }

        var currentMetrics = CreateMetrics(testCoverage: 7.5); // Drop of 1.5

        // Act
        var report = tracker.CompareAgainstBaseline(currentMetrics);

        // Assert
        report.HasRegression.Should().BeTrue();
        report.Regressions.Should().Contain(r => r.Contains("Test coverage dropped"));
    }

    [Fact]
    public void CompareAgainstBaseline_WithCodeQualityDrop_ReportsRegression()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        for (int i = 0; i < 5; i++)
        {
            tracker.RecordMetrics(CreateMetrics(codeQuality: 9.0, success: true));
        }

        var currentMetrics = CreateMetrics(codeQuality: 7.5); // Drop of 1.5

        // Act
        var report = tracker.CompareAgainstBaseline(currentMetrics);

        // Assert
        report.HasRegression.Should().BeTrue();
        report.Regressions.Should().Contain(r => r.Contains("Code quality dropped"));
    }

    [Fact]
    public void CompareAgainstBaseline_WithMultipleRegressions_ReportsAll()
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

        // Current: multiple scores dropped
        var currentMetrics = CreateMetrics(
            overallScore: 7.5,
            testCoverage: 7.0,
            codeQuality: 7.5);

        // Act
        var report = tracker.CompareAgainstBaseline(currentMetrics);

        // Assert
        report.HasRegression.Should().BeTrue();
        report.Regressions.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void CompareAgainstBaseline_WithSmallDrop_ReportsNoRegression()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        for (int i = 0; i < 5; i++)
        {
            tracker.RecordMetrics(CreateMetrics(overallScore: 9.0, success: true));
        }

        // Drop of only 0.5 (threshold is 1.0)
        var currentMetrics = CreateMetrics(overallScore: 8.5);

        // Act
        var report = tracker.CompareAgainstBaseline(currentMetrics);

        // Assert
        report.HasRegression.Should().BeFalse();
    }

    [Fact]
    public void RecordMetrics_WithRagEnabled_StoresRagFlag()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);
        var metrics = CreateMetrics(ragEnabled: true);

        // Act
        tracker.RecordMetrics(metrics);

        // Assert
        var recent = tracker.GetRecentMetrics(1);
        recent[0].RagEnabled.Should().BeTrue();
    }

    [Fact]
    public void RecordMetrics_WithRepositoryStructure_StoresStructure()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);
        var metrics = CreateMetrics(repositoryStructure: "nonstandard");

        // Act
        tracker.RecordMetrics(metrics);

        // Assert
        var recent = tracker.GetRecentMetrics(1);
        recent[0].RepositoryStructure.Should().Be("nonstandard");
    }

    [Fact]
    public void CompareAgainstBaseline_IgnoresFailedRuns_InBaseline()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        // Record 3 successful runs with high scores
        for (int i = 0; i < 3; i++)
        {
            tracker.RecordMetrics(CreateMetrics(overallScore: 9.0, success: true));
        }

        // Record 5 failed runs with low scores (should be ignored)
        for (int i = 0; i < 5; i++)
        {
            tracker.RecordMetrics(CreateMetrics(overallScore: 2.0, success: false));
        }

        // Current run with slightly lower score
        var currentMetrics = CreateMetrics(overallScore: 8.5);

        // Act
        var report = tracker.CompareAgainstBaseline(currentMetrics);

        // Assert - Should compare against 9.0 baseline (ignoring failed runs)
        report.HasRegression.Should().BeFalse("small drop from 9.0 to 8.5 is within threshold");
    }

    [Fact]
    public void CompareAgainstBaseline_IgnoresLowQualityRuns_InBaseline()
    {
        // Arrange
        using var tracker = BaselineTracker.Create(_testDatabasePath);

        // Record 3 high-quality successful runs
        for (int i = 0; i < 3; i++)
        {
            tracker.RecordMetrics(CreateMetrics(overallScore: 9.0, success: true));
        }

        // Record 5 low-quality runs (score < 7.0, should be ignored)
        for (int i = 0; i < 5; i++)
        {
            tracker.RecordMetrics(CreateMetrics(overallScore: 5.0, success: true));
        }

        var currentMetrics = CreateMetrics(overallScore: 8.5);

        // Act
        var report = tracker.CompareAgainstBaseline(currentMetrics);

        // Assert - Baseline should be 9.0 (ignoring low-quality runs)
        report.BaselineScore.Should().BeApproximately(9.0, 0.1);
    }

    public void Dispose()
    {
        foreach (var dbFile in _databaseFilesToCleanup)
        {
            try
            {
                if (File.Exists(dbFile))
                {
                    File.Delete(dbFile);
                }
            }
            catch
            {
                // Ignore cleanup failures
            }
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
