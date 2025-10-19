using DevPilot.Telemetry;
using FluentAssertions;

namespace DevPilot.Telemetry.Tests;

/// <summary>
/// Tests for PipelineMetrics class.
/// </summary>
public sealed class PipelineMetricsTests
{
    [Fact]
    public void TestPassRate_WithNoTests_ReturnsZero()
    {
        // Arrange
        var metrics = CreateMetrics(testsGenerated: 0, testsPassed: 0, testsFailed: 0);

        // Act
        var passRate = metrics.TestPassRate;

        // Assert
        passRate.Should().Be(0);
    }

    [Fact]
    public void TestPassRate_WithAllTestsPassing_ReturnsOne()
    {
        // Arrange
        var metrics = CreateMetrics(testsGenerated: 10, testsPassed: 10, testsFailed: 0);

        // Act
        var passRate = metrics.TestPassRate;

        // Assert
        passRate.Should().Be(1.0);
    }

    [Fact]
    public void TestPassRate_WithPartialFailures_ReturnsCorrectRatio()
    {
        // Arrange
        var metrics = CreateMetrics(testsGenerated: 10, testsPassed: 7, testsFailed: 3);

        // Act
        var passRate = metrics.TestPassRate;

        // Assert
        passRate.Should().Be(0.7);
    }

    [Fact]
    public void MeetsQualityThresholds_WithHighScores_ReturnsTrue()
    {
        // Arrange
        var metrics = CreateMetrics(
            success: true,
            overallScore: 9.0,
            testCoverage: 9.0,
            codeQuality: 9.0,
            testsGenerated: 10,
            testsPassed: 10,
            testsFailed: 0);

        // Act
        var meetsThresholds = metrics.MeetsQualityThresholds();

        // Assert
        meetsThresholds.Should().BeTrue();
    }

    [Fact]
    public void MeetsQualityThresholds_WithLowOverallScore_ReturnsFalse()
    {
        // Arrange
        var metrics = CreateMetrics(
            success: true,
            overallScore: 6.0, // Below 7.0 threshold
            testCoverage: 9.0,
            codeQuality: 9.0,
            testsGenerated: 10,
            testsPassed: 10,
            testsFailed: 0);

        // Act
        var meetsThresholds = metrics.MeetsQualityThresholds();

        // Assert
        meetsThresholds.Should().BeFalse();
    }

    [Fact]
    public void MeetsQualityThresholds_WithLowTestCoverage_ReturnsFalse()
    {
        // Arrange
        var metrics = CreateMetrics(
            success: true,
            overallScore: 9.0,
            testCoverage: 5.0, // Below 7.0 threshold
            codeQuality: 9.0,
            testsGenerated: 10,
            testsPassed: 10,
            testsFailed: 0);

        // Act
        var meetsThresholds = metrics.MeetsQualityThresholds();

        // Assert
        meetsThresholds.Should().BeFalse();
    }

    [Fact]
    public void MeetsQualityThresholds_WithLowCodeQuality_ReturnsFalse()
    {
        // Arrange
        var metrics = CreateMetrics(
            success: true,
            overallScore: 9.0,
            testCoverage: 9.0,
            codeQuality: 6.0, // Below 7.0 threshold
            testsGenerated: 10,
            testsPassed: 10,
            testsFailed: 0);

        // Act
        var meetsThresholds = metrics.MeetsQualityThresholds();

        // Assert
        meetsThresholds.Should().BeFalse();
    }

    [Fact]
    public void MeetsQualityThresholds_WithLowTestPassRate_ReturnsFalse()
    {
        // Arrange
        var metrics = CreateMetrics(
            success: true,
            overallScore: 9.0,
            testCoverage: 9.0,
            codeQuality: 9.0,
            testsGenerated: 10,
            testsPassed: 9, // 90% pass rate, below 95% threshold
            testsFailed: 1);

        // Act
        var meetsThresholds = metrics.MeetsQualityThresholds();

        // Assert
        meetsThresholds.Should().BeFalse();
    }

    [Fact]
    public void MeetsQualityThresholds_WithPipelineFailure_ReturnsFalse()
    {
        // Arrange
        var metrics = CreateMetrics(
            success: false, // Pipeline failed
            overallScore: 9.0,
            testCoverage: 9.0,
            codeQuality: 9.0,
            testsGenerated: 10,
            testsPassed: 10,
            testsFailed: 0);

        // Act
        var meetsThresholds = metrics.MeetsQualityThresholds();

        // Assert
        meetsThresholds.Should().BeFalse();
    }

    [Fact]
    public void MeetsQualityThresholds_WithExactThresholdValues_ReturnsTrue()
    {
        // Arrange
        var metrics = CreateMetrics(
            success: true,
            overallScore: 7.0, // Exactly at threshold
            testCoverage: 7.0, // Exactly at threshold
            codeQuality: 7.0,  // Exactly at threshold
            testsGenerated: 20,
            testsPassed: 19, // 95% pass rate (exactly at threshold)
            testsFailed: 1);

        // Act
        var meetsThresholds = metrics.MeetsQualityThresholds();

        // Assert
        meetsThresholds.Should().BeTrue();
    }

    private static PipelineMetrics CreateMetrics(
        bool success = true,
        double overallScore = 8.0,
        double planQuality = 8.0,
        double codeQuality = 8.0,
        double testCoverage = 8.0,
        double documentation = 8.0,
        double maintainability = 8.0,
        int testsGenerated = 0,
        int testsPassed = 0,
        int testsFailed = 0,
        bool ragEnabled = false,
        int filesModified = 0)
    {
        return new PipelineMetrics
        {
            PipelineId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            UserRequest = "Test request",
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
            Duration = TimeSpan.FromSeconds(120),
            FinalStage = "Completed",
            RagEnabled = ragEnabled,
            FilesModified = filesModified
        };
    }
}
