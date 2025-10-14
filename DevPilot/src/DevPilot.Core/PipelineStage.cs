namespace DevPilot.Core;

/// <summary>
/// Represents the stages in the MASAI pipeline execution.
/// </summary>
public enum PipelineStage
{
    /// <summary>
    /// Pipeline has not started yet.
    /// </summary>
    NotStarted,

    /// <summary>
    /// Running the planner agent to decompose the task.
    /// </summary>
    Planning,

    /// <summary>
    /// Running the coder agent to generate patches.
    /// </summary>
    Coding,

    /// <summary>
    /// Running the reviewer agent to validate code.
    /// </summary>
    Reviewing,

    /// <summary>
    /// Running the tester agent to execute tests.
    /// </summary>
    Testing,

    /// <summary>
    /// Running the evaluator agent to score the work.
    /// </summary>
    Evaluating,

    /// <summary>
    /// All pipeline stages completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Pipeline failed at some stage.
    /// </summary>
    Failed,

    /// <summary>
    /// Pipeline is paused awaiting human approval (hard stop).
    /// </summary>
    AwaitingApproval
}
