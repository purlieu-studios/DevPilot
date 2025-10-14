using DevPilot.Core;
using FluentAssertions;

namespace DevPilot.Agents.Tests;

public sealed class PipelineContextBehaviorTests
{
    [Fact]
    public void AdvanceToStage_UpdatesCurrentStage()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        context.AdvanceToStage(PipelineStage.Planning, "Starting plan");

        // Assert
        context.CurrentStage.Should().Be(PipelineStage.Planning);
    }

    [Fact]
    public void AdvanceToStage_RecordsStageHistory()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        context.AdvanceToStage(PipelineStage.Planning, "Plan output");

        // Assert
        context.StageHistory.Should().HaveCount(1);
        context.StageHistory[0].Stage.Should().Be(PipelineStage.Planning);
        context.StageHistory[0].PreviousStage.Should().Be(PipelineStage.NotStarted);
    }

    [Fact]
    public void AdvanceToStage_SetsPlanWhenAdvancingToPlanning()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        context.AdvanceToStage(PipelineStage.Planning, "Initial plan");

        // Assert
        context.Plan.Should().Be("Initial plan");
    }

    [Fact]
    public void AdvanceToStage_SetsPatchWhenAdvancingToCoding()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Planning, "Plan");

        // Act
        context.AdvanceToStage(PipelineStage.Coding, "Patch content");

        // Assert
        context.Patch.Should().Be("Patch content");
    }

    [Fact]
    public void AdvanceToStage_ThrowsArgumentException_WhenOutputIsEmpty()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        var act = () => context.AdvanceToStage(PipelineStage.Planning, "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AdvanceToStage_ThrowsArgumentException_WhenOutputIsWhitespace()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        var act = () => context.AdvanceToStage(PipelineStage.Planning, "   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequestApproval_SetsApprovalRequiredToTrue()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        context.RequestApproval("High risk operation");

        // Assert
        context.ApprovalRequired.Should().BeTrue();
    }

    [Fact]
    public void RequestApproval_SetsApprovalReason()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        context.RequestApproval("File deletion required");

        // Assert
        context.ApprovalReason.Should().Be("File deletion required");
    }

    [Fact]
    public void RequestApproval_ChangesStageToAwaitingApproval()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Planning, "Plan");

        // Act
        context.RequestApproval("Caps breach");

        // Assert
        context.CurrentStage.Should().Be(PipelineStage.AwaitingApproval);
    }

    [Fact]
    public void RequestApproval_AddsToStageHistory()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        context.RequestApproval("Needs review");

        // Assert
        context.StageHistory.Should().HaveCount(1);
        context.StageHistory[0].Stage.Should().Be(PipelineStage.AwaitingApproval);
    }

    [Fact]
    public void RequestApproval_RecordsPreviousStageCorrectly()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Planning, "Plan output");

        // Act
        context.RequestApproval("LOC breach");

        // Assert
        context.StageHistory.Should().HaveCount(2);
        context.StageHistory[1].Stage.Should().Be(PipelineStage.AwaitingApproval);
        context.StageHistory[1].PreviousStage.Should().Be(PipelineStage.Planning);
    }

    [Fact]
    public void RequestApproval_ThrowsArgumentException_WhenReasonIsEmpty()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        var act = () => context.RequestApproval("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ClearApproval_SetsApprovalRequiredToFalse()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.RequestApproval("High risk");

        // Act
        context.ClearApproval();

        // Assert
        context.ApprovalRequired.Should().BeFalse();
    }

    [Fact]
    public void ClearApproval_ClearsApprovalReason()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.RequestApproval("High risk");

        // Act
        context.ClearApproval();

        // Assert
        context.ApprovalReason.Should().BeNull();
    }

    [Fact]
    public void GetStageOutput_ReturnsOutputFromSpecificStage()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Planning, "Plan output");
        context.AdvanceToStage(PipelineStage.Coding, "Patch output");

        // Act
        var planOutput = context.GetStageOutput(PipelineStage.Planning);

        // Assert
        planOutput.Should().Be("Plan output");
    }

    [Fact]
    public void GetStageOutput_ReturnsNull_WhenStageNotExecuted()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        var output = context.GetStageOutput(PipelineStage.Testing);

        // Assert
        output.Should().BeNull();
    }

    [Fact]
    public void FullPipelineFlow_TrackingAllStages()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Create calculator" };

        // Act - Simulate full pipeline
        context.AdvanceToStage(PipelineStage.Planning, "{\"plan\": \"steps\"}");
        context.AdvanceToStage(PipelineStage.Coding, "diff --git...");
        context.AdvanceToStage(PipelineStage.Reviewing, "{\"verdict\": \"APPROVE\"}");
        context.AdvanceToStage(PipelineStage.Testing, "{\"pass\": true}");
        context.AdvanceToStage(PipelineStage.Evaluating, "{\"score\": 9}");
        context.AdvanceToStage(PipelineStage.Completed, "Success");

        // Assert
        context.CurrentStage.Should().Be(PipelineStage.Completed);
        context.Plan.Should().Be("{\"plan\": \"steps\"}");
        context.Patch.Should().Be("diff --git...");
        context.Review.Should().Be("{\"verdict\": \"APPROVE\"}");
        context.TestReport.Should().Be("{\"pass\": true}");
        context.Scores.Should().Be("{\"score\": 9}");
        context.StageHistory.Should().HaveCount(6);
    }
}
