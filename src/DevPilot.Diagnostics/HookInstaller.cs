using System.Text;

namespace DevPilot.Diagnostics;

/// <summary>
/// Installs Git hooks for DevPilot quality checks.
/// </summary>
public sealed class HookInstaller
{
    /// <summary>
    /// Installs a pre-commit hook that runs DevPilot diagnostics.
    /// </summary>
    /// <param name="workingDirectory">The git repository root directory.</param>
    /// <param name="hookType">The type of hook to install (default: "pre-commit").</param>
    /// <param name="force">If true, overwrites existing hook.</param>
    public static InstallResult Install(
        string? workingDirectory = null,
        string hookType = "pre-commit",
        bool force = false)
    {
        workingDirectory ??= Directory.GetCurrentDirectory();

        var gitDir = Path.Combine(workingDirectory, ".git");
        if (!Directory.Exists(gitDir))
        {
            return new InstallResult(
                false,
                $"Not a git repository: {workingDirectory}");
        }

        var hooksDir = Path.Combine(gitDir, "hooks");
        if (!Directory.Exists(hooksDir))
        {
            Directory.CreateDirectory(hooksDir);
        }

        var hookPath = Path.Combine(hooksDir, hookType);

        if (File.Exists(hookPath) && !force)
        {
            return new InstallResult(
                false,
                $"Hook already exists: {hookPath}\nUse --force to overwrite");
        }

        try
        {
            var hookContent = GeneratePreCommitHook();
            File.WriteAllText(hookPath, hookContent);

            // Make executable on Unix systems
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(hookPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            return new InstallResult(
                true,
                $"Successfully installed {hookType} hook at {hookPath}");
        }
#pragma warning disable CA1031 // Do not catch general exception types - hook installation should be resilient
        catch (Exception ex)
        {
            return new InstallResult(
                false,
                $"Failed to install hook: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Uninstalls a Git hook.
    /// </summary>
    /// <param name="workingDirectory">The git repository root directory.</param>
    /// <param name="hookType">The type of hook to uninstall.</param>
    public static InstallResult Uninstall(
        string? workingDirectory = null,
        string hookType = "pre-commit")
    {
        workingDirectory ??= Directory.GetCurrentDirectory();

        var gitDir = Path.Combine(workingDirectory, ".git");
        if (!Directory.Exists(gitDir))
        {
            return new InstallResult(
                false,
                $"Not a git repository: {workingDirectory}");
        }

        var hookPath = Path.Combine(gitDir, "hooks", hookType);

        if (!File.Exists(hookPath))
        {
            return new InstallResult(
                false,
                $"Hook does not exist: {hookPath}");
        }

        try
        {
            File.Delete(hookPath);
            return new InstallResult(
                true,
                $"Successfully removed {hookType} hook");
        }
#pragma warning disable CA1031 // Do not catch general exception types - hook installation should be resilient
        catch (Exception ex)
        {
            return new InstallResult(
                false,
                $"Failed to remove hook: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    private static string GeneratePreCommitHook()
    {
        var script = new StringBuilder();

        if (OperatingSystem.IsWindows())
        {
            // Windows batch script
            script.AppendLine("@echo off");
            script.AppendLine("REM DevPilot Pre-Commit Hook");
            script.AppendLine("REM Runs diagnostic tests before allowing commit");
            script.AppendLine();
            script.AppendLine("echo Running DevPilot pre-commit diagnostics...");
            script.AppendLine();
            script.AppendLine("REM Run test diagnostics");
            script.AppendLine("dotnet run --project src/DevPilot.Console/DevPilot.Console.csproj --no-build diagnose tests");
            script.AppendLine();
            script.AppendLine("if %ERRORLEVEL% NEQ 0 (");
            script.AppendLine("    echo.");
            script.AppendLine("    echo ❌ Pre-commit checks failed!");
            script.AppendLine("    echo Fix the issues above or use 'git commit --no-verify' to skip checks.");
            script.AppendLine("    exit /b 1");
            script.AppendLine(")");
            script.AppendLine();
            script.AppendLine("echo ✅ Pre-commit checks passed!");
            script.AppendLine("exit /b 0");
        }
        else
        {
            // Unix shell script
            script.AppendLine("#!/bin/sh");
            script.AppendLine("# DevPilot Pre-Commit Hook");
            script.AppendLine("# Runs diagnostic tests before allowing commit");
            script.AppendLine();
            script.AppendLine("echo 'Running DevPilot pre-commit diagnostics...'");
            script.AppendLine();
            script.AppendLine("# Run test diagnostics");
            script.AppendLine("dotnet run --project src/DevPilot.Console/DevPilot.Console.csproj --no-build diagnose tests");
            script.AppendLine();
            script.AppendLine("if [ $? -ne 0 ]; then");
            script.AppendLine("    echo");
            script.AppendLine("    echo '❌ Pre-commit checks failed!'");
            script.AppendLine("    echo 'Fix the issues above or use \"git commit --no-verify\" to skip checks.'");
            script.AppendLine("    exit 1");
            script.AppendLine("fi");
            script.AppendLine();
            script.AppendLine("echo '✅ Pre-commit checks passed!'");
            script.AppendLine("exit 0");
        }

        return script.ToString();
    }
}

/// <summary>
/// Result of a hook installation operation.
/// </summary>
public sealed record InstallResult(
    bool Success,
    string Message);
