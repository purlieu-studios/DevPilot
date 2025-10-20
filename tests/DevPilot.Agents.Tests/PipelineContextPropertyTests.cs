using DevPilot.Core;
using FluentAssertions;

namespace DevPilot.Agents.Tests;

public sealed class PipelineContextPropertyTests
{
    [Fact]
    public void Constructor_GeneratesUniquePipelineId()
    {
        // Arrange & Act
        var context1 = new PipelineContext { UserRequest = "Request 1" };
        var context2 = new PipelineContext { UserRequest = "Request 2" };

        // Assert
        context1.PipelineId.Should().NotBeNullOrWhiteSpace();
        context2.PipelineId.Should().NotBeNullOrWhiteSpace();
        context1.PipelineId.Should().NotBe(context2.PipelineId);
    }

    [Fact]
    public void Constructor_InitializesCurrentStageAsNotStarted()
    {
        // Arrange & Act
        var context = new PipelineContext { UserRequest = "Test request" };

        // Assert
        context.CurrentStage.Should().Be(PipelineStage.NotStarted);
    }

    [Fact]
    public void Constructor_SetsUserRequest()
    {
        // Arrange & Act
        var context = new PipelineContext { UserRequest = "Create a calculator" };

        // Assert
        context.UserRequest.Should().Be("Create a calculator");
    }

    [Fact]
    public void Constructor_InitializesNullablePropertiesAsNull()
    {
        // Arrange & Act
        var context = new PipelineContext { UserRequest = "Test" };

        // Assert
        context.Plan.Should().BeNull();
        context.Patch.Should().BeNull();
        context.Review.Should().BeNull();
        context.TestReport.Should().BeNull();
        context.Scores.Should().BeNull();
        context.ApprovalReason.Should().BeNull();
        context.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_InitializesApprovalRequiredAsFalse()
    {
        // Arrange & Act
        var context = new PipelineContext { UserRequest = "Test" };

        // Assert
        context.ApprovalRequired.Should().BeFalse();
    }

    [Fact]
    public void Constructor_SetsStartedAtAutomatically()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var context = new PipelineContext { UserRequest = "Test" };

        var after = DateTimeOffset.UtcNow;

        // Assert
        context.StartedAt.Should().BeOnOrAfter(before);
        context.StartedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void PipelineStage_HasExpectedValues()
    {
        // Assert
        PipelineStage.NotStarted.Should().Be(PipelineStage.NotStarted);
        PipelineStage.Planning.Should().Be(PipelineStage.Planning);
        PipelineStage.Coding.Should().Be(PipelineStage.Coding);
        PipelineStage.Reviewing.Should().Be(PipelineStage.Reviewing);
        PipelineStage.Testing.Should().Be(PipelineStage.Testing);
        PipelineStage.Evaluating.Should().Be(PipelineStage.Evaluating);
        PipelineStage.Completed.Should().Be(PipelineStage.Completed);
        PipelineStage.Failed.Should().Be(PipelineStage.Failed);
        PipelineStage.AwaitingApproval.Should().Be(PipelineStage.AwaitingApproval);
    }
}
