using DevPilot.Core;
using FluentAssertions;

namespace DevPilot.Core.Tests;

/// <summary>
/// Comprehensive tests for PipelineContext state management, stage transitions, and context tracking.
/// </summary>
public sealed class PipelineContextTests
{
    [Fact]
    public void Context_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var context = new PipelineContext
        {
            UserRequest = "Test request"
        };

        // Assert
        context.UserRequest.Should().Be("Test request");
        context.PipelineId.Should().NotBeNullOrWhiteSpace();
        context.CurrentStage.Should().Be(PipelineStage.NotStarted);
        context.Plan.Should().BeNull();
        context.Patch.Should().BeNull();
        context.Review.Should().BeNull();
        context.TestReport.Should().BeNull();
        context.Scores.Should().BeNull();
        context.WorkspaceRoot.Should().BeNull();
        context.RevisionIteration.Should().Be(0);
        context.TestFailureCount.Should().Be(0);
        context.HasTestFailures.Should().BeFalse();
        context.ApprovalRequired.Should().BeFalse();
        context.StageHistory.Should().BeEmpty();
    }

    [Fact]
    public void Context_GeneratesUniquePipelineIds()
    {
        // Arrange & Act
        var context1 = new PipelineContext { UserRequest = "Test 1" };
        var context2 = new PipelineContext { UserRequest = "Test 2" };

        // Assert
        context1.PipelineId.Should().NotBe(context2.PipelineId);
        Guid.TryParse(context1.PipelineId, out _).Should().BeTrue();
        Guid.TryParse(context2.PipelineId, out _).Should().BeTrue();
    }

    [Fact]
    public void AdvanceToStage_UpdatesCurrentStage()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        context.AdvanceToStage(PipelineStage.Planning, "{\"plan\": \"test\"}");

        // Assert
        context.CurrentStage.Should().Be(PipelineStage.Planning);
    }

    [Fact]
    public void AdvanceToStage_StoresOutputForStage()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        var planOutput = "{\"plan\": \"test plan\"}";

        // Act
        context.AdvanceToStage(PipelineStage.Planning, planOutput);

        // Assert
        context.Plan.Should().Be(planOutput);
    }

    [Fact]
    public void AdvanceToStage_RecordsStageHistory()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        context.AdvanceToStage(PipelineStage.Planning, "plan");
        context.AdvanceToStage(PipelineStage.Coding, "patch");

        // Assert
        context.StageHistory.Should().HaveCount(2);
        context.StageHistory[0].Stage.Should().Be(PipelineStage.Planning);
        context.StageHistory[0].PreviousStage.Should().Be(PipelineStage.NotStarted);
        context.StageHistory[1].Stage.Should().Be(PipelineStage.Coding);
        context.StageHistory[1].PreviousStage.Should().Be(PipelineStage.Planning);
    }

    [Fact]
    public void AdvanceToStage_ThroughAllStages_TracksCorrectly()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        context.AdvanceToStage(PipelineStage.Planning, "plan output");
        context.AdvanceToStage(PipelineStage.Coding, "patch output");
        context.AdvanceToStage(PipelineStage.Reviewing, "review output");
        context.AdvanceToStage(PipelineStage.Testing, "test output");
        context.AdvanceToStage(PipelineStage.Evaluating, "scores output");

        // Assert
        context.CurrentStage.Should().Be(PipelineStage.Evaluating);
        context.Plan.Should().Be("plan output");
        context.Patch.Should().Be("patch output");
        context.Review.Should().Be("review output");
        context.TestReport.Should().Be("test output");
        context.Scores.Should().Be("scores output");
        context.StageHistory.Should().HaveCount(5);
    }

    [Fact]
    public void AdvanceToStage_ThrowsOnNullOutput()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act & Assert
        var act = () => context.AdvanceToStage(PipelineStage.Planning, null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AdvanceToStage_ThrowsOnEmptyOutput()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act & Assert
        var act = () => context.AdvanceToStage(PipelineStage.Planning, "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetWorkspaceRoot_StoresPath()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        var workspaceRoot = @"C:\workspace\test";

        // Act
        context.SetWorkspaceRoot(workspaceRoot);

        // Assert
        context.WorkspaceRoot.Should().Be(workspaceRoot);
    }

    [Fact]
    public void SetWorkspaceRoot_ThrowsOnNullPath()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act & Assert
        var act = () => context.SetWorkspaceRoot(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetSourceRoot_StoresPath()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        var sourceRoot = @"C:\repos\my-project";

        // Act
        context.SetSourceRoot(sourceRoot);

        // Assert
        context.SourceRoot.Should().Be(sourceRoot);
    }

    [Fact]
    public void SetProjectStructure_StoresStructure()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        var structure = new ProjectStructureInfo
        {
            MainProject = "src",
            TestProjects = new List<string> { "tests" },
            AllProjects = new List<string> { "src", "tests" }
        };

        // Act
        context.SetProjectStructure(structure);

        // Assert
        context.ProjectStructure.Should().BeSameAs(structure);
        context.ProjectStructure!.MainProject.Should().Be("src");
    }

    [Fact]
    public void SetProjectStructure_ThrowsOnNull()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act & Assert
        var act = () => context.SetProjectStructure(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RequestApproval_SetsApprovalRequired()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Planning, "plan");

        // Act
        context.RequestApproval("High-risk operation detected");

        // Assert
        context.ApprovalRequired.Should().BeTrue();
        context.ApprovalReason.Should().Be("High-risk operation detected");
        context.CurrentStage.Should().Be(PipelineStage.AwaitingApproval);
    }

    [Fact]
    public void RequestApproval_AddsToStageHistory()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Planning, "plan");

        // Act
        context.RequestApproval("Need approval");

        // Assert
        context.StageHistory.Should().HaveCount(2);
        context.StageHistory[1].Stage.Should().Be(PipelineStage.AwaitingApproval);
        context.StageHistory[1].PreviousStage.Should().Be(PipelineStage.Planning);
    }

    [Fact]
    public void ClearApproval_RemovesApprovalRequirement()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.RequestApproval("Test approval");

        // Act
        context.ClearApproval();

        // Assert
        context.ApprovalRequired.Should().BeFalse();
        context.ApprovalReason.Should().BeNull();
    }

    [Fact]
    public void IncrementRevisionIteration_TracksRevisions()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        context.IncrementRevisionIteration();
        context.IncrementRevisionIteration();

        // Assert
        context.RevisionIteration.Should().Be(2);
    }

    [Fact]
    public void SetTestFailures_TracksFailureCount()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        context.SetTestFailures(5);

        // Assert
        context.TestFailureCount.Should().Be(5);
        context.HasTestFailures.Should().BeTrue();
    }

    [Fact]
    public void SetTestFailures_ReplacesNotAccumulates()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        context.SetTestFailures(3);
        context.SetTestFailures(2);

        // Assert
        context.TestFailureCount.Should().Be(2, "SetTestFailures replaces the count");
    }

    [Fact]
    public void StartedAt_IsSetOnConstruction()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var context = new PipelineContext { UserRequest = "Test" };
        var after = DateTimeOffset.UtcNow;

        // Assert
        context.StartedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void CompletedAt_CanBeSet()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        var completedTime = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        context.CompletedAt = completedTime;

        // Assert
        context.CompletedAt.Should().Be(completedTime);
    }

    [Fact]
    public void GetStageOutput_ReturnsCorrectOutput()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Planning, "plan content");
        context.AdvanceToStage(PipelineStage.Coding, "patch content");

        // Act
        var planOutput = context.GetStageOutput(PipelineStage.Planning);
        var codeOutput = context.GetStageOutput(PipelineStage.Coding);

        // Assert
        planOutput.Should().Be("plan content");
        codeOutput.Should().Be("patch content");
    }

    [Fact]
    public void GetStageOutput_ReturnsNullForUnexecutedStage()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act
        var output = context.GetStageOutput(PipelineStage.Planning);

        // Assert
        output.Should().BeNull();
    }

    [Fact]
    public void StageHistory_IsImmutable()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        context.AdvanceToStage(PipelineStage.Planning, "plan");

        // Act
        var history = context.StageHistory;

        // Assert
        history.Should().BeAssignableTo<IReadOnlyList<PipelineStageEntry>>();
    }

    [Fact]
    public void MultipleContexts_MaintainSeparateState()
    {
        // Arrange & Act
        var context1 = new PipelineContext { UserRequest = "Request 1" };
        var context2 = new PipelineContext { UserRequest = "Request 2" };

        context1.AdvanceToStage(PipelineStage.Planning, "plan 1");
        context2.AdvanceToStage(PipelineStage.Coding, "patch 2");

        // Assert
        context1.CurrentStage.Should().Be(PipelineStage.Planning);
        context1.Plan.Should().Be("plan 1");
        context1.Patch.Should().BeNull();

        context2.CurrentStage.Should().Be(PipelineStage.Coding);
        context2.Plan.Should().BeNull();
        context2.Patch.Should().Be("patch 2");
    }

    [Fact]
    public void Context_HandlesVeryLongOutputs()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };
        var longOutput = string.Join("\n", Enumerable.Repeat("Very long line of output", 1000));

        // Act
        context.AdvanceToStage(PipelineStage.Planning, longOutput);

        // Assert
        context.Plan.Should().Be(longOutput);
        context.Plan!.Length.Should().BeGreaterThan(10000);
    }

    [Fact]
    public void Context_HandlesRapidStageTransitions()
    {
        // Arrange
        var context = new PipelineContext { UserRequest = "Test" };

        // Act - rapid transitions
        for (int i = 0; i < 100; i++)
        {
            var stage = (PipelineStage)(i % 5 + 1); // Cycle through stages
            context.AdvanceToStage(stage, $"output {i}");
        }

        // Assert
        context.StageHistory.Should().HaveCount(100);
    }
}
