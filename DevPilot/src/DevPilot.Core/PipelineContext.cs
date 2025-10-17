namespace DevPilot.Core;

/// <summary>
/// Manages state and data flow through the MASAI pipeline stages.
/// </summary>
public sealed class PipelineContext
{
    private readonly List<PipelineStageEntry> _stageHistory = new();
    private readonly Dictionary<PipelineStage, string> _stageOutputs = new();

    /// <summary>
    /// Gets the unique identifier for this pipeline execution.
    /// </summary>
    public string PipelineId { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the current stage of the pipeline.
    /// </summary>
    public PipelineStage CurrentStage { get; private set; } = PipelineStage.NotStarted;

    /// <summary>
    /// Gets the original user request.
    /// </summary>
    public required string UserRequest { get; init; }

    /// <summary>
    /// Gets the JSON plan output from the planner agent.
    /// </summary>
    public string? Plan { get; private set; }

    /// <summary>
    /// Gets the unified diff patch from the coder agent.
    /// </summary>
    public string? Patch { get; private set; }

    /// <summary>
    /// Gets the JSON review verdict from the reviewer agent.
    /// </summary>
    public string? Review { get; private set; }

    /// <summary>
    /// Gets the JSON test report from the tester agent.
    /// </summary>
    public string? TestReport { get; private set; }

    /// <summary>
    /// Gets the JSON scores from the evaluator agent.
    /// </summary>
    public string? Scores { get; private set; }

    /// <summary>
    /// Gets the root directory of the isolated workspace for this pipeline execution.
    /// </summary>
    public string? WorkspaceRoot { get; private set; }

    /// <summary>
    /// Gets the detected project structure of the workspace.
    /// </summary>
    public ProjectStructureInfo? ProjectStructure { get; private set; }

    /// <summary>
    /// Gets the list of files that were created or modified by applying the patch.
    /// </summary>
    public IReadOnlyList<string>? AppliedFiles { get; private set; }

    /// <summary>
    /// Gets the current revision iteration count (0 = first attempt, 1+ = revisions after reviewer feedback).
    /// </summary>
    public int RevisionIteration { get; private set; }

    /// <summary>
    /// Gets whether the pipeline requires human approval (hard stop).
    /// </summary>
    public bool ApprovalRequired { get; private set; }

    /// <summary>
    /// Gets the reason why approval is required.
    /// </summary>
    public string? ApprovalReason { get; private set; }

    /// <summary>
    /// Gets the audit trail of stage transitions.
    /// </summary>
    public IReadOnlyList<PipelineStageEntry> StageHistory => _stageHistory.AsReadOnly();

    /// <summary>
    /// Gets the timestamp when the pipeline started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the pipeline completed or failed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Advances the pipeline to a new stage and stores its output.
    /// </summary>
    /// <param name="newStage">The stage to advance to.</param>
    /// <param name="output">The output for the new stage.</param>
    public void AdvanceToStage(PipelineStage newStage, string output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(output);

        // Record stage transition
        _stageHistory.Add(new PipelineStageEntry
        {
            Stage = newStage,
            EnteredAt = DateTimeOffset.UtcNow,
            PreviousStage = CurrentStage
        });

        CurrentStage = newStage;

        // Store output for new stage
        _stageOutputs[newStage] = output;
        SetStageSpecificOutput(newStage, output);
    }

    /// <summary>
    /// Requests human approval, triggering a hard stop in the pipeline.
    /// </summary>
    /// <param name="reason">The reason approval is required.</param>
    public void RequestApproval(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var previousStage = CurrentStage;

        ApprovalRequired = true;
        ApprovalReason = reason;
        CurrentStage = PipelineStage.AwaitingApproval;

        _stageHistory.Add(new PipelineStageEntry
        {
            Stage = PipelineStage.AwaitingApproval,
            EnteredAt = DateTimeOffset.UtcNow,
            PreviousStage = previousStage
        });
    }

    /// <summary>
    /// Clears the approval requirement, allowing the pipeline to resume.
    /// </summary>
    public void ClearApproval()
    {
        ApprovalRequired = false;
        ApprovalReason = null;
    }

    /// <summary>
    /// Sets the workspace root directory for this pipeline execution.
    /// </summary>
    /// <param name="workspaceRoot">The absolute path to the workspace root directory.</param>
    public void SetWorkspaceRoot(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        WorkspaceRoot = workspaceRoot;
    }

    /// <summary>
    /// Sets the detected project structure for the workspace.
    /// </summary>
    /// <param name="projectStructure">The detected project structure information.</param>
    public void SetProjectStructure(ProjectStructureInfo projectStructure)
    {
        ArgumentNullException.ThrowIfNull(projectStructure);
        ProjectStructure = projectStructure;
    }

    /// <summary>
    /// Sets the list of files that were applied by the patch.
    /// </summary>
    /// <param name="appliedFiles">The list of file paths that were created or modified.</param>
    public void SetAppliedFiles(IReadOnlyList<string> appliedFiles)
    {
        ArgumentNullException.ThrowIfNull(appliedFiles);
        AppliedFiles = appliedFiles;
    }

    /// <summary>
    /// Increments the revision iteration count when the reviewer requests code revision.
    /// </summary>
    public void IncrementRevisionIteration()
    {
        RevisionIteration++;
    }

    /// <summary>
    /// Retrieves the output from a specific pipeline stage.
    /// </summary>
    /// <param name="stage">The stage to get output for.</param>
    /// <returns>The stage output if available; otherwise, null.</returns>
    public string? GetStageOutput(PipelineStage stage)
    {
        return _stageOutputs.TryGetValue(stage, out var output) ? output : null;
    }

    private void SetStageSpecificOutput(PipelineStage stage, string output)
    {
        switch (stage)
        {
            case PipelineStage.Planning:
                Plan = output;
                break;
            case PipelineStage.Coding:
                Patch = output;
                break;
            case PipelineStage.Reviewing:
                Review = output;
                break;
            case PipelineStage.Testing:
                TestReport = output;
                break;
            case PipelineStage.Evaluating:
                Scores = output;
                break;
        }
    }
}

/// <summary>
/// Represents an entry in the pipeline stage history.
/// </summary>
public sealed class PipelineStageEntry
{
    /// <summary>
    /// Gets the stage that was entered.
    /// </summary>
    public required PipelineStage Stage { get; init; }

    /// <summary>
    /// Gets the previous stage before this transition.
    /// </summary>
    public PipelineStage PreviousStage { get; init; }

    /// <summary>
    /// Gets the timestamp when this stage was entered.
    /// </summary>
    public required DateTimeOffset EnteredAt { get; init; }
}
