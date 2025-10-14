using System.Diagnostics;
using System.Text;

namespace DevPilot.Agents;

/// <summary>
/// Client for executing Claude CLI commands as subprocesses.
/// </summary>
public sealed class ClaudeCliClient
{
    private readonly string _cliPath;
    private readonly TimeSpan _defaultTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeCliClient"/> class.
    /// </summary>
    /// <param name="cliPath">Path to the Claude CLI executable (default: "claude").</param>
    /// <param name="defaultTimeout">Default timeout for CLI commands (default: 5 minutes).</param>
    public ClaudeCliClient(string cliPath = "claude", TimeSpan? defaultTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cliPath);

        _cliPath = cliPath;
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Executes a Claude CLI command asynchronously.
    /// </summary>
    /// <param name="prompt">The user prompt to send to Claude.</param>
    /// <param name="systemPrompt">The system prompt to configure Claude's behavior.</param>
    /// <param name="model">The model to use (e.g., "sonnet", "opus", or full model name).</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ClaudeCliResponse with the execution result.</returns>
    public async Task<ClaudeCliResponse> ExecuteAsync(
        string prompt,
        string systemPrompt,
        string model = "sonnet",
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var effectiveTimeout = timeout ?? _defaultTimeout;

        try
        {
            var arguments = BuildArguments(systemPrompt, model);
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _cliPath,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Write prompt to stdin
            await process.StandardInput.WriteLineAsync(prompt);
            await process.StandardInput.FlushAsync(cancellationToken);
            process.StandardInput.Close();

            // Wait for process to exit with timeout
            var completed = await WaitForExitAsync(process, effectiveTimeout, cancellationToken);

            if (!completed)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore kill errors
                }

                return ClaudeCliResponse.CreateFailure(
                    $"Claude CLI command timed out after {effectiveTimeout.TotalSeconds} seconds",
                    exitCode: -1);
            }

            var output = outputBuilder.ToString().Trim();
            var error = errorBuilder.ToString().Trim();

            if (process.ExitCode != 0)
            {
                var errorMessage = string.IsNullOrWhiteSpace(error)
                    ? $"Claude CLI exited with code {process.ExitCode}"
                    : $"Claude CLI error: {error}";

                return ClaudeCliResponse.CreateFailure(errorMessage, process.ExitCode);
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                return ClaudeCliResponse.CreateFailure(
                    "Claude CLI returned empty output",
                    exitCode: process.ExitCode);
            }

            return ClaudeCliResponse.CreateSuccess(output);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.Message.Contains("system cannot find the file"))
        {
            return ClaudeCliResponse.CreateFailure(
                $"Claude CLI not found at path: {_cliPath}. Ensure Claude is installed and in PATH.",
                exitCode: -1);
        }
        catch (OperationCanceledException)
        {
            return ClaudeCliResponse.CreateFailure(
                "Claude CLI execution was cancelled",
                exitCode: -1);
        }
        catch (Exception ex)
        {
            return ClaudeCliResponse.CreateFailure(
                $"Claude CLI execution failed: {ex.Message}",
                exitCode: -1);
        }
    }

    /// <summary>
    /// Builds the command-line arguments for Claude CLI.
    /// </summary>
    /// <param name="systemPrompt">The system prompt.</param>
    /// <param name="model">The model name.</param>
    /// <returns>The arguments string.</returns>
    private static string BuildArguments(string systemPrompt, string model)
    {
        // Use --print for non-interactive mode
        // Use --system-prompt to configure behavior
        // Use --model to select model
        // Use --output-format text for plain text output
        var escapedSystemPrompt = EscapeArgument(systemPrompt);
        var escapedModel = EscapeArgument(model);

        return $"--print --system-prompt {escapedSystemPrompt} --model {escapedModel} --output-format text";
    }

    /// <summary>
    /// Escapes a command-line argument for safe shell execution.
    /// </summary>
    /// <param name="argument">The argument to escape.</param>
    /// <returns>The escaped argument.</returns>
    private static string EscapeArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        // On Windows, wrap in quotes and escape internal quotes
        return $"\"{argument.Replace("\"", "\\\"")}\"";
    }

    /// <summary>
    /// Waits for a process to exit with timeout support.
    /// </summary>
    /// <param name="process">The process to wait for.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the process exited within the timeout; otherwise, false.</returns>
    private static async Task<bool> WaitForExitAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
