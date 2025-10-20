using DevPilot.Core;
using DevPilot.Orchestrator;
using FluentAssertions;
using Xunit;

namespace DevPilot.Agents.Tests;

/// <summary>
/// Tests for the TestRunner utility.
/// </summary>
public sealed class TestRunnerTests
{
    [Fact]
    public async Task ExecuteTestsAsync_ReturnsFailure_WhenWorkspaceDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var result = await TestRunner.ExecuteTestsAsync(nonExistentPath);

        // Assert
        result.Pass.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Workspace directory does not exist");
    }

    [Fact]
    public void CreateFailure_CreatesTestRunResultWithErrorMessage()
    {
        // Act
        var result = TestRunResultExtensions.CreateFailure("Build failed");

        // Assert
        result.Pass.Should().BeFalse();
        result.Summary.Should().Be("Test execution failed");
        result.ErrorMessage.Should().Be("Build failed");
        result.TestResults.Should().BeEmpty();
        result.Performance.TotalDurationMs.Should().Be(0);
    }

    [Fact]
    public void TestResult_StatusEnum_HasCorrectValues()
    {
        // Assert - Verify all enum values are present
        Enum.GetValues<TestStatus>().Should().HaveCount(3);
        Enum.GetValues<TestStatus>().Should().Contain(new[]
        {
            TestStatus.Passed,
            TestStatus.Failed,
            TestStatus.Skipped
        });
    }

    [Fact]
    public void TestRunResult_CanBeCreated_WithRequiredProperties()
    {
        // Arrange
        var testResults = new List<TestResult>
        {
            new TestResult
            {
                TestName = "Test1",
                Status = TestStatus.Passed,
                DurationMs = 123.45,
                Message = null
            }
        };

        // Act
        var result = new TestRunResult
        {
            Pass = true,
            Summary = "All tests passed",
            TestResults = testResults,
            Performance = new PerformanceInfo
            {
                TotalDurationMs = 123.45,
                SlowestTest = "Test1"
            }
        };

        // Assert
        result.Pass.Should().BeTrue();
        result.Summary.Should().Be("All tests passed");
        result.TestResults.Should().HaveCount(1);
        result.TestResults[0].TestName.Should().Be("Test1");
        result.Performance.TotalDurationMs.Should().Be(123.45);
        result.Performance.SlowestTest.Should().Be("Test1");
    }

    [Fact]
    public void CoverageInfo_CanBeCreated_WithPercentages()
    {
        // Act
        var coverage = new CoverageInfo
        {
            LineCoveragePercent = 95.5,
            BranchCoveragePercent = 87.3
        };

        // Assert
        coverage.LineCoveragePercent.Should().Be(95.5);
        coverage.BranchCoveragePercent.Should().Be(87.3);
    }

    [Fact]
    public void PerformanceInfo_CanBeCreated_WithOptionalSlowestTest()
    {
        // Act
        var performance = new PerformanceInfo
        {
            TotalDurationMs = 1234.56,
            SlowestTest = null
        };

        // Assert
        performance.TotalDurationMs.Should().Be(1234.56);
        performance.SlowestTest.Should().BeNull();
    }

    [Fact]
    public void TestResult_CanHaveFailureMessage()
    {
        // Act
        var testResult = new TestResult
        {
            TestName = "FailingTest",
            Status = TestStatus.Failed,
            DurationMs = 45.67,
            Message = "Assert.Equal() Failure\nExpected: 5\nActual: 3"
        };

        // Assert
        testResult.Status.Should().Be(TestStatus.Failed);
        testResult.Message.Should().Contain("Assert.Equal() Failure");
        testResult.Message.Should().Contain("Expected: 5");
    }
}
