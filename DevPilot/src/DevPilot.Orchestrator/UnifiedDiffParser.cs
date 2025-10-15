using System.Text.RegularExpressions;

namespace DevPilot.Orchestrator;

/// <summary>
/// Parses unified diff format (git-style diffs) into structured data.
/// </summary>
public sealed class UnifiedDiffParser
{
    private static readonly Regex DiffHeaderRegex = new(@"^diff --git a/(.+) b/(.+)$", RegexOptions.Compiled);
    private static readonly Regex FileHeaderRegex = new(@"^[-+]{3} (.+)$", RegexOptions.Compiled);
    private static readonly Regex HunkHeaderRegex = new(@"^@@ -(\d+),?(\d*) \+(\d+),?(\d*) @@", RegexOptions.Compiled);
    private static readonly Regex NewFileRegex = new(@"^new file mode \d+$", RegexOptions.Compiled);
    private static readonly Regex DeletedFileRegex = new(@"^deleted file mode \d+$", RegexOptions.Compiled);

    /// <summary>
    /// Parses a unified diff patch into structured data.
    /// </summary>
    /// <param name="patch">The unified diff content.</param>
    /// <returns>A parsed patch containing all file changes.</returns>
    /// <exception cref="ArgumentException">Thrown when the patch format is invalid.</exception>
    public static ParsedPatch Parse(string patch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patch);

        var lines = patch.Split('\n', StringSplitOptions.None);
        var filePatches = new List<FilePatch>();
        FilePatch? currentFile = null;
        var currentHunk = new List<DiffLine>();
        int? currentOldStart = null;
        int? currentOldLines = null;
        int? currentNewStart = null;
        int? currentNewLines = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');

            // Check for diff header (start of new file)
            var diffMatch = DiffHeaderRegex.Match(line);
            if (diffMatch.Success)
            {
                // Save previous file if exists
                if (currentFile != null)
                {
                    if (currentHunk.Count > 0 && currentOldStart.HasValue)
                    {
                        currentFile.Hunks.Add(new Hunk
                        {
                            OldStart = currentOldStart.Value,
                            OldLines = currentOldLines ?? 1,
                            NewStart = currentNewStart ?? 1,
                            NewLines = currentNewLines ?? 1,
                            Lines = currentHunk.ToList()
                        });
                        currentHunk.Clear();
                    }
                    filePatches.Add(currentFile);
                }

                var filePath = diffMatch.Groups[2].Value;
                currentFile = new FilePatch
                {
                    FilePath = filePath,
                    Operation = FileOperation.Modify, // Default, will be updated if we see new/deleted markers
                    Hunks = new List<Hunk>()
                };
                currentOldStart = null;
                currentOldLines = null;
                currentNewStart = null;
                currentNewLines = null;
                continue;
            }

            // Check for new file marker
            if (NewFileRegex.IsMatch(line) && currentFile != null)
            {
                currentFile.Operation = FileOperation.Create;
                continue;
            }

            // Check for deleted file marker
            if (DeletedFileRegex.IsMatch(line) && currentFile != null)
            {
                currentFile.Operation = FileOperation.Delete;
                continue;
            }

            // Check for file header (--- or +++)
            var fileHeaderMatch = FileHeaderRegex.Match(line);
            if (fileHeaderMatch.Success)
            {
                var path = fileHeaderMatch.Groups[1].Value;
                if (path == "/dev/null" && currentFile != null && line.StartsWith("---"))
                {
                    currentFile.Operation = FileOperation.Create;
                }
                else if (path == "/dev/null" && currentFile != null && line.StartsWith("+++"))
                {
                    currentFile.Operation = FileOperation.Delete;
                }
                continue;
            }

            // Check for hunk header
            var hunkMatch = HunkHeaderRegex.Match(line);
            if (hunkMatch.Success)
            {
                // Save previous hunk if exists
                if (currentHunk.Count > 0 && currentOldStart.HasValue && currentFile != null)
                {
                    currentFile.Hunks.Add(new Hunk
                    {
                        OldStart = currentOldStart.Value,
                        OldLines = currentOldLines ?? 1,
                        NewStart = currentNewStart ?? 1,
                        NewLines = currentNewLines ?? 1,
                        Lines = currentHunk.ToList()
                    });
                    currentHunk.Clear();
                }

                currentOldStart = int.Parse(hunkMatch.Groups[1].Value);
                currentOldLines = string.IsNullOrEmpty(hunkMatch.Groups[2].Value) ? 1 : int.Parse(hunkMatch.Groups[2].Value);
                currentNewStart = int.Parse(hunkMatch.Groups[3].Value);
                currentNewLines = string.IsNullOrEmpty(hunkMatch.Groups[4].Value) ? 1 : int.Parse(hunkMatch.Groups[4].Value);
                continue;
            }

