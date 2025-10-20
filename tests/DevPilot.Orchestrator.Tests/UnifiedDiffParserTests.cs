using DevPilot.Orchestrator;
using FluentAssertions;

namespace DevPilot.Orchestrator.Tests;

public sealed class UnifiedDiffParserTests
{
    [Fact]
    public void Parse_ExtractsFilePath_FromGitDiffHeader()
    {
        // Arrange
        var patch = @"diff --git a/src/Calculator.cs b/src/Calculator.cs
new file mode 100644
--- /dev/null
+++ b/src/Calculator.cs
@@ -0,0 +1,5 @@
+public class Calculator
+{
+    public int Add(int a, int b) => a + b;
+}
";

        // Act
        var result = UnifiedDiffParser.Parse(patch);

        // Assert
        result.FilePatches.Should().HaveCount(1);
        result.FilePatches[0].FilePath.Should().Be("src/Calculator.cs");
    }

    [Fact]
    public void Parse_IdentifiesNewFile_WhenDevNullSource()
    {
        // Arrange
        var patch = @"diff --git a/src/NewFile.cs b/src/NewFile.cs
new file mode 100644
--- /dev/null
+++ b/src/NewFile.cs
@@ -0,0 +1,3 @@
+namespace DevPilot;
+
+public class NewFile { }
";

        // Act
        var result = UnifiedDiffParser.Parse(patch);

        // Assert
        result.FilePatches[0].Operation.Should().Be(FileOperation.Create);
    }

    [Fact]
    public void Parse_IdentifiesDeletedFile_WhenDevNullTarget()
    {
        // Arrange
        var patch = @"diff --git a/src/OldFile.cs b/src/OldFile.cs
deleted file mode 100644
--- a/src/OldFile.cs
+++ /dev/null
@@ -1,3 +0,0 @@
-namespace DevPilot;
-
-public class OldFile { }
";

        // Act
        var result = UnifiedDiffParser.Parse(patch);

        // Assert
        result.FilePatches[0].Operation.Should().Be(FileOperation.Delete);
    }

    [Fact]
    public void Parse_IdentifiesModification_WhenBothPathsExist()
    {
        // Arrange
        var patch = @"diff --git a/src/Calculator.cs b/src/Calculator.cs
--- a/src/Calculator.cs
+++ b/src/Calculator.cs
@@ -1,3 +1,4 @@
 public class Calculator
 {
     public int Add(int a, int b) => a + b;
+    public int Subtract(int a, int b) => a - b;
 }
";

        // Act
        var result = UnifiedDiffParser.Parse(patch);

        // Assert
        result.FilePatches[0].Operation.Should().Be(FileOperation.Modify);
    }

    [Fact]
    public void Parse_ParsesHunks_WithCorrectLineNumbers()
    {
        // Arrange
        var patch = @"diff --git a/src/Calculator.cs b/src/Calculator.cs
--- a/src/Calculator.cs
+++ b/src/Calculator.cs
@@ -1,5 +1,6 @@
 public class Calculator
 {
     public int Add(int a, int b) => a + b;
+    public int Subtract(int a, int b) => a - b;
 }
";

        // Act
        var result = UnifiedDiffParser.Parse(patch);

        // Assert
        var hunk = result.FilePatches[0].Hunks[0];
        hunk.OldStart.Should().Be(1);
        hunk.OldLines.Should().Be(5);
        hunk.NewStart.Should().Be(1);
        hunk.NewLines.Should().Be(6);
    }

    [Fact]
    public void Parse_ParsesAddedLines_Correctly()
    {
        // Arrange
        var patch = @"diff --git a/src/Calculator.cs b/src/Calculator.cs
--- a/src/Calculator.cs
+++ b/src/Calculator.cs
@@ -1,3 +1,4 @@
 public class Calculator
 {
+    public int Add(int a, int b) => a + b;
 }
";

        // Act
        var result = UnifiedDiffParser.Parse(patch);

        // Assert
        var lines = result.FilePatches[0].Hunks[0].Lines;
        lines.Should().Contain(l => l.Type == DiffLineType.Add && l.Content == "    public int Add(int a, int b) => a + b;");
    }

