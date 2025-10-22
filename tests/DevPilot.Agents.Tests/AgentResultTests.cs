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

    #region Agent Name Edge Cases

    [Fact]
    public void AgentResult_WithVeryLongAgentName_StoresCorrectly()
    {
        // Arrange
        var longName = new string('a', 1000);

        // Act
        var result = AgentResult.CreateSuccess(longName, "Output");

        // Assert
        result.AgentName.Should().HaveLength(1000);
        result.AgentName.Should().Be(longName);
    }

    [Fact]
    public void AgentResult_WithSpecialCharactersInAgentName_StoresCorrectly()
    {
        // Arrange & Act
        var result = AgentResult.CreateSuccess("agent-name_with.special@chars#123", "Output");

        // Assert
        result.AgentName.Should().Be("agent-name_with.special@chars#123");
    }

    [Fact]
    public void AgentResult_WithUnicodeAgentName_StoresCorrectly()
    {
        // Arrange & Act
        var result = AgentResult.CreateSuccess("agent-名称-агент", "Output");

        // Assert
        result.AgentName.Should().Be("agent-名称-агент");
    }

    #endregion

    #region Output Edge Cases

    [Fact]
    public void AgentResult_WithEmptyOutput_StoresEmptyString()
    {
        // Arrange & Act
        var result = AgentResult.CreateSuccess("test-agent", string.Empty);

        // Assert
        result.Output.Should().BeEmpty();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void AgentResult_WithMultilineOutput_PreservesFormatting()
    {
        // Arrange
        var multilineOutput = "Line 1\nLine 2\n\nLine 4 (with blank line above)";

        // Act
        var result = AgentResult.CreateSuccess("test-agent", multilineOutput);

        // Assert
        result.Output.Should().Contain("\n");
        result.Output.Should().Contain("Line 1");
        result.Output.Should().Contain("Line 4");
    }

    [Fact]
    public void AgentResult_WithJsonOutput_PreservesStructure()
    {
        // Arrange
        var jsonOutput = "{\"key\": \"value\", \"nested\": {\"data\": 123}}";

        // Act
        var result = AgentResult.CreateSuccess("test-agent", jsonOutput);

        // Assert
        result.Output.Should().Contain("key");
        result.Output.Should().Contain("nested");
        result.Output.Should().Be(jsonOutput);
    }

    #endregion

    #region Error Message Edge Cases

    [Fact]
    public void AgentResult_WithMultilineErrorMessage_PreservesFormatting()
    {
        // Arrange
        var multilineError = "Error on line 1\nError on line 2\nStack trace here";

        // Act
        var result = AgentResult.CreateFailure("test-agent", multilineError);

        // Assert
        result.ErrorMessage.Should().Contain("\n");
        result.ErrorMessage.Should().Contain("Error on line 1");
        result.ErrorMessage.Should().Contain("Stack trace here");
    }

    [Fact]
    public void AgentResult_WithStructuredErrorMessage_PreservesFormat()
    {
        // Arrange
        var structuredError = "Error Code: CS1001\nFile: Program.cs\nLine: 42\nMessage: Identifier expected";

        // Act
        var result = AgentResult.CreateFailure("test-agent", structuredError);

        // Assert
        result.ErrorMessage.Should().Contain("CS1001");
        result.ErrorMessage.Should().Contain("Program.cs");
        result.ErrorMessage.Should().Contain("Line: 42");
    }

    #endregion

    #region Duration Edge Cases

    [Fact]
    public void AgentResult_WithZeroDuration_StoresZero()
    {
        // Arrange & Act
        var result = AgentResult.CreateSuccess("test-agent", "Output", TimeSpan.Zero);

        // Assert
        result.Duration.Should().Be(TimeSpan.Zero);
        result.Duration!.Value.TotalMilliseconds.Should().Be(0);
    }

    [Fact]
    public void AgentResult_WithVeryLongDuration_StoresCorrectly()
    {
        // Arrange
        var longDuration = TimeSpan.FromHours(24); // 1 day

        // Act
        var result = AgentResult.CreateSuccess("test-agent", "Output", longDuration);

        // Assert
        result.Duration.Should().Be(TimeSpan.FromHours(24));
        result.Duration!.Value.TotalDays.Should().Be(1);
    }

    [Fact]
    public void AgentResult_WithMillisecondPrecision_StoresExactValue()
    {
        // Arrange
        var preciseDuration = TimeSpan.FromMilliseconds(123.456);

        // Act
        var result = AgentResult.CreateSuccess("test-agent", "Output", preciseDuration);

        // Assert
        result.Duration.Should().Be(preciseDuration);
    }

    #endregion

    #region Metadata Edge Cases

    [Fact]
    public void AgentResult_WithEmptyMetadata_StoresEmptyDictionary()
    {
        // Arrange & Act
        var result = new AgentResult
        {
            Success = true,
            AgentName = "test-agent",
            Output = "Output",
            Metadata = new Dictionary<string, object>()
        };

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void AgentResult_WithMixedMetadataTypes_StoresAllTypes()
    {
        // Arrange & Act
        var result = new AgentResult
        {
            Success = true,
            AgentName = "test-agent",
            Output = "Output",
            Metadata = new Dictionary<string, object>
            {
                ["string"] = "value",
                ["int"] = 42,
                ["double"] = 3.14,
                ["bool"] = true,
                ["datetime"] = DateTime.Parse("2025-01-01"),
                ["list"] = new List<int> { 1, 2, 3 }
            }
        };

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata!["string"].Should().Be("value");
        result.Metadata["int"].Should().Be(42);
        result.Metadata["double"].Should().Be(3.14);
        result.Metadata["bool"].Should().Be(true);
        result.Metadata["datetime"].Should().Be(DateTime.Parse("2025-01-01"));
        result.Metadata["list"].Should().BeEquivalentTo(new List<int> { 1, 2, 3 });
    }

    [Fact]
    public void AgentResult_WithLargeMetadata_StoresAllEntries()
    {
        // Arrange
        var metadata = new Dictionary<string, object>();
        for (int i = 0; i < 100; i++)
        {
            metadata[$"key{i}"] = $"value{i}";
        }

        // Act
        var result = new AgentResult
        {
            Success = true,
            AgentName = "test-agent",
            Output = "Output",
            Metadata = metadata
        };

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().HaveCount(100);
        result.Metadata!["key0"].Should().Be("value0");
        result.Metadata["key99"].Should().Be("value99");
    }

    [Fact]
    public void AgentResult_WithNestedMetadata_StoresComplexObjects()
    {
        // Arrange & Act
        var result = new AgentResult
        {
            Success = true,
            AgentName = "test-agent",
            Output = "Output",
            Metadata = new Dictionary<string, object>
            {
                ["config"] = new Dictionary<string, object>
                {
                    ["timeout"] = 5000,
                    ["retries"] = 3
                }
            }
        };

        // Assert
        result.Metadata.Should().NotBeNull();
        var config = result.Metadata!["config"] as Dictionary<string, object>;
        config.Should().NotBeNull();
        config!["timeout"].Should().Be(5000);
        config["retries"].Should().Be(3);
    }

    #endregion

    #region Combined Success and Failure Scenarios

    [Fact]
    public void AgentResult_SuccessWithoutOutput_IsValid()
    {
        // Arrange & Act
        var result = AgentResult.CreateSuccess("test-agent", string.Empty);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().BeEmpty();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void AgentResult_FailureWithDurationAndMetadata_IsValid()
    {
        // Arrange & Act
        var result = new AgentResult
        {
            Success = false,
            AgentName = "test-agent",
            Output = string.Empty,
            ErrorMessage = "Execution failed",
            Duration = TimeSpan.FromSeconds(1.5),
            Metadata = new Dictionary<string, object>
            {
                ["attemptCount"] = 3,
                ["lastError"] = "Timeout"
            }
        };

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Execution failed");
        result.Duration.Should().Be(TimeSpan.FromSeconds(1.5));
        result.Metadata.Should().NotBeNull();
        result.Metadata!["attemptCount"].Should().Be(3);
    }

    [Fact]
    public void AgentResult_SuccessWithAllOptionalFields_IsValid()
    {
        // Arrange & Act
        var result = new AgentResult
        {
            Success = true,
            AgentName = "comprehensive-agent",
            Output = "Complete output",
            Duration = TimeSpan.FromSeconds(2),
            Metadata = new Dictionary<string, object>
            {
                ["version"] = "1.0.0",
                ["model"] = "sonnet"
            }
        };

        // Assert
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("comprehensive-agent");
        result.Output.Should().Be("Complete output");
        result.ErrorMessage.Should().BeNull();
        result.Duration.Should().Be(TimeSpan.FromSeconds(2));
        result.Metadata.Should().NotBeNull();
        result.Metadata!.Should().HaveCount(2);
    }

    #endregion

    #region Property Comparison Tests

    [Fact]
    public void AgentResult_TwoSuccessResultsWithSameValues_HaveMatchingProperties()
    {
        // Arrange
        var result1 = AgentResult.CreateSuccess("test-agent", "Output", TimeSpan.FromSeconds(1));
        var result2 = AgentResult.CreateSuccess("test-agent", "Output", TimeSpan.FromSeconds(1));

        // Assert - Properties match, but reference equality is false (class not record)
        result1.Success.Should().Be(result2.Success);
        result1.AgentName.Should().Be(result2.AgentName);
        result1.Output.Should().Be(result2.Output);
        result1.Duration.Should().Be(result2.Duration);

        result1.Should().NotBeSameAs(result2);
    }

    [Fact]
    public void AgentResult_TwoFailureResultsWithSameValues_HaveMatchingProperties()
    {
        // Arrange
        var result1 = AgentResult.CreateFailure("test-agent", "Error", TimeSpan.FromSeconds(1));
        var result2 = AgentResult.CreateFailure("test-agent", "Error", TimeSpan.FromSeconds(1));

        // Assert - Properties match, but reference equality is false (class not record)
        result1.Success.Should().Be(result2.Success);
        result1.AgentName.Should().Be(result2.AgentName);
        result1.ErrorMessage.Should().Be(result2.ErrorMessage);
        result1.Duration.Should().Be(result2.Duration);

        result1.Should().NotBeSameAs(result2);
    }

    #endregion

    #region Init-Only Properties

    [Fact]
    public void AgentResult_PropertiesAreInitOnly_CannotBeModifiedAfterConstruction()
    {
        // Arrange & Act
        var result = new AgentResult
        {
            Success = true,
            AgentName = "test-agent",
            Output = "Test output",
            ErrorMessage = "No error",
            Duration = TimeSpan.FromSeconds(1)
        };

        // Assert - Compiler enforces init-only, so we just verify they were set
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("test-agent");
        result.Output.Should().Be("Test output");
        result.ErrorMessage.Should().Be("No error");
        result.Duration.Should().Be(TimeSpan.FromSeconds(1));
    }

    #endregion
}
