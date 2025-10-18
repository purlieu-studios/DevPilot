using System.Text;
using System.Text.Json;
using DevPilot.Core;

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

        // Find all .csproj files, excluding build artifacts and workspaces DURING traversal (not after)
        // This is much faster than Directory.GetFiles with SearchOption.AllDirectories
        var excludedDirectories = new[] { ".devpilot", "bin", "obj", ".git", ".vs", "node_modules", "packages", "TestResults", "nupkg" };
        var projectFiles = FindFilesRecursive(solutionRoot, "*.csproj", excludedDirectories)
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
    /// Loads the DevPilot configuration from devpilot.json in the source repository.
    /// Returns default configuration if the file does not exist.
    /// </summary>
    /// <param name="sourceRoot">The source directory to search for devpilot.json.</param>
    /// <returns>The loaded configuration, or default configuration if not found.</returns>
    private static DevPilotConfig LoadConfig(string sourceRoot)
    {
        var configPath = Path.Combine(sourceRoot, "devpilot.json");

        if (!File.Exists(configPath))
        {
            return DevPilotConfig.Default;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<DevPilotConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return config ?? DevPilotConfig.Default;
        }
        catch (JsonException)
        {
            // Invalid JSON, return default config
            return DevPilotConfig.Default;
        }
    }

    /// <summary>
    /// Copies domain-specific files from the target repository to the workspace.
    /// This includes:
    /// - Individual files: .editorconfig (NOTE: CLAUDE.md is copied separately via CopyClaudeMd() after Planning)
    /// - Default directories: .agents/, docs/, src/, tests/, experiments/
    /// - Auto-detected project directories: Any directory containing .csproj files (except bin, obj, .git, etc.)
    /// - Configured folders: Additional folders specified in devpilot.json
    /// </summary>
    /// <param name="sourceRoot">The source directory containing the target repository.</param>
    /// <exception cref="ArgumentException">Thrown when sourceRoot is null or whitespace.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when source directory does not exist.</exception>
    public void CopyDomainFiles(string sourceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);

        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException($"Source directory does not exist: {sourceRoot}");
        }

        // Load configuration
        var config = LoadConfig(sourceRoot);

        // If CopyAllFiles is true, copy entire repository
        if (config.CopyAllFiles == true)
        {
            CopyDirectoryRecursive(sourceRoot, _workspaceRoot);
            return;
        }

        // Copy individual files (NOTE: CLAUDE.md is copied separately after Planning to reduce context)
        var filesToCopy = new[] { ".editorconfig" };
        foreach (var fileName in filesToCopy)
        {
            var sourceFile = Path.Combine(sourceRoot, fileName);
            if (File.Exists(sourceFile))
            {
                var destFile = Path.Combine(_workspaceRoot, fileName);
                File.Copy(sourceFile, destFile, overwrite: true);
            }
        }

        // Collect all directories to copy
        var directoriesToCopy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add default directories (for backward compatibility)
        var defaultDirectories = new[] { ".agents", "docs", "src", "tests", "experiments" };
        foreach (var dirName in defaultDirectories)
        {
            var sourceDir = Path.Combine(sourceRoot, dirName);
            if (Directory.Exists(sourceDir))
            {
                directoriesToCopy.Add(dirName);
            }
        }

        // Auto-detect project directories (any directory containing .csproj files)
        var excludedDirectories = new[] { "bin", "obj", ".git", ".vs", "node_modules", ".devpilot", "packages" };
        foreach (var directory in Directory.GetDirectories(sourceRoot))
        {
            var dirName = Path.GetFileName(directory);

            // Skip excluded directories
            if (excludedDirectories.Contains(dirName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            // Check if directory contains .csproj files (project directory)
            var hasCsproj = Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0;
            if (hasCsproj)
            {
                directoriesToCopy.Add(dirName);
            }
        }

        // Add configured additional folders from devpilot.json
        if (config.Folders != null && config.Folders.Length > 0)
        {
            foreach (var folder in config.Folders)
            {
                var sourceDir = Path.Combine(sourceRoot, folder);
                if (Directory.Exists(sourceDir))
                {
                    directoriesToCopy.Add(folder);
                }
            }
        }

        // Copy all collected directories
        foreach (var dirName in directoriesToCopy)
        {
            var sourceDir = Path.Combine(sourceRoot, dirName);
            var destDir = Path.Combine(_workspaceRoot, dirName);
            CopyDirectoryRecursive(sourceDir, destDir);
        }
    }

    /// <summary>
    /// Copies CLAUDE.md from the target repository to the workspace.
    /// This is called separately after Planning stage to reduce context overload for the Planner agent.
    /// Large CLAUDE.md files can overwhelm the Planner's context window when using MCP tools.
    /// </summary>
    /// <param name="sourceRoot">The source directory containing the target repository.</param>
    /// <exception cref="ArgumentException">Thrown when sourceRoot is null or whitespace.</exception>
    public void CopyClaudeMd(string sourceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);

        var sourceFile = Path.Combine(sourceRoot, "CLAUDE.md");
        if (File.Exists(sourceFile))
        {
            var destFile = Path.Combine(_workspaceRoot, "CLAUDE.md");
            File.Copy(sourceFile, destFile, overwrite: true);
        }
    }

    /// <summary>
    /// Analyzes the project structure of the workspace to detect main/test projects and other artifacts.
    /// </summary>
    /// <returns>ProjectStructureInfo containing detected project organization.</returns>
    public ProjectStructureInfo AnalyzeProjectStructure()
    {
        // Find all .csproj files in workspace
        var projectFiles = Directory.GetFiles(_workspaceRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                       !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .ToList();

        var allProjects = new List<string>();
        var testProjects = new List<string>();
        string? mainProject = null;

        foreach (var projectFile in projectFiles)
        {
            var projectDir = Path.GetDirectoryName(projectFile);
            if (projectDir == null) continue;

            // Get relative path from workspace root
            var relativePath = Path.GetRelativePath(_workspaceRoot, projectDir);
            var projectName = Path.GetFileName(projectDir);

            allProjects.Add(relativePath + "/");

            // Determine if this is a test project
            if (IsTestProject(projectFile, projectName))
            {
                testProjects.Add(relativePath + "/");
            }
            else if (mainProject == null)
            {
                // First non-test project is assumed to be main project
                mainProject = relativePath + "/";
            }
        }

        // Detect other structure elements
        var hasDocs = Directory.Exists(Path.Combine(_workspaceRoot, "docs"));
        var hasAgents = Directory.Exists(Path.Combine(_workspaceRoot, ".agents"));
        var hasClaudeMd = File.Exists(Path.Combine(_workspaceRoot, "CLAUDE.md"));

        return new ProjectStructureInfo
        {
            MainProject = mainProject,
            TestProjects = testProjects,
            AllProjects = allProjects,
            HasDocs = hasDocs,
            HasAgents = hasAgents,
            HasClaudeMd = hasClaudeMd
        };
    }

    /// <summary>
    /// Determines if a project is a test project based on its name and dependencies.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file.</param>
    /// <param name="projectName">Name of the project directory.</param>
    /// <returns>True if this is a test project; otherwise, false.</returns>
    private static bool IsTestProject(string projectPath, string projectName)
    {
        // Check name patterns first (fast heuristic)
        var testNamePatterns = new[] { ".Tests", ".Test", "Tests.", "Test." };
        if (testNamePatterns.Any(pattern => projectName.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        try
        {
            // Check project file for test framework references
            var projectContent = File.ReadAllText(projectPath);
            var testFrameworks = new[]
            {
                "xunit", "nunit", "mstest", "NUnit", "MSTest", "xUnit",
                "Microsoft.NET.Test.Sdk", "coverlet.collector"
            };

            return testFrameworks.Any(framework => projectContent.Contains(framework, StringComparison.OrdinalIgnoreCase));
        }
        catch (IOException)
        {
            // If we can't read the file (file access errors), use name-based heuristic only
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // If we don't have permission to read the file, use name-based heuristic only
            return false;
        }
    }

    /// <summary>
    /// Recursively copies a directory and all its contents.
    /// </summary>
    /// <param name="sourceDir">The source directory to copy from.</param>
    /// <param name="destDir">The destination directory to copy to.</param>
    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        // Create destination directory if it doesn't exist
        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);
            File.Copy(file, destFile, overwrite: true);
        }

        // Recursively copy subdirectories (exclude build artifacts and workspaces DURING traversal)
        var excludedDirs = new[] { "bin", "obj", ".git", ".vs", "node_modules", ".devpilot", "packages", "TestResults", "nupkg" };
        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(directory);

            // Skip excluded directories
            if (excludedDirs.Contains(dirName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var destSubDir = Path.Combine(destDir, dirName);
            CopyDirectoryRecursive(directory, destSubDir);
        }
    }

    /// <summary>
    /// Recursively finds files matching a pattern, excluding specified directories during traversal.
    /// This is more efficient than Directory.GetFiles with SearchOption.AllDirectories because
    /// it skips excluded directories during traversal instead of filtering after.
    /// </summary>
    /// <param name="directory">The directory to search in.</param>
    /// <param name="pattern">The file pattern to match (e.g., "*.csproj", "*.cs"). Supports standard file system wildcards.</param>
    /// <param name="excludedDirectories">Directory names to skip during traversal (case-insensitive comparison). Common examples: bin, obj, .git, node_modules.</param>
    /// <returns>An enumerable of absolute file paths that match the specified pattern, excluding files in the specified directories.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller does not have the required permission to access the directory.</exception>
    /// <exception cref="System.Security.SecurityException">Thrown when the caller does not have the required security permissions.</exception>
    /// <remarks>
    /// This method uses lazy evaluation (yield return) and will only enumerate directories as needed.
    /// Directory name comparisons are case-insensitive for cross-platform compatibility.
    /// </remarks>
    private static IEnumerable<string> FindFilesRecursive(string directory, string pattern, string[] excludedDirectories)
    {
        // Find files in current directory
        foreach (var file in Directory.GetFiles(directory, pattern))
        {
            yield return file;
        }

        // Recurse into subdirectories, skipping excluded ones
        foreach (var subDir in Directory.GetDirectories(directory))
        {
            var dirName = Path.GetFileName(subDir);

            // Skip excluded directories
            if (excludedDirectories.Contains(dirName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            // Recursively search subdirectory
            foreach (var file in FindFilesRecursive(subDir, pattern, excludedDirectories))
            {
                yield return file;
            }
        }
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
        catch (IOException ex)
        {
            return FileApplicationResult.CreateFailure(filePatch.FilePath, $"I/O error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return FileApplicationResult.CreateFailure(filePatch.FilePath, $"Access denied: {ex.Message}");
        }
        catch (PatchApplicationException ex)
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
            catch (IOException)
            {
                // Continue rolling back even if individual operations fail
            }
            catch (UnauthorizedAccessException)
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
            catch (IOException)
            {
                // Ignore cleanup failures - directory may be in use
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore cleanup failures - insufficient permissions
            }
        }

        _appliedChanges.Clear();
    }

    /// <summary>
    /// Generates a git-style diff for a file in the workspace.
    /// </summary>
    /// <param name="filePath">The relative path to the file (e.g., "Testing/Calculator.cs").</param>
    /// <param name="sourceRoot">The source repository root to compare against (optional).</param>
    /// <returns>A formatted diff string showing the changes, or null if file doesn't exist.</returns>
    public string? GenerateFileDiff(string filePath, string? sourceRoot = null)
    {
        var workspaceFilePath = Path.Combine(_workspaceRoot, filePath);

        if (!File.Exists(workspaceFilePath))
        {
            return null;
        }

        var workspaceContent = File.ReadAllLines(workspaceFilePath);

        // Check if this is a new file or modification
        var sourceFilePath = sourceRoot != null ? Path.Combine(sourceRoot, filePath) : filePath;
        var isNewFile = !File.Exists(sourceFilePath);

        if (isNewFile)
        {
            // New file: Show full content with + prefix
            var diffLines = new List<string>
            {
                $"diff --git a/{filePath} b/{filePath}",
                "new file mode 100644",
                "--- /dev/null",
                $"+++ b/{filePath}",
                $"@@ -0,0 +1,{workspaceContent.Length} @@"
            };

            foreach (var line in workspaceContent)
            {
                diffLines.Add($"+{line}");
            }

            return string.Join(Environment.NewLine, diffLines);
        }
        else
        {
            // Modified file: Show line-by-line diff
            var originalContent = File.ReadAllLines(sourceFilePath);
            return GenerateUnifiedDiff(filePath, originalContent, workspaceContent);
        }
    }

    /// <summary>
    /// Generates a unified diff between two versions of a file.
    /// </summary>
    private static string GenerateUnifiedDiff(string filePath, string[] originalLines, string[] modifiedLines)
    {
        var diffLines = new List<string>
        {
            $"diff --git a/{filePath} b/{filePath}",
            $"--- a/{filePath}",
            $"+++ b/{filePath}"
        };

        // Simple line-by-line comparison (context: 3 lines before/after changes)
        var changes = new List<(int index, string type, string line)>();
        var maxLength = Math.Max(originalLines.Length, modifiedLines.Length);

        // Detect changes
        for (int i = 0; i < maxLength; i++)
        {
            var originalLine = i < originalLines.Length ? originalLines[i] : null;
            var modifiedLine = i < modifiedLines.Length ? modifiedLines[i] : null;

            if (originalLine != modifiedLine)
            {
                if (originalLine != null && modifiedLine != null)
                {
                    // Line changed
                    changes.Add((i, "remove", originalLine));
                    changes.Add((i, "add", modifiedLine));
                }
                else if (originalLine != null)
                {
                    // Line removed
                    changes.Add((i, "remove", originalLine));
                }
                else if (modifiedLine != null)
                {
                    // Line added
                    changes.Add((i, "add", modifiedLine));
                }
            }
        }

        if (changes.Count == 0)
        {
            diffLines.Add("@@ -1,1 +1,1 @@");
            diffLines.Add(" (no changes)");
            return string.Join(Environment.NewLine, diffLines);
        }

        // Group changes into hunks (simplified: one hunk for all changes)
        var firstChange = changes.First().index;
        var lastChange = changes.Last().index;
        var contextStart = Math.Max(0, firstChange - 3);
        var contextEnd = Math.Min(maxLength - 1, lastChange + 3);

        diffLines.Add($"@@ -{contextStart + 1},{contextEnd - contextStart + 1} +{contextStart + 1},{contextEnd - contextStart + 1} @@");

        // Add context lines and changes
        for (int i = contextStart; i <= contextEnd; i++)
        {
            var change = changes.FirstOrDefault(c => c.index == i);
            if (change != default)
            {
                if (change.type == "remove")
                {
                    diffLines.Add($"-{change.line}");
                }
                else if (change.type == "add")
                {
                    diffLines.Add($"+{change.line}");
                }
            }
            else
            {
                // Context line
                var contextLine = i < originalLines.Length ? originalLines[i] : modifiedLines[i];
                diffLines.Add($" {contextLine}");
            }
        }

        return string.Join(Environment.NewLine, diffLines);
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
