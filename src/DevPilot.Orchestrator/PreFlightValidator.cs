using System.Diagnostics;

namespace DevPilot.Orchestrator;

/// <summary>
/// Validates the environment before starting a DevPilot pipeline.
/// </summary>
/// <remarks>
/// Pre-flight validation catches common issues early, preventing mysterious failures
/// during pipeline execution. This includes checking for Claude CLI availability,
/// workspace permissions, project structure, and disk space.
/// </remarks>
public sealed class PreFlightValidator
{
    /// <summary>
    /// Represents the result of a pre-flight validation check.
    /// </summary>
    public sealed record ValidationResult(
        bool IsValid,
        string? ErrorMessage = null,
        string? WarningMessage = null,
        string? Suggestion = null);

    /// <summary>
    /// Validates the environment before creating a workspace.
    /// </summary>
    /// <param name="sourceDirectory">Target directory to analyze (usually current working directory).</param>
    /// <returns>Validation result with errors, warnings, and suggestions.</returns>
    public ValidationResult Validate(string sourceDirectory)
    {
        // Check 1: Claude CLI availability
        var claudeResult = ValidateClaudeCli();
        if (!claudeResult.IsValid)
        {
            return claudeResult;
        }

        // Check 2: Source directory validity
        var directoryResult = ValidateSourceDirectory(sourceDirectory);
        if (!directoryResult.IsValid)
        {
            return directoryResult;
        }

        // Check 3: Project structure
        var projectResult = ValidateProjectStructure(sourceDirectory);
        if (!projectResult.IsValid)
        {
            return projectResult;
        }

        // Check 4: Workspace permissions
        var permissionsResult = ValidateWorkspacePermissions(sourceDirectory);
        if (!permissionsResult.IsValid)
        {
            return permissionsResult;
        }

        // Check 5: Disk space
        var diskSpaceResult = ValidateDiskSpace(sourceDirectory);
        if (!diskSpaceResult.IsValid)
        {
            return diskSpaceResult;
        }

        // Check 6: CLAUDE.md presence (warning only)
        var claudeMdResult = ValidateClaudeMdPresence(sourceDirectory);
        if (claudeMdResult.WarningMessage != null)
        {
            return new ValidationResult(true, WarningMessage: claudeMdResult.WarningMessage);
        }

        return new ValidationResult(true);
    }

    private ValidationResult ValidateClaudeCli()
    {
        try
        {
            // Try to locate claude command
            var processInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "where" : "which",
                Arguments = "claude",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return new ValidationResult(
                    false,
                    "Failed to check for Claude CLI installation.",
                    Suggestion: "Ensure 'where' (Windows) or 'which' (Unix) command is available.");
            }

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                return new ValidationResult(
                    false,
                    "Claude CLI is not installed or not in PATH.",
                    Suggestion: "Install Claude CLI: npm install -g @anthropic-ai/claude-code\n" +
                               "Then authenticate: claude login");
            }

            return new ValidationResult(true);
        }
        catch (Exception ex)
        {
            return new ValidationResult(
                false,
                $"Failed to validate Claude CLI: {ex.Message}",
                Suggestion: "Install Claude CLI: npm install -g @anthropic-ai/claude-code");
        }
    }

    private ValidationResult ValidateSourceDirectory(string sourceDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return new ValidationResult(
                false,
                $"Source directory does not exist: {sourceDirectory}",
                Suggestion: "Ensure you're running DevPilot from a valid directory.");
        }

        // Check if running from DevPilot's own repository (common mistake)
        var devPilotSlnPath = Path.Combine(sourceDirectory, "DevPilot.sln");
        if (File.Exists(devPilotSlnPath))
        {
            // Check if this is actually the DevPilot repo itself
            var srcDevPilotCore = Path.Combine(sourceDirectory, "src", "DevPilot.Core");
            if (Directory.Exists(srcDevPilotCore))
            {
                return new ValidationResult(
                    false,
                    "DevPilot cannot run on itself from the main repository directory.",
                    Suggestion: "Navigate to a target project directory (e.g., examples/simple-calculator) or create a new project.");
            }
        }

        return new ValidationResult(true);
    }

    private ValidationResult ValidateProjectStructure(string sourceDirectory)
    {
        // Check for .sln or .csproj files
        var slnFiles = Directory.GetFiles(sourceDirectory, "*.sln", SearchOption.TopDirectoryOnly);
        var csprojFiles = Directory.GetFiles(sourceDirectory, "*.csproj", SearchOption.AllDirectories);

        if (slnFiles.Length == 0 && csprojFiles.Length == 0)
        {
            return new ValidationResult(
                false,
                "No .sln or .csproj files found in the source directory.",
                Suggestion: "DevPilot requires a C# project. Create one with:\n" +
                           "  dotnet new sln\n" +
                           "  dotnet new classlib -n MyProject\n" +
                           "  dotnet sln add MyProject/MyProject.csproj");
        }

        // Warn if multiple .sln files (potential for confusion)
        if (slnFiles.Length > 1)
        {
            return new ValidationResult(
                true,
                WarningMessage: $"Found {slnFiles.Length} solution files. DevPilot will use the first one: {Path.GetFileName(slnFiles[0])}");
        }

        return new ValidationResult(true);
    }

    private ValidationResult ValidateWorkspacePermissions(string sourceDirectory)
    {
        try
        {
            // Check if we can create .devpilot directory
            var devPilotDir = Path.Combine(sourceDirectory, ".devpilot");
            if (!Directory.Exists(devPilotDir))
            {
                Directory.CreateDirectory(devPilotDir);
            }

            // Try to create a test file to verify write permissions
            var testFile = Path.Combine(devPilotDir, $".permission-test-{Guid.NewGuid()}");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            return new ValidationResult(true);
        }
        catch (UnauthorizedAccessException)
        {
            return new ValidationResult(
                false,
                "Insufficient permissions to create workspace directory.",
                Suggestion: "Run DevPilot with appropriate permissions or choose a different directory.");
        }
        catch (IOException ex)
        {
            return new ValidationResult(
                false,
                $"Failed to validate workspace permissions: {ex.Message}",
                Suggestion: "Check disk permissions and available space.");
        }
    }

    private ValidationResult ValidateDiskSpace(string sourceDirectory)
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(sourceDirectory)!);
            const long minimumSpaceBytes = 100 * 1024 * 1024; // 100 MB

            if (driveInfo.AvailableFreeSpace < minimumSpaceBytes)
            {
                return new ValidationResult(
                    false,
                    $"Insufficient disk space: {driveInfo.AvailableFreeSpace / (1024 * 1024)} MB available, need at least 100 MB.",
                    Suggestion: "Free up disk space before running DevPilot.");
            }

            return new ValidationResult(true);
        }
        catch (Exception ex)
        {
            // Disk space check is not critical - log warning but proceed
            return new ValidationResult(
                true,
                WarningMessage: $"Could not check disk space: {ex.Message}");
        }
    }

    private ValidationResult ValidateClaudeMdPresence(string sourceDirectory)
    {
        var claudeMdPath = Path.Combine(sourceDirectory, "CLAUDE.md");
        if (!File.Exists(claudeMdPath))
        {
            return new ValidationResult(
                true,
                WarningMessage: "CLAUDE.md not found. DevPilot will use default agent prompts without project-specific context.\n" +
                               "Consider creating CLAUDE.md to improve code generation quality.");
        }

        return new ValidationResult(true);
    }
}
