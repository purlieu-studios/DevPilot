using System.Diagnostics;
using System.Text;
using System.Text.Json;

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

        // If using default "claude", try to find it in PATH
        if (cliPath == "claude")
        {
            var foundPath = FindExecutableInPath("claude");
            _cliPath = foundPath ?? cliPath;
        }
        else
        {
            _cliPath = cliPath;
        }

        _defaultTimeout = defaultTimeout ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Executes a Claude CLI command asynchronously.
    /// </summary>
    /// <param name="prompt">The user prompt to send to Claude.</param>
    /// <param name="systemPrompt">The system prompt to configure Claude's behavior.</param>
    /// <param name="model">The model to use (e.g., "sonnet", "opus", or full model name).</param>
    /// <param name="mcpConfigPath">Optional MCP config file path for tool usage.</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ClaudeCliResponse with the execution result.</returns>
    public async Task<ClaudeCliResponse> ExecuteAsync(
        string prompt,
        string systemPrompt,
        string model = "sonnet",
        string? mcpConfigPath = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var effectiveTimeout = timeout ?? _defaultTimeout;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var arguments = BuildArguments(systemPrompt, model, mcpConfigPath);

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

            // Copy current environment variables to subprocess to ensure PATH is inherited
            foreach (System.Collections.DictionaryEntry env in Environment.GetEnvironmentVariables())
            {
                var key = env.Key?.ToString();
                var value = env.Value?.ToString();
                if (!string.IsNullOrEmpty(key) && value != null)
                {
                    processStartInfo.EnvironmentVariables[key] = value;
                }
            }

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

            // If MCP was used (stream-json format), extract the final result
            string finalOutput = output;
            if (!string.IsNullOrWhiteSpace(mcpConfigPath))
            {
                finalOutput = ExtractFinalResultFromStreamJson(output);
            }

            return ClaudeCliResponse.CreateSuccess(finalOutput);
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
    /// <param name="mcpConfigPath">Optional MCP config file path.</param>
    /// <returns>The arguments string.</returns>
    private static string BuildArguments(string systemPrompt, string model, string? mcpConfigPath)
    {
        // Use --print for non-interactive mode
        // Use --system-prompt to configure behavior
        // Use --model to select model
        // Use appropriate output format based on MCP usage
        var escapedSystemPrompt = EscapeArgument(systemPrompt);
        var escapedModel = EscapeArgument(model);

        // When using MCP, use stream-json format for tool interactions
        // Otherwise, use text format for regular conversation
        var outputFormat = !string.IsNullOrWhiteSpace(mcpConfigPath) ? "stream-json" : "text";

        var args = $"--print --system-prompt {escapedSystemPrompt} --model {escapedModel} --output-format {outputFormat}";

        // Add MCP config if specified
        if (!string.IsNullOrWhiteSpace(mcpConfigPath))
        {
            // When using stream-json with --print, --verbose is required
            args += " --verbose";
            var escapedMcpConfigPath = EscapeArgument(mcpConfigPath);
            args += $" --mcp-config {escapedMcpConfigPath}";
            // Add permission bypass for MCP tools
            args += " --permission-mode bypassPermissions";
        }

        return args;
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

    /// <summary>
    /// Finds an executable in the system PATH.
    /// </summary>
    /// <param name="fileName">The executable name to find (e.g., "claude").</param>
    /// <returns>The full path to the executable, or null if not found.</returns>
    private static string? FindExecutableInPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return null;
        }

        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var extensions = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? new[] { ".exe", ".cmd", ".bat", "" }
            : new[] { "" };

        foreach (var basePath in paths)
        {
            foreach (var ext in extensions)
            {
                var fullPath = Path.Combine(basePath, fileName + ext);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts the final result from stream-json output when using MCP tools.
    /// </summary>
    /// <param name="streamJsonOutput">The full stream-json output from Claude CLI.</param>
    /// <returns>The final result JSON string from tool calls.</returns>
    private static string ExtractFinalResultFromStreamJson(string streamJsonOutput)
    {
        if (string.IsNullOrWhiteSpace(streamJsonOutput))
        {
            return "{}";
        }

        // Stream-json format contains one JSON object per line
        // Look for tool call results, especially the finalize_plan result
        var lines = streamJsonOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string? finalResult = null;
        var toolCallCount = 0;
        var mcpServerStatus = "unknown";

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                // Check for MCP server status in init message
                if (root.TryGetProperty("type", out var type) && type.GetString() == "system" &&
                    root.TryGetProperty("mcp_servers", out var mcpServers))
                {
                    foreach (var server in mcpServers.EnumerateArray())
                    {
                        if (server.TryGetProperty("name", out var name) && name.GetString() == "planning-tools")
                        {
                            if (server.TryGetProperty("status", out var status))
                            {
                                mcpServerStatus = status.GetString() ?? "unknown";
                            }
                        }
                    }
                }

                // Track tool calls being made
                if (root.TryGetProperty("tool_name", out var toolNameProp))
                {
                    toolCallCount++;
                }

                // Look for tool results
                if (root.TryGetProperty("tool_result", out var toolResult))
                {
                    if (toolResult.TryGetProperty("result", out var result))
                    {
                        // Check if this is from finalize_plan tool (with MCP prefix)
                        if (root.TryGetProperty("tool_name", out var toolName) &&
                            toolName.GetString() == "mcp__planning-tools__finalize_plan")
                        {
                            // The result is nested JSON with { "success": true, "plan": {...} }
                            // We need to extract the "plan" property
                            try
                            {
                                var resultStr = result.GetString();
                                if (!string.IsNullOrWhiteSpace(resultStr))
                                {
                                    using var resultDoc = JsonDocument.Parse(resultStr);
                                    if (resultDoc.RootElement.TryGetProperty("plan", out var planElement))
                                    {
                                        finalResult = planElement.GetRawText();
                                        break;
                                    }
                                }
                            }
                            catch
                            {
                                // If parsing fails, use the raw result
                                finalResult = result.GetRawText();
                                break;
                            }
                        }
                        // Keep the last tool result as fallback
                        finalResult = result.GetRawText();
                    }
                }
                // Also check for content blocks that might contain the result
                else if (root.TryGetProperty("content", out var content))
                {
                    if (content.ValueKind == JsonValueKind.String)
                    {
                        var contentStr = content.GetString();
                        if (!string.IsNullOrWhiteSpace(contentStr) &&
                            (contentStr.StartsWith('{') || contentStr.StartsWith('[')))
                        {
                            finalResult = contentStr;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Skip malformed JSON lines
                continue;
            }
        }

        return finalResult ?? "{}";
    }
}
