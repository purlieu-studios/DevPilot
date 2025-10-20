namespace DevPilot.Core;

/// <summary>
/// Represents a task that can be executed by an agent.
/// </summary>
public sealed class AgentTask
{
    /// <summary>
    /// Gets the unique task identifier.
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// Gets the task description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the name of the agent that should execute this task.
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// Gets the task status.
    /// </summary>
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    /// <summary>
    /// Gets the task dependencies (IDs of tasks that must complete first).
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the task context/parameters.
    /// </summary>
    public Dictionary<string, object>? Context { get; init; }

    /// <summary>
    /// Gets the task result when completed.
    /// </summary>
    public AgentResult? Result { get; set; }

    /// <summary>
    /// Gets the task creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the task start timestamp.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// Gets the task completion timestamp.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Checks if all dependencies are completed.
    /// </summary>
    /// <param name="completedTaskIds">Set of completed task IDs.</param>
    /// <returns>True if all dependencies are completed.</returns>
    public bool AreDependenciesMet(HashSet<string> completedTaskIds)
    {
        ArgumentNullException.ThrowIfNull(completedTaskIds);
        return Dependencies.All(completedTaskIds.Contains);
    }
}

/// <summary>
/// Represents the status of a task.
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// Task is waiting to be executed.
    /// </summary>
    Pending,

    /// <summary>
    /// Task is currently executing.
    /// </summary>
    InProgress,

    /// <summary>
    /// Task completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Task failed during execution.
    /// </summary>
    Failed,

    /// <summary>
    /// Task was cancelled.
    /// </summary>
    Cancelled
}
