using DevPilot.Core;
using FluentAssertions;

namespace DevPilot.Core.Tests;

/// <summary>
/// Tests for TestRunResult - test execution results and metadata.
/// </summary>
public sealed class TestRunResultTests
{
    #region Construction and Factory Methods

    [Fact]
    public void TestRunResult_AllTestsPassed_SetsPassTrue()
    {
        // Arrange & Act
        var result = new TestRunResult
        {
            Pass = true,
            Summary = "All tests passed",
            TestResults = new List<TestResult>
            {
                new() { TestName = "Test1", Status = TestStatus.Passed, DurationMs = 10.0 },
                new() { TestName = "Test2", Status = TestStatus.Passed, DurationMs = 15.0 }
            },
            Performance = new PerformanceInfo { TotalDurationMs = 25.0 }
        };

        // Assert
        result.Pass.Should().BeTrue();
        result.TestResults.Should().HaveCount(2);
        result.TestResults.Should().AllSatisfy(t => t.Status.Should().Be(TestStatus.Passed));
    }

    [Fact]
    public void TestRunResult_SomeTestsFailed_SetsPassFalse()
    {
        // Arrange & Act
        var result = new TestRunResult
        {
            Pass = false,
            Summary = "2 of 3 tests passed",
            TestResults = new List<TestResult>
            {
                new() { TestName = "Test1", Status = TestStatus.Passed, DurationMs = 10.0 },
                new() { TestName = "Test2", Status = TestStatus.Failed, DurationMs = 15.0, Message = "Assert failed" },
                new() { TestName = "Test3", Status = TestStatus.Passed, DurationMs = 12.0 }
            },
            Performance = new PerformanceInfo { TotalDurationMs = 37.0 }
        };

        // Assert
        result.Pass.Should().BeFalse();
        result.TestResults.Count(t => t.Status == TestStatus.Failed).Should().Be(1);
    }

    [Fact]
    public void TestRunResult_WithCoverage_StoresCoverageInfo()
    {
        // Arrange & Act
        var result = new TestRunResult
        {
            Pass = true,
            Summary = "All tests passed with 85% coverage",
            TestResults = new List<TestResult>(),
            Coverage = new CoverageInfo
            {
                LineCoveragePercent = 85.5,
                BranchCoveragePercent = 72.3
            },
            Performance = new PerformanceInfo { TotalDurationMs = 1000.0 }
        };

        // Assert
        result.Coverage.Should().NotBeNull();
        result.Coverage!.LineCoveragePercent.Should().Be(85.5);
        result.Coverage.BranchCoveragePercent.Should().Be(72.3);
    }

    [Fact]
    public void TestRunResult_NoCoverage_AllowsNullCoverage()
    {
        // Arrange & Act
        var result = new TestRunResult
        {
            Pass = true,
            Summary = "Tests passed, no coverage collected",
            TestResults = new List<TestResult>(),
            Coverage = null,
            Performance = new PerformanceInfo { TotalDurationMs = 500.0 }
        };

        // Assert
        result.Coverage.Should().BeNull();
    }

    #endregion

    #region Performance Tracking

    [Fact]
    public void PerformanceInfo_TracksTotalDuration()
    {
        // Arrange & Act
        var performance = new PerformanceInfo
        {
            TotalDurationMs = 12345.67,
            SlowestTest = "IntegrationTests.SlowTest"
        };

        // Assert
        performance.TotalDurationMs.Should().Be(12345.67);
        performance.SlowestTest.Should().Be("IntegrationTests.SlowTest");
    }

    [Fact]
    public void PerformanceInfo_SlowestTest_CanBeNull()
    {
        // Arrange & Act
        var performance = new PerformanceInfo
        {
            TotalDurationMs = 100.0,
            SlowestTest = null
        };

        // Assert
        performance.SlowestTest.Should().BeNull();
    }

    #endregion

    #region Test Status and Results

    [Fact]
    public void TestStatus_Enum_HasCorrectValues()
    {
        // Assert
        Enum.GetValues<TestStatus>().Should().Contain(new[]
        {
            TestStatus.Passed,
            TestStatus.Failed,
            TestStatus.Skipped
        });
    }

    [Fact]
    public void TestResult_SkippedTest_HasMessage()
    {
        // Arrange & Act
        var testResult = new TestResult
        {
            TestName = "SkippedTest",
            Status = TestStatus.Skipped,
            DurationMs = 0.0,
            Message = "Test skipped due to missing dependency"
        };

        // Assert
        testResult.Status.Should().Be(TestStatus.Skipped);
        testResult.Message.Should().Be("Test skipped due to missing dependency");
    }

    #endregion
}
