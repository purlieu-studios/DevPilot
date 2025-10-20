using DevPilot.Core;
using DevPilot.Orchestrator;
using FluentAssertions;

namespace DevPilot.Orchestrator.Tests;

/// <summary>
/// End-to-end regression tests for the DevPilot pipeline.
/// These tests validate critical workflows to prevent regressions in pipeline execution.
/// </summary>
/// <remarks>
/// These tests use mocked agents to avoid expensive API calls while still validating
/// the overall pipeline orchestration, workspace management, and error handling.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class EndToEndRegressionTests : IDisposable
{
    private readonly string _testBaseDirectory;
    private readonly List<string> _workspacesToCleanup;

    public EndToEndRegressionTests()
    {
        _testBaseDirectory = Path.Combine(Path.GetTempPath(), "devpilot-e2e-tests", Guid.NewGuid().ToString());
        _workspacesToCleanup = new List<string>();
        Directory.CreateDirectory(_testBaseDirectory);
    }

    /// <summary>
    /// Validates that the pipeline creates isolated workspaces and cleans them up properly.
    /// Prevents regressions where workspaces are not properly isolated or leak across executions.
    /// </summary>
    [Fact]
    public void Pipeline_CreatesIsolatedWorkspace_AndCleansUpCorrectly()
    {
        // Arrange
        var pipelineId1 = Guid.NewGuid().ToString();
        var pipelineId2 = Guid.NewGuid().ToString();

        // Act - Create two separate workspaces
        using var workspace1 = WorkspaceManager.CreateWorkspace(pipelineId1, _testBaseDirectory);
        using var workspace2 = WorkspaceManager.CreateWorkspace(pipelineId2, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace1.WorkspaceRoot);
        _workspacesToCleanup.Add(workspace2.WorkspaceRoot);

        // Assert - Workspaces should be isolated
        workspace1.WorkspaceRoot.Should().NotBe(workspace2.WorkspaceRoot,
            "because each pipeline execution must have its own isolated workspace");

        Directory.Exists(workspace1.WorkspaceRoot).Should().BeTrue();
        Directory.Exists(workspace2.WorkspaceRoot).Should().BeTrue();

        // Create files in each workspace to verify isolation
        var file1 = Path.Combine(workspace1.WorkspaceRoot, "Test1.cs");
        var file2 = Path.Combine(workspace2.WorkspaceRoot, "Test2.cs");
        File.WriteAllText(file1, "// workspace 1");
        File.WriteAllText(file2, "// workspace 2");

        // Verify files don't cross-contaminate
        File.Exists(Path.Combine(workspace1.WorkspaceRoot, "Test2.cs")).Should().BeFalse(
            "because workspace 1 should not contain files from workspace 2");
        File.Exists(Path.Combine(workspace2.WorkspaceRoot, "Test1.cs")).Should().BeFalse(
            "because workspace 2 should not contain files from workspace 1");

        // Act - Cleanup workspace1
        workspace1.Cleanup();

        // Assert - Only workspace1 should be cleaned up
        Directory.Exists(workspace1.WorkspaceRoot).Should().BeFalse(
            "because workspace1.Cleanup() should delete the workspace directory");
        Directory.Exists(workspace2.WorkspaceRoot).Should().BeTrue(
            "because workspace2 should remain untouched when workspace1 is cleaned up");
    }

    /// <summary>
    /// Validates that patch application and rollback work correctly in a realistic scenario.
    /// Prevents regressions in patch parsing, file operations, and error recovery.
    /// </summary>
    [Fact]
    public void Pipeline_AppliesPatch_AndRollsBackCorrectly()
    {
        // Arrange - Create workspace with existing file
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var existingFile = Path.Combine(workspace.WorkspaceRoot, "Calculator.cs");
        var originalContent = @"public class Calculator
{
    public int Add(int a, int b) => a + b;
}";
        File.WriteAllText(existingFile, originalContent);

        // Valid patch that modifies existing file and creates new file
        var validPatch = @"diff --git a/Calculator.cs b/Calculator.cs
--- a/Calculator.cs
+++ b/Calculator.cs
@@ -1,4 +1,5 @@
 public class Calculator
 {
     public int Add(int a, int b) => a + b;
+    public int Subtract(int a, int b) => a - b;
 }
diff --git a/CalculatorTests.cs b/CalculatorTests.cs
new file mode 100644
--- /dev/null
+++ b/CalculatorTests.cs
@@ -0,0 +1,3 @@
+public class CalculatorTests
+{
+}";

        // Act - Apply valid patch
        var result = workspace.ApplyPatch(validPatch);

        // Assert - Patch applied successfully
        result.Success.Should().BeTrue();
        var modifiedContent = File.ReadAllText(existingFile);
        modifiedContent.Should().Contain("public int Add(int a, int b) => a + b;");
        modifiedContent.Should().Contain("public int Subtract(int a, int b) => a - b;");
        File.Exists(Path.Combine(workspace.WorkspaceRoot, "CalculatorTests.cs")).Should().BeTrue();

        // Act - Rollback to original state
        workspace.Rollback();

        // Assert - Rollback restores original content and removes created files
        File.ReadAllText(existingFile).Should().Be(originalContent,
            "because rollback should restore original file content");
        File.Exists(Path.Combine(workspace.WorkspaceRoot, "CalculatorTests.cs")).Should().BeFalse(
            "because rollback should delete created files");
    }

    /// <summary>
    /// Validates that TestRunner correctly detects and reports test results.
    /// Prevents regressions in test discovery, execution, and result parsing.
    /// </summary>
    [Fact]
    public async Task Pipeline_ExecutesTests_AndReportsResults()
    {
        // Arrange - Create workspace with simple test project
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        // Create minimal .csproj file
        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""xunit"" Version=""2.9.3"" />
    <PackageReference Include=""xunit.runner.visualstudio"" Version=""2.9.3"" />
  </ItemGroup>
</Project>";

        var projectDir = Path.Combine(workspace.WorkspaceRoot, "TestProject");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "TestProject.csproj"), csprojContent);

        // Create test file with passing and failing tests
        var testFileContent = @"using Xunit;

