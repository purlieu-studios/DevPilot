using DevPilot.Orchestrator;
using FluentAssertions;

namespace DevPilot.Orchestrator.Tests;

public sealed class WorkspaceManagerTests : IDisposable
{
    private readonly string _testBaseDirectory;
    private readonly List<string> _workspacesToCleanup;

    public WorkspaceManagerTests()
    {
        _testBaseDirectory = Path.Combine(Path.GetTempPath(), "devpilot-test-workspaces", Guid.NewGuid().ToString());
        _workspacesToCleanup = new List<string>();
        Directory.CreateDirectory(_testBaseDirectory);
    }

    [Fact]
    public void CreateWorkspace_CreatesDirectory_WithValidPipelineId()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();

        // Act
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        // Assert
        Directory.Exists(workspace.WorkspaceRoot).Should().BeTrue();
        workspace.WorkspaceRoot.Should().Contain(pipelineId);
    }

    [Fact]
    public void CreateWorkspace_ThrowsException_WhenPipelineIdIsEmpty()
    {
        // Act
        var act = () => WorkspaceManager.CreateWorkspace("", _testBaseDirectory);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateWorkspace_ThrowsException_WhenWorkspaceAlreadyExists()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace1 = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace1.WorkspaceRoot);

        // Act
        var act = () => WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);

        // Assert
        act.Should().Throw<IOException>().WithMessage("*already exists*");
    }

    [Fact]
    public void ApplyPatch_CreatesNewFile_WithCreateOperation()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var patch = @"diff --git a/Calculator.cs b/Calculator.cs
new file mode 100644
--- /dev/null
+++ b/Calculator.cs
@@ -0,0 +1,5 @@
+public class Calculator
+{
+    public int Add(int a, int b) => a + b;
+}
";

        // Act
        var result = workspace.ApplyPatch(patch);

        // Assert
        result.Success.Should().BeTrue();
        result.FileResults.Should().HaveCount(1);
        result.FileResults[0].FilePath.Should().Be("Calculator.cs");
        result.FileResults[0].Success.Should().BeTrue();

        var filePath = Path.Combine(workspace.WorkspaceRoot, "Calculator.cs");
        File.Exists(filePath).Should().BeTrue();

        var content = File.ReadAllText(filePath);
        content.Should().Contain("public class Calculator");
        content.Should().Contain("public int Add(int a, int b) => a + b;");
    }

    [Fact]
    public void ApplyPatch_ModifiesExistingFile_WithModifyOperation()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var filePath = Path.Combine(workspace.WorkspaceRoot, "Calculator.cs");
        var originalContent = @"public class Calculator
{
    public int Add(int a, int b) => a + b;
}";
        File.WriteAllText(filePath, originalContent);

        var patch = @"diff --git a/Calculator.cs b/Calculator.cs
--- a/Calculator.cs
+++ b/Calculator.cs
@@ -1,4 +1,5 @@
 public class Calculator
 {
     public int Add(int a, int b) => a + b;
+    public int Subtract(int a, int b) => a - b;
 }";

        // Act
        var result = workspace.ApplyPatch(patch);

        // Assert
        result.Success.Should().BeTrue();
        var content = File.ReadAllText(filePath);
        content.Should().Contain("public int Add(int a, int b) => a + b;");
        content.Should().Contain("public int Subtract(int a, int b) => a - b;");
    }

    [Fact]
    public void ApplyPatch_DeletesFile_WithDeleteOperation()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var filePath = Path.Combine(workspace.WorkspaceRoot, "OldFile.cs");
        File.WriteAllText(filePath, "public class OldFile { }");

        var patch = @"diff --git a/OldFile.cs b/OldFile.cs
deleted file mode 100644
--- a/OldFile.cs
+++ /dev/null
@@ -1,1 +0,0 @@
-public class OldFile { }";

        // Act
        var result = workspace.ApplyPatch(patch);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public void ApplyPatch_HandlesMultipleFiles_InSinglePatch()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var patch = @"diff --git a/File1.cs b/File1.cs
