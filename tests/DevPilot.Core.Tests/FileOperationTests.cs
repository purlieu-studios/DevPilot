using DevPilot.Core;
using FluentAssertions;

namespace DevPilot.Core.Tests;

public sealed class FileOperationTests
{
    #region MCPFileOperationType Enum Tests

    [Fact]
    public void MCPFileOperationType_HasExpectedValues()
    {
        // Assert
        Enum.GetNames(typeof(MCPFileOperationType)).Should().Contain(new[]
        {
            nameof(MCPFileOperationType.Create),
            nameof(MCPFileOperationType.Modify),
            nameof(MCPFileOperationType.Delete),
            nameof(MCPFileOperationType.Rename)
        });
    }

    [Fact]
    public void MCPFileOperationType_CanBeCompared()
    {
        // Arrange
        var createType = MCPFileOperationType.Create;
        var modifyType = MCPFileOperationType.Modify;

        // Assert
        createType.Should().NotBe(modifyType);
        createType.Should().Be(MCPFileOperationType.Create);
    }

    #endregion

    #region MCPFileOperation - Create Operation Tests

    [Fact]
    public void MCPFileOperation_CreateOperation_CanBeConstructed()
    {
        // Arrange & Act
        var operation = new MCPFileOperation
        {
            Type = MCPFileOperationType.Create,
            Path = "src/Calculator.cs",
            Content = "public class Calculator { }",
            Reason = "Adding new calculator class"
        };

        // Assert
        operation.Type.Should().Be(MCPFileOperationType.Create);
        operation.Path.Should().Be("src/Calculator.cs");
        operation.Content.Should().Be("public class Calculator { }");
        operation.Reason.Should().Be("Adding new calculator class");
    }

    [Fact]
    public void MCPFileOperation_CreateOperation_WithMultilineContent_StoresCorrectly()
    {
        // Arrange
        var multilineContent = "public class Calculator\n{\n    public int Add(int a, int b) => a + b;\n}";

        // Act
        var operation = new MCPFileOperation
        {
            Type = MCPFileOperationType.Create,
            Path = "src/Calculator.cs",
            Content = multilineContent
        };

        // Assert
        operation.Content.Should().Contain("\n");
        operation.Content.Should().Contain("public int Add");
    }

    [Fact]
    public void MCPFileOperation_CreateOperation_WithoutReason_AllowsNull()
    {
        // Arrange & Act
        var operation = new MCPFileOperation
        {
            Type = MCPFileOperationType.Create,
            Path = "test.txt",
            Content = "content"
        };

        // Assert
        operation.Reason.Should().BeNull();
    }

    #endregion

    #region MCPFileOperation - Modify Operation Tests

    [Fact]
    public void MCPFileOperation_ModifyOperation_CanBeConstructed()
    {
        // Arrange
        var changes = new List<MCPLineChange>
        {
            new MCPLineChange { LineNumber = 5, NewContent = "    public int Add(int a, int b) => a + b;" }
        };

        // Act
        var operation = new MCPFileOperation
        {
            Type = MCPFileOperationType.Modify,
            Path = "src/Calculator.cs",
            Changes = changes,
            Reason = "Adding Add method"
        };

        // Assert
        operation.Type.Should().Be(MCPFileOperationType.Modify);
        operation.Path.Should().Be("src/Calculator.cs");
        operation.Changes.Should().HaveCount(1);
        operation.Changes![0].LineNumber.Should().Be(5);
    }

    [Fact]
    public void MCPFileOperation_ModifyOperation_WithMultipleChanges_StoresAll()
    {
        // Arrange
        var changes = new List<MCPLineChange>
        {
            new MCPLineChange { LineNumber = 5, NewContent = "line 5" },
            new MCPLineChange { LineNumber = 10, NewContent = "line 10" },
            new MCPLineChange { LineNumber = 15, NewContent = "line 15" }
        };

        // Act
        var operation = new MCPFileOperation
        {
            Type = MCPFileOperationType.Modify,
            Path = "test.cs",
            Changes = changes
        };

        // Assert
        operation.Changes.Should().HaveCount(3);
        operation.Changes![1].LineNumber.Should().Be(10);
    }

    #endregion

