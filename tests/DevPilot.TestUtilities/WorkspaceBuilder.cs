using DevPilot.Core;
using DevPilot.Orchestrator;

namespace DevPilot.TestUtilities;

/// <summary>
/// Fluent API for creating test workspaces with predefined structures.
/// Simplifies setup for workspace-related tests.
/// </summary>
/// <example>
/// <code>
/// var workspace = new WorkspaceBuilder()
///     .WithFile("src/Calculator.cs", "public class Calculator { }")
///     .WithFile("tests/CalculatorTests.cs", "public class CalculatorTests { }")
///     .WithDevPilotConfig(new DevPilotConfig { Folders = new[] { "custom-lib" } })
///     .Build();
/// </code>
/// </example>
public sealed class WorkspaceBuilder : IDisposable
{
    private readonly string _testBaseDirectory;
    private readonly string _sourceDirectory;
    private readonly List<(string Path, string Content)> _files = new();
    private readonly List<string> _directories = new();
    private DevPilotConfig? _config;
    private string? _claudeMd;
    private WorkspaceManager? _workspace;

    public WorkspaceBuilder(string? testBaseDirectory = null)
    {
        _testBaseDirectory = testBaseDirectory ?? Path.Combine(Path.GetTempPath(), "devpilot-test", Guid.NewGuid().ToString());
        _sourceDirectory = Path.Combine(_testBaseDirectory, "source");
        Directory.CreateDirectory(_sourceDirectory);
    }

    /// <summary>
    /// Adds a file to the source repository with the specified content.
    /// </summary>
    /// <param name="relativePath">Relative path from source root (e.g., "src/Calculator.cs")</param>
    /// <param name="content">File content</param>
    public WorkspaceBuilder WithFile(string relativePath, string content)
    {
        _files.Add((relativePath, content));
        return this;
    }

    /// <summary>
    /// Adds an empty directory to the source repository.
    /// </summary>
    /// <param name="relativePath">Relative path from source root (e.g., "src/Models")</param>
    public WorkspaceBuilder WithDirectory(string relativePath)
    {
        _directories.Add(relativePath);
        return this;
    }

    /// <summary>
    /// Adds a .csproj file to create a project directory.
    /// Auto-detected by WorkspaceManager during copying.
    /// </summary>
    /// <param name="projectName">Project name (e.g., "MyApp")</param>
    /// <param name="isTestProject">Whether this is a test project</param>
    public WorkspaceBuilder WithProject(string projectName, bool isTestProject = false)
    {
        var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  {(isTestProject ? @"<ItemGroup>
    <PackageReference Include=""xunit"" Version=""2.9.3"" />
    <PackageReference Include=""xunit.runner.visualstudio"" Version=""2.9.3"" />
  </ItemGroup>" : "")}
</Project>";

        _files.Add(($"{projectName}/{projectName}.csproj", csprojContent));
        return this;
    }

    /// <summary>
    /// Adds a devpilot.json configuration file to the source repository.
    /// </summary>
    public WorkspaceBuilder WithDevPilotConfig(DevPilotConfig config)
    {
        _config = config;
        return this;
    }

    /// <summary>
    /// Adds a CLAUDE.md file to the source repository.
    /// </summary>
    public WorkspaceBuilder WithClaudeMd(string content)
    {
        _claudeMd = content;
        return this;
    }

    /// <summary>
    /// Creates the source repository structure and a DevPilot workspace from it.
    /// </summary>
    /// <returns>A workspace instance with all files copied</returns>
    public WorkspaceManager Build()
    {
        // Create all directories first
        foreach (var dir in _directories)
        {
            Directory.CreateDirectory(Path.Combine(_sourceDirectory, dir));
        }

        // Create all files
        foreach (var (path, content) in _files)
        {
            var fullPath = Path.Combine(_sourceDirectory, path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(fullPath, content);
        }

        // Create devpilot.json if specified
        if (_config != null)
        {
            var configJson = System.Text.Json.JsonSerializer.Serialize(_config, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(Path.Combine(_sourceDirectory, "devpilot.json"), configJson);
        }

        // Create CLAUDE.md if specified
        if (_claudeMd != null)
        {
            File.WriteAllText(Path.Combine(_sourceDirectory, "CLAUDE.md"), _claudeMd);
        }

        // Create workspace and copy files
        var pipelineId = Guid.NewGuid().ToString();
        _workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory, WorkspaceType.Test);
        _workspace.CopyDomainFiles(_sourceDirectory);

        return _workspace;
    }

    /// <summary>
    /// Gets the source directory path (where files are created before workspace copy).
    /// </summary>
    public string SourceDirectory => _sourceDirectory;

    public void Dispose()
    {
        _workspace?.Dispose();

        // Clean up test directories
        try
        {
            if (Directory.Exists(_testBaseDirectory))
            {
                Directory.Delete(_testBaseDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }
}