            // Parse diff content lines (must be inside a hunk)
            if (currentOldStart.HasValue)
            {
                if (line.StartsWith('+') && !line.StartsWith("+++"))
                {
                    currentHunk.Add(new DiffLine
                    {
                        Type = DiffLineType.Add,
                        Content = line.Length > 1 ? line.Substring(1) : string.Empty
                    });
                }
                else if (line.StartsWith('-') && !line.StartsWith("---"))
                {
                    currentHunk.Add(new DiffLine
                    {
                        Type = DiffLineType.Remove,
                        Content = line.Length > 1 ? line.Substring(1) : string.Empty
                    });
                }
                else if (line.StartsWith(' ') || string.IsNullOrEmpty(line))
                {
                    currentHunk.Add(new DiffLine
                    {
                        Type = DiffLineType.Context,
                        Content = line.Length > 1 ? line.Substring(1) : string.Empty
                    });
                }
            }
        }

        // Save last file
        if (currentFile != null)
        {
            if (currentHunk.Count > 0 && currentOldStart.HasValue)
            {
                currentFile.Hunks.Add(new Hunk
                {
                    OldStart = currentOldStart.Value,
                    OldLines = currentOldLines ?? 1,
                    NewStart = currentNewStart ?? 1,
                    NewLines = currentNewLines ?? 1,
                    Lines = currentHunk.ToList()
                });
            }
            filePatches.Add(currentFile);
        }

        if (filePatches.Count == 0)
        {
            throw new ArgumentException("No valid file patches found in the unified diff");
        }

        return new ParsedPatch { FilePatches = filePatches };
    }
}

/// <summary>
/// Represents a parsed unified diff patch containing multiple file changes.
/// </summary>
public sealed class ParsedPatch
{
    /// <summary>
    /// Gets the list of file patches in this diff.
    /// </summary>
    public required List<FilePatch> FilePatches { get; init; }
}

/// <summary>
/// Represents changes to a single file in a patch.
/// </summary>
public sealed class FilePatch
{
    /// <summary>
    /// Gets the path to the file being changed.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the type of operation being performed on this file.
    /// </summary>
    public required FileOperation Operation { get; set; }

    /// <summary>
    /// Gets the list of hunks (change blocks) for this file.
    /// </summary>
    public required List<Hunk> Hunks { get; init; }
}

/// <summary>
/// Represents a single hunk (block of changes) in a file patch.
/// </summary>
public sealed class Hunk
{
    /// <summary>
    /// Gets the starting line number in the old file.
    /// </summary>
    public required int OldStart { get; init; }

    /// <summary>
    /// Gets the number of lines from the old file.
    /// </summary>
    public required int OldLines { get; init; }

    /// <summary>
    /// Gets the starting line number in the new file.
    /// </summary>
    public required int NewStart { get; init; }

    /// <summary>
    /// Gets the number of lines in the new file.
    /// </summary>
    public required int NewLines { get; init; }

    /// <summary>
    /// Gets the list of diff lines in this hunk.
    /// </summary>
    public required List<DiffLine> Lines { get; init; }
}

/// <summary>
/// Represents a single line in a diff hunk.
/// </summary>
public sealed class DiffLine
{
    /// <summary>
    /// Gets the type of this diff line (add/remove/context).
    /// </summary>
    public required DiffLineType Type { get; init; }

    /// <summary>
    /// Gets the content of this line (without the leading +/- character).
    /// </summary>
    public required string Content { get; init; }
}

/// <summary>
/// Specifies the type of operation being performed on a file.
/// </summary>
public enum FileOperation
{
    /// <summary>
    /// The file is being created.
    /// </summary>
    Create,

    /// <summary>
    /// The file is being modified.
    /// </summary>
    Modify,

    /// <summary>
    /// The file is being deleted.
    /// </summary>
    Delete
}

/// <summary>
/// Specifies the type of a diff line.
/// </summary>
public enum DiffLineType
{
    /// <summary>
    /// A line being added to the file.
    /// </summary>
    Add,

    /// <summary>
    /// A line being removed from the file.
    /// </summary>
    Remove,

    /// <summary>
    /// A context line (unchanged, for reference).
    /// </summary>
    Context
}
