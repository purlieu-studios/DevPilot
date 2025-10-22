using DevPilot.Core;
using FluentAssertions;

namespace DevPilot.Core.Tests;

public sealed class AgentDefinitionTests
{
    #region Constructor and Property Tests

    [Fact]
    public void AgentDefinition_CanBeConstructed_WithAllRequiredProperties()
    {
        // Arrange & Act
        var definition = new AgentDefinition
        {
            Name = "planner",
            Version = "1.0.0",
            Description = "Planning agent",
            SystemPrompt = "You are a planner",
            Model = "sonnet"
        };

        // Assert
        definition.Name.Should().Be("planner");
        definition.Version.Should().Be("1.0.0");
        definition.Description.Should().Be("Planning agent");
        definition.SystemPrompt.Should().Be("You are a planner");
        definition.Model.Should().Be("sonnet");
        definition.McpConfigPath.Should().BeNull();
    }

    [Fact]
    public void AgentDefinition_WithMcpConfigPath_StoresValue()
    {
        // Arrange & Act
        var definition = new AgentDefinition
        {
            Name = "planner",
            Version = "1.0.0",
            Description = "Planning agent",
            SystemPrompt = "You are a planner",
            Model = "sonnet",
            McpConfigPath = "/path/to/mcp-config.json"
        };

        // Assert
        definition.McpConfigPath.Should().Be("/path/to/mcp-config.json");
    }

    [Fact]
    public void AgentDefinition_McpConfigPath_CanBeNull()
    {
        // Arrange & Act
        var definition = new AgentDefinition
        {
            Name = "coder",
            Version = "1.0.0",
            Description = "Coding agent",
            SystemPrompt = "You are a coder",
            Model = "opus"
        };

        // Assert
        definition.McpConfigPath.Should().BeNull();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void AgentDefinition_WithVeryLongSystemPrompt_StoresCorrectly()
    {
        // Arrange
        var longPrompt = new string('A', 100000); // 100K characters

        // Act
        var definition = new AgentDefinition
        {
            Name = "tester",
            Version = "2.0.0",
            Description = "Testing agent",
            SystemPrompt = longPrompt,
            Model = "haiku"
        };

        // Assert
        definition.SystemPrompt.Should().HaveLength(100000);
        definition.SystemPrompt.Should().Be(longPrompt);
    }

    [Fact]
    public void AgentDefinition_WithMultilineSystemPrompt_PreservesFormatting()
    {
        // Arrange
        var multilinePrompt = "Line 1\nLine 2\n\nLine 4 (with blank line above)";

        // Act
        var definition = new AgentDefinition
        {
            Name = "reviewer",
            Version = "1.5.0",
            Description = "Review agent",
            SystemPrompt = multilinePrompt,
            Model = "sonnet"
        };

        // Assert
        definition.SystemPrompt.Should().Contain("\n");
        definition.SystemPrompt.Should().Contain("Line 1");
        definition.SystemPrompt.Should().Contain("Line 4");
    }

    [Fact]
    public void AgentDefinition_WithWindowsPath_McpConfigPath_StoresCorrectly()
    {
        // Arrange & Act
        var definition = new AgentDefinition
        {
            Name = "evaluator",
            Version = "1.0.0",
            Description = "Evaluation agent",
            SystemPrompt = "You evaluate code",
            Model = "opus",
            McpConfigPath = "C:\\Path\\To\\Config\\mcp-config.json"
        };

        // Assert
        definition.McpConfigPath.Should().Contain("\\");
        definition.McpConfigPath.Should().StartWith("C:");
    }

    [Fact]
    public void AgentDefinition_WithUnixPath_McpConfigPath_StoresCorrectly()
    {
        // Arrange & Act
        var definition = new AgentDefinition
        {
            Name = "planner",
            Version = "1.0.0",
            Description = "Planning agent",
            SystemPrompt = "You are a planner",
            Model = "sonnet",
            McpConfigPath = "/usr/local/share/mcp-config.json"
        };

        // Assert
        definition.McpConfigPath.Should().StartWith("/");
        definition.McpConfigPath.Should().Contain("/usr/local/");
    }

    [Fact]
    public void AgentDefinition_WithRelativePath_McpConfigPath_StoresCorrectly()
    {
        // Arrange & Act
        var definition = new AgentDefinition
        {
            Name = "coder",
            Version = "1.0.0",
            Description = "Coding agent",
            SystemPrompt = "You write code",
            Model = "sonnet",
            McpConfigPath = "../../config/mcp-planner.json"
        };

        // Assert
        definition.McpConfigPath.Should().StartWith("..");
        definition.McpConfigPath.Should().Contain("config");
    }

    #endregion

    #region Model Alias Tests

    [Theory]
    [InlineData("sonnet")]
    [InlineData("opus")]
    [InlineData("haiku")]
    public void AgentDefinition_WithValidModelAlias_StoresCorrectly(string modelAlias)
    {
        // Arrange & Act
        var definition = new AgentDefinition
        {
            Name = "test-agent",
            Version = "1.0.0",
            Description = "Test agent",
            SystemPrompt = "Test prompt",
            Model = modelAlias
        };

        // Assert
        definition.Model.Should().Be(modelAlias);
    }

    [Fact]
    public void AgentDefinition_WithCustomModelName_StoresCorrectly()
    {
        // Arrange & Act
        var definition = new AgentDefinition
        {
            Name = "custom-agent",
            Version = "1.0.0",
            Description = "Custom agent",
            SystemPrompt = "Custom prompt",
            Model = "claude-3-5-sonnet-20241022"
        };

        // Assert
        definition.Model.Should().Be("claude-3-5-sonnet-20241022");
    }

    #endregion

    #region Version String Tests

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("2.5.3")]
    [InlineData("1.0.0-beta.1")]
    [InlineData("3.0.0-rc.2+build.123")]
    public void AgentDefinition_WithVariousVersionFormats_StoresCorrectly(string version)
    {
        // Arrange & Act
        var definition = new AgentDefinition
        {
            Name = "versioned-agent",
            Version = version,
            Description = "Agent with version",
            SystemPrompt = "Version test",
            Model = "sonnet"
        };

        // Assert
        definition.Version.Should().Be(version);
    }

