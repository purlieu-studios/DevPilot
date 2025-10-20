using DevPilot.Core;
using FluentAssertions;

namespace DevPilot.Core.Tests;

/// <summary>
/// Tests for PipelineStage enum and stage-related functionality.
/// </summary>
public sealed class PipelineStageTests
{
    [Fact]
    public void PipelineStage_HasCorrectValues()
    {
        // Assert - verify all expected stages exist
        Enum.GetValues<PipelineStage>().Should().Contain(new[]
        {
            PipelineStage.NotStarted,
            PipelineStage.Planning,
            PipelineStage.Coding,
            PipelineStage.Reviewing,
            PipelineStage.Testing,
            PipelineStage.Evaluating,
            PipelineStage.Completed,
            PipelineStage.Failed,
            PipelineStage.AwaitingApproval
        });
    }

    [Fact]
    public void PipelineStage_NotStarted_IsZero()
    {
        // Assert
        ((int)PipelineStage.NotStarted).Should().Be(0);
    }

    [Fact]
    public void PipelineStage_Planning_IsFirst()
    {
        // Assert
        ((int)PipelineStage.Planning).Should().Be(1);
    }

    [Fact]
    public void PipelineStage_Coding_IsSecond()
    {
        // Assert
        ((int)PipelineStage.Coding).Should().Be(2);
    }

    [Fact]
    public void PipelineStage_Reviewing_IsThird()
    {
        // Assert
        ((int)PipelineStage.Reviewing).Should().Be(3);
    }

    [Fact]
    public void PipelineStage_Testing_IsFourth()
    {
        // Assert
        ((int)PipelineStage.Testing).Should().Be(4);
    }

    [Fact]
    public void PipelineStage_Evaluating_IsFifth()
    {
        // Assert
        ((int)PipelineStage.Evaluating).Should().Be(5);
    }

    [Fact]
    public void PipelineStage_ToString_ReturnsCorrectName()
    {
        // Arrange & Act & Assert
        PipelineStage.Planning.ToString().Should().Be("Planning");
        PipelineStage.Coding.ToString().Should().Be("Coding");
        PipelineStage.Reviewing.ToString().Should().Be("Reviewing");
        PipelineStage.Testing.ToString().Should().Be("Testing");
        PipelineStage.Evaluating.ToString().Should().Be("Evaluating");
    }

    [Fact]
    public void PipelineStage_AllStages_AreUnique()
    {
        // Arrange & Act
        var stages = Enum.GetValues<PipelineStage>();

        // Assert
        stages.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void PipelineStage_Count_IsCorrect()
    {
        // Arrange & Act
        var stageCount = Enum.GetValues<PipelineStage>().Length;

        // Assert
        stageCount.Should().Be(9, "we have 9 pipeline stages");
    }

    [Fact]
    public void PipelineStage_CanBeCompared()
    {
        // Assert - stage ordering
        (PipelineStage.Planning < PipelineStage.Coding).Should().BeTrue();
        (PipelineStage.Coding < PipelineStage.Reviewing).Should().BeTrue();
        (PipelineStage.Reviewing < PipelineStage.Testing).Should().BeTrue();
        (PipelineStage.Testing < PipelineStage.Evaluating).Should().BeTrue();
    }

    [Fact]
    public void PipelineStage_Completed_IsAfterEvaluating()
    {
        // Assert
        ((int)PipelineStage.Completed).Should().BeGreaterThan((int)PipelineStage.Evaluating);
    }

    [Fact]
    public void PipelineStage_Failed_IsDifferentFromCompleted()
    {
        // Assert
        PipelineStage.Failed.Should().NotBe(PipelineStage.Completed);
    }

    [Fact]
    public void PipelineStage_AwaitingApproval_IsDifferentFromFailed()
    {
        // Assert
        PipelineStage.AwaitingApproval.Should().NotBe(PipelineStage.Failed);
    }

    [Fact]
    public void PipelineStage_CanBeUsedInSwitch()
    {
        // Arrange
        var stage = PipelineStage.Planning;

        // Act
        var result = stage switch
        {
            PipelineStage.NotStarted => "not started",
            PipelineStage.Planning => "planning",
            PipelineStage.Coding => "coding",
            PipelineStage.Reviewing => "reviewing",
            PipelineStage.Testing => "testing",
            PipelineStage.Evaluating => "evaluating",
            PipelineStage.Completed => "completed",
            PipelineStage.Failed => "failed",
            PipelineStage.AwaitingApproval => "awaiting approval",
            _ => throw new ArgumentException("Unknown stage")
        };

        // Assert
        result.Should().Be("planning");
    }
}
