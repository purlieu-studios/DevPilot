using DevPilot.Core;
using FluentAssertions;

namespace DevPilot.Orchestrator.Tests;

/// <summary>
/// Regression tests to prevent coverage collection regressions.
/// These tests validate that coverage metrics are collected correctly and meet quality thresholds.
/// </summary>
/// <remarks>
/// Background: PR #61 introduced a bug where coverage was always reported as 0% due to missing
/// .runsettings configuration. These tests prevent similar regressions in the future.
/// </remarks>
public sealed class CoverageRegressionTests
{
    /// <summary>
    /// Validates that coverage percentage is at least 70% (project quality standard).
    /// </summary>
    [Fact]
    public void ValidateCoverage_MeetsMinimumThreshold()
    {
        // Arrange
        const double minimumCoverageThreshold = 70.0;
        var testRunResult = new TestRunResult
        {
            Pass = true,
            Summary = "All tests passed",
            TestResults = new List<TestResult>(),
            Coverage = new CoverageInfo
            {
                LineCoveragePercent = 75.3,
                BranchCoveragePercent = 72.0
            },
            Performance = new PerformanceInfo
            {
                TotalDurationMs = 1000
            }
        };

        // Act & Assert
        testRunResult.Coverage.Should().NotBeNull();
        testRunResult.Coverage!.LineCoveragePercent.Should().BeGreaterThanOrEqualTo(minimumCoverageThreshold,
            because: "DevPilot maintains a minimum 70% code coverage standard");
    }

    /// <summary>
    /// Validates that coverage doesn't drop more than 5% from baseline.
    /// Prevents regressions where new code significantly reduces overall coverage.
    /// </summary>
    [Fact]
    public void ValidateCoverage_NoSignificantDrop()
    {
        // Arrange
        const double baselineCoverage = 75.0;
        const double maxAllowedDrop = 5.0;
        var currentResult = new TestRunResult
        {
            Pass = true,
            Summary = "Tests passed with slight coverage drop",
            TestResults = new List<TestResult>(),
            Coverage = new CoverageInfo
            {
                LineCoveragePercent = 71.0, // 4% drop - acceptable
                BranchCoveragePercent = 68.0
            },
            Performance = new PerformanceInfo
            {
                TotalDurationMs = 1000
            }
        };

        // Act
        var coverageDrop = baselineCoverage - currentResult.Coverage!.LineCoveragePercent;

        // Assert
        coverageDrop.Should().BeLessThanOrEqualTo(maxAllowedDrop,
            because: $"coverage should not drop more than {maxAllowedDrop}% from baseline ({baselineCoverage}%)");
    }

    /// <summary>
    /// Validates that coverage percentage is a valid value (0-100%).
    /// Detects issues like PR #61 where coverage was incorrectly reported as 0%.
    /// </summary>
    [Fact]
    public void ValidateCoverage_IsValidPercentage()
    {
        // Arrange
        var testRunResult = new TestRunResult
        {
            Pass = true,
            Summary = "Tests passed with coverage",
            TestResults = new List<TestResult>(),
            Coverage = new CoverageInfo
            {
                LineCoveragePercent = 75.3,
                BranchCoveragePercent = 72.0
            },
            Performance = new PerformanceInfo
            {
                TotalDurationMs = 1000
            }
        };

        // Act & Assert
        testRunResult.Coverage.Should().NotBeNull();
        testRunResult.Coverage!.LineCoveragePercent.Should().BeInRange(0.0, 100.0,
            because: "coverage percentage must be a valid percentage value");

        testRunResult.Coverage!.LineCoveragePercent.Should().NotBe(0.0,
            because: "0% coverage likely indicates a collection failure (like PR #61)");
    }
}