    #endregion

    #region Agent Name Tests

    [Theory]
    [InlineData("planner")]
    [InlineData("coder")]
    [InlineData("reviewer")]
    [InlineData("tester")]
    [InlineData("evaluator")]
    public void AgentDefinition_WithStandardAgentNames_StoresCorrectly(string agentName)
    {
        // Arrange & Act
        var definition = new AgentDefinition
        {
            Name = agentName,
            Version = "1.0.0",
            Description = $"{agentName} agent",
            SystemPrompt = $"You are a {agentName}",
            Model = "sonnet"
        };

        // Assert
        definition.Name.Should().Be(agentName);
    }

    [Fact]
    public void AgentDefinition_WithCustomAgentName_StoresCorrectly()
    {
        // Arrange & Act
        var definition = new AgentDefinition
        {
            Name = "custom-security-scanner",
            Version = "1.0.0",
            Description = "Security scanning agent",
            SystemPrompt = "You scan for security issues",
            Model = "opus"
        };

        // Assert
        definition.Name.Should().Be("custom-security-scanner");
    }

    #endregion

    #region Description Tests

    [Fact]
    public void AgentDefinition_WithLongDescription_StoresCorrectly()
    {
        // Arrange
        var longDescription = "This is a very detailed description of an agent that performs complex tasks " +
                              "including code generation, review, testing, and evaluation. It supports multiple " +
                              "programming languages and can integrate with various tools and services.";

        // Act
        var definition = new AgentDefinition
        {
            Name = "multi-purpose",
            Version = "1.0.0",
            Description = longDescription,
            SystemPrompt = "Multi-purpose agent",
            Model = "opus"
        };

        // Assert
        definition.Description.Should().Be(longDescription);
        definition.Description.Should().Contain("complex tasks");
    }

    [Fact]
    public void AgentDefinition_WithShortDescription_StoresCorrectly()
    {
        // Arrange & Act
        var definition = new AgentDefinition
        {
            Name = "simple",
            Version = "1.0.0",
            Description = "Simple agent",
            SystemPrompt = "You are simple",
            Model = "haiku"
        };

        // Assert
        definition.Description.Should().Be("Simple agent");
    }

    #endregion

    #region Property Comparison Tests

