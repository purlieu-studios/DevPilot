using System.Text.Json;
using DevPilot.Core;

namespace DevPilot.Orchestrator.State;

/// <summary>
/// Manages persistence of pipeline execution state for resume, approve, reject workflows.
/// </summary>
public sealed class StateManager
{
    private readonly string _stateDirectory;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="StateManager"/> class.
    /// </summary>
    /// <param name="workingDirectory">The working directory (defaults to current directory).</param>
    public StateManager(string? workingDirectory = null)
    {
        var root = workingDirectory ?? Directory.GetCurrentDirectory();
        _stateDirectory = Path.Combine(root, ".devpilot", "state");

        // Ensure state directory exists
        Directory.CreateDirectory(_stateDirectory);
    }

    /// <summary>
    /// Saves a pipeline state to disk.
    /// </summary>
    /// <param name="state">The pipeline state to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveStateAsync(PipelineState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var filePath = GetStateFilePath(state.PipelineId);
        var json = JsonSerializer.Serialize(state, _jsonOptions);

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    /// <summary>
    /// Loads a pipeline state by its ID.
    /// </summary>
    /// <param name="pipelineId">The pipeline ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded pipeline state, or null if not found.</returns>
    public async Task<PipelineState?> LoadStateAsync(string pipelineId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineId);

        var filePath = GetStateFilePath(pipelineId);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<PipelineState>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            // Corrupted state file - log and return null
            Console.Error.WriteLine($"Warning: Corrupted state file for pipeline {pipelineId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Lists all saved pipeline states.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of all pipeline states, ordered by timestamp descending (newest first).</returns>
    public async Task<List<PipelineState>> ListStatesAsync(CancellationToken cancellationToken = default)
    {
        var states = new List<PipelineState>();

        if (!Directory.Exists(_stateDirectory))
        {
            return states;
        }

        var stateFiles = Directory.GetFiles(_stateDirectory, "*.json");

        foreach (var filePath in stateFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var state = JsonSerializer.Deserialize<PipelineState>(json, _jsonOptions);

                if (state != null)
                {
                    states.Add(state);
                }
            }
            catch (JsonException ex)
            {
                // Skip corrupted files
                Console.Error.WriteLine($"Warning: Skipping corrupted state file {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        // Return newest first
        return states.OrderByDescending(s => s.Timestamp).ToList();
    }

    /// <summary>
    /// Deletes a pipeline state by its ID.
    /// </summary>
    /// <param name="pipelineId">The pipeline ID.</param>
    /// <returns>True if the state was deleted; false if it didn't exist.</returns>
    public bool DeleteState(string pipelineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineId);

        var filePath = GetStateFilePath(pipelineId);

        if (!File.Exists(filePath))
        {
            return false;
        }

        File.Delete(filePath);
        return true;
    }

    /// <summary>
    /// Deletes pipeline states older than the specified number of days.
    /// </summary>
    /// <param name="maxAgeDays">Maximum age in days (default: 7).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of states deleted.</returns>
    public async Task<int> CleanupOldStatesAsync(int maxAgeDays = 7, CancellationToken cancellationToken = default)
    {
        if (maxAgeDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAgeDays), "Max age cannot be negative");
        }

        var cutoffDate = DateTime.UtcNow.AddDays(-maxAgeDays);
        var states = await ListStatesAsync(cancellationToken);
        var oldStates = states.Where(s => s.Timestamp < cutoffDate).ToList();

        foreach (var state in oldStates)
        {
            DeleteState(state.PipelineId);
        }

        return oldStates.Count;
    }

    /// <summary>
    /// Updates the status of an existing pipeline state.
    /// </summary>
    /// <param name="pipelineId">The pipeline ID.</param>
    /// <param name="newStatus">The new status.</param>
    /// <param name="completedAt">Optional completion timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the state was updated; false if not found.</returns>
    public async Task<bool> UpdateStatusAsync(
        string pipelineId,
        PipelineStatus newStatus,
        DateTime? completedAt = null,
        CancellationToken cancellationToken = default)
    {
        var state = await LoadStateAsync(pipelineId, cancellationToken);

        if (state == null)
        {
            return false;
        }

        // Create updated state with new status
        var updatedState = state with
        {
            Status = newStatus,
            CompletedAt = completedAt ?? state.CompletedAt
        };

        await SaveStateAsync(updatedState, cancellationToken);
        return true;
    }

    /// <summary>
    /// Gets the file path for a pipeline state.
    /// </summary>
    /// <param name="pipelineId">The pipeline ID.</param>
    /// <returns>The absolute path to the state file.</returns>
    private string GetStateFilePath(string pipelineId)
    {
        return Path.Combine(_stateDirectory, $"{pipelineId}.json");
    }
}
