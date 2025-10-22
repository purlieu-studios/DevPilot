using System.Text.Json;
using DevPilot.Core;

namespace DevPilot.Orchestrator;

/// <summary>
/// Manages recording and retrieval of development session context.
/// Enables maintaining continuity across Claude Code conversations.
/// </summary>
public sealed class SessionManager
{
    private readonly string _sessionsDirectory;
    private SessionMemory? _activeSession;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionManager"/> class.
    /// </summary>
    /// <param name="workingDirectory">The working directory (defaults to current directory).</param>
    public SessionManager(string? workingDirectory = null)
    {
        var root = workingDirectory ?? Directory.GetCurrentDirectory();
        _sessionsDirectory = Path.Combine(root, ".devpilot", "sessions");

        // Ensure sessions directory exists
        Directory.CreateDirectory(_sessionsDirectory);
    }

    /// <summary>
    /// Starts a new development session.
    /// </summary>
    /// <param name="workingDirectory">The working directory for this session.</param>
    /// <returns>The newly created session.</returns>
    public SessionMemory StartSession(string? workingDirectory = null)
    {
        _activeSession = new SessionMemory
        {
            SessionId = Guid.NewGuid().ToString(),
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>(),
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory()
        };

        return _activeSession;
    }

    /// <summary>
    /// Records a pipeline execution activity in the active session.
    /// </summary>
    /// <param name="pipelineId">The pipeline ID.</param>
    /// <param name="userRequest">The user's request.</param>
    /// <param name="success">Whether the pipeline succeeded.</param>
    /// <param name="qualityScore">The overall quality score (if available).</param>
    public void RecordPipelineExecution(string pipelineId, string userRequest, bool success, double? qualityScore = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userRequest);

        if (_activeSession == null)
        {
            throw new InvalidOperationException("No active session. Call StartSession() first.");
        }

        var metadata = new Dictionary<string, string>
        {
            ["pipelineId"] = pipelineId,
            ["success"] = success.ToString(),
            ["userRequest"] = userRequest
        };

        if (qualityScore.HasValue)
        {
            metadata["qualityScore"] = qualityScore.Value.ToString("F1");
        }

        var activity = new SessionActivity
        {
            Type = ActivityType.PipelineExecution,
            Timestamp = DateTime.UtcNow,
            Description = $"Pipeline: {userRequest.Substring(0, Math.Min(50, userRequest.Length))}{(userRequest.Length > 50 ? "..." : "")}",
            Metadata = metadata
        };

