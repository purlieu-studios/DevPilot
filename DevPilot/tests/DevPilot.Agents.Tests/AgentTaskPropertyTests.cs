using DevPilot.Core;
using FluentAssertions;
using TaskStatus = DevPilot.Core.TaskStatus;

namespace DevPilot.Agents.Tests;

public sealed class AgentTaskPropertyTests
{
    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = "task-1",
            Description = "Test task",
            AgentName = "test-agent"
        };

        // Assert
        task.TaskId.Should().Be("task-1");
        task.Description.Should().Be("Test task");
        task.AgentName.Should().Be("test-agent");
    }

    [Fact]
    public void Constructor_DefaultsStatusToPending()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = "task-1",
            Description = "Test task",
            AgentName = "test-agent"
        };

        // Assert
        task.Status.Should().Be(TaskStatus.Pending);
    }

    [Fact]
    public void Constructor_DefaultsDependenciesToEmptyArray()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = "task-1",
            Description = "Test task",
            AgentName = "test-agent"
        };

        // Assert
        task.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_SetsCreatedAtAutomatically()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var task = new AgentTask
        {
            TaskId = "task-1",
            Description = "Test task",
            AgentName = "test-agent"
        };

        var after = DateTimeOffset.UtcNow;

        // Assert
        task.CreatedAt.Should().BeOnOrAfter(before);
        task.CreatedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Constructor_InitializesNullablePropertiesAsNull()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = "task-1",
            Description = "Test task",
            AgentName = "test-agent"
        };

        // Assert
        task.Context.Should().BeNull();
        task.Result.Should().BeNull();
        task.StartedAt.Should().BeNull();
        task.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Context_CanBeSetWithDictionary()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = "task-1",
            Description = "Test task",
            AgentName = "test-agent",
            Context = new Dictionary<string, object>
            {
                ["key1"] = "value1",
                ["key2"] = 42
            }
        };

        // Assert
        task.Context.Should().NotBeNull();
        task.Context!["key1"].Should().Be("value1");
        task.Context["key2"].Should().Be(42);
    }

    [Fact]
    public void Dependencies_CanBeSetWithList()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = "task-1",
            Description = "Test task",
            AgentName = "test-agent",
            Dependencies = new[] { "task-0", "task-00" }
        };

        // Assert
        task.Dependencies.Should().HaveCount(2);
        task.Dependencies.Should().Contain("task-0");
        task.Dependencies.Should().Contain("task-00");
    }

    [Fact]
    public void Status_CanBeChanged()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = "task-1",
            Description = "Test task",
            AgentName = "test-agent"
        };

        // Act
        task.Status = TaskStatus.InProgress;

        // Assert
        task.Status.Should().Be(TaskStatus.InProgress);
    }

    [Fact]
    public void StartedAt_CanBeSet()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = "task-1",
            Description = "Test task",
            AgentName = "test-agent"
        };

        var startTime = DateTimeOffset.UtcNow;

        // Act
        task.StartedAt = startTime;

        // Assert
        task.StartedAt.Should().Be(startTime);
    }

    [Fact]
    public void CompletedAt_CanBeSet()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = "task-1",
            Description = "Test task",
            AgentName = "test-agent"
        };

        var completionTime = DateTimeOffset.UtcNow;

        // Act
        task.CompletedAt = completionTime;

        // Assert
        task.CompletedAt.Should().Be(completionTime);
    }

    [Fact]
    public void Result_CanBeSet()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = "task-1",
            Description = "Test task",
            AgentName = "test-agent"
        };

        var result = AgentResult.CreateSuccess("test-agent", "Success output");

        // Act
        task.Result = result;

        // Assert
        task.Result.Should().Be(result);
        task.Result!.Success.Should().BeTrue();
        task.Result.Output.Should().Be("Success output");
    }

    [Fact]
    public void TaskStatus_HasExpectedValues()
    {
        // Assert
        TaskStatus.Pending.Should().Be(TaskStatus.Pending);
        TaskStatus.InProgress.Should().Be(TaskStatus.InProgress);
        TaskStatus.Completed.Should().Be(TaskStatus.Completed);
        TaskStatus.Failed.Should().Be(TaskStatus.Failed);
        TaskStatus.Cancelled.Should().Be(TaskStatus.Cancelled);
    }
}
