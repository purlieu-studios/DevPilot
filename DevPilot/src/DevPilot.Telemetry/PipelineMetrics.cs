namespace DevPilot.Telemetry;

/// <summary>
/// Captures quality metrics from a single pipeline execution for regression tracking.
/// </summary>
public sealed class PipelineMetrics
{
    /// <summary>
    /// Gets the unique identifier for this pipeline run.
    /// </summary>
    public required string PipelineId { get; init; }

    /// <summary>
    /// Gets the timestamp when this pipeline started.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the user request that initiated this pipeline.
    /// </summary>
    public required string UserRequest { get; init; }

    /// <summary>
    /// Gets whether the pipeline completed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the overall quality score (0-10).
    /// </summary>
    public required double OverallScore { get; init; }

    /// <summary>
    /// Gets the plan quality score (0-10).
    /// </summary>
    public required double PlanQuality { get; init; }

    /// <summary>
    /// Gets the code quality score (0-10).
    /// </summary>
    public required double CodeQuality { get; init; }

    /// <summary>
    /// Gets the test coverage score (0-10).
    /// </summary>
    public required double TestCoverage { get; init; }

    /// <summary>
    /// Gets the documentation quality score (0-10).
    /// </summary>
    public required double Documentation { get; init; }

    /// <summary>
    /// Gets the maintainability score (0-10).
    /// </summary>
    public required double Maintainability { get; init; }

    /// <summary>
    /// Gets the number of tests generated.
    /// </summary>
    public required int TestsGenerated { get; init; }

    /// <summary>
    /// Gets the number of tests that passed.
    /// </summary>
    public required int TestsPassed { get; init; }

    /// <summary>
    /// Gets the number of tests that failed.
    /// </summary>
    public required int TestsFailed { get; init; }

    /// <summary>
    /// Gets the pipeline execution duration.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the final pipeline stage reached.
    /// </summary>
    public required string FinalStage { get; init; }

    /// <summary>
    /// Gets whether RAG was enabled for this pipeline run.
    /// </summary>
    public required bool RagEnabled { get; init; }

    /// <summary>
    /// Gets the number of files created or modified.
    /// </summary>
    public required int FilesModified { get; init; }

    /// <summary>
    /// Gets the repository structure type (e.g., "standard", "nonstandard", "no-sln").
    /// </summary>
    public string? RepositoryStructure { get; init; }

    /// <summary>
    /// Calculates the test pass rate (0-1).
    /// </summary>
    public double TestPassRate => TestsGenerated > 0
        ? (double)TestsPassed / TestsGenerated
        : 0;

    /// <summary>
    /// Determines if this pipeline execution meets quality thresholds.
    /// </summary>
    public bool MeetsQualityThresholds()
    {
        return Success
            && OverallScore >= 7.0
            && TestCoverage >= 7.0
            && CodeQuality >= 7.0
            && TestPassRate >= 0.95; // 95% test pass rate
    }
}
