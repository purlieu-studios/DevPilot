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

            var argumentList = BuildArgumentList(systemPrompt, model, mcpConfigPath);

            // Resolve .cmd wrappers to underlying Node.js scripts
            // This fixes argument passing issues with Windows batch files
            var resolvedPath = ResolveCmdToNodeScript(_cliPath);
            var isNodeScript = resolvedPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = isNodeScript ? "node" : resolvedPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // If calling a .js file, add it as first argument to node
            if (isNodeScript)
            {
                processStartInfo.ArgumentList.Add(resolvedPath);
            }

            // Add all CLI arguments
            foreach (var arg in argumentList)
            {
                processStartInfo.ArgumentList.Add(arg);
            }

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

            // Write prompt to stdin (works correctly with node.exe + cli.js)
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
                finalOutput = ExtractFinalResultFromStreamJson(output, enableDiagnostics: false);
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
    /// Builds the command-line arguments for Claude CLI as a list.
    /// </summary>
    /// <param name="systemPrompt">The system prompt.</param>
    /// <param name="model">The model name.</param>
    /// <param name="mcpConfigPath">Optional MCP config file path.</param>
    /// <returns>The arguments list.</returns>
    private static List<string> BuildArgumentList(string systemPrompt, string model, string? mcpConfigPath)
    {
        var args = new List<string>
        {
            "--print",
            "--system-prompt",
            systemPrompt,
            "--model",
            model
        };

        // When using MCP, use stream-json format for tool interactions
        if (!string.IsNullOrWhiteSpace(mcpConfigPath))
        {
            args.Add("--output-format");
            args.Add("stream-json");
            args.Add("--verbose");
            args.Add("--mcp-config");
            args.Add(mcpConfigPath);
            args.Add("--permission-mode");
            args.Add("bypassPermissions");
        }
        else
        {
            args.Add("--output-format");
            args.Add("text");
        }

        // NOTE: Prompt is passed via stdin, not as argument
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

        // For .cmd files on Windows, use doubled quotes for escaping
        // See: https://learn.microsoft.com/en-us/archive/blogs/twistylittlepassagesallalike/everyone-quotes-command-line-arguments-the-wrong-way
        argument = argument.Replace("\"", "\"\"");

        // Wrap in quotes if contains spaces or special characters
        if (argument.Contains(' ') || argument.Contains('\t'))
        {
            return $"\"{argument}\"";
        }

        return argument;
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
    /// Resolves a .cmd wrapper to its underlying Node.js script.
    /// For npm-installed tools like claude.cmd, finds the actual cli.js file.
    /// </summary>
    /// <param name="cmdPath">Path to .cmd file.</param>
    /// <returns>Path to underlying cli.js, or original path if not resolvable.</returns>
    private static string ResolveCmdToNodeScript(string cmdPath)
    {
        if (!cmdPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            return cmdPath;
        }

        // For npm global packages, the structure is:
        // {npmDir}/claude.cmd
        // {npmDir}/node_modules/@anthropic-ai/claude-code/cli.js
        var cmdDir = Path.GetDirectoryName(cmdPath);
        if (string.IsNullOrEmpty(cmdDir))
        {
            return cmdPath;
        }

        var cliJsPath = Path.Combine(cmdDir, "node_modules", "@anthropic-ai", "claude-code", "cli.js");
        if (File.Exists(cliJsPath))
        {
            return cliJsPath;
        }

        // Fallback: return original path
        return cmdPath;
    }

    /// <summary>
    /// Extracts the final result from stream-json output when using MCP tools.
    /// </summary>
    /// <param name="streamJsonOutput">The full stream-json output from Claude CLI.</param>
    /// <param name="enableDiagnostics">Whether to print diagnostic information.</param>
    /// <returns>The final result JSON string from tool calls.</returns>
    private static string ExtractFinalResultFromStreamJson(string streamJsonOutput, bool enableDiagnostics = false)
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

                // Track tool calls (nested in message.content array)
                if (root.TryGetProperty("type", out var msgType) && msgType.GetString() == "assistant" &&
                    root.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.Array)
                {
                    foreach (var contentItem in content.EnumerateArray())
                    {
                        if (contentItem.TryGetProperty("type", out var contentType) &&
                            contentType.GetString() == "tool_use")
                        {
                            toolCallCount++;
                        }
                    }
                }

                // Look for tool results (nested in user message content array)
                if (root.TryGetProperty("type", out var userMsgType) && userMsgType.GetString() == "user" &&
                    root.TryGetProperty("message", out var userMessage) &&
                    userMessage.TryGetProperty("content", out var userContent) &&
                    userContent.ValueKind == JsonValueKind.Array)
                {
                    foreach (var contentItem in userContent.EnumerateArray())
                    {
                        if (contentItem.TryGetProperty("type", out var contentType) &&
                            contentType.GetString() == "tool_result" &&
                            contentItem.TryGetProperty("content", out var resultContent) &&
                            resultContent.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var resultItem in resultContent.EnumerateArray())
                            {
                                if (resultItem.TryGetProperty("type", out var resultType) &&
                                    resultType.GetString() == "text" &&
                                    resultItem.TryGetProperty("text", out var textContent))
                                {
                                    var textStr = textContent.GetString();
                                    if (!string.IsNullOrWhiteSpace(textStr))
                                    {
                                        // Parse the text content as JSON
                                        try
                                        {
                                            using var resultDoc = JsonDocument.Parse(textStr);
                                            // Check if this has a "plan" property (from finalize_plan)
                                            if (resultDoc.RootElement.TryGetProperty("plan", out var planElement))
                                            {
                                                finalResult = planElement.GetRawText();
                                                break;
                                            }
                                        }
                                        catch
                                        {
                                            // If not JSON, keep as fallback
                                            finalResult = textStr;
                                        }
                                    }
                                }
                            }
                            if (finalResult != null) break;
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

        // Diagnostic logging for MCP status
        if (enableDiagnostics)
        {
            System.Console.WriteLine("[ClaudeCliClient] MCP Diagnostics:");
            System.Console.WriteLine($"  Server Status: {mcpServerStatus}");
            System.Console.WriteLine($"  Tool Calls: {toolCallCount}");
            System.Console.WriteLine($"  Final Result: {(finalResult != null ? "Found" : "Not Found")}");
            System.Console.WriteLine();
        }

        return finalResult ?? "{}";
    }
}
