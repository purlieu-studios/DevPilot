using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DevPilot.Orchestrator.Validation;

/// <summary>
/// Validates code in the workspace before building to catch common errors early.
/// </summary>
public sealed class CodeValidator
{
    private static readonly Regex TestAttributePattern = new(
        @"\[(Fact|Theory|Test)\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Validates the workspace for common issues that would cause build failures.
    /// </summary>
    /// <param name="workspaceRoot">The root directory of the workspace.</param>
    /// <returns>A validation result indicating success or failure with detailed messages.</returns>
    public ValidationResult ValidateWorkspace(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        if (!Directory.Exists(workspaceRoot))
        {
            return ValidationResult.CreateFailure(
                "Workspace directory does not exist",
                $"Directory not found: {workspaceRoot}");
        }

        var errors = new List<string>();

        // Find all C# files in the workspace
        var csFiles = Directory.GetFiles(workspaceRoot, "*.cs", SearchOption.AllDirectories);

        foreach (var csFile in csFiles)
        {
            // Check if this is a test file (contains [Fact], [Theory], or [Test] attributes)
            if (IsTestFile(csFile))
            {
                var validationError = ValidateTestFile(csFile, workspaceRoot);
                if (validationError != null)
                {
                    errors.Add(validationError);
                }
            }
        }

        if (errors.Count > 0)
        {
            return ValidationResult.CreateFailure(
                "Pre-build validation failed",
                string.Join("\n\n", errors));
        }

        return ValidationResult.CreateSuccess("All validation checks passed");
    }

    /// <summary>
    /// Determines if a C# file is a test file by checking for test attributes.
    /// </summary>
    /// <param name="filePath">Path to the C# file.</param>
    /// <returns>True if the file contains test attributes; otherwise, false.</returns>
    private static bool IsTestFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            return TestAttributePattern.IsMatch(content);
        }
        catch (IOException)
        {
            // If we can't read the file, skip validation
            return false;
        }
    }

    /// <summary>
    /// Validates that a test file is correctly placed and configured.
    /// </summary>
    /// <param name="testFilePath">Path to the test file.</param>
    /// <param name="workspaceRoot">Root directory of the workspace.</param>
    /// <returns>An error message if validation fails; otherwise, null.</returns>
    private static string? ValidateTestFile(string testFilePath, string workspaceRoot)
    {
        var directory = Path.GetDirectoryName(testFilePath);
        if (directory == null)
        {
            return null;
        }

        // Find the nearest .csproj file by walking up the directory tree
        var projectFile = FindNearestProjectFile(directory, workspaceRoot);

        if (projectFile == null)
        {
            var relativePath = Path.GetRelativePath(workspaceRoot, testFilePath);
            return $"❌ Test file '{relativePath}' is not in a directory with a .csproj file.\n" +
                   $"   Tests must be in a proper test project (e.g., ProjectName.Tests/).\n" +
                   $"   This file will not be discovered by 'dotnet test'.";
        }

        // Validate that the project file has xUnit dependencies
        var xunitError = ValidateXunitDependencies(projectFile, workspaceRoot);
        if (xunitError != null)
        {
            var relativePath = Path.GetRelativePath(workspaceRoot, testFilePath);
            var relativeProjectPath = Path.GetRelativePath(workspaceRoot, projectFile);
            return $"❌ Test file '{relativePath}' is in a project without xUnit dependencies.\n" +
                   $"   Project: {relativeProjectPath}\n" +
                   $"   {xunitError}";
        }

        return null;
    }

    /// <summary>
    /// Finds the nearest .csproj file by walking up the directory tree.
    /// </summary>
    /// <param name="startDirectory">The directory to start searching from.</param>
    /// <param name="workspaceRoot">The workspace root (boundary for search).</param>
    /// <returns>Path to the nearest .csproj file; or null if none found.</returns>
    private static string? FindNearestProjectFile(string startDirectory, string workspaceRoot)
    {
        var currentDir = startDirectory;

        while (currentDir != null && currentDir.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            var projectFiles = Directory.GetFiles(currentDir, "*.csproj");
            if (projectFiles.Length > 0)
            {
                return projectFiles[0]; // Return first .csproj found
            }

            currentDir = Path.GetDirectoryName(currentDir);
        }

        return null;
    }

    /// <summary>
    /// Validates that a project file has required xUnit dependencies.
    /// </summary>
    /// <param name="projectFilePath">Path to the .csproj file.</param>
    /// <param name="workspaceRoot">Root directory of the workspace.</param>
    /// <returns>An error message if validation fails; otherwise, null.</returns>
    private static string? ValidateXunitDependencies(string projectFilePath, string workspaceRoot)
    {
        try
        {
            var projectXml = XDocument.Load(projectFilePath);
            var packageReferences = projectXml.Descendants("PackageReference")
                .Select(pr => pr.Attribute("Include")?.Value?.ToLowerInvariant())
                .Where(name => name != null)
                .ToHashSet();

            // Check for xUnit (support both v2 and v3)
            var hasXunit = packageReferences.Contains("xunit") ||
                          packageReferences.Contains("xunit.v3");

            if (!hasXunit)
            {
                return "Missing xUnit package reference.\n" +
                       "   Add: <PackageReference Include=\"xunit.v3\" Version=\"3.0.0\" />";
            }

            // Check for test SDK (required for test discovery)
            var hasTestSdk = packageReferences.Contains("microsoft.net.test.sdk");

            if (!hasTestSdk)
            {
                return "Missing Microsoft.NET.Test.Sdk package reference.\n" +
                       "   Add: <PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.14.1\" />";
            }

            return null; // All required dependencies present
        }
        catch (Exception ex) when (ex is IOException || ex is System.Xml.XmlException)
        {
            return $"Could not read or parse project file: {ex.Message}";
        }
    }
}

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether validation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets a brief summary of the validation result.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Gets detailed error messages (if validation failed).
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="summary">Summary message.</param>
    /// <returns>A validation result indicating success.</returns>
    public static ValidationResult CreateSuccess(string summary)
    {
        return new ValidationResult
        {
            Success = true,
            Summary = summary
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="summary">Summary message.</param>
    /// <param name="details">Detailed error information.</param>
    /// <returns>A validation result indicating failure.</returns>
    public static ValidationResult CreateFailure(string summary, string details)
    {
        return new ValidationResult
        {
            Success = false,
            Summary = summary,
            Details = details
        };
    }
}
