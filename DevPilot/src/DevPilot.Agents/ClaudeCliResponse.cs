namespace DevPilot.Agents;

/// <summary>
/// Represents the result of a Claude CLI subprocess execution.
/// </summary>
public sealed class ClaudeCliResponse
{
    /// <summary>
    /// Gets whether the CLI command succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the response text from Claude (stdout).
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Gets the error message if the command failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the process exit code.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    /// <param name="output">The output text from Claude.</param>
    /// <returns>A success response.</returns>
    public static ClaudeCliResponse CreateSuccess(string output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(output);

        return new ClaudeCliResponse
        {
            Success = true,
            Output = output,
            ExitCode = 0
        };
    }

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="exitCode">The process exit code.</param>
    /// <returns>A failure response.</returns>
    public static ClaudeCliResponse CreateFailure(string errorMessage, int exitCode = -1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new ClaudeCliResponse
        {
            Success = false,
            ErrorMessage = errorMessage,
            ExitCode = exitCode
        };
    }
}