        _activeSession.Activities.Add(activity);
    }

    /// <summary>
    /// Records a git commit activity in the active session.
    /// </summary>
    /// <param name="commitHash">The commit hash.</param>
    /// <param name="commitMessage">The commit message.</param>
    public void RecordGitCommit(string commitHash, string commitMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitMessage);

        if (_activeSession == null)
        {
            throw new InvalidOperationException("No active session. Call StartSession() first.");
        }

        var activity = new SessionActivity
        {
            Type = ActivityType.GitCommit,
            Timestamp = DateTime.UtcNow,
            Description = $"Commit: {commitMessage.Substring(0, Math.Min(50, commitMessage.Length))}{(commitMessage.Length > 50 ? "..." : "")}",
            Metadata = new Dictionary<string, string>
            {
                ["commitHash"] = commitHash,
                ["commitMessage"] = commitMessage
            }
        };

        _activeSession.Activities.Add(activity);
    }

    /// <summary>
    /// Records a decision made during development.
    /// </summary>
    /// <param name="decision">Description of the decision.</param>
    /// <param name="rationale">Why this decision was made.</param>
    public void RecordDecision(string decision, string rationale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decision);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);

        if (_activeSession == null)
        {
            throw new InvalidOperationException("No active session. Call StartSession() first.");
        }

        var activity = new SessionActivity
        {
            Type = ActivityType.Decision,
            Timestamp = DateTime.UtcNow,
            Description = decision,
            Metadata = new Dictionary<string, string>
            {
                ["rationale"] = rationale
            }
        };

        _activeSession.Activities.Add(activity);
    }

    /// <summary>
    /// Records an issue encountered during development.
    /// </summary>
    /// <param name="issue">Description of the issue.</param>
    /// <param name="severity">Severity level (e.g., "error", "warning").</param>
    public void RecordIssue(string issue, string severity = "error")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issue);

        if (_activeSession == null)
        {
            throw new InvalidOperationException("No active session. Call StartSession() first.");
        }

        var activity = new SessionActivity
        {
            Type = ActivityType.IssueEncountered,
            Timestamp = DateTime.UtcNow,
            Description = issue,
            Metadata = new Dictionary<string, string>
            {
                ["severity"] = severity
            }
        };

        _activeSession.Activities.Add(activity);
    }

    /// <summary>
    /// Records a fix or solution for a previously encountered issue.
    /// </summary>
    /// <param name="fix">Description of the fix.</param>
    /// <param name="relatedIssue">Optional reference to the issue this fixes.</param>
    public void RecordFix(string fix, string? relatedIssue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fix);

        if (_activeSession == null)
        {
            throw new InvalidOperationException("No active session. Call StartSession() first.");
        }

        var metadata = new Dictionary<string, string>();
        if (relatedIssue != null)
        {
            metadata["relatedIssue"] = relatedIssue;
        }

        var activity = new SessionActivity
        {
            Type = ActivityType.IssueFix,
            Timestamp = DateTime.UtcNow,
            Description = fix,
            Metadata = metadata
        };

        _activeSession.Activities.Add(activity);
    }

    /// <summary>
    /// Ends the active session and saves it to disk.
    /// </summary>
    /// <param name="summary">Optional human-readable summary of the session.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The completed session.</returns>
    public async Task<SessionMemory> EndSessionAsync(string? summary = null, CancellationToken cancellationToken = default)
    {
        if (_activeSession == null)
        {
            throw new InvalidOperationException("No active session to end.");
        }

        var completedSession = _activeSession with
        {
            EndTime = DateTime.UtcNow,
            Summary = summary
        };

        await SaveSessionAsync(completedSession, cancellationToken);

        _activeSession = null;
        return completedSession;
    }

    /// <summary>
    /// Saves a session to disk.
    /// </summary>
    private async Task SaveSessionAsync(SessionMemory session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var filename = $"{session.StartTime:yyyyMMdd-HHmmss}-{session.SessionId.Substring(0, 8)}.json";
        var filePath = Path.Combine(_sessionsDirectory, filename);

        var json = JsonSerializer.Serialize(session, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    /// <summary>
    /// Loads the most recent session from disk.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The most recent session, or null if no sessions exist.</returns>
    public async Task<SessionMemory?> LoadLastSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_sessionsDirectory))
        {
            return null;
        }

        var sessionFiles = Directory.GetFiles(_sessionsDirectory, "*.json")
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .ToList();

        if (sessionFiles.Count == 0)
        {
            return null;
        }

        var latestFile = sessionFiles[0];

        try
        {
            var json = await File.ReadAllTextAsync(latestFile, cancellationToken);
            return JsonSerializer.Deserialize<SessionMemory>(json, _jsonOptions);
        }
        catch (JsonException)
        {
            // Corrupted file, skip
            return null;
        }
    }

    /// <summary>
    /// Lists all sessions ordered by start time (newest first).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all sessions.</returns>
    public async Task<List<SessionMemory>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessions = new List<SessionMemory>();

        if (!Directory.Exists(_sessionsDirectory))
        {
            return sessions;
        }

        var sessionFiles = Directory.GetFiles(_sessionsDirectory, "*.json");

        foreach (var filePath in sessionFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var session = JsonSerializer.Deserialize<SessionMemory>(json, _jsonOptions);

                if (session != null)
                {
                    sessions.Add(session);
                }
            }
            catch (JsonException)
            {
                // Skip corrupted files
                continue;
            }
        }

        return sessions.OrderByDescending(s => s.StartTime).ToList();
    }

    /// <summary>
    /// Searches sessions by keyword in descriptions, summaries, or tags.
    /// </summary>
    /// <param name="keyword">The keyword to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching sessions.</returns>
    public async Task<List<SessionMemory>> SearchSessionsAsync(string keyword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);

        var allSessions = await ListSessionsAsync(cancellationToken);
        var keywordLower = keyword.ToLowerInvariant();

        return allSessions
            .Where(s =>
                (s.Summary?.Contains(keywordLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                s.Tags.Any(t => t.Contains(keywordLower, StringComparison.OrdinalIgnoreCase)) ||
                s.Activities.Any(a => a.Description.Contains(keywordLower, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
