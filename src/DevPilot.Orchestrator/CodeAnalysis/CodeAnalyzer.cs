using System.Collections.Immutable;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;

namespace DevPilot.Orchestrator.CodeAnalysis;

/// <summary>
/// Analyzes code using Roslyn analyzers to detect quality issues, style violations, and potential bugs.
/// </summary>
public sealed class CodeAnalyzer
{
    private readonly AnalyzerOptions _options;
    private static bool _msbuildRegistered = false;
    private static readonly object _registrationLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeAnalyzer"/> class.
    /// </summary>
    /// <param name="options">Analysis options (optional).</param>
    public CodeAnalyzer(AnalyzerOptions? options = null)
    {
        _options = options ?? AnalyzerOptions.Default;

        // Register MSBuild once per process
        lock (_registrationLock)
        {
            if (!_msbuildRegistered)
            {
                try
                {
                    MSBuildLocator.RegisterDefaults();
                    _msbuildRegistered = true;
                }
                catch (InvalidOperationException)
                {
                    // Already registered - this is fine
                }
            }
        }
    }

    /// <summary>
    /// Analyzes the specified workspace for code quality issues.
    /// </summary>
    /// <param name="workspaceRoot">The root directory of the workspace.</param>
    /// <param name="modifiedFiles">The list of modified files to analyze (relative paths).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis results containing diagnostics.</returns>
    public async Task<AnalysisResult> AnalyzeWorkspaceAsync(
        string workspaceRoot,
        IReadOnlyList<string> modifiedFiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(modifiedFiles);

        // Check if analysis is disabled
        if (!_options.Enabled)
        {
            return AnalysisResult.Empty;
        }

        if (modifiedFiles.Count == 0)
        {
            return AnalysisResult.Empty;
        }

        try
        {
            // Find .sln or .csproj file
            var solutionOrProjectPath = FindSolutionOrProject(workspaceRoot);
            if (solutionOrProjectPath == null)
            {
                // No project found - skip analysis
                return AnalysisResult.Empty;
            }

            // Load workspace
            using var workspace = MSBuildWorkspace.Create();
            workspace.WorkspaceFailed += (sender, args) =>
            {
                // Suppress workspace loading warnings (non-critical)
                if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                {
                    System.Console.Error.WriteLine($"Workspace loading issue: {args.Diagnostic.Message}");
                }
            };

            // Load solution or project
            var solution = solutionOrProjectPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                ? await workspace.OpenSolutionAsync(solutionOrProjectPath, cancellationToken: cancellationToken)
                : await workspace.OpenProjectAsync(solutionOrProjectPath, cancellationToken: cancellationToken).ContinueWith(
                    t => workspace.CurrentSolution, cancellationToken);

            // Collect diagnostics from modified files only
            var diagnostics = new List<CodeDiagnostic>();

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation == null)
                {
                    continue;
                }

                IEnumerable<Diagnostic> allDiagnostics;

                // Run analyzers if project has any
                if (project.AnalyzerReferences.Any())
                {
                    // Load analyzers from project references
                    var analyzers = project.AnalyzerReferences
                        .SelectMany(r => r.GetAnalyzers(compilation.Language))
                        .ToImmutableArray();

                    if (analyzers.Length > 0)
                    {
                        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers, options: null);
                        allDiagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync(cancellationToken);
                    }
                    else
                    {
                        // No analyzers loaded, just get compilation diagnostics
                        allDiagnostics = compilation.GetDiagnostics(cancellationToken);
                    }
                }
                else
                {
                    // No analyzer references, just get compilation diagnostics
                    allDiagnostics = compilation.GetDiagnostics(cancellationToken);
                }

                // Filter to only modified files
                var modifiedFilePaths = modifiedFiles
                    .Select(f => Path.Combine(workspaceRoot, f))
                    .Select(Path.GetFullPath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var diagnostic in allDiagnostics)
                {
                    // Skip if diagnostic doesn't have a location
                    if (diagnostic.Location == Location.None || diagnostic.Location.SourceTree == null)
                    {
                        continue;
                    }

                    var filePath = diagnostic.Location.SourceTree.FilePath;
                    if (!modifiedFilePaths.Contains(filePath))
                    {
                        continue; // Not a modified file
                    }

                    // Filter by severity
                    if (diagnostic.Severity < _options.MinimumSeverity)
                    {
                        continue;
                    }

                    // Filter by ignored rule IDs
                    if (_options.IgnoredRuleIds.Contains(diagnostic.Id))
                    {
                        continue;
                    }

                    // Map to CodeDiagnostic
                    var lineSpan = diagnostic.Location.GetLineSpan();
                    var relativePath = Path.GetRelativePath(workspaceRoot, filePath);

                    diagnostics.Add(new CodeDiagnostic
                    {
                        FilePath = relativePath,
                        LineNumber = lineSpan.StartLinePosition.Line + 1, // Convert 0-based to 1-based
                        RuleId = diagnostic.Id,
                        Severity = diagnostic.Severity,
                        Message = diagnostic.GetMessage(),
                        Category = diagnostic.Descriptor.Category
                    });
                }
            }

            return new AnalysisResult
            {
                Diagnostics = diagnostics.OrderBy(d => d.FilePath).ThenBy(d => d.LineNumber).ToList()
            };
        }
        catch (Exception ex)
        {
            // Analysis failure - return empty result with error logged
            System.Console.Error.WriteLine($"Code analysis failed: {ex.Message}");
            return AnalysisResult.Empty;
        }
    }

    /// <summary>
    /// Finds a .sln or .csproj file in the workspace.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root directory.</param>
    /// <returns>Path to .sln or .csproj file, or null if none found.</returns>
    private static string? FindSolutionOrProject(string workspaceRoot)
    {
        // Priority 1: .sln file
        var slnFiles = Directory.GetFiles(workspaceRoot, "*.sln", SearchOption.TopDirectoryOnly);
        if (slnFiles.Length > 0)
        {
            return slnFiles[0];
        }

        // Priority 2: .csproj file
        var csprojFiles = Directory.GetFiles(workspaceRoot, "*.csproj", SearchOption.AllDirectories);
        if (csprojFiles.Length > 0)
        {
            return csprojFiles[0];
        }

        return null;
    }
}