    #region MCPFileOperation - Delete Operation Tests

    [Fact]
    public void MCPFileOperation_DeleteOperation_CanBeConstructed()
    {
        // Arrange & Act
        var operation = new MCPFileOperation
        {
            Type = MCPFileOperationType.Delete,
            Path = "src/OldClass.cs",
            Reason = "Removing deprecated class"
        };

        // Assert
        operation.Type.Should().Be(MCPFileOperationType.Delete);
        operation.Path.Should().Be("src/OldClass.cs");
        operation.Reason.Should().Be("Removing deprecated class");
    }

    [Fact]
    public void MCPFileOperation_DeleteOperation_DoesNotRequireContent()
    {
        // Arrange & Act
        var operation = new MCPFileOperation
        {
            Type = MCPFileOperationType.Delete,
            Path = "temp.txt"
        };

        // Assert
        operation.Content.Should().BeNull();
        operation.Changes.Should().BeNull();
    }

    #endregion

    #region MCPFileOperation - Rename Operation Tests

    [Fact]
    public void MCPFileOperation_RenameOperation_CanBeConstructed()
    {
        // Arrange & Act
        var operation = new MCPFileOperation
        {
            Type = MCPFileOperationType.Rename,
            OldPath = "src/OldName.cs",
            NewPath = "src/NewName.cs",
            Reason = "Renaming for clarity"
        };

        // Assert
        operation.Type.Should().Be(MCPFileOperationType.Rename);
        operation.OldPath.Should().Be("src/OldName.cs");
        operation.NewPath.Should().Be("src/NewName.cs");
    }

    [Fact]
    public void MCPFileOperation_RenameOperation_WithDirectoryChange_StoresCorrectly()
    {
        // Arrange & Act
        var operation = new MCPFileOperation
        {
            Type = MCPFileOperationType.Rename,
            OldPath = "src/Utilities.cs",
            NewPath = "lib/Utilities.cs",
            Reason = "Moving to lib folder"
        };

        // Assert
        operation.OldPath.Should().Contain("src");
        operation.NewPath.Should().Contain("lib");
    }

    #endregion

    #region MCPLineChange Tests

    [Fact]
    public void MCPLineChange_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var change = new MCPLineChange
        {
            LineNumber = 10,
            NewContent = "updated line content"
        };

        // Assert
        change.LineNumber.Should().Be(10);
        change.NewContent.Should().Be("updated line content");
    }

    [Fact]
    public void MCPLineChange_WithOldContent_StoresForValidation()
    {
        // Arrange & Act
        var change = new MCPLineChange
        {
            LineNumber = 5,
            OldContent = "original content",
            NewContent = "updated content"
        };

        // Assert
        change.OldContent.Should().Be("original content");
        change.NewContent.Should().Be("updated content");
    }

    [Fact]
    public void MCPLineChange_LinesToReplace_DefaultsToOne()
    {
        // Arrange & Act
        var change = new MCPLineChange
        {
            LineNumber = 3,
            NewContent = "new content"
        };

        // Assert
        change.LinesToReplace.Should().Be(1);
    }

    [Fact]
    public void MCPLineChange_LinesToReplace_CanBeSetExplicitly()
    {
        // Arrange & Act
        var change = new MCPLineChange
        {
            LineNumber = 5,
            NewContent = "replacement method",
            LinesToReplace = 10
        };

        // Assert
        change.LinesToReplace.Should().Be(10);
    }

    [Fact]
    public void MCPLineChange_WithEmptyNewContent_AllowsDeletion()
    {
        // Arrange & Act
        var change = new MCPLineChange
        {
            LineNumber = 8,
            NewContent = string.Empty
        };

        // Assert
        change.NewContent.Should().BeEmpty();
        change.NewContent.Should().NotBeNull();
    }

    [Fact]
    public void MCPLineChange_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var change1 = new MCPLineChange
        {
            LineNumber = 5,
            NewContent = "test",
            LinesToReplace = 1
        };

        var change2 = new MCPLineChange
        {
            LineNumber = 5,
            NewContent = "test",
            LinesToReplace = 1
        };

        // Assert
        change1.Should().Be(change2);
        (change1 == change2).Should().BeTrue();
    }

    #endregion
}
