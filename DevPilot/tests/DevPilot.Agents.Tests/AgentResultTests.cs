using DevPilot.Core;
using FluentAssertions;

namespace DevPilot.Agents.Tests;

public sealed class AgentResultTests
{
    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        // Arrange & Act
        var result = new AgentResult
        {
            Success = true,
            AgentName = "test-agent",
            Output = "Test output"
        };

        // Assert
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("test-agent");
        result.Output.Should().Be("Test output");
    }

    [Fact]
    public void Constructor_InitializesOptionalPropertiesAsNull()
    {
        // Arrange & Act
        var result = new AgentResult
        {
            Success = true,
            AgentName = "test-agent",
            Output = "Test output"
        };

        // Assert
        result.ErrorMessage.Should().BeNull();
        result.Metadata.Should().BeNull();
        result.Duration.Should().BeNull();
    }

    [Fact]
    public void ErrorMessage_CanBeSet()
    {
        // Arrange & Act
        var result = new AgentResult
        {
            Success = false,
            AgentName = "test-agent",
            Output = string.Empty,
            ErrorMessage = "Test error"
        };

        // Assert
        result.ErrorMessage.Should().Be("Test error");
    }

    [Fact]
    public void Metadata_CanBeSetWithDictionary()
    {
        // Arrange & Act
        var result = new AgentResult
        {
            Success = true,
            AgentName = "test-agent",
            Output = "Test output",
            Metadata = new Dictionary<string, object>
            {
                ["model"] = "claude-sonnet-4-5",
                ["tokens"] = 1000
            }
        };

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata!["model"].Should().Be("claude-sonnet-4-5");
        result.Metadata["tokens"].Should().Be(1000);
    }

    [Fact]
    public void Duration_CanBeSet()
    {
        // Arrange & Act
        var duration = TimeSpan.FromSeconds(5);
        var result = new AgentResult
        {
            Success = true,
            AgentName = "test-agent",
            Output = "Test output",
            Duration = duration
        };

        // Assert
        result.Duration.Should().Be(duration);
    }

    [Fact]
    public void CreateSuccess_CreatesSuccessfulResult()
    {
        // Arrange & Act
        var result = AgentResult.CreateSuccess("test-agent", "Success output");

        // Assert
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("test-agent");
        result.Output.Should().Be("Success output");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void CreateSuccess_WithDuration_SetsDuration()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(250);

        // Act
        var result = AgentResult.CreateSuccess("test-agent", "Success output", duration);

        // Assert
        result.Success.Should().BeTrue();
        result.Duration.Should().Be(duration);
    }

    [Fact]
    public void CreateSuccess_WithoutDuration_DurationIsNull()
    {
        // Arrange & Act
        var result = AgentResult.CreateSuccess("test-agent", "Success output");

        // Assert
        result.Duration.Should().BeNull();
    }

    [Fact]
    public void CreateFailure_CreatesFailedResult()
    {
        // Arrange & Act
        var result = AgentResult.CreateFailure("test-agent", "Error occurred");

        // Assert
        result.Success.Should().BeFalse();
        result.AgentName.Should().Be("test-agent");
        result.ErrorMessage.Should().Be("Error occurred");
        result.Output.Should().BeEmpty();
    }

    [Fact]
    public void CreateFailure_WithDuration_SetsDuration()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        var result = AgentResult.CreateFailure("test-agent", "Error occurred", duration);

        // Assert
        result.Success.Should().BeFalse();
        result.Duration.Should().Be(duration);
    }

    [Fact]
    public void CreateFailure_WithoutDuration_DurationIsNull()
    {
        // Arrange & Act
        var result = AgentResult.CreateFailure("test-agent", "Error occurred");

        // Assert
        result.Duration.Should().BeNull();
    }

    [Fact]
    public void SuccessResult_HasNoErrorMessage()
    {
        // Arrange & Act
        var result = AgentResult.CreateSuccess("test-agent", "Output");

        // Assert
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void FailureResult_HasEmptyOutput()
    {
        // Arrange & Act
        var result = AgentResult.CreateFailure("test-agent", "Error");

        // Assert
        result.Output.Should().BeEmpty();
    }

    [Fact]
    public void SuccessResult_WithMetadata()
    {
        // Arrange & Act
        var result = new AgentResult
        {
            Success = true,
            AgentName = "code-generator",
            Output = "Generated code",
            Metadata = new Dictionary<string, object>
            {
                ["linesOfCode"] = 150,
                ["complexity"] = "low"
            }
        };

        // Assert
        result.Success.Should().BeTrue();
        result.Metadata.Should().NotBeNull();
        result.Metadata!["linesOfCode"].Should().Be(150);
        result.Metadata["complexity"].Should().Be("low");
    }

    [Fact]
    public void FailureResult_WithMetadata()
    {
        // Arrange & Act
        var result = new AgentResult
        {
            Success = false,
            AgentName = "validator",
            Output = string.Empty,
            ErrorMessage = "Validation failed",
            Metadata = new Dictionary<string, object>
            {
                ["errorCount"] = 5,
                ["warningCount"] = 2
            }
        };

        // Assert
        result.Success.Should().BeFalse();
        result.Metadata.Should().NotBeNull();
        result.Metadata!["errorCount"].Should().Be(5);
        result.Metadata["warningCount"].Should().Be(2);
    }

    [Fact]
    public void CreateSuccess_WithLongOutput()
    {
        // Arrange
        var longOutput = new string('x', 10000);

        // Act
        var result = AgentResult.CreateSuccess("test-agent", longOutput);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().HaveLength(10000);
    }

    [Fact]
    public void CreateFailure_WithLongErrorMessage()
    {
        // Arrange
        var longError = new string('e', 5000);

        // Act
        var result = AgentResult.CreateFailure("test-agent", longError);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().HaveLength(5000);
    }

    [Fact]
    public void Duration_ReflectsActualExecutionTime()
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(2.5);

        // Act
        var result = AgentResult.CreateSuccess("test-agent", "Output", duration);

        // Assert
        result.Duration.Should().Be(TimeSpan.FromSeconds(2.5));
        result.Duration!.Value.TotalMilliseconds.Should().Be(2500);
    }
}
