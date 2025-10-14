using System.Text.Json.Serialization;

namespace DevPilot.Core;

/// <summary>
/// Represents the complete output from the Planner agent.
/// </summary>
public sealed class PlannerOutput
{
    /// <summary>
    /// Gets the execution plan details.
    /// </summary>
    [JsonPropertyName("plan")]
    public required PlanDetails Plan { get; init; }

    /// <summary>
    /// Gets the list of files to be created, modified, or deleted.
    /// </summary>
    [JsonPropertyName("file_list")]
    public required List<FileOperation> FileList { get; init; }

    /// <summary>
    /// Gets the risk assessment for the plan.
    /// </summary>
    [JsonPropertyName("risk")]
    public required RiskAssessment Risk { get; init; }

    /// <summary>
    /// Gets the verification criteria.
    /// </summary>
    [JsonPropertyName("verify")]
    public VerificationPlan? Verify { get; init; }

    /// <summary>
    /// Gets the rollback strategy.
    /// </summary>
    [JsonPropertyName("rollback")]
    public RollbackPlan? Rollback { get; init; }

    /// <summary>
    /// Gets whether the plan requires human approval.
    /// </summary>
    [JsonPropertyName("needs_approval")]
    public bool NeedsApproval { get; init; }

    /// <summary>
    /// Gets the reason why approval is required.
    /// </summary>
    [JsonPropertyName("approval_reason")]
    public string? ApprovalReason { get; init; }
}

/// <summary>
/// Represents the execution plan details.
/// </summary>
public sealed class PlanDetails
{
    /// <summary>
    /// Gets the summary of what will be accomplished.
    /// </summary>
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    /// <summary>
    /// Gets the list of execution steps.
    /// </summary>
    [JsonPropertyName("steps")]
    public required List<PlanStep> Steps { get; init; }
}

/// <summary>
/// Represents a single step in the execution plan.
/// </summary>
public sealed class PlanStep
{
    /// <summary>
    /// Gets the step number.
    /// </summary>
    [JsonPropertyName("step_number")]
    public int StepNumber { get; init; }

    /// <summary>
    /// Gets the description of what this step does.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>
    /// Gets the file target for this step (can be null).
    /// </summary>
    [JsonPropertyName("file_target")]
    public string? FileTarget { get; init; }

    /// <summary>
    /// Gets the agent responsible for this step.
    /// </summary>
    [JsonPropertyName("agent")]
    public required string Agent { get; init; }

    /// <summary>
    /// Gets the estimated lines of code for this step.
    /// </summary>
    [JsonPropertyName("estimated_loc")]
    public int EstimatedLoc { get; init; }
}

/// <summary>
/// Represents a file operation in the plan.
/// </summary>
public sealed class FileOperation
{
    /// <summary>
    /// Gets the file path.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// Gets the operation type (create, modify, delete).
    /// </summary>
    [JsonPropertyName("operation")]
    public required string Operation { get; init; }

    /// <summary>
    /// Gets the reason for this file operation.
    /// </summary>
    [JsonPropertyName("reason")]
    public required string Reason { get; init; }
}

/// <summary>
/// Represents the risk assessment for the plan.
/// </summary>
public sealed class RiskAssessment
{
    /// <summary>
    /// Gets the risk level (low, medium, high).
    /// </summary>
    [JsonPropertyName("level")]
    public required string Level { get; init; }

    /// <summary>
    /// Gets the risk factors identified.
    /// </summary>
    [JsonPropertyName("factors")]
    public required List<string> Factors { get; init; }

    /// <summary>
    /// Gets the mitigation strategy.
    /// </summary>
    [JsonPropertyName("mitigation")]
    public required string Mitigation { get; init; }
}

/// <summary>
/// Represents the verification plan.
/// </summary>
public sealed class VerificationPlan
{
    /// <summary>
    /// Gets the acceptance criteria.
    /// </summary>
    [JsonPropertyName("acceptance_criteria")]
    public required List<string> AcceptanceCriteria { get; init; }

    /// <summary>
    /// Gets the test commands to run.
    /// </summary>
    [JsonPropertyName("test_commands")]
    public required List<string> TestCommands { get; init; }

    /// <summary>
    /// Gets the manual checks required.
    /// </summary>
    [JsonPropertyName("manual_checks")]
    public required List<string> ManualChecks { get; init; }
}

/// <summary>
/// Represents the rollback strategy.
/// </summary>
public sealed class RollbackPlan
{
    /// <summary>
    /// Gets the rollback strategy description.
    /// </summary>
    [JsonPropertyName("strategy")]
    public required string Strategy { get; init; }

    /// <summary>
    /// Gets the rollback commands.
    /// </summary>
    [JsonPropertyName("commands")]
    public required List<string> Commands { get; init; }

    /// <summary>
    /// Gets additional rollback notes.
    /// </summary>
    [JsonPropertyName("notes")]
    public required string Notes { get; init; }
}
