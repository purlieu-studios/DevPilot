using DevPilot.Orchestrator.CodeAnalysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace DevPilot.Orchestrator.Tests;

/// <summary>
/// Integration tests for CodeAnalyzer that validate Roslyn analyzer integration
/// with real .csproj files and NuGet-restored packages.
///
/// These tests ensure package upgrades (NetAnalyzers, MSBuild.Locator, etc.)
/// don't break CodeAnalyzer functionality.
/// </summary>
public sealed class CodeAnalyzerIntegrationTests : IDisposable
{
    private readonly string _testProjectPath;
    private readonly string _testWorkspacePath;

    public CodeAnalyzerIntegrationTests()
    {
        // Use a real project directory for testing (examples/simple-calculator)
        _testWorkspacePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "examples", "simple-calculator"));
        _testProjectPath = Path.Combine(_testWorkspacePath, "Calculator", "Calculator.csproj");
    }

    #region Smoke Tests for Roslyn Package Compatibility

    [Fact]
    public async Task CodeAnalyzer_WithRealProject_LoadsSuccessfully()
    {
        // Arrange
        if (!File.Exists(_testProjectPath))
        {
            // Skip if test project doesn't exist
            return;
        }

        var analyzer = new CodeAnalyzer();
        var modifiedFiles = new List<string> { "Calculator/Calculator.cs" };

        // Act
        var result = await analyzer.AnalyzeWorkspaceAsync(_testWorkspacePath, modifiedFiles, CancellationToken.None);

        // Assert
        result.Should().NotBeNull("CodeAnalyzer should return results for real projects");
        result.Diagnostics.Should().NotBeNull("Diagnostics collection should be initialized");
    }

    [Fact]
    public async Task CodeAnalyzer_WithRealProject_DetectsCompilerDiagnostics()
    {
        // Arrange
        if (!File.Exists(_testProjectPath))
        {
            return; // Skip if test project doesn't exist
        }

        // Create a file with intentional compiler error
        var testFilePath = Path.Combine(_testWorkspacePath, "Calculator", "TestError.cs");
        File.WriteAllText(testFilePath, @"
namespace Calculator;

public class TestError
{
    public void Test()
    {
        var x = undefinedVariable;  // CS0103
    }
}");

        try
        {
            var analyzer = new CodeAnalyzer();
            var modifiedFiles = new List<string> { "Calculator/TestError.cs" };

            // Act
            var result = await analyzer.AnalyzeWorkspaceAsync(_testWorkspacePath, modifiedFiles, CancellationToken.None);

            // Assert
            result.HasErrors.Should().BeTrue("compiler errors should be detected");
            result.Diagnostics.Should().Contain(d => d.RuleId == "CS0103", "CS0103 error should be detected");
        }
        finally
        {
            // Cleanup
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }

    [Fact]
    public async Task CodeAnalyzer_WithRealProject_DetectsAnalyzerWarnings()
    {
        // Arrange
        if (!File.Exists(_testProjectPath))
        {
            return; // Skip if test project doesn't exist
        }

        // Create a file with analyzer warning
        var testFilePath = Path.Combine(_testWorkspacePath, "Calculator", "TestWarning.cs");
        File.WriteAllText(testFilePath, @"
namespace Calculator;

public class TestWarning
{
    private int _unusedField = 42;  // CS0414 (compiler warning)

    public void Test()
    {
        // Method body
    }
}");

        try
        {
            var analyzer = new CodeAnalyzer();
            var modifiedFiles = new List<string> { "Calculator/TestWarning.cs" };

            // Act
            var result = await analyzer.AnalyzeWorkspaceAsync(_testWorkspacePath, modifiedFiles, CancellationToken.None);

            // Assert
            result.HasWarnings.Should().BeTrue("compiler warnings should be detected");
            result.Diagnostics.Should().Contain(d => d.RuleId == "CS0414", "CS0414 warning should be detected");
        }
        finally
        {
            // Cleanup
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }

    #endregion

    #region MSBuildWorkspace Compatibility Tests

    [Fact]
    public async Task CodeAnalyzer_MSBuildWorkspace_LoadsProjectWithPackages()
    {
        // Arrange
        if (!File.Exists(_testProjectPath))
        {
            return; // Skip if test project doesn't exist
        }

        var analyzer = new CodeAnalyzer();
        var modifiedFiles = new List<string> { "Calculator/Calculator.cs" };

        // Act
        var result = await analyzer.AnalyzeWorkspaceAsync(_testWorkspacePath, modifiedFiles, CancellationToken.None);

        // Assert
        result.Should().NotBeNull("MSBuildWorkspace should load project with NuGet packages");
        // If this test passes, MSBuildWorkspace + MSBuildLocator are working correctly
    }

    [Fact]
    public async Task CodeAnalyzer_FiltersToModifiedFiles_InRealProject()
    {
        // Arrange
        if (!File.Exists(_testProjectPath))
        {
            return; // Skip if test project doesn't exist
        }

        // Create two files with warnings
        var fileA = Path.Combine(_testWorkspacePath, "Calculator", "FileA.cs");
        var fileB = Path.Combine(_testWorkspacePath, "Calculator", "FileB.cs");

        File.WriteAllText(fileA, @"
namespace Calculator;
public class FileA
{
    private int _unusedField = 1;  // CS0414
}");

        File.WriteAllText(fileB, @"
namespace Calculator;
public class FileB
{
    private int _unusedField = 2;  // CS0414
}");

        try
        {
            var analyzer = new CodeAnalyzer();
            var modifiedFiles = new List<string> { "Calculator/FileA.cs" };  // Only FileA

            // Act
            var result = await analyzer.AnalyzeWorkspaceAsync(_testWorkspacePath, modifiedFiles, CancellationToken.None);

            // Assert
            result.Diagnostics.Should().OnlyContain(d => d.FilePath.EndsWith("FileA.cs"),
                "only modified files should have diagnostics");
            result.Diagnostics.Should().NotContain(d => d.FilePath.EndsWith("FileB.cs"),
                "unmodified files should be filtered out");
        }
        finally
        {
            // Cleanup
            if (File.Exists(fileA)) File.Delete(fileA);
            if (File.Exists(fileB)) File.Delete(fileB);
        }
    }

    #endregion

    #region Package Version Validation Tests

    [Fact]
    public async Task CodeAnalyzer_CurrentPackageVersions_DoNotCrash()
    {
        // This test validates that the current package versions (from .csproj)
        // work correctly together. If this test fails after a package upgrade,
        // it indicates an incompatibility issue.

        // Arrange
        if (!File.Exists(_testProjectPath))
        {
            return; // Skip if test project doesn't exist
        }

        var analyzer = new CodeAnalyzer();
        var modifiedFiles = new List<string> { "Calculator/Calculator.cs" };

        // Act & Assert
        var exception = await Record.ExceptionAsync(async () =>
        {
            var result = await analyzer.AnalyzeWorkspaceAsync(_testWorkspacePath, modifiedFiles, CancellationToken.None);
            result.Should().NotBeNull();
        });

        exception.Should().BeNull("CodeAnalyzer should not crash with current package versions");
    }

    #endregion

    public void Dispose()
    {
        // Cleanup is handled in individual tests
    }
}
