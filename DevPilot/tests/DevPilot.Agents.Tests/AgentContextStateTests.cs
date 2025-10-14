using DevPilot.Core;
using FluentAssertions;

namespace DevPilot.Agents.Tests;

public sealed class AgentContextStateTests
{
    [Fact]
    public void Constructor_GeneratesUniqueContextId()
    {
        // Arrange & Act
        var context1 = new AgentContext();
        var context2 = new AgentContext();

        // Assert
        context1.ContextId.Should().NotBeNullOrWhiteSpace();
        context2.ContextId.Should().NotBeNullOrWhiteSpace();
        context1.ContextId.Should().NotBe(context2.ContextId);
    }

    [Fact]
    public void SetValue_And_GetValue_WorksWithString()
    {
        // Arrange
        var context = new AgentContext();
        const string key = "test-key";
        const string value = "test-value";

        // Act
        context.SetValue(key, value);
        var retrieved = context.GetValue<string>(key);

        // Assert
        retrieved.Should().Be(value);
    }

    [Fact]
    public void SetValue_And_GetValue_WorksWithComplexType()
    {
        // Arrange
        var context = new AgentContext();
        const string key = "complex-key";
        var value = new TestData { Id = 42, Name = "Test" };

        // Act
        context.SetValue(key, value);
        var retrieved = context.GetValue<TestData>(key);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(42);
        retrieved.Name.Should().Be("Test");
    }

    [Fact]
    public void GetValue_ReturnsDefault_WhenKeyDoesNotExist()
    {
        // Arrange
        var context = new AgentContext();

        // Act
        var retrieved = context.GetValue<string>("non-existent-key");

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public void ContainsKey_ReturnsTrue_WhenKeyExists()
    {
        // Arrange
        var context = new AgentContext();
        const string key = "existing-key";
        context.SetValue(key, "value");

        // Act
        var exists = context.ContainsKey(key);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public void ContainsKey_ReturnsFalse_WhenKeyDoesNotExist()
    {
        // Arrange
        var context = new AgentContext();

        // Act
        var exists = context.ContainsKey("non-existent-key");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void ClearState_RemovesAllValues()
    {
        // Arrange
        var context = new AgentContext();
        context.SetValue("key1", "value1");
        context.SetValue("key2", "value2");

        // Act
        context.ClearState();

        // Assert
        context.ContainsKey("key1").Should().BeFalse();
        context.ContainsKey("key2").Should().BeFalse();
    }

    [Fact]
    public void SetValue_ThrowsArgumentException_WhenKeyIsEmpty()
    {
        // Arrange
        var context = new AgentContext();

        // Act
        var act = () => context.SetValue("", "value");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetValue_ThrowsArgumentException_WhenKeyIsWhitespace()
    {
        // Arrange
        var context = new AgentContext();

        // Act
        var act = () => context.GetValue<string>("   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // Helper class for complex type testing
    private sealed class TestData
    {
        public int Id { get; init; }
        public string? Name { get; init; }
    }
}
