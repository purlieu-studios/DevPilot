using System.Diagnostics;

namespace DevPilot.Diagnostics;

/// <summary>
/// Cleans up orphaned and locked workspaces.
/// </summary>
public sealed class WorkspaceCleanup
{
    /// <summary>
    /// Runs workspace cleanup.
    /// </summary>
    /// <param name="workingDirectory">The working directory containing .devpilot/workspaces.</param>
    /// <param name="force">If true, terminates locking processes before cleanup.</param>
    /// <param name="dryRun">If true, only shows what would be deleted without actually deleting.</param>
    /// <param name="ageThresholdHours">Minimum age in hours for a workspace to be considered orphaned (default: 24).</param>
    public static async Task<CleanupResult> RunAsync(
        string? workingDirectory = null,
        bool force = false,
        bool dryRun = false,
        int ageThresholdHours = 24)
    {
        var startTime = DateTime.UtcNow;
        workingDirectory ??= Directory.GetCurrentDirectory();

        var workspacesDir = Path.Combine(workingDirectory, ".devpilot", "workspaces");

        if (!Directory.Exists(workspacesDir))
        {
            return new CleanupResult(
                WorkspacesDeleted: 0,
                ProcessesTerminated: 0,
                BytesFreed: 0,
                DryRun: dryRun,
                Duration: DateTime.UtcNow - startTime,
                Message: "No .devpilot/workspaces directory found");
        }

        var workspaces = Directory.GetDirectories(workspacesDir);

        if (workspaces.Length == 0)
        {
            return new CleanupResult(
                WorkspacesDeleted: 0,
                ProcessesTerminated: 0,
                BytesFreed: 0,
                DryRun: dryRun,
                Duration: DateTime.UtcNow - startTime,
                Message: "No workspaces found - directory is already clean");
        }

        var deletedCount = 0;
        var terminatedCount = 0;
        long bytesFreed = 0;
        var errors = new List<string>();

        foreach (var workspace in workspaces)
        {
            var workspaceId = Path.GetFileName(workspace);
            var createdTime = Directory.GetCreationTime(workspace);
            var age = DateTime.Now - createdTime;

            // Skip workspaces younger than threshold
            if (age.TotalHours < ageThresholdHours)
            {
                continue;
            }

            try
            {
                // Calculate size before deletion
                var size = GetDirectorySize(workspace);

                if (dryRun)
                {
                    bytesFreed += size;
                    deletedCount++;
                    continue;
                }

                // Check if workspace is locked
                var isLocked = await IsDirectoryLockedAsync(workspace);

                if (isLocked)
                {
                    if (force)
                    {
                        // Terminate locking processes
                        var terminated = await TerminateLockingProcessesAsync(workspace);
                        terminatedCount += terminated;

                        // Wait a moment for processes to release locks
                        await Task.Delay(500);
                    }
                    else
                    {
                        errors.Add($"Workspace {workspaceId} is locked - use --force to terminate processes");
                        continue;
                    }
                }

                // Delete the workspace
                Directory.Delete(workspace, recursive: true);
                bytesFreed += size;
                deletedCount++;
            }
#pragma warning disable CA1031 // Do not catch general exception types - cleanup should be resilient
            catch (Exception ex)
            {
                errors.Add($"Failed to delete workspace {workspaceId}: {ex.Message}");
            }
#pragma warning restore CA1031
        }

        var message = dryRun
            ? $"Would delete {deletedCount} workspace(s) and free ~{bytesFreed / (1024 * 1024)} MB"
            : $"Deleted {deletedCount} workspace(s) and freed ~{bytesFreed / (1024 * 1024)} MB";

        if (terminatedCount > 0)
        {
            message += $" (terminated {terminatedCount} process(es))";
        }

        if (errors.Count > 0)
        {
            message += $"\nErrors: {string.Join(", ", errors)}";
        }

        return new CleanupResult(
            WorkspacesDeleted: deletedCount,
            ProcessesTerminated: terminatedCount,
            BytesFreed: bytesFreed,
            DryRun: dryRun,
            Duration: DateTime.UtcNow - startTime,
            Message: message,
            Errors: errors);
    }

    private static async Task<bool> IsDirectoryLockedAsync(string directoryPath)
    {
        try
        {
            // Try to create a temp file in the directory to test write access
            var testFile = Path.Combine(directoryPath, ".devpilot_cleanup_test");
            await File.WriteAllTextAsync(testFile, "test");
            File.Delete(testFile);
            return false;
        }
#pragma warning disable CA1031 // Do not catch general exception types - cleanup should be resilient
        catch
        {
            return true;
        }
#pragma warning restore CA1031
    }

    private static async Task<int> TerminateLockingProcessesAsync(string directoryPath)
    {
        var terminatedCount = 0;

        if (OperatingSystem.IsWindows())
        {
            // Try to find processes locking this directory
            var processNames = new[] { "MSBuild", "dotnet", "VBCSCompiler", "testhost" };

            foreach (var procName in processNames)
            {
                try
                {
                    var procs = Process.GetProcessesByName(procName);
                    foreach (var proc in procs)
                    {
                        try
                        {
                            proc.Kill();
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            await proc.WaitForExitAsync(cts.Token);
                            terminatedCount++;
                        }
#pragma warning disable CA1031 // Do not catch general exception types - cleanup should be resilient
                        catch
                        {
                            // Process already exited or access denied
                        }
#pragma warning restore CA1031
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types - cleanup should be resilient
                catch
                {
                    // Process not found or access denied
                }
#pragma warning restore CA1031
            }
        }

        return terminatedCount;
    }

    private static long GetDirectorySize(string directoryPath)
    {
        try
        {
            return new DirectoryInfo(directoryPath)
                .GetFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        }
#pragma warning disable CA1031 // Do not catch general exception types - cleanup should be resilient
        catch
        {
            return 0;
        }
#pragma warning restore CA1031
    }
}

/// <summary>
/// Result of a workspace cleanup operation.
/// </summary>
public sealed record CleanupResult(
    int WorkspacesDeleted,
    int ProcessesTerminated,
    long BytesFreed,
    bool DryRun,
    TimeSpan Duration,
    string Message,
    List<string>? Errors = null);