new file mode 100644
--- /dev/null
+++ b/File1.cs
@@ -0,0 +1,1 @@
+public class File1 { }
diff --git a/File2.cs b/File2.cs
new file mode 100644
--- /dev/null
+++ b/File2.cs
@@ -0,0 +1,1 @@
+public class File2 { }";

        // Act
        var result = workspace.ApplyPatch(patch);

        // Assert
        result.Success.Should().BeTrue();
        result.FileResults.Should().HaveCount(2);
        File.Exists(Path.Combine(workspace.WorkspaceRoot, "File1.cs")).Should().BeTrue();
        File.Exists(Path.Combine(workspace.WorkspaceRoot, "File2.cs")).Should().BeTrue();
    }

    [Fact]
    public void ApplyPatch_CreatesNestedDirectories_WhenFilesInSubdirectories()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var patch = @"diff --git a/src/Calculator.cs b/src/Calculator.cs
new file mode 100644
--- /dev/null
+++ b/src/Calculator.cs
@@ -0,0 +1,1 @@
+public class Calculator { }";

        // Act
        var result = workspace.ApplyPatch(patch);

        // Assert
        result.Success.Should().BeTrue();
        var filePath = Path.Combine(workspace.WorkspaceRoot, "src", "Calculator.cs");
        File.Exists(filePath).Should().BeTrue();
        Directory.Exists(Path.Combine(workspace.WorkspaceRoot, "src")).Should().BeTrue();
    }

    [Fact]
    public void ApplyPatch_ThrowsException_WhenPatchIsEmpty()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        // Act
        var act = () => workspace.ApplyPatch("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyPatch_FailsAndRollsBack_WhenFileAlreadyExists()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var filePath = Path.Combine(workspace.WorkspaceRoot, "Existing.cs");
        File.WriteAllText(filePath, "existing content");

        var patch = @"diff --git a/Existing.cs b/Existing.cs
new file mode 100644
--- /dev/null
+++ b/Existing.cs
@@ -0,0 +1,1 @@
+public class Existing { }";

        // Act
        var result = workspace.ApplyPatch(patch);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already exists");
        File.ReadAllText(filePath).Should().Be("existing content"); // Original content preserved
    }

    [Fact]
    public void ApplyPatch_FailsAndRollsBack_WhenModifyingNonExistentFile()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var patch = @"diff --git a/NonExistent.cs b/NonExistent.cs
--- a/NonExistent.cs
+++ b/NonExistent.cs
@@ -1,1 +1,2 @@
 public class Test { }
+public class Added { }";

        // Act
        var result = workspace.ApplyPatch(patch);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not exist");
    }

    [Fact]
    public void AppliedFiles_ReturnsListOfCreatedAndModifiedFiles()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        // Create a file to modify
        var modifyFilePath = Path.Combine(workspace.WorkspaceRoot, "Modify.cs");
        File.WriteAllText(modifyFilePath, "public class Modify { }");

        // Create a file to delete
        var deleteFilePath = Path.Combine(workspace.WorkspaceRoot, "Delete.cs");
        File.WriteAllText(deleteFilePath, "public class Delete { }");

        var patch = @"diff --git a/Create.cs b/Create.cs
new file mode 100644
--- /dev/null
+++ b/Create.cs
@@ -0,0 +1,1 @@
+public class Create { }
diff --git a/Modify.cs b/Modify.cs
--- a/Modify.cs
+++ b/Modify.cs
@@ -1,1 +1,2 @@
 public class Modify { }
+public class ModifyAddition { }
diff --git a/Delete.cs b/Delete.cs
deleted file mode 100644
--- a/Delete.cs
+++ /dev/null
@@ -1,1 +0,0 @@
-public class Delete { }";

        // Act
        workspace.ApplyPatch(patch);

        // Assert
        workspace.AppliedFiles.Should().HaveCount(2);
        workspace.AppliedFiles.Should().Contain("Create.cs");
        workspace.AppliedFiles.Should().Contain("Modify.cs");
        workspace.AppliedFiles.Should().NotContain("Delete.cs"); // Deleted files not included
    }

    [Fact]
    public void Rollback_UndoesCreateOperation()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var patch = @"diff --git a/NewFile.cs b/NewFile.cs
new file mode 100644
--- /dev/null
+++ b/NewFile.cs
@@ -0,0 +1,1 @@
+public class NewFile { }";

        workspace.ApplyPatch(patch);
        var filePath = Path.Combine(workspace.WorkspaceRoot, "NewFile.cs");
        File.Exists(filePath).Should().BeTrue();

        // Act
        workspace.Rollback();

        // Assert
        File.Exists(filePath).Should().BeFalse();
        workspace.AppliedFiles.Should().BeEmpty();
    }

    [Fact]
    public void Rollback_UndoesModifyOperation()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var filePath = Path.Combine(workspace.WorkspaceRoot, "File.cs");
        var originalContent = "original content";
        File.WriteAllText(filePath, originalContent);

        var patch = @"diff --git a/File.cs b/File.cs
