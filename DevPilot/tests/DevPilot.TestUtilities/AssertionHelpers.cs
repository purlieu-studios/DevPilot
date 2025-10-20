using DevPilot.Core;
using DevPilot.Orchestrator;
using FluentAssertions;
using FluentAssertions.Execution;

namespace DevPilot.TestUtilities;

/// <summary>
/// Custom assertion helpers for DevPilot-specific test scenarios.
/// Extends FluentAssertions with domain-specific validation.
/// </summary>
/// <example>
/// <code>
/// // Assert agent result succeeded
/// var result = await agent.ExecuteAsync(input, context);
/// AssertionHelpers.ShouldHaveSucceeded(result);
///
/// // Assert agent result failed with specific error
/// AssertionHelpers.ShouldHaveFailedWith(result, "Authentication failed");
///
/// // Assert test run passed
/// var testResult = await TestRunner.ExecuteTestsAsync(workspace);
/// AssertionHelpers.ShouldHavePassedAllTests(testResult);
/// </code>
/// </example>
public static class AssertionHelpers
{
    /// <summary>
    /// Asserts that an agent result succeeded.
    /// </summary>
    /// <param name="result">The agent result to check</param>
    /// <param name="because">Optional reason for the assertion</param>
    public static void ShouldHaveSucceeded(AgentResult result, string? because = null)
    {
        using (new AssertionScope())
        {
            result.Should().NotBeNull();
            result.Success.Should().BeTrue(because ?? "agent execution should have succeeded");
            result.Output.Should().NotBeNullOrWhiteSpace(because ?? "successful execution should have output");
            result.ErrorMessage.Should().BeNullOrEmpty(because ?? "successful execution should have no error message");
        }
    }

    /// <summary>
    /// Asserts that an agent result failed.
    /// </summary>
    /// <param name="result">The agent result to check</param>
    /// <param name="because">Optional reason for the assertion</param>
    public static void ShouldHaveFailed(AgentResult result, string? because = null)
    {
        using (new AssertionScope())
        {
            result.Should().NotBeNull();
            result.Success.Should().BeFalse(because ?? "agent execution should have failed");
            result.ErrorMessage.Should().NotBeNullOrWhiteSpace(because ?? "failed execution should have an error message");
        }
    }

    /// <summary>
    /// Asserts that an agent result failed with a specific error message.
    /// </summary>
    /// <param name="result">The agent result to check</param>
    /// <param name="expectedError">The expected error message (substring match)</param>
    /// <param name="because">Optional reason for the assertion</param>
    public static void ShouldHaveFailedWith(AgentResult result, string expectedError, string? because = null)
    {
        using (new AssertionScope())
        {
            ShouldHaveFailed(result, because);
            result.ErrorMessage.Should().Contain(expectedError,
                because ?? $"error message should contain '{expectedError}'");
        }
    }

    /// <summary>
    /// Asserts that a test run result passed all tests.
    /// </summary>
    /// <param name="testResult">The test run result to check</param>
    /// <param name="because">Optional reason for the assertion</param>
    public static void ShouldHavePassedAllTests(TestRunResult testResult, string? because = null)
    {
        using (new AssertionScope())
        {
            testResult.Should().NotBeNull();
            testResult.Pass.Should().BeTrue(because ?? "all tests should have passed");
            testResult.TestResults.Should().NotBeEmpty(because ?? "test results should not be empty");
            testResult.TestResults.Should().OnlyContain(t => t.Status == TestStatus.Passed,
                because ?? "all test results should have passed status");
        }
    }

    /// <summary>
    /// Asserts that a test run result has at least one test failure.
    /// </summary>
    /// <param name="testResult">The test run result to check</param>
    /// <param name="because">Optional reason for the assertion</param>
    public static void ShouldHaveFailedTests(TestRunResult testResult, string? because = null)
    {
        using (new AssertionScope())
        {
            testResult.Should().NotBeNull();
            testResult.Pass.Should().BeFalse(because ?? "test run should have failures");
            testResult.TestResults.Should().Contain(t => t.Status == TestStatus.Failed,
                because ?? "test results should contain at least one failure");
        }
    }

    /// <summary>
    /// Asserts that coverage meets a minimum threshold.
    /// </summary>
    /// <param name="testResult">The test run result with coverage</param>
    /// <param name="minimumPercent">The minimum coverage percentage required</param>
    /// <param name="because">Optional reason for the assertion</param>
    public static void ShouldHaveMinimumCoverage(
        TestRunResult testResult,
        double minimumPercent,
        string? because = null)
    {
        using (new AssertionScope())
        {
            testResult.Should().NotBeNull();
            testResult.Coverage.Should().NotBeNull(because ?? "coverage information should be present");
            testResult.Coverage!.LineCoveragePercent.Should().BeGreaterThanOrEqualTo(minimumPercent,
                because ?? $"line coverage should be at least {minimumPercent}%");
        }
    }

