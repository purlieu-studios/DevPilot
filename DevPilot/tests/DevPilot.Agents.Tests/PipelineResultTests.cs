using DevPilot.Core;
using FluentAssertions;

namespace DevPilot.Agents.Tests;

public sealed class PipelineResultTests
{
    [Fact]
    public void CreateSuccess_SetsSuccessToTrue()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Completed, "Done");
        var duration = TimeSpan.FromSeconds(10);

        // Act
        var result = PipelineResult.CreateSuccess(context, duration);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void CreateSuccess_SetsFinalStageFromContext()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Completed, "Done");
        var duration = TimeSpan.FromSeconds(10);

        // Act
        var result = PipelineResult.CreateSuccess(context, duration);

        // Assert
        result.FinalStage.Should().Be(PipelineStage.Completed);
    }

    [Fact]
    public void CreateSuccess_SetsRequiresApprovalToFalse()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Completed, "Done");
        var duration = TimeSpan.FromSeconds(10);

        // Act
        var result = PipelineResult.CreateSuccess(context, duration);

        // Assert
        result.RequiresApproval.Should().BeFalse();
    }

    [Fact]
    public void CreateSuccess_StoresContextAndDuration()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Completed, "Done");
        var duration = TimeSpan.FromSeconds(10);

        // Act
        var result = PipelineResult.CreateSuccess(context, duration);

        // Assert
        result.Context.Should().BeSameAs(context);
        result.Duration.Should().Be(duration);
    }

    [Fact]
    public void CreateFailure_SetsSuccessToFalse()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Failed, "Error");
        var duration = TimeSpan.FromSeconds(5);

        // Act
        var result = PipelineResult.CreateFailure(context, duration, "Pipeline failed");

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void CreateFailure_SetsErrorMessage()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Failed, "Error");
        var duration = TimeSpan.FromSeconds(5);

        // Act
        var result = PipelineResult.CreateFailure(context, duration, "Agent execution failed");

        // Assert
        result.ErrorMessage.Should().Be("Agent execution failed");
    }

    [Fact]
    public void CreateFailure_ThrowsArgumentException_WhenErrorMessageIsEmpty()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        var duration = TimeSpan.FromSeconds(5);

        // Act
        var act = () => PipelineResult.CreateFailure(context, duration, "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateFailure_ThrowsArgumentException_WhenErrorMessageIsWhitespace()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        var duration = TimeSpan.FromSeconds(5);

        // Act
        var act = () => PipelineResult.CreateFailure(context, duration, "   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateAwaitingApproval_SetsRequiresApprovalToTrue()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.RequestApproval("High risk operation");
        var duration = TimeSpan.FromSeconds(7);

        // Act
        var result = PipelineResult.CreateAwaitingApproval(context, duration);

        // Assert
        result.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public void CreateAwaitingApproval_SetsFinalStageToAwaitingApproval()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.RequestApproval("Needs review");
        var duration = TimeSpan.FromSeconds(7);

        // Act
        var result = PipelineResult.CreateAwaitingApproval(context, duration);

        // Assert
        result.FinalStage.Should().Be(PipelineStage.AwaitingApproval);
    }

    [Fact]
    public void CreateAwaitingApproval_SetsSuccessToFalse()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.RequestApproval("Needs review");
        var duration = TimeSpan.FromSeconds(7);

        // Act
        var result = PipelineResult.CreateAwaitingApproval(context, duration);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void CreateAwaitingApproval_StoresContextAndDuration()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.RequestApproval("Needs review");
        var duration = TimeSpan.FromSeconds(7);

        // Act
        var result = PipelineResult.CreateAwaitingApproval(context, duration);

        // Assert
        result.Context.Should().BeSameAs(context);
        result.Duration.Should().Be(duration);
    }
}
