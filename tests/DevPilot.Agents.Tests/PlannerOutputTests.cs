using DevPilot.Core;
using FluentAssertions;
using System.Text.Json;

namespace DevPilot.Agents.Tests;

public sealed class PlannerOutputTests
{
    [Fact]
    public void Deserialize_ValidPlan_Success()
    {
        // Arrange
        var json = """
            {
              "plan": {
                "summary": "Create Calculator class",
                "steps": [
                  {
                    "step_number": 1,
                    "description": "Create Calculator.cs",
                    "file_target": "src/Calculator.cs",
                    "agent": "coder",
                    "estimated_loc": 50
                  }
                ]
              },
              "file_list": [
                {"path": "src/Calculator.cs", "operation": "create", "reason": "Implementation"}
              ],
              "risk": {
                "level": "low",
                "factors": ["Simple addition"],
                "mitigation": "Unit tests"
              },
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Plan.Summary.Should().Be("Create Calculator class");
        output.Plan.Steps.Should().HaveCount(1);
        output.Plan.Steps[0].StepNumber.Should().Be(1);
        output.Plan.Steps[0].EstimatedLoc.Should().Be(50);
        output.FileList.Should().HaveCount(1);
        output.FileList[0].Operation.Should().Be("create");
        output.Risk.Level.Should().Be("low");
        output.NeedsApproval.Should().BeFalse();
    }

    [Fact]
    public void Deserialize_PlanWithApprovalRequired_SetsNeedsApprovalTrue()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Blocked", "steps": []},
              "file_list": [],
              "risk": {"level": "high", "factors": ["Deletion"], "mitigation": "None"},
              "needs_approval": true,
              "approval_reason": "File deletion required"
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.NeedsApproval.Should().BeTrue();
        output.ApprovalReason.Should().Be("File deletion required");
    }

    [Fact]
    public void Deserialize_HighRiskPlan_SetsRiskLevelHigh()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Risky operation", "steps": []},
              "file_list": [],
              "risk": {
                "level": "high",
                "factors": ["Database migration", "Breaking changes"],
                "mitigation": "Backup required"
              },
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Risk.Level.Should().Be("high");
        output.Risk.Factors.Should().HaveCount(2);
        output.Risk.Factors.Should().Contain("Database migration");
    }

    [Fact]
    public void Deserialize_PlanWithDeletion_ParsesFileOperations()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Delete old files", "steps": []},
              "file_list": [
                {"path": "old/File.cs", "operation": "delete", "reason": "Cleanup"}
              ],
              "risk": {"level": "medium", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.FileList.Should().HaveCount(1);
        output.FileList[0].Operation.Should().Be("delete");
        output.FileList[0].Path.Should().Be("old/File.cs");
    }

    [Fact]
    public void Deserialize_PlanWithLocBreach_ParsesEstimatedLoc()
    {
        // Arrange
        var json = """
            {
              "plan": {
                "summary": "Large feature",
                "steps": [
                  {
                    "step_number": 1,
                    "description": "Create large class",
                    "file_target": "src/Large.cs",
                    "agent": "coder",
                    "estimated_loc": 450
                  }
                ]
              },
              "file_list": [],
              "risk": {"level": "medium", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Plan.Steps[0].EstimatedLoc.Should().Be(450);
    }

    [Fact]
    public void Deserialize_PlanWithMoreThan7Steps_ParsesAllSteps()
    {
        // Arrange
        var json = """
            {
              "plan": {
                "summary": "Many steps",
                "steps": [
                  {"step_number": 1, "description": "Step 1", "file_target": null, "agent": "coder", "estimated_loc": 50},
                  {"step_number": 2, "description": "Step 2", "file_target": null, "agent": "coder", "estimated_loc": 50},
                  {"step_number": 3, "description": "Step 3", "file_target": null, "agent": "coder", "estimated_loc": 50},
                  {"step_number": 4, "description": "Step 4", "file_target": null, "agent": "coder", "estimated_loc": 50},
                  {"step_number": 5, "description": "Step 5", "file_target": null, "agent": "coder", "estimated_loc": 50},
                  {"step_number": 6, "description": "Step 6", "file_target": null, "agent": "coder", "estimated_loc": 50},
                  {"step_number": 7, "description": "Step 7", "file_target": null, "agent": "coder", "estimated_loc": 50},
                  {"step_number": 8, "description": "Step 8", "file_target": null, "agent": "coder", "estimated_loc": 50}
                ]
              },
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Plan.Steps.Should().HaveCount(8);
    }

    [Fact]
    public void Deserialize_PlanWithNullFileTarget_HandlesNull()
    {
        // Arrange
        var json = """
            {
              "plan": {
                "summary": "Test",
                "steps": [
                  {
                    "step_number": 1,
                    "description": "Review",
                    "file_target": null,
                    "agent": "reviewer",
                    "estimated_loc": 0
                  }
                ]
              },
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Plan.Steps[0].FileTarget.Should().BeNull();
    }
}
