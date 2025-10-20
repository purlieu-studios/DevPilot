using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DevPilot.Diagnostics;

/// <summary>
/// Analyzes workspace health - identifies locked files and orphaned workspaces.
/// </summary>
public sealed class WorkspaceDiagnostics
{
    /// <summary>
    /// Runs workspace diagnostics on the .devpilot/workspaces directory.
    /// </summary>
    public static async Task<DiagnosticResult> RunAsync(string? workingDirectory = null)
    {
        var startTime = DateTime.UtcNow;
        workingDirectory ??= Directory.GetCurrentDirectory();

        var workspacesDir = Path.Combine(workingDirectory, ".devpilot", "workspaces");

        var categories = new List<DiagnosticCategory>();

        if (!Directory.Exists(workspacesDir))
        {
            return new DiagnosticResult(
                "Workspace Diagnostics",
                new List<DiagnosticCategory>
                {
                    new DiagnosticCategory(
                        "No Workspaces",
                        "No .devpilot/workspaces directory found",
                        DiagnosticSeverity.Info,
                        new List<DiagnosticIssue>
                        {
                            new DiagnosticIssue("No workspace directory exists - nothing to diagnose")
                        })
                },
                DateTime.UtcNow - startTime);
        }

        // Find all workspace directories
        var workspaces = Directory.GetDirectories(workspacesDir);

        if (workspaces.Length == 0)
        {
            return new DiagnosticResult(
                "Workspace Diagnostics",
                new List<DiagnosticCategory>
                {
                    new DiagnosticCategory(
                        "No Workspaces",
                        "Workspace directory is empty",
                        DiagnosticSeverity.Info,
                        new List<DiagnosticIssue>
                        {
                            new DiagnosticIssue("No workspaces found - directory is clean")
                        })
                },
                DateTime.UtcNow - startTime);
        }

        // Check for locked directories
        var lockedWorkspaces = new List<DiagnosticIssue>();
        var orphanedWorkspaces = new List<DiagnosticIssue>();

        foreach (var workspace in workspaces)
        {
            var workspaceId = Path.GetFileName(workspace);
            var createdTime = Directory.GetCreationTime(workspace);
            var age = DateTime.Now - createdTime;

            // Try to delete the workspace to check if it's locked
            var isLocked = await IsDirectoryLockedAsync(workspace);

            if (isLocked)
            {
                var lockingProcesses = await GetLockingProcessesAsync(workspace);
                lockedWorkspaces.Add(new DiagnosticIssue(
                    $"Workspace {workspaceId} is locked",
                    workspace)
                {
                    Context = lockingProcesses.Count > 0
                        ? "Locked by: " + string.Join(", ", lockingProcesses.Select(p => $"{p.ProcessName} (PID {p.Pid})"))
                        : "Locked by unknown process"
                });
            }
            else if (age.TotalHours > 24)
            {
                // Consider workspaces older than 24 hours as orphaned
                orphanedWorkspaces.Add(new DiagnosticIssue(
                    $"Workspace {workspaceId} (age: {age.TotalDays:F1} days)",
                    workspace)
                {
                    Context = $"Created: {createdTime:yyyy-MM-dd HH:mm:ss}"
                });
            }
        }

        if (lockedWorkspaces.Count > 0)
        {
            categories.Add(new DiagnosticCategory(
                "Locked Workspaces",
                $"{lockedWorkspaces.Count} workspace(s) locked by running processes",
                DiagnosticSeverity.Error,
                lockedWorkspaces)
            {
                SuggestedFix = "Run 'devpilot cleanup --force' to forcefully clean locked workspaces, or manually terminate the processes listed above"
            });
        }

        if (orphanedWorkspaces.Count > 0)
        {
            categories.Add(new DiagnosticCategory(
                "Orphaned Workspaces",
                $"{orphanedWorkspaces.Count} workspace(s) older than 24 hours",
                DiagnosticSeverity.Warning,
                orphanedWorkspaces)
            {
                SuggestedFix = $"Run 'devpilot cleanup' to remove old workspaces (will free ~{GetDirectorySize(workspacesDir) / (1024 * 1024)} MB)"
            });
        }

        if (categories.Count == 0)
        {
            categories.Add(new DiagnosticCategory(
                "Healthy Workspaces",
                $"All {workspaces.Length} workspace(s) are accessible and recent",
                DiagnosticSeverity.Info,
                new List<DiagnosticIssue>
                {
                    new DiagnosticIssue("No workspace issues detected")
                }));
        }

        return new DiagnosticResult("Workspace Diagnostics", categories, DateTime.UtcNow - startTime);
    }

    private static async Task<bool> IsDirectoryLockedAsync(string directoryPath)
    {
        try
        {
            // Try to create a temp file in the directory to test write access
            var testFile = Path.Combine(directoryPath, ".devpilot_lock_test");
            await File.WriteAllTextAsync(testFile, "test");
            File.Delete(testFile);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static async Task<List<(string ProcessName, int Pid)>> GetLockingProcessesAsync(string directoryPath)
    {
        var processes = new List<(string ProcessName, int Pid)>();

        if (OperatingSystem.IsWindows())
        {
            // Use handle.exe if available (Sysinternals tool)
            // Fall back to manual detection if not available
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "handle.exe",
                        Arguments = $"\"{directoryPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                // Parse handle.exe output
                // Example: "MSBuild.exe  pid: 12345  type: File  C:\path\to\file"
                var matches = Regex.Matches(output, @"(\w+\.exe)\s+pid:\s+(\d+)");
                foreach (Match match in matches)
                {
                    processes.Add((match.Groups[1].Value, int.Parse(match.Groups[2].Value)));
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types - diagnostics should be resilient
            catch
            {
                // handle.exe not available, try alternative approach
                processes.AddRange(await GetWindowsLockingProcessesAlternative(directoryPath));
            }
#pragma warning restore CA1031
        }
        else
        {
            // Use lsof on Unix-like systems
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "lsof",
                        Arguments = $"\"{directoryPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                // Parse lsof output
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1); // Skip header
                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        processes.Add((parts[0], int.Parse(parts[1])));
                    }
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types - diagnostics should be resilient
            catch
            {
                // lsof not available or failed
            }
#pragma warning restore CA1031
        }

        return processes.DistinctBy(p => p.Pid).ToList();
    }

    private static async Task<List<(string ProcessName, int Pid)>> GetWindowsLockingProcessesAlternative(string directoryPath)
    {
        var processes = new List<(string ProcessName, int Pid)>();

        // Check common processes that lock build directories
        var suspectProcessNames = new[] { "MSBuild", "dotnet", "VBCSCompiler", "testhost" };

        foreach (var procName in suspectProcessNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(procName);
                foreach (var proc in procs)
                {
                    processes.Add((proc.ProcessName, proc.Id));
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types - diagnostics should be resilient
            catch
            {
                // Process access denied or not found
            }
#pragma warning restore CA1031
        }

        return await Task.FromResult(processes);
    }

    private static long GetDirectorySize(string directoryPath)
    {
        try
        {
            return new DirectoryInfo(directoryPath)
                .GetFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        }
#pragma warning disable CA1031 // Do not catch general exception types - diagnostics should be resilient
        catch
        {
            return 0;
        }
#pragma warning restore CA1031
    }
}
