namespace DevPilot.Core;

/// <summary>
/// Represents the final result of a complete pipeline execution.
/// </summary>
public sealed class PipelineResult
{
    /// <summary>
    /// Gets whether the pipeline completed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the final stage the pipeline reached before completing or failing.
    /// </summary>
    public required PipelineStage FinalStage { get; init; }

    /// <summary>
    /// Gets the complete pipeline context with all stage outputs and history.
    /// </summary>
    public required PipelineContext Context { get; init; }

    /// <summary>
    /// Gets the total duration of the pipeline execution.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets whether the pipeline is awaiting human approval.
    /// </summary>
    public bool RequiresApproval { get; init; }

    /// <summary>
    /// Gets the error message if the pipeline failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful pipeline result.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="duration">The execution duration.</param>
    /// <returns>A successful pipeline result.</returns>
    public static PipelineResult CreateSuccess(PipelineContext context, TimeSpan duration)
    {
        return new PipelineResult
        {
            Success = true,
            FinalStage = context.CurrentStage,
            Context = context,
            Duration = duration,
            RequiresApproval = false
        };
    }

    /// <summary>
    /// Creates a failed pipeline result.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="duration">The execution duration.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed pipeline result.</returns>
    public static PipelineResult CreateFailure(PipelineContext context, TimeSpan duration, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new PipelineResult
        {
            Success = false,
            FinalStage = context.CurrentStage,
            Context = context,
            Duration = duration,
            RequiresApproval = false,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Creates a pipeline result that is awaiting approval.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="duration">The execution duration.</param>
    /// <returns>A pipeline result awaiting approval.</returns>
    public static PipelineResult CreateAwaitingApproval(PipelineContext context, TimeSpan duration)
    {
        return new PipelineResult
        {
            Success = false,
            FinalStage = PipelineStage.AwaitingApproval,
            Context = context,
            Duration = duration,
            RequiresApproval = true
        };
    }

    /// <summary>
    /// Creates a pipeline result that passed with warnings (e.g., test failures).
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="duration">The execution duration.</param>
    /// <param name="warningMessage">The warning message describing what went wrong.</param>
    /// <returns>A pipeline result that completed with warnings.</returns>
    public static PipelineResult CreatePassedWithWarnings(
        PipelineContext context,
        TimeSpan duration,
        string warningMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(warningMessage);

        return new PipelineResult
        {
            Success = true, // Still successful - Evaluator ran and provided feedback
            FinalStage = PipelineStage.Completed,
            Context = context,
            Duration = duration,
            RequiresApproval = false,
            ErrorMessage = warningMessage // Repurposed as warning message
        };
    }
}