public class SampleTests
{
    [Fact]
    public void PassingTest()
    {
        Assert.True(true);
    }

    [Fact]
    public void FailingTest()
    {
        Assert.True(false);
    }
}";
        File.WriteAllText(Path.Combine(projectDir, "SampleTests.cs"), testFileContent);

        // Generate solution file for test discovery
        workspace.CopyProjectFiles(workspace.WorkspaceRoot);

        // Act - Run tests using TestRunner
        var testResult = await TestRunner.ExecuteTestsAsync(workspace.WorkspaceRoot, CancellationToken.None);

        // Assert - TestRunner reports results correctly
        testResult.Should().NotBeNull();
        testResult.Pass.Should().BeFalse("because there's one failing test");
        testResult.Summary.Should().NotBeNullOrWhiteSpace();

        testResult.TestResults.Should().NotBeNullOrEmpty();
        testResult.TestResults.Should().HaveCountGreaterThanOrEqualTo(2, "because we have 2 tests");

        var passedTests = testResult.TestResults.Count(t => t.Status == TestStatus.Passed);
        var failedTests = testResult.TestResults.Count(t => t.Status == TestStatus.Failed);

        passedTests.Should().BeGreaterThanOrEqualTo(1, "because PassingTest should pass");
        failedTests.Should().BeGreaterThanOrEqualTo(1, "because FailingTest should fail");
    }

    public void Dispose()
    {
        // Clean up all test workspaces
        foreach (var workspaceRoot in _workspacesToCleanup)
        {
            try
            {
                if (Directory.Exists(workspaceRoot))
                {
                    Directory.Delete(workspaceRoot, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup failures
            }
        }

        // Clean up base test directory
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