    [Fact]
    public void AgentDefinition_WithSameValues_HaveMatchingProperties()
    {
        // Arrange
        var definition1 = new AgentDefinition
        {
            Name = "planner",
            Version = "1.0.0",
            Description = "Planning agent",
            SystemPrompt = "You are a planner",
            Model = "sonnet",
            McpConfigPath = "/path/to/config.json"
        };

        var definition2 = new AgentDefinition
        {
            Name = "planner",
            Version = "1.0.0",
            Description = "Planning agent",
            SystemPrompt = "You are a planner",
            Model = "sonnet",
            McpConfigPath = "/path/to/config.json"
        };

        // Assert - Properties match, but reference equality is false (class not record)
        definition1.Name.Should().Be(definition2.Name);
        definition1.Version.Should().Be(definition2.Version);
        definition1.Description.Should().Be(definition2.Description);
        definition1.SystemPrompt.Should().Be(definition2.SystemPrompt);
        definition1.Model.Should().Be(definition2.Model);
        definition1.McpConfigPath.Should().Be(definition2.McpConfigPath);

        // Reference equality is false for classes
        definition1.Should().NotBeSameAs(definition2);
    }

    [Fact]
    public void AgentDefinition_WithDifferentNames_HaveDifferentProperties()
    {
        // Arrange
        var definition1 = new AgentDefinition
        {
            Name = "planner",
            Version = "1.0.0",
            Description = "Agent",
            SystemPrompt = "Prompt",
            Model = "sonnet"
        };

        var definition2 = new AgentDefinition
        {
            Name = "coder",
            Version = "1.0.0",
            Description = "Agent",
            SystemPrompt = "Prompt",
            Model = "sonnet"
        };

        // Assert - Names differ
        definition1.Name.Should().NotBe(definition2.Name);
        definition1.Name.Should().Be("planner");
        definition2.Name.Should().Be("coder");
    }

    [Fact]
    public void AgentDefinition_WithDifferentMcpConfigPath_HaveDifferentPaths()
    {
        // Arrange
        var definition1 = new AgentDefinition
        {
            Name = "planner",
            Version = "1.0.0",
            Description = "Agent",
            SystemPrompt = "Prompt",
            Model = "sonnet",
            McpConfigPath = "/path/1.json"
        };

        var definition2 = new AgentDefinition
        {
            Name = "planner",
            Version = "1.0.0",
            Description = "Agent",
            SystemPrompt = "Prompt",
            Model = "sonnet",
            McpConfigPath = "/path/2.json"
        };

        // Assert - McpConfigPath differs
        definition1.McpConfigPath.Should().NotBe(definition2.McpConfigPath);
        definition1.McpConfigPath.Should().Be("/path/1.json");
        definition2.McpConfigPath.Should().Be("/path/2.json");
    }

    [Fact]
    public void AgentDefinition_OneWithMcpPathOneWithout_HaveDifferentPaths()
    {
        // Arrange
        var definition1 = new AgentDefinition
        {
            Name = "planner",
            Version = "1.0.0",
            Description = "Agent",
            SystemPrompt = "Prompt",
            Model = "sonnet",
            McpConfigPath = "/path/config.json"
        };

        var definition2 = new AgentDefinition
        {
            Name = "planner",
            Version = "1.0.0",
            Description = "Agent",
            SystemPrompt = "Prompt",
            Model = "sonnet"
        };

        // Assert - One has path, one is null
        definition1.McpConfigPath.Should().NotBeNull();
        definition2.McpConfigPath.Should().BeNull();
        definition1.McpConfigPath.Should().Be("/path/config.json");
    }

    #endregion

    #region Properties Are Init-Only Tests

    [Fact]
    public void AgentDefinition_Properties_AreInitOnly_CannotBeModifiedAfterConstruction()
    {
        // Arrange
        var definition = new AgentDefinition
        {
            Name = "planner",
            Version = "1.0.0",
            Description = "Planning agent",
            SystemPrompt = "You are a planner",
            Model = "sonnet"
        };

        // Assert - Compiler enforces init-only, so we just verify they were set
        definition.Name.Should().Be("planner");
        definition.Version.Should().Be("1.0.0");
        definition.Description.Should().Be("Planning agent");
        definition.SystemPrompt.Should().Be("You are a planner");
        definition.Model.Should().Be("sonnet");
    }

    #endregion
}
