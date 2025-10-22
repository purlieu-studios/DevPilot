namespace DevPilot.Core;

/// <summary>
/// Represents the persisted state of a pipeline execution.
/// Used for saving/loading pipeline state to enable resume, approve, reject workflows.
/// </summary>
public sealed record PipelineState
{
    /// <summary>
    /// Gets the unique identifier for this pipeline execution.
    /// </summary>
    public required string PipelineId { get; init; }

    /// <summary>
    /// Gets the original user request that started this pipeline.
    /// </summary>
    public required string UserRequest { get; init; }

    /// <summary>
    /// Gets the timestamp when this pipeline execution started.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the current status of the pipeline.
    /// </summary>
    public required PipelineStatus Status { get; init; }

    /// <summary>
    /// Gets the absolute path to the isolated workspace for this pipeline.
    /// </summary>
    public required string WorkspacePath { get; init; }

    /// <summary>
    /// Gets the absolute path to the source repository root (where DevPilot was executed from).
    /// </summary>
    public string? SourceRoot { get; init; }

    /// <summary>
    /// Gets the current stage the pipeline has reached.
    /// </summary>
    public PipelineStage CurrentStage { get; init; }

    /// <summary>
    /// Gets all stage outputs indexed by stage.
    /// </summary>
    public required Dictionary<string, string> StageOutputs { get; init; }

    /// <summary>
    /// Gets the unified diff patch from the Coder stage (if available).
    /// </summary>
    public string? Patch { get; init; }

    /// <summary>
    /// Gets the evaluation scores JSON from the Evaluator stage (if available).
    /// </summary>
    public string? Scores { get; init; }

    /// <summary>
    /// Gets whether this pipeline requires human approval before proceeding.
    /// </summary>
    public bool RequiresApproval { get; init; }

    /// <summary>
    /// Gets the reason why approval is required (if applicable).
    /// </summary>
    public string? ApprovalReason { get; init; }

    /// <summary>
    /// Gets the error message if the pipeline failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the total duration of the pipeline execution.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Gets whether RAG was enabled for this pipeline execution.
    /// </summary>
    public bool RAGEnabled { get; init; }

    /// <summary>
    /// Gets the timestamp when the pipeline completed (if completed).
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Creates a PipelineState from a PipelineContext and PipelineResult.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="result">The pipeline result.</param>
    /// <param name="status">The current pipeline status.</param>
    /// <returns>A new PipelineState instance.</returns>
    public static PipelineState FromContext(PipelineContext context, PipelineResult? result, PipelineStatus status)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Build stage outputs dictionary with string keys for JSON serialization
        var stageOutputs = new Dictionary<string, string>();

        if (context.Plan != null)
        {
            stageOutputs[nameof(PipelineStage.Planning)] = context.Plan;
        }
        if (context.Patch != null)
        {
            stageOutputs[nameof(PipelineStage.Coding)] = context.Patch;
        }
        if (context.Review != null)
        {
            stageOutputs[nameof(PipelineStage.Reviewing)] = context.Review;
        }
        if (context.TestReport != null)
        {
            stageOutputs[nameof(PipelineStage.Testing)] = context.TestReport;
        }
        if (context.Scores != null)
        {
            stageOutputs[nameof(PipelineStage.Evaluating)] = context.Scores;
        }

        return new PipelineState
        {
            PipelineId = context.PipelineId,
            UserRequest = context.UserRequest,
            Timestamp = context.StartedAt.DateTime,
            Status = status,
            WorkspacePath = context.WorkspaceRoot ?? string.Empty,
            SourceRoot = context.SourceRoot,
            CurrentStage = context.CurrentStage,
            StageOutputs = stageOutputs,
            Patch = context.Patch,
            Scores = context.Scores,
            RequiresApproval = context.ApprovalRequired,
            ApprovalReason = context.ApprovalReason,
            ErrorMessage = result?.ErrorMessage,
            Duration = result?.Duration,
            RAGEnabled = context.RAGEnabled,
            CompletedAt = context.CompletedAt?.DateTime
        };
    }
}

/// <summary>
/// Represents the status of a persisted pipeline execution.
/// </summary>
public enum PipelineStatus
{
    /// <summary>
    /// Pipeline is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Pipeline is awaiting human approval before proceeding.
    /// </summary>
    AwaitingApproval,

    /// <summary>
    /// Pipeline was approved and changes were applied.
    /// </summary>
    Approved,

    /// <summary>
    /// Pipeline was rejected and changes were discarded.
    /// </summary>
    Rejected,

    /// <summary>
    /// Pipeline failed with an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Pipeline completed successfully.
    /// </summary>
    Completed
}