--- a/File.cs
+++ b/File.cs
@@ -1,1 +1,2 @@
 original content
+added line";

        workspace.ApplyPatch(patch);
        File.ReadAllText(filePath).Should().Contain("added line");

        // Act
        workspace.Rollback();

        // Assert
        File.ReadAllText(filePath).Should().Be(originalContent);
        workspace.AppliedFiles.Should().BeEmpty();
    }

    [Fact]
    public void Rollback_UndoesDeleteOperation()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var filePath = Path.Combine(workspace.WorkspaceRoot, "File.cs");
        var originalContent = "original content";
        File.WriteAllText(filePath, originalContent);

        var patch = @"diff --git a/File.cs b/File.cs
deleted file mode 100644
--- a/File.cs
+++ /dev/null
@@ -1,1 +0,0 @@
-original content";

        workspace.ApplyPatch(patch);
        File.Exists(filePath).Should().BeFalse();

        // Act
        workspace.Rollback();

        // Assert
        File.Exists(filePath).Should().BeTrue();
        File.ReadAllText(filePath).Should().Be(originalContent);
    }

    [Fact]
    public void Cleanup_RemovesWorkspaceDirectory()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        var workspaceRoot = workspace.WorkspaceRoot;

        Directory.Exists(workspaceRoot).Should().BeTrue();

        // Act
        workspace.Cleanup();

        // Assert
        Directory.Exists(workspaceRoot).Should().BeFalse();
    }

    [Fact]
    public void Dispose_CleansUpWorkspace()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        var workspaceRoot = workspace.WorkspaceRoot;

        Directory.Exists(workspaceRoot).Should().BeTrue();

        // Act
        workspace.Dispose();

        // Assert
        Directory.Exists(workspaceRoot).Should().BeFalse();
    }

    [Fact]
    public void ApplyPatch_HandlesMultilineAdditions()
    {
        // Arrange
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var patch = @"diff --git a/Class.cs b/Class.cs
new file mode 100644
--- /dev/null
+++ b/Class.cs
@@ -0,0 +1,7 @@
+public class Calculator
+{
+    public int Add(int a, int b)
+    {
+        return a + b;
+    }
+}";

        // Act
        var result = workspace.ApplyPatch(patch);

        // Assert
        result.Success.Should().BeTrue();
        var content = File.ReadAllText(Path.Combine(workspace.WorkspaceRoot, "Class.cs"));
        content.Should().Contain("public class Calculator");
        content.Should().Contain("public int Add(int a, int b)");
        content.Should().Contain("return a + b;");
    }

    [Fact]
    public void CopyProjectFiles_GeneratesSolutionFile_WithAllProjects()
    {
        // Arrange - Create workspace
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        // Create source directory with main project
        var sourceDir = Path.Combine(_testBaseDirectory, "Source");
        Directory.CreateDirectory(sourceDir);
        var mainProjectDir = Path.Combine(sourceDir, "TestApp");
        Directory.CreateDirectory(mainProjectDir);
        var mainCsprojPath = Path.Combine(mainProjectDir, "TestApp.csproj");
        File.WriteAllText(mainCsprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");

        // Create patch that adds test project
        var patch = @"diff --git a/TestApp.Tests/TestApp.Tests.csproj b/TestApp.Tests/TestApp.Tests.csproj
new file mode 100644
--- /dev/null
+++ b/TestApp.Tests/TestApp.Tests.csproj
@@ -0,0 +1,5 @@
+<Project Sdk=""Microsoft.NET.Sdk"">
+  <PropertyGroup>
+    <TargetFramework>net8.0</TargetFramework>
+  </PropertyGroup>
+</Project>";

        workspace.ApplyPatch(patch);

        // Act - Copy project files (this should generate .sln)
        workspace.CopyProjectFiles(sourceDir);

        // Assert - Verify .sln file was generated
        var slnPath = Path.Combine(workspace.WorkspaceRoot, "DevPilot.sln");
        File.Exists(slnPath).Should().BeTrue("solution file should be generated");

        // Verify .sln contains both projects
        var slnContent = File.ReadAllText(slnPath);
        slnContent.Should().Contain("TestApp");
        slnContent.Should().Contain("TestApp.Tests");
        slnContent.Should().Contain("Microsoft Visual Studio Solution File");
        slnContent.Should().Contain("GlobalSection(SolutionConfigurationPlatforms)");
        slnContent.Should().Contain("Debug|Any CPU");
        slnContent.Should().Contain("Release|Any CPU");

        // Verify both .csproj files are referenced
        slnContent.Should().Contain("TestApp\\TestApp.csproj");
        slnContent.Should().Contain("TestApp.Tests\\TestApp.Tests.csproj");
    }

    [Fact]
    public void CopyProjectFiles_RegeneratesSolutionFile_WhenAlreadyExists()
    {
        // Arrange - Create workspace with existing incomplete .sln
        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        // Create source directory with incomplete .sln (only 1 project)
        var sourceDir = Path.Combine(_testBaseDirectory, "Source2");
        Directory.CreateDirectory(sourceDir);
        var mainProjectDir = Path.Combine(sourceDir, "MyApp");
        Directory.CreateDirectory(mainProjectDir);
        File.WriteAllText(Path.Combine(mainProjectDir, "MyApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");

        // Create incomplete .sln in source (only has MyApp, missing MyApp.Tests)
        var incompleteSln = @"Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""MyApp"", ""MyApp\MyApp.csproj"", ""{11111111-1111-1111-1111-111111111111}""
EndProject
Global
EndGlobal";
        File.WriteAllText(Path.Combine(sourceDir, "DevPilot.sln"), incompleteSln);

        // Create patch that adds test project
        var patch = @"diff --git a/MyApp.Tests/MyApp.Tests.csproj b/MyApp.Tests/MyApp.Tests.csproj
new file mode 100644
--- /dev/null
+++ b/MyApp.Tests/MyApp.Tests.csproj
@@ -0,0 +1,5 @@
+<Project Sdk=""Microsoft.NET.Sdk"">
+  <PropertyGroup>
+    <TargetFramework>net8.0</TargetFramework>
+  </PropertyGroup>
+</Project>";

        workspace.ApplyPatch(patch);

        // Act - Copy project files (should regenerate .sln to include both projects)
        workspace.CopyProjectFiles(sourceDir);

        // Assert - Verify .sln was regenerated with BOTH projects
        var slnPath = Path.Combine(workspace.WorkspaceRoot, "DevPilot.sln");
        var slnContent = File.ReadAllText(slnPath);

        // Should have both projects now (not just MyApp from the incomplete source .sln)
        slnContent.Should().Contain("MyApp.csproj");
        slnContent.Should().Contain("MyApp.Tests.csproj");

        // Count project entries (should be 2)
        var projectCount = System.Text.RegularExpressions.Regex.Matches(slnContent, @"Project\(""").Count;
        projectCount.Should().Be(2, "solution should contain exactly 2 projects after regeneration");
    }

    /// <summary>
    /// Validates that devpilot.json configuration is properly parsed and applied.
    /// Regression test to ensure config-driven directory copying works correctly.
    /// </summary>
    [Fact]
    public void CopyDomainFiles_RespectsDevPilotJsonConfiguration()
    {
        // Arrange - Create source directory with devpilot.json
        var sourceDir = Path.Combine(_testBaseDirectory, "ConfigTest");
        Directory.CreateDirectory(sourceDir);

        // Create custom directories specified in devpilot.json
        var customDir1 = Path.Combine(sourceDir, "custom-lib");
        var customDir2 = Path.Combine(sourceDir, "shared");
        Directory.CreateDirectory(customDir1);
        Directory.CreateDirectory(customDir2);
        File.WriteAllText(Path.Combine(customDir1, "test.cs"), "// custom lib");
        File.WriteAllText(Path.Combine(customDir2, "shared.cs"), "// shared code");

        // Create devpilot.json with custom folders
        var configJson = @"{
  ""folders"": [""custom-lib"", ""shared""],
  ""copyAllFiles"": false
}";
        File.WriteAllText(Path.Combine(sourceDir, "devpilot.json"), configJson);

        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        // Act - Copy domain files (should respect devpilot.json config)
        workspace.CopyDomainFiles(sourceDir);

        // Assert - Custom directories from config should be copied
        Directory.Exists(Path.Combine(workspace.WorkspaceRoot, "custom-lib")).Should().BeTrue(
            "because 'custom-lib' is specified in devpilot.json folders");
        Directory.Exists(Path.Combine(workspace.WorkspaceRoot, "shared")).Should().BeTrue(
            "because 'shared' is specified in devpilot.json folders");

        File.Exists(Path.Combine(workspace.WorkspaceRoot, "custom-lib", "test.cs")).Should().BeTrue();
        File.Exists(Path.Combine(workspace.WorkspaceRoot, "shared", "shared.cs")).Should().BeTrue();
    }

    /// <summary>
    /// Validates that CopyDomainFiles auto-detects project directories (directories containing .csproj files).
    /// Ensures WorkspaceManager discovers all project directories regardless of naming conventions.
    /// </summary>
    [Fact]
    public void CopyDomainFiles_AutoDetectsProjectDirectories()
    {
        // Arrange - Create source with non-standard project directory names
        var sourceDir = Path.Combine(_testBaseDirectory, "AutoDetectTest");
        Directory.CreateDirectory(sourceDir);

        // Create project directory with non-standard name (not "src" or "tests")
        var customProjectDir = Path.Combine(sourceDir, "MyCustomApp");
        Directory.CreateDirectory(customProjectDir);
        File.WriteAllText(Path.Combine(customProjectDir, "MyCustomApp.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(customProjectDir, "Program.cs"), "// main program");

        // Create another project with different name
        var anotherProjectDir = Path.Combine(sourceDir, "Utilities");
        Directory.CreateDirectory(anotherProjectDir);
        File.WriteAllText(Path.Combine(anotherProjectDir, "Utilities.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(anotherProjectDir, "Helper.cs"), "// utilities");

        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        // Act - Copy domain files (should auto-detect project directories)
        workspace.CopyDomainFiles(sourceDir);

        // Assert - Both project directories should be auto-detected and copied
        Directory.Exists(Path.Combine(workspace.WorkspaceRoot, "MyCustomApp")).Should().BeTrue(
            "because MyCustomApp contains a .csproj file and should be auto-detected");
        Directory.Exists(Path.Combine(workspace.WorkspaceRoot, "Utilities")).Should().BeTrue(
            "because Utilities contains a .csproj file and should be auto-detected");

        File.Exists(Path.Combine(workspace.WorkspaceRoot, "MyCustomApp", "Program.cs")).Should().BeTrue();
        File.Exists(Path.Combine(workspace.WorkspaceRoot, "Utilities", "Helper.cs")).Should().BeTrue();
    }

    /// <summary>
    /// Validates that .devpilot directories are excluded from copying to prevent infinite recursion.
    /// Regression test for critical bug where WorkspaceManager recursively copied .devpilot/workspaces/.
    /// </summary>
    [Fact]
    public void CopyDomainFiles_ExcludesDevPilotDirectories_PreventsRecursion()
    {
        // Arrange - Create source with .devpilot directory (workspace nesting scenario)
        var sourceDir = Path.Combine(_testBaseDirectory, "RecursionTest");
        Directory.CreateDirectory(sourceDir);

        // Create .devpilot/workspaces directory with fake nested workspace
        var devpilotDir = Path.Combine(sourceDir, ".devpilot");
        var workspacesDir = Path.Combine(devpilotDir, "workspaces");
        var nestedWorkspaceDir = Path.Combine(workspacesDir, "old-pipeline-123");
        Directory.CreateDirectory(nestedWorkspaceDir);
        File.WriteAllText(Path.Combine(nestedWorkspaceDir, "OldFile.cs"), "// old workspace artifact");

        // Create legitimate src directory
        var srcDir = Path.Combine(sourceDir, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "App.cs"), "// application code");

        var pipelineId = Guid.NewGuid().ToString();
        using var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        // Act - Copy domain files (should exclude .devpilot directory)
        workspace.CopyDomainFiles(sourceDir);

        // Assert - .devpilot directory should NOT be copied (prevents recursion)
        Directory.Exists(Path.Combine(workspace.WorkspaceRoot, ".devpilot")).Should().BeFalse(
            "because .devpilot directories must be excluded to prevent recursive workspace nesting");
        File.Exists(Path.Combine(workspace.WorkspaceRoot, ".devpilot", "workspaces", "old-pipeline-123", "OldFile.cs"))
            .Should().BeFalse("because nested workspace artifacts must not be copied");

        // Legitimate src directory should still be copied
        Directory.Exists(Path.Combine(workspace.WorkspaceRoot, "src")).Should().BeTrue();
        File.Exists(Path.Combine(workspace.WorkspaceRoot, "src", "App.cs")).Should().BeTrue();
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
