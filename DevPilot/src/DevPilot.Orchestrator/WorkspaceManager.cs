using System.Text;

namespace DevPilot.Orchestrator;

/// <summary>
/// Manages isolated workspaces for pipeline execution and applies unified diff patches to files.
/// </summary>
public sealed class WorkspaceManager : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly List<AppliedChange> _appliedChanges;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceManager"/> class.
    /// </summary>
    /// <param name="workspaceRoot">The root directory for this workspace.</param>
    private WorkspaceManager(string workspaceRoot)
    {
        _workspaceRoot = workspaceRoot;
        _appliedChanges = new List<AppliedChange>();
    }

    /// <summary>
    /// Gets the root directory of this workspace.
    /// </summary>
    public string WorkspaceRoot => _workspaceRoot;

    /// <summary>
    /// Gets the list of files that have been created or modified in this workspace.
    /// </summary>
    public IReadOnlyList<string> AppliedFiles => _appliedChanges
        .Where(c => c.Operation is FileOperation.Create or FileOperation.Modify)
        .Select(c => c.FilePath)
        .ToList();

    /// <summary>
    /// Creates a new workspace for a pipeline execution.
    /// </summary>
    /// <param name="pipelineId">The unique pipeline execution ID.</param>
    /// <param name="baseDirectory">The base directory for all workspaces (defaults to .devpilot/workspaces).</param>
    /// <returns>A new WorkspaceManager instance.</returns>
    /// <exception cref="ArgumentException">Thrown when pipelineId is null or whitespace.</exception>
    /// <exception cref="IOException">Thrown when workspace directory already exists or cannot be created.</exception>
    public static WorkspaceManager CreateWorkspace(string pipelineId, string? baseDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineId);

        baseDirectory ??= Path.Combine(".devpilot", "workspaces");
        var workspaceRoot = Path.Combine(baseDirectory, pipelineId);

        if (Directory.Exists(workspaceRoot))
        {
            throw new IOException($"Workspace directory already exists: {workspaceRoot}");
        }

        try
        {
            Directory.CreateDirectory(workspaceRoot);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to create workspace directory: {workspaceRoot}", ex);
        }

        return new WorkspaceManager(workspaceRoot);
    }

    /// <summary>
    /// Copies project infrastructure files (.csproj, .sln) from source to workspace.
    /// Automatically finds the solution root to ensure all referenced projects are copied.
    /// </summary>
    /// <param name="sourceRoot">The source directory containing project files.</param>
    /// <exception cref="ArgumentException">Thrown when sourceRoot is null or whitespace.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when source directory does not exist.</exception>
    public void CopyProjectFiles(string sourceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);

        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException($"Source directory does not exist: {sourceRoot}");
        }

        // Find solution root (directory containing .sln file)
        var solutionRoot = FindSolutionRoot(sourceRoot);
        if (solutionRoot == null)
        {
            // No solution file found, use sourceRoot as-is
            solutionRoot = sourceRoot;
        }

        // Find all .csproj and .sln files from solution root
        var projectFiles = Directory.GetFiles(solutionRoot, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(solutionRoot, "*.sln", SearchOption.TopDirectoryOnly))
            .ToList();

        // Also find common configuration files in project directories
        var configFiles = new List<string>();
        foreach (var projectFile in projectFiles.Where(f => f.EndsWith(".csproj")))
        {
            var projectDir = Path.GetDirectoryName(projectFile);
            if (projectDir != null)
            {
                // Common config file patterns
                var patterns = new[] { "*.json", "*.config", "*.settings", ".editorconfig" };
                foreach (var pattern in patterns)
                {
                    var files = Directory.GetFiles(projectDir, pattern, SearchOption.TopDirectoryOnly);
                    configFiles.AddRange(files);
                }
            }
        }

        var allFiles = projectFiles.Concat(configFiles).Distinct().ToList();

        foreach (var sourceFile in allFiles)
        {
            // Get relative path from solution root
            var relativePath = Path.GetRelativePath(solutionRoot, sourceFile);
            var destFile = Path.Combine(_workspaceRoot, relativePath);

            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(destFile);
            if (destDir != null && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Copy the file
            File.Copy(sourceFile, destFile, overwrite: true);
        }
    }

    /// <summary>
    /// Finds the solution root by walking up the directory tree looking for .sln files.
    /// </summary>
    /// <param name="startDirectory">The directory to start searching from.</param>
    /// <returns>The solution root directory, or null if no solution file is found.</returns>
    private static string? FindSolutionRoot(string startDirectory)
    {
        var currentDir = new DirectoryInfo(startDirectory);

        while (currentDir != null)
        {
            // Check if this directory contains a .sln file
            var slnFiles = currentDir.GetFiles("*.sln");
            if (slnFiles.Length > 0)
            {
                return currentDir.FullName;
            }

            // Move up to parent directory
            currentDir = currentDir.Parent;
        }

        return null;
    }

    /// <summary>
    /// Applies a unified diff patch to the workspace.
    /// </summary>
    /// <param name="patch">The unified diff patch content.</param>
    /// <returns>The result of applying the patch.</returns>
    /// <exception cref="ArgumentException">Thrown when patch is null or whitespace.</exception>
    /// <exception cref="PatchApplicationException">Thrown when patch application fails.</exception>
    public PatchApplicationResult ApplyPatch(string patch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patch);

        try
        {
            var parsedPatch = UnifiedDiffParser.Parse(patch);
            var results = new List<FileApplicationResult>();

            foreach (var filePatch in parsedPatch.FilePatches)
            {
                var fileResult = ApplyFilePatch(filePatch);
                results.Add(fileResult);

                if (!fileResult.Success)
                {
                    // Rollback all changes if any file fails
                    Rollback();
                    return PatchApplicationResult.CreateFailure(results, fileResult.ErrorMessage ?? "Unknown error");
                }
            }

            return PatchApplicationResult.CreateSuccess(results);
        }
        catch (Exception ex) when (ex is not PatchApplicationException)
        {
            Rollback();
            throw new PatchApplicationException($"Failed to apply patch: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Applies a single file patch to the workspace.
    /// </summary>
    /// <param name="filePatch">The file patch to apply.</param>
    /// <returns>The result of applying the file patch.</returns>
    private FileApplicationResult ApplyFilePatch(FilePatch filePatch)
    {
        var absolutePath = Path.Combine(_workspaceRoot, filePatch.FilePath);

        try
        {
            return filePatch.Operation switch
            {
                FileOperation.Create => ApplyCreateOperation(filePatch, absolutePath),
                FileOperation.Modify => ApplyModifyOperation(filePatch, absolutePath),
                FileOperation.Delete => ApplyDeleteOperation(filePatch, absolutePath),
                _ => FileApplicationResult.CreateFailure(filePatch.FilePath, $"Unknown operation: {filePatch.Operation}")
            };
        }
        catch (Exception ex)
        {
            return FileApplicationResult.CreateFailure(filePatch.FilePath, ex.Message);
        }
    }

    /// <summary>
    /// Applies a create file operation.
    /// </summary>
    private FileApplicationResult ApplyCreateOperation(FilePatch filePatch, string absolutePath)
    {
        if (File.Exists(absolutePath))
        {
            return FileApplicationResult.CreateFailure(filePatch.FilePath, "File already exists");
        }

        var content = ReconstructFileFromHunks(filePatch.Hunks, isNewFile: true);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(absolutePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(absolutePath, content);

        _appliedChanges.Add(new AppliedChange
        {
            FilePath = filePatch.FilePath,
            Operation = FileOperation.Create,
            BackupContent = null
        });

        return FileApplicationResult.CreateSuccess(filePatch.FilePath);
    }

    /// <summary>
    /// Applies a modify file operation.
    /// </summary>
    private FileApplicationResult ApplyModifyOperation(FilePatch filePatch, string absolutePath)
    {
        if (!File.Exists(absolutePath))
        {
            return FileApplicationResult.CreateFailure(filePatch.FilePath, "File does not exist");
        }

        var originalContent = File.ReadAllText(absolutePath);
        var modifiedContent = ApplyHunksToContent(originalContent, filePatch.Hunks);

        File.WriteAllText(absolutePath, modifiedContent);

        _appliedChanges.Add(new AppliedChange
        {
            FilePath = filePatch.FilePath,
            Operation = FileOperation.Modify,
            BackupContent = originalContent
        });

        return FileApplicationResult.CreateSuccess(filePatch.FilePath);
    }

    /// <summary>
    /// Applies a delete file operation.
    /// </summary>
    private FileApplicationResult ApplyDeleteOperation(FilePatch filePatch, string absolutePath)
    {
        if (!File.Exists(absolutePath))
        {
            return FileApplicationResult.CreateFailure(filePatch.FilePath, "File does not exist");
        }

        var originalContent = File.ReadAllText(absolutePath);
        File.Delete(absolutePath);

        _appliedChanges.Add(new AppliedChange
        {
            FilePath = filePatch.FilePath,
            Operation = FileOperation.Delete,
            BackupContent = originalContent
        });

        return FileApplicationResult.CreateSuccess(filePatch.FilePath);
    }

    /// <summary>
    /// Reconstructs a complete file from hunks (for new files).
    /// </summary>
    private static string ReconstructFileFromHunks(List<Hunk> hunks, bool isNewFile)
    {
        var lines = new List<string>();

        foreach (var hunk in hunks)
        {
            foreach (var line in hunk.Lines)
            {
                if (isNewFile)
                {
                    // For new files, only add the "+" lines
                    if (line.Type == DiffLineType.Add)
                    {
                        lines.Add(line.Content);
                    }
                }
                else
                {
                    // For existing files, this shouldn't be called
                    throw new InvalidOperationException("ReconstructFileFromHunks should only be called for new files");
                }
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Applies hunks to existing file content.
    /// </summary>
    private static string ApplyHunksToContent(string originalContent, List<Hunk> hunks)
    {
        var originalLines = originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var resultLines = new List<string>(originalLines);

        // Apply hunks in reverse order to maintain line number accuracy
        foreach (var hunk in hunks.OrderByDescending(h => h.OldStart))
        {
            ApplyHunk(resultLines, hunk);
        }

        return string.Join(Environment.NewLine, resultLines);
    }

    /// <summary>
    /// Applies a single hunk to a list of lines.
    /// </summary>
    private static void ApplyHunk(List<string> lines, Hunk hunk)
    {
        var currentLine = hunk.OldStart - 1; // Convert to 0-based index
        var diffLineIndex = 0;

        while (diffLineIndex < hunk.Lines.Count)
        {
            var diffLine = hunk.Lines[diffLineIndex];

            switch (diffLine.Type)
            {
                case DiffLineType.Context:
                    // Context line - verify it matches
                    if (currentLine < lines.Count && lines[currentLine] != diffLine.Content)
                    {
                        throw new PatchApplicationException(
                            $"Context mismatch at line {currentLine + 1}. Expected: '{diffLine.Content}', Found: '{lines[currentLine]}'");
                    }
                    currentLine++;
                    break;

                case DiffLineType.Remove:
                    // Remove line - verify it matches and remove
                    if (currentLine >= lines.Count || lines[currentLine] != diffLine.Content)
                    {
                        throw new PatchApplicationException(
                            $"Remove line mismatch at line {currentLine + 1}. Expected: '{diffLine.Content}', Found: '{(currentLine < lines.Count ? lines[currentLine] : "EOF")}'");
                    }
                    lines.RemoveAt(currentLine);
                    break;

                case DiffLineType.Add:
                    // Add line - insert at current position
                    lines.Insert(currentLine, diffLine.Content);
                    currentLine++;
                    break;
            }

            diffLineIndex++;
        }
    }

    /// <summary>
    /// Rolls back all applied changes to the workspace.
    /// </summary>
    public void Rollback()
    {
        // Apply changes in reverse order
        foreach (var change in Enumerable.Reverse(_appliedChanges))
        {
            var absolutePath = Path.Combine(_workspaceRoot, change.FilePath);

            try
            {
                switch (change.Operation)
                {
                    case FileOperation.Create:
                        // Remove created file
                        if (File.Exists(absolutePath))
                        {
                            File.Delete(absolutePath);
                        }
                        break;

                    case FileOperation.Modify:
                        // Restore original content
                        if (change.BackupContent != null)
                        {
                            File.WriteAllText(absolutePath, change.BackupContent);
                        }
                        break;

                    case FileOperation.Delete:
                        // Restore deleted file
                        if (change.BackupContent != null)
                        {
                            var directory = Path.GetDirectoryName(absolutePath);
                            if (directory != null && !Directory.Exists(directory))
                            {
                                Directory.CreateDirectory(directory);
                            }
                            File.WriteAllText(absolutePath, change.BackupContent);
                        }
                        break;
                }
            }
            catch
            {
                // Continue rolling back even if individual operations fail
            }
        }

        _appliedChanges.Clear();
    }

    /// <summary>
    /// Cleans up the workspace directory.
    /// </summary>
    public void Cleanup()
    {
        if (Directory.Exists(_workspaceRoot))
        {
            try
            {
                Directory.Delete(_workspaceRoot, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures
            }
        }

        _appliedChanges.Clear();
    }

    /// <summary>
    /// Disposes the workspace manager and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Cleanup();
        _disposed = true;
    }
}

/// <summary>
/// Represents a change that has been applied to the workspace.
/// </summary>
internal sealed class AppliedChange
{
    public required string FilePath { get; init; }
    public required FileOperation Operation { get; init; }
    public string? BackupContent { get; init; }
}

/// <summary>
/// Represents the result of applying a patch to the workspace.
/// </summary>
public sealed class PatchApplicationResult
{
    public required bool Success { get; init; }
    public required IReadOnlyList<FileApplicationResult> FileResults { get; init; }
    public string? ErrorMessage { get; init; }

    public static PatchApplicationResult CreateSuccess(List<FileApplicationResult> fileResults)
    {
        return new PatchApplicationResult
        {
            Success = true,
            FileResults = fileResults,
            ErrorMessage = null
        };
    }

    public static PatchApplicationResult CreateFailure(List<FileApplicationResult> fileResults, string errorMessage)
    {
        return new PatchApplicationResult
        {
            Success = false,
            FileResults = fileResults,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Represents the result of applying a single file patch.
/// </summary>
public sealed class FileApplicationResult
{
    public required string FilePath { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static FileApplicationResult CreateSuccess(string filePath)
    {
        return new FileApplicationResult
        {
            FilePath = filePath,
            Success = true,
            ErrorMessage = null
        };
    }

    public static FileApplicationResult CreateFailure(string filePath, string errorMessage)
    {
        return new FileApplicationResult
        {
            FilePath = filePath,
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Exception thrown when patch application fails.
/// </summary>
public sealed class PatchApplicationException : Exception
{
    public PatchApplicationException(string message) : base(message)
    {
    }

    public PatchApplicationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