    [Fact]
    public void Parse_ParsesRemovedLines_Correctly()
    {
        // Arrange
        var patch = @"diff --git a/src/Calculator.cs b/src/Calculator.cs
--- a/src/Calculator.cs
+++ b/src/Calculator.cs
@@ -1,4 +1,3 @@
 public class Calculator
 {
-    public int Add(int a, int b) => a + b;
 }
";

        // Act
        var result = UnifiedDiffParser.Parse(patch);

        // Assert
        var lines = result.FilePatches[0].Hunks[0].Lines;
        lines.Should().Contain(l => l.Type == DiffLineType.Remove && l.Content == "    public int Add(int a, int b) => a + b;");
    }

    [Fact]
    public void Parse_ParsesContextLines_Correctly()
    {
        // Arrange
        var patch = @"diff --git a/src/Calculator.cs b/src/Calculator.cs
--- a/src/Calculator.cs
+++ b/src/Calculator.cs
@@ -1,4 +1,5 @@
 public class Calculator
 {
     public int Add(int a, int b) => a + b;
+    public int Subtract(int a, int b) => a - b;
 }
";

        // Act
        var result = UnifiedDiffParser.Parse(patch);

        // Assert
        var lines = result.FilePatches[0].Hunks[0].Lines;
        lines.Should().Contain(l => l.Type == DiffLineType.Context && l.Content == "public class Calculator");
        lines.Should().Contain(l => l.Type == DiffLineType.Context && l.Content == "{");
    }

    [Fact]
    public void Parse_HandlesMultipleFiles_InSinglePatch()
    {
        // Arrange
        var patch = @"diff --git a/src/Calculator.cs b/src/Calculator.cs
new file mode 100644
--- /dev/null
+++ b/src/Calculator.cs
@@ -0,0 +1,3 @@
+public class Calculator
+{
+}
diff --git a/tests/CalculatorTests.cs b/tests/CalculatorTests.cs
new file mode 100644
--- /dev/null
+++ b/tests/CalculatorTests.cs
@@ -0,0 +1,5 @@
+public class CalculatorTests
+{
+    [Fact]
+    public void TestAdd() { }
+}
";

        // Act
        var result = UnifiedDiffParser.Parse(patch);

        // Assert
        result.FilePatches.Should().HaveCount(2);
        result.FilePatches[0].FilePath.Should().Be("src/Calculator.cs");
        result.FilePatches[1].FilePath.Should().Be("tests/CalculatorTests.cs");
    }

    [Fact]
    public void Parse_HandlesMultipleHunks_InSingleFile()
    {
        // Arrange
        var patch = @"diff --git a/src/Calculator.cs b/src/Calculator.cs
--- a/src/Calculator.cs
+++ b/src/Calculator.cs
@@ -1,3 +1,4 @@
 public class Calculator
 {
+    public int Add(int a, int b) => a + b;
 }
@@ -10,3 +11,4 @@
 public class AdvancedCalculator
 {
+    public int Multiply(int a, int b) => a * b;
 }
";

        // Act
        var result = UnifiedDiffParser.Parse(patch);

        // Assert
        result.FilePatches[0].Hunks.Should().HaveCount(2);
        result.FilePatches[0].Hunks[0].OldStart.Should().Be(1);
        result.FilePatches[0].Hunks[1].OldStart.Should().Be(10);
    }

    [Fact]
    public void Parse_ThrowsException_WhenPatchIsEmpty()
    {
        // Arrange
        var patch = "";

        // Act
        var act = () => UnifiedDiffParser.Parse(patch);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_ThrowsException_WhenPatchIsWhitespace()
    {
        // Arrange
        var patch = "   \n  \n  ";

        // Act
        var act = () => UnifiedDiffParser.Parse(patch);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_ThrowsException_WhenNoValidFilePatches()
    {
        // Arrange
        var patch = @"This is not a valid unified diff
Just some random text
That doesn't match the format
";

        // Act
        var act = () => UnifiedDiffParser.Parse(patch);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*No valid file patches found*");
    }

    [Fact]
    public void Parse_HandlesWindowsLineEndings()
    {
        // Arrange
        var patch = "diff --git a/src/File.cs b/src/File.cs\r\n" +
                   "new file mode 100644\r\n" +
                   "--- /dev/null\r\n" +
                   "+++ b/src/File.cs\r\n" +
                   "@@ -0,0 +1,2 @@\r\n" +
                   "+public class File\r\n" +
                   "+{\r\n";

        // Act
        var result = UnifiedDiffParser.Parse(patch);

        // Assert
        result.FilePatches.Should().HaveCount(1);
        result.FilePatches[0].FilePath.Should().Be("src/File.cs");
    }
}
