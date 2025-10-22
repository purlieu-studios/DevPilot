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

    #region CreatePassedWithWarnings Tests

    [Fact]
    public void CreatePassedWithWarnings_SetsSuccessToTrue()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Completed, "Done");
        var duration = TimeSpan.FromSeconds(15);

        // Act
        var result = PipelineResult.CreatePassedWithWarnings(context, duration, "Some tests failed");

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void CreatePassedWithWarnings_SetsFinalStageToCompleted()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Testing, "Running tests");
        var duration = TimeSpan.FromSeconds(15);

        // Act
        var result = PipelineResult.CreatePassedWithWarnings(context, duration, "2 tests failed");

        // Assert
        result.FinalStage.Should().Be(PipelineStage.Completed);
    }

    [Fact]
    public void CreatePassedWithWarnings_StoresWarningMessageInErrorMessage()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Completed, "Done");
        var duration = TimeSpan.FromSeconds(15);

        // Act
        var result = PipelineResult.CreatePassedWithWarnings(context, duration, "Code coverage is below 80%");

        // Assert
        result.ErrorMessage.Should().Be("Code coverage is below 80%");
    }

    [Fact]
    public void CreatePassedWithWarnings_SetsRequiresApprovalToFalse()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Completed, "Done");
        var duration = TimeSpan.FromSeconds(15);

        // Act
        var result = PipelineResult.CreatePassedWithWarnings(context, duration, "Minor issues found");

        // Assert
        result.RequiresApproval.Should().BeFalse();
    }

    [Fact]
    public void CreatePassedWithWarnings_ThrowsArgumentException_WhenWarningMessageIsEmpty()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        var duration = TimeSpan.FromSeconds(15);

        // Act
        var act = () => PipelineResult.CreatePassedWithWarnings(context, duration, "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreatePassedWithWarnings_ThrowsArgumentException_WhenWarningMessageIsWhitespace()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        var duration = TimeSpan.FromSeconds(15);

        // Act
        var act = () => PipelineResult.CreatePassedWithWarnings(context, duration, "   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreatePassedWithWarnings_StoresContextAndDuration()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Completed, "Done");
        var duration = TimeSpan.FromSeconds(15);

        // Act
        var result = PipelineResult.CreatePassedWithWarnings(context, duration, "Test warnings");

        // Assert
        result.Context.Should().BeSameAs(context);
        result.Duration.Should().Be(duration);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void CreateSuccess_WithVeryShortDuration_StoresCorrectly()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Completed, "Done");
        var duration = TimeSpan.FromMilliseconds(1);

        // Act
        var result = PipelineResult.CreateSuccess(context, duration);

        // Assert
        result.Duration.Should().Be(duration);
        result.Duration.TotalMilliseconds.Should().Be(1);
    }

    [Fact]
    public void CreateSuccess_WithVeryLongDuration_StoresCorrectly()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Completed, "Done");
        var duration = TimeSpan.FromHours(2.5);

        // Act
        var result = PipelineResult.CreateSuccess(context, duration);

        // Assert
        result.Duration.Should().Be(duration);
        result.Duration.TotalHours.Should().BeApproximately(2.5, 0.001);
    }

    [Fact]
    public void CreateFailure_WithVeryLongErrorMessage_StoresCorrectly()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Failed, "Error");
        var duration = TimeSpan.FromSeconds(5);
        var longError = new string('E', 10000); // 10K character error

        // Act
        var result = PipelineResult.CreateFailure(context, duration, longError);

        // Assert
        result.ErrorMessage.Should().HaveLength(10000);
        result.ErrorMessage.Should().Be(longError);
    }

    [Fact]
    public void CreateFailure_WithMultilineErrorMessage_StoresCorrectly()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Failed, "Error");
        var duration = TimeSpan.FromSeconds(5);
        var multilineError = "Error on line 1\nError on line 2\nError on line 3";

        // Act
        var result = PipelineResult.CreateFailure(context, duration, multilineError);

        // Assert
        result.ErrorMessage.Should().Contain("\n");
        result.ErrorMessage.Should().Contain("line 1");
        result.ErrorMessage.Should().Contain("line 3");
    }

    [Fact]
    public void CreateSuccess_ErrorMessage_RemainsNull()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Completed, "Done");
        var duration = TimeSpan.FromSeconds(10);

        // Act
        var result = PipelineResult.CreateSuccess(context, duration);

        // Assert
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void CreateAwaitingApproval_ErrorMessage_RemainsNull()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.RequestApproval("Needs review");
        var duration = TimeSpan.FromSeconds(7);

        // Act
        var result = PipelineResult.CreateAwaitingApproval(context, duration);

        // Assert
        result.ErrorMessage.Should().BeNull();
    }

    #endregion
}