    /// <summary>
    /// Asserts that a patch application succeeded.
    /// </summary>
    /// <param name="result">The patch result to check</param>
    /// <param name="because">Optional reason for the assertion</param>
    public static void ShouldHaveAppliedSuccessfully(PatchApplicationResult result, string? because = null)
    {
        using (new AssertionScope())
        {
            result.Should().NotBeNull();
            result.Success.Should().BeTrue(because ?? "patch should have applied successfully");
            result.ErrorMessage.Should().BeNullOrEmpty(because ?? "successful patch should have no error message");
        }
    }

    /// <summary>
    /// Asserts that a patch application failed.
    /// </summary>
    /// <param name="result">The patch result to check</param>
    /// <param name="expectedError">Optional expected error message (substring match)</param>
    /// <param name="because">Optional reason for the assertion</param>
    public static void ShouldHaveFailedToApply(PatchApplicationResult result, string? expectedError = null, string? because = null)
    {
        using (new AssertionScope())
        {
            result.Should().NotBeNull();
            result.Success.Should().BeFalse(because ?? "patch should have failed to apply");
            result.ErrorMessage.Should().NotBeNullOrWhiteSpace(because ?? "failed patch should have an error message");

            if (expectedError != null)
            {
                result.ErrorMessage.Should().Contain(expectedError,
                    because ?? $"error message should contain '{expectedError}'");
            }
        }
    }

    /// <summary>
    /// Asserts that an agent context has a specific state value.
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="context">The agent context</param>
    /// <param name="key">The state key</param>
    /// <param name="expectedValue">The expected value</param>
    /// <param name="because">Optional reason for the assertion</param>
    public static void ShouldHaveStateValue<T>(
        AgentContext context,
        string key,
        T expectedValue,
        string? because = null)
    {
        using (new AssertionScope())
        {
            context.Should().NotBeNull();
            context.ContainsKey(key).Should().BeTrue(because ?? $"context should contain key '{key}'");
            context.GetValue<T>(key).Should().Be(expectedValue,
                because ?? $"context value for '{key}' should be {expectedValue}");
        }
    }

    /// <summary>
    /// Asserts that an agent context has messages from specific agents.
    /// </summary>
    /// <param name="context">The agent context</param>
    /// <param name="agentNames">The expected agent names in order</param>
    /// <param name="because">Optional reason for the assertion</param>
    public static void ShouldHaveMessagesFromAgents(
        AgentContext context,
        string[] agentNames,
        string? because = null)
    {
        using (new AssertionScope())
        {
            context.Should().NotBeNull();
            context.History.Should().HaveCount(agentNames.Length,
                because ?? $"should have {agentNames.Length} messages");

            for (int i = 0; i < agentNames.Length; i++)
            {
                context.History[i].AgentName.Should().Be(agentNames[i],
                    because ?? $"message {i} should be from {agentNames[i]}");
            }
        }
    }

    /// <summary>
    /// Asserts that a file exists in a directory.
    /// </summary>
    /// <param name="directory">The directory to check</param>
    /// <param name="fileName">The file name to look for</param>
    /// <param name="because">Optional reason for the assertion</param>
    public static void ShouldContainFile(string directory, string fileName, string? because = null)
    {
        Directory.Exists(directory).Should().BeTrue(because ?? $"directory '{directory}' should exist");
        File.Exists(Path.Combine(directory, fileName)).Should().BeTrue(
            because ?? $"file '{fileName}' should exist in '{directory}'");
    }

    /// <summary>
    /// Asserts that a file does not exist in a directory.
    /// </summary>
    /// <param name="directory">The directory to check</param>
    /// <param name="fileName">The file name to look for</param>
    /// <param name="because">Optional reason for the assertion</param>
    public static void ShouldNotContainFile(string directory, string fileName, string? because = null)
    {
        if (Directory.Exists(directory))
        {
            File.Exists(Path.Combine(directory, fileName)).Should().BeFalse(
                because ?? $"file '{fileName}' should not exist in '{directory}'");
        }
    }

    /// <summary>
    /// Asserts that a project structure has the expected main and test projects.
    /// </summary>
    /// <param name="structure">The project structure</param>
    /// <param name="expectedMainProject">The expected main project directory</param>
    /// <param name="expectedTestProjects">The expected test project directories</param>
    /// <param name="because">Optional reason for the assertion</param>
    public static void ShouldHaveProjectStructure(
        ProjectStructureInfo structure,
        string expectedMainProject,
        string[] expectedTestProjects,
        string? because = null)
    {
        using (new AssertionScope())
        {
            structure.Should().NotBeNull();
            structure.MainProject.Should().Be(expectedMainProject,
                because ?? $"main project should be '{expectedMainProject}'");
            structure.TestProjects.Should().BeEquivalentTo(expectedTestProjects,
                because ?? "test projects should match expected list");
        }
    }
}
