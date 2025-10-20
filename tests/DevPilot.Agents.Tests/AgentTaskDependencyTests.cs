using DevPilot.Core;
using FluentAssertions;
using TaskStatus = DevPilot.Core.TaskStatus;

namespace DevPilot.Agents.Tests;

public sealed class AgentTaskDependencyTests
{
    [Fact]
    public void AreDependenciesMet_ReturnsTrue_WhenNoDependencies()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = "task-1",
            Description = "Test task",
            AgentName = "test-agent"
        };

        var completedTasks = new HashSet<string>();

        // Act
        var result = task.AreDependenciesMet(completedTasks);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreDependenciesMet_ReturnsTrue_WhenAllDependenciesMet()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = "task-3",
            Description = "Test task",
            AgentName = "test-agent",
            Dependencies = new[] { "task-1", "task-2" }
        };

        var completedTasks = new HashSet<string> { "task-1", "task-2" };

        // Act
        var result = task.AreDependenciesMet(completedTasks);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreDependenciesMet_ReturnsFalse_WhenSomeDependenciesNotMet()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = "task-3",
            Description = "Test task",
            AgentName = "test-agent",
            Dependencies = new[] { "task-1", "task-2" }
        };

        var completedTasks = new HashSet<string> { "task-1" };

        // Act
        var result = task.AreDependenciesMet(completedTasks);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AreDependenciesMet_ReturnsFalse_WhenNoDependenciesMet()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = "task-3",
            Description = "Test task",
            AgentName = "test-agent",
            Dependencies = new[] { "task-1", "task-2" }
        };

        var completedTasks = new HashSet<string>();

        // Act
        var result = task.AreDependenciesMet(completedTasks);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AreDependenciesMet_ThrowsArgumentNullException_WhenCompletedTasksIsNull()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = "task-1",
            Description = "Test task",
            AgentName = "test-agent"
        };

        // Act
        var act = () => task.AreDependenciesMet(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FullWorkflow_TaskLifecycle()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = "workflow-task",
            Description = "Complete workflow test",
            AgentName = "workflow-agent",
            Dependencies = new[] { "dep-1" }
        };

        var completedTasks = new HashSet<string>();

        // Act & Assert - Initial state
        task.Status.Should().Be(TaskStatus.Pending);
        task.AreDependenciesMet(completedTasks).Should().BeFalse();
        task.StartedAt.Should().BeNull();
        task.CompletedAt.Should().BeNull();
        task.Result.Should().BeNull();

        // Dependency completes
        completedTasks.Add("dep-1");
        task.AreDependenciesMet(completedTasks).Should().BeTrue();

        // Task starts
        task.Status = TaskStatus.InProgress;
        task.StartedAt = DateTimeOffset.UtcNow;
        task.Status.Should().Be(TaskStatus.InProgress);
        task.StartedAt.Should().NotBeNull();

        // Task completes
        task.Status = TaskStatus.Completed;
        task.CompletedAt = DateTimeOffset.UtcNow;
        task.Result = AgentResult.CreateSuccess("workflow-agent", "Task completed successfully");

        task.Status.Should().Be(TaskStatus.Completed);
        task.CompletedAt.Should().NotBeNull();
        task.Result.Should().NotBeNull();
        task.Result!.Success.Should().BeTrue();
    }
}
