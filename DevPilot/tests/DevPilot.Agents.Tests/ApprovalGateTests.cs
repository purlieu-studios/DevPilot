using DevPilot.Orchestrator;
using FluentAssertions;

namespace DevPilot.Agents.Tests;

public sealed class ApprovalGateTests
{
    [Fact]
    public void Evaluate_SafePlan_DoesNotRequireApproval()
    {
        // Arrange
        var json = """
            {
              "plan": {
                "summary": "Safe operation",
                "steps": [
                  {"step_number": 1, "description": "Add method", "file_target": "Calc.cs", "agent": "coder", "estimated_loc": 50}
                ]
              },
              "file_list": [{"path": "Calc.cs", "operation": "create", "reason": "New file"}],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var decision = ApprovalGate.Evaluate(json);

        // Assert
        decision.Required.Should().BeFalse();
        decision.Triggers.Should().BeEmpty();
        decision.Reason.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_PlannerFlagsNeedsApproval_RequiresApproval()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Blocked", "steps": []},
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": true,
              "approval_reason": "Ambiguous requirements"
            }
            """;

        // Act
        var decision = ApprovalGate.Evaluate(json);

        // Assert
        decision.Required.Should().BeTrue();
        decision.Triggers.Should().HaveCount(1);
        decision.Triggers[0].Should().Contain("needs_approval");
        decision.Triggers[0].Should().Contain("Ambiguous requirements");
    }

    [Fact]
    public void Evaluate_HighRiskPlan_RequiresApproval()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Risky", "steps": []},
              "file_list": [],
              "risk": {"level": "high", "factors": ["Database migration", "Auth changes"], "mitigation": "Backup"},
              "needs_approval": false
            }
            """;

        // Act
        var decision = ApprovalGate.Evaluate(json);

        // Assert
        decision.Required.Should().BeTrue();
        decision.Triggers.Should().HaveCount(1);
        decision.Triggers[0].Should().Contain("High-risk");
        decision.Triggers[0].Should().Contain("Database migration");
    }

    [Fact]
    public void Evaluate_LocBreach_RequiresApproval()
    {
        // Arrange
        var json = """
            {
              "plan": {
                "summary": "Large change",
                "steps": [
                  {"step_number": 1, "description": "Big feature", "file_target": "Big.cs", "agent": "coder", "estimated_loc": 450}
                ]
              },
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var decision = ApprovalGate.Evaluate(json);

        // Assert
        decision.Required.Should().BeTrue();
        decision.Triggers.Should().HaveCount(1);
        decision.Triggers[0].Should().Contain("LOC limit exceeded");
        decision.Triggers[0].Should().Contain("450");
    }

    [Fact]
    public void Evaluate_StepLimitBreach_RequiresApproval()
    {
        // Arrange
        var json = """
            {
              "plan": {
                "summary": "Many steps",
                "steps": [
                  {"step_number": 1, "description": "S1", "file_target": null, "agent": "coder", "estimated_loc": 50},
                  {"step_number": 2, "description": "S2", "file_target": null, "agent": "coder", "estimated_loc": 50},
                  {"step_number": 3, "description": "S3", "file_target": null, "agent": "coder", "estimated_loc": 50},
                  {"step_number": 4, "description": "S4", "file_target": null, "agent": "coder", "estimated_loc": 50},
                  {"step_number": 5, "description": "S5", "file_target": null, "agent": "coder", "estimated_loc": 50},
                  {"step_number": 6, "description": "S6", "file_target": null, "agent": "coder", "estimated_loc": 50},
                  {"step_number": 7, "description": "S7", "file_target": null, "agent": "coder", "estimated_loc": 50},
                  {"step_number": 8, "description": "S8", "file_target": null, "agent": "coder", "estimated_loc": 50}
                ]
              },
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var decision = ApprovalGate.Evaluate(json);

        // Assert
        decision.Required.Should().BeTrue();
        decision.Triggers.Should().HaveCount(1);
        decision.Triggers[0].Should().Contain("Step limit exceeded");
        decision.Triggers[0].Should().Contain("8 steps");
    }

    [Fact]
    public void Evaluate_FileDeletion_RequiresApproval()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Delete old", "steps": []},
              "file_list": [
                {"path": "old/Legacy.cs", "operation": "delete", "reason": "Cleanup"}
              ],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var decision = ApprovalGate.Evaluate(json);

        // Assert
        decision.Required.Should().BeTrue();
        decision.Triggers.Should().HaveCount(1);
        decision.Triggers[0].Should().Contain("File deletion detected");
        decision.Triggers[0].Should().Contain("old/Legacy.cs");
    }

    [Fact]
    public void Evaluate_MultipleTriggers_CombinesReasons()
    {
        // Arrange
        var json = """
            {
              "plan": {
                "summary": "Bad plan",
                "steps": [
                  {"step_number": 1, "description": "Huge", "file_target": "Big.cs", "agent": "coder", "estimated_loc": 500}
                ]
              },
              "file_list": [
                {"path": "old/File.cs", "operation": "delete", "reason": "Remove"}
              ],
              "risk": {"level": "high", "factors": ["Breaking changes"], "mitigation": ""},
              "needs_approval": true,
              "approval_reason": "Too complex"
            }
            """;

        // Act
        var decision = ApprovalGate.Evaluate(json);

        // Assert
        decision.Required.Should().BeTrue();
        decision.Triggers.Should().HaveCount(4); // needs_approval, high-risk, LOC, deletion
        decision.Reason.Should().Contain("needs_approval");
        decision.Reason.Should().Contain("High-risk");
        decision.Reason.Should().Contain("LOC limit");
        decision.Reason.Should().Contain("deletion");
    }

    [Fact]
    public void Evaluate_EmptyJson_RequiresApproval()
    {
        // Act
        var act = () => ApprovalGate.Evaluate("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Evaluate_InvalidJson_RequiresApprovalWithError()
    {
        // Arrange
        var json = "{ invalid json }";

        // Act
        var decision = ApprovalGate.Evaluate(json);

        // Assert
        decision.Required.Should().BeTrue();
        decision.Reason.Should().Contain("Invalid planner JSON");
    }

    [Fact]
    public void Evaluate_NullJson_ThrowsArgumentException()
    {
        // Act
        var act = () => ApprovalGate.Evaluate(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
