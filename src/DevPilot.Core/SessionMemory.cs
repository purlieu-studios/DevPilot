namespace DevPilot.Core;

/// <summary>
/// Represents a development session's recorded context and activities.
/// Tracks work done across multiple pipeline executions to maintain context between conversations.
/// </summary>
public sealed record SessionMemory
{
    /// <summary>
    /// Gets the unique identifier for this session.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the timestamp when this session started.
    /// </summary>
    public required DateTime StartTime { get; init; }

    /// <summary>
    /// Gets the timestamp when this session ended (null if still active).
    /// </summary>
    public DateTime? EndTime { get; init; }

    /// <summary>
    /// Gets the list of activities recorded during this session.
    /// </summary>
    public required List<SessionActivity> Activities { get; init; }

    /// <summary>
    /// Gets tags for categorizing this session (e.g., feature names, areas of codebase).
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Gets the working directory where this session took place.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Gets a human-readable summary of what was accomplished in this session.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Gets the total duration of this session.
    /// </summary>
    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;

    /// <summary>
    /// Gets the number of pipeline executions in this session.
    /// </summary>
    public int PipelineCount => Activities.Count(a => a.Type == ActivityType.PipelineExecution);

    /// <summary>
    /// Gets the number of commits made in this session.
    /// </summary>
    public int CommitCount => Activities.Count(a => a.Type == ActivityType.GitCommit);
}

/// <summary>
/// Represents a single activity within a development session.
/// </summary>
public sealed record SessionActivity
{
    /// <summary>
    /// Gets the type of activity.
    /// </summary>
    public required ActivityType Type { get; init; }

    /// <summary>
    /// Gets the timestamp when this activity occurred.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the description of this activity.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets additional context data for this activity (e.g., pipeline ID, commit hash).
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// Types of activities that can be recorded in a session.
/// </summary>
public enum ActivityType
{
    /// <summary>
    /// A DevPilot pipeline was executed.
    /// </summary>
    PipelineExecution,

    /// <summary>
    /// A git commit was created.
    /// </summary>
    GitCommit,

    /// <summary>
    /// An architectural or implementation decision was made.
    /// </summary>
    Decision,

    /// <summary>
    /// An issue, bug, or problem was encountered.
    /// </summary>
    IssueEncountered,

    /// <summary>
    /// A solution or fix was applied.
    /// </summary>
    IssueFix,

    /// <summary>
    /// A note or observation was recorded.
    /// </summary>
    Note
}
