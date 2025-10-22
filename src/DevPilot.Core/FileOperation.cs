namespace DevPilot.Core;

/// <summary>
/// Type of MCP file operation to perform.
/// </summary>
public enum MCPFileOperationType
{
    /// <summary>
    /// Create a brand new file.
    /// </summary>
    Create,

    /// <summary>
    /// Modify an existing file with line-based changes.
    /// </summary>
    Modify,

    /// <summary>
    /// Delete a file.
    /// </summary>
    Delete,

    /// <summary>
    /// Rename or move a file.
    /// </summary>
    Rename
}

/// <summary>
/// Represents a single MCP file operation from the Coder agent.
/// </summary>
public record MCPFileOperation
{
    /// <summary>
    /// Type of operation to perform.
    /// </summary>
    public required MCPFileOperationType Type { get; init; }

    /// <summary>
    /// Path to the file (for Create, Modify, Delete operations).
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Complete file content (for Create operations).
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// List of line-based changes (for Modify operations).
    /// </summary>
    public List<MCPLineChange>? Changes { get; init; }

    /// <summary>
    /// Reason for the operation (documentation).
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Old path (for Rename operations).
    /// </summary>
    public string? OldPath { get; init; }

    /// <summary>
    /// New path (for Rename operations).
    /// </summary>
    public string? NewPath { get; init; }
}

/// <summary>
/// Represents a change to a single line in a file for MCP operations.
/// </summary>
public record MCPLineChange
{
    /// <summary>
    /// Line number to modify (1-indexed).
    /// </summary>
    public required int LineNumber { get; init; }

    /// <summary>
    /// Expected current line content (optional, for validation).
    /// </summary>
    public string? OldContent { get; init; }

    /// <summary>
    /// New line content (empty string to delete line).
    /// </summary>
    public required string NewContent { get; init; }

    /// <summary>
    /// Number of lines to replace starting from LineNumber (default: 1).
    /// When modifying existing methods, set this to the number of lines in the old method
    /// to avoid creating duplicates.
    /// </summary>
    public int LinesToReplace { get; init; } = 1;
}

/// <summary>
/// Result of applying MCP file operations.
/// </summary>
public record MCPFileOperationResult
{
    /// <summary>
    /// Whether all operations succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of files that were modified.
    /// </summary>
    public List<string> FilesModified { get; init; } = new();
}
