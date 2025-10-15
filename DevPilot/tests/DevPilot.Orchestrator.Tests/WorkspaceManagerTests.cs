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
