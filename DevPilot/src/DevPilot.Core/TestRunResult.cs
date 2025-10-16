namespace DevPilot.Core;

/// <summary>
/// Represents the result of executing tests in a workspace.
/// </summary>
public sealed class TestRunResult
{
    /// <summary>
    /// Gets whether all tests passed.
    /// </summary>
    public required bool Pass { get; init; }

    /// <summary>
    /// Gets a human-readable summary of test results.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets the list of individual test results.
    /// </summary>
    public required IReadOnlyList<TestResult> TestResults { get; init; }

    /// <summary>
    /// Gets code coverage information, if available.
    /// </summary>
    public CoverageInfo? Coverage { get; init; }

    /// <summary>
    /// Gets performance metrics for the test run.
    /// </summary>
    public required PerformanceInfo Performance { get; init; }

    /// <summary>
    /// Gets any error message if the test run failed to execute.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Represents the result of a single test case.
/// </summary>
public sealed class TestResult
{
    /// <summary>
    /// Gets the fully qualified name of the test.
    /// </summary>
    public required string TestName { get; init; }

    /// <summary>
    /// Gets the test execution status.
    /// </summary>
    public required TestStatus Status { get; init; }

    /// <summary>
    /// Gets the test execution duration in milliseconds.
    /// </summary>
    public required double DurationMs { get; init; }

    /// <summary>
    /// Gets the failure or skip message, if applicable.
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// Represents test execution status.
/// </summary>
public enum TestStatus
{
    /// <summary>Test passed successfully.</summary>
    Passed,

    /// <summary>Test failed with assertion error.</summary>
    Failed,

    /// <summary>Test was skipped or ignored.</summary>
    Skipped
}

/// <summary>
/// Represents code coverage metrics.
/// </summary>
public sealed class CoverageInfo
{
    /// <summary>
    /// Gets the line coverage percentage (0-100).
    /// </summary>
    public required double LineCoveragePercent { get; init; }

    /// <summary>
    /// Gets the branch coverage percentage (0-100).
    /// </summary>
    public required double BranchCoveragePercent { get; init; }
}

/// <summary>
/// Represents performance metrics for a test run.
/// </summary>
public sealed class PerformanceInfo
{
    /// <summary>
    /// Gets the total test execution duration in milliseconds.
    /// </summary>
    public required double TotalDurationMs { get; init; }

    /// <summary>
    /// Gets the name of the slowest test, if identified.
    /// </summary>
    public string? SlowestTest { get; init; }
}
