using DevPilot.Core;
using FluentAssertions;

namespace DevPilot.Core.Tests;

/// <summary>
/// Tests for ProjectStructureInfo - repository structure metadata.
/// </summary>
public sealed class ProjectStructureInfoTests
{
    #region ToAgentContext Formatting Tests

    [Fact]
    public void ToAgentContext_StandardStructure_FormatsCorrectly()
    {
        // Arrange
        var structure = new ProjectStructureInfo
        {
            MainProject = "src",
            TestProjects = new List<string> { "tests" },
            AllProjects = new List<string> { "src", "tests" },
            HasDocs = true,
            HasAgents = true,
            HasClaudeMd = true
        };

        // Act
        var context = structure.ToAgentContext();

        // Assert
        context.Should().Contain("Main Project: src");
        context.Should().Contain("Test Projects: tests");
        context.Should().Contain("Documentation: docs/");
        context.Should().Contain("Custom Agents: .agents/");
        context.Should().Contain("Project Instructions: CLAUDE.md");
    }

    [Fact]
    public void ToAgentContext_NoTestProjects_ShowsNone()
    {
        // Arrange
        var structure = new ProjectStructureInfo
        {
            MainProject = "MyApp",
            TestProjects = new List<string>(),
            AllProjects = new List<string> { "MyApp" },
            HasDocs = false,
            HasAgents = false,
            HasClaudeMd = false
        };

        // Act
        var context = structure.ToAgentContext();

        // Assert
        context.Should().Contain("Main Project: MyApp");
        context.Should().Contain("Test Projects: None");
        context.Should().NotContain("Documentation:");
        context.Should().NotContain("Custom Agents:");
        context.Should().NotContain("CLAUDE.md");
    }

    [Fact]
    public void ToAgentContext_MultipleTestProjects_ListsAll()
    {
        // Arrange
        var structure = new ProjectStructureInfo
        {
            MainProject = "src/MyApp",
            TestProjects = new List<string> { "tests/Unit", "tests/Integration", "tests/E2E" },
            AllProjects = new List<string> { "src/MyApp", "tests/Unit", "tests/Integration", "tests/E2E" },
            HasDocs = false,
            HasAgents = false,
            HasClaudeMd = false
        };

        // Act
        var context = structure.ToAgentContext();

        // Assert
        context.Should().Contain("Test Projects: tests/Unit, tests/Integration, tests/E2E");
    }

    [Fact]
    public void ToAgentContext_VeryLongProjectNames_FormatsWithoutTruncation()
    {
        // Arrange
        var longName = "src/MyVeryLongApplicationNameThatExceedsNormalLengthForTestingPurposes";
        var structure = new ProjectStructureInfo
        {
            MainProject = longName,
            TestProjects = new List<string>(),
            AllProjects = new List<string> { longName },
            HasDocs = false,
            HasAgents = false,
            HasClaudeMd = false
        };

        // Act
        var context = structure.ToAgentContext();

        // Assert
        context.Should().Contain(longName);
    }

    [Fact]
    public void ToAgentContext_IncludesWarningAboutStandardNames()
    {
        // Arrange
        var structure = new ProjectStructureInfo
        {
            MainProject = "Application",
            TestProjects = new List<string> { "Tests" },
            AllProjects = new List<string> { "Application", "Tests" },
            HasDocs = false,
            HasAgents = false,
            HasClaudeMd = false
        };

        // Act
        var context = structure.ToAgentContext();

        // Assert
        context.Should().Contain("IMPORTANT:");
        context.Should().Contain("Do NOT assume standard names");
    }

    [Fact]
    public void ToAgentContext_HasClaudeMd_IncludesReadInstructions()
    {
        // Arrange
        var structure = new ProjectStructureInfo
        {
            MainProject = "src",
            TestProjects = new List<string>(),
            AllProjects = new List<string> { "src" },
            HasDocs = false,
            HasAgents = false,
            HasClaudeMd = true
        };

        // Act
        var context = structure.ToAgentContext();

        // Assert
        context.Should().Contain("read this for context");
    }

    #endregion

    #region Property Behavior Tests

    [Fact]
    public void Properties_AreInitOnly_CannotBeModifiedAfterConstruction()
    {
        // Arrange & Act
        var structure = new ProjectStructureInfo
        {
            MainProject = "src",
            TestProjects = new List<string> { "tests" },
            AllProjects = new List<string> { "src", "tests" },
            HasDocs = true,
            HasAgents = false,
            HasClaudeMd = true
        };

        // Assert - properties should be init-only (compile-time check)
        structure.MainProject.Should().Be("src");
        structure.TestProjects.Should().ContainSingle("tests");
        structure.HasDocs.Should().BeTrue();
    }

    [Fact]
    public void MainProject_CanBeNull()
    {
        // Arrange & Act
        var structure = new ProjectStructureInfo
        {
            MainProject = null,
            TestProjects = new List<string>(),
            AllProjects = new List<string>(),
            HasDocs = false,
            HasAgents = false,
            HasClaudeMd = false
        };

        // Assert
        structure.MainProject.Should().BeNull();
    }

    [Fact]
    public void TestProjects_EmptyList_IsValid()
    {
        // Arrange & Act
        var structure = new ProjectStructureInfo
        {
            MainProject = "src",
            TestProjects = new List<string>(),
            AllProjects = new List<string> { "src" },
            HasDocs = false,
            HasAgents = false,
            HasClaudeMd = false
        };

        // Assert
        structure.TestProjects.Should().BeEmpty();
        structure.AllProjects.Should().ContainSingle("src");
    }

    [Fact]
    public void AllProjects_IncludesMainAndTestProjects()
    {
        // Arrange & Act
        var structure = new ProjectStructureInfo
        {
            MainProject = "MyApp",
            TestProjects = new List<string> { "MyApp.Tests", "MyApp.Integration.Tests" },
            AllProjects = new List<string> { "MyApp", "MyApp.Tests", "MyApp.Integration.Tests" },
            HasDocs = false,
            HasAgents = false,
            HasClaudeMd = false
        };

        // Assert
        structure.AllProjects.Should().HaveCount(3);
        structure.AllProjects.Should().Contain("MyApp");
        structure.AllProjects.Should().Contain("MyApp.Tests");
        structure.AllProjects.Should().Contain("MyApp.Integration.Tests");
    }

    [Fact]
    public void BooleanFlags_DefaultToFalse()
    {
        // Arrange & Act
        var structure = new ProjectStructureInfo
        {
            MainProject = "src",
            TestProjects = new List<string>(),
            AllProjects = new List<string>(),
            HasDocs = false,
            HasAgents = false,
            HasClaudeMd = false
        };

        // Assert
        structure.HasDocs.Should().BeFalse();
        structure.HasAgents.Should().BeFalse();
        structure.HasClaudeMd.Should().BeFalse();
    }

    [Fact]
    public void BooleanFlags_CanBeSetToTrue()
    {
        // Arrange & Act
        var structure = new ProjectStructureInfo
        {
            MainProject = "src",
            TestProjects = new List<string>(),
            AllProjects = new List<string>(),
            HasDocs = true,
            HasAgents = true,
            HasClaudeMd = true
        };

        // Assert
        structure.HasDocs.Should().BeTrue();
        structure.HasAgents.Should().BeTrue();
        structure.HasClaudeMd.Should().BeTrue();
    }

    #endregion
}
