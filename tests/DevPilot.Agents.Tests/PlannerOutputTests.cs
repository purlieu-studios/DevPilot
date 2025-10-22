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

    #region VerificationPlan Tests

    [Fact]
    public void Deserialize_PlanWithVerificationPlan_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Test", "steps": []},
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "verify": {
                "acceptance_criteria": ["Tests pass", "Code compiles"],
                "test_commands": ["dotnet test"],
                "manual_checks": ["Visual inspection"]
              },
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Verify.Should().NotBeNull();
        output.Verify!.AcceptanceCriteria.Should().HaveCount(2);
        output.Verify.TestCommands.Should().Contain("dotnet test");
        output.Verify.ManualChecks.Should().Contain("Visual inspection");
    }

    [Fact]
    public void Deserialize_PlanWithoutVerificationPlan_VerifyIsNull()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Test", "steps": []},
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Verify.Should().BeNull();
    }

    [Fact]
    public void Deserialize_VerificationPlan_WithEmptyLists_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Test", "steps": []},
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "verify": {
                "acceptance_criteria": [],
                "test_commands": [],
                "manual_checks": []
              },
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Verify.Should().NotBeNull();
        output.Verify!.AcceptanceCriteria.Should().BeEmpty();
        output.Verify.TestCommands.Should().BeEmpty();
        output.Verify.ManualChecks.Should().BeEmpty();
    }

    #endregion

    #region RollbackPlan Tests

    [Fact]
    public void Deserialize_PlanWithRollbackPlan_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Test", "steps": []},
              "file_list": [],
              "risk": {"level": "medium", "factors": [], "mitigation": ""},
              "rollback": {
                "strategy": "Git revert",
                "commands": ["git revert HEAD"],
                "notes": "Revert if tests fail"
              },
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Rollback.Should().NotBeNull();
        output.Rollback!.Strategy.Should().Be("Git revert");
        output.Rollback.Commands.Should().Contain("git revert HEAD");
        output.Rollback.Notes.Should().Be("Revert if tests fail");
    }

    [Fact]
    public void Deserialize_PlanWithoutRollbackPlan_RollbackIsNull()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Test", "steps": []},
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Rollback.Should().BeNull();
    }

    [Fact]
    public void Deserialize_RollbackPlan_WithMultipleCommands_ParsesAll()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Test", "steps": []},
              "file_list": [],
              "risk": {"level": "high", "factors": [], "mitigation": ""},
              "rollback": {
                "strategy": "Manual rollback",
                "commands": ["git reset --hard", "git clean -fd", "dotnet restore"],
                "notes": "Full reset required"
              },
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Rollback.Should().NotBeNull();
        output.Rollback!.Commands.Should().HaveCount(3);
        output.Rollback.Commands[1].Should().Be("git clean -fd");
    }

    #endregion

    #region PlanStep Edge Cases

    [Fact]
    public void Deserialize_PlanStep_WithZeroEstimatedLoc_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
              "plan": {
                "summary": "Test",
                "steps": [
                  {
                    "step_number": 1,
                    "description": "Delete file",
                    "file_target": "old.cs",
                    "agent": "coder",
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
        output!.Plan.Steps[0].EstimatedLoc.Should().Be(0);
    }

    [Fact]
    public void Deserialize_PlanStep_WithVeryLargeEstimatedLoc_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
              "plan": {
                "summary": "Large feature",
                "steps": [
                  {
                    "step_number": 1,
                    "description": "Generate large file",
                    "file_target": "huge.cs",
                    "agent": "coder",
                    "estimated_loc": 10000
                  }
                ]
              },
              "file_list": [],
              "risk": {"level": "high", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Plan.Steps[0].EstimatedLoc.Should().Be(10000);
    }

    [Fact]
    public void Deserialize_PlanStep_WithVeryLongDescription_ParsesCorrectly()
    {
        // Arrange
        var longDescription = new string('A', 1000);
        var json = $$"""
            {
              "plan": {
                "summary": "Test",
                "steps": [
                  {
                    "step_number": 1,
                    "description": "{{longDescription}}",
                    "file_target": null,
                    "agent": "coder",
                    "estimated_loc": 50
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
        output!.Plan.Steps[0].Description.Should().HaveLength(1000);
    }

    #endregion

    #region FileOperation Edge Cases

    [Fact]
    public void Deserialize_FileOperation_WithMultipleOperations_ParsesAll()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Multi-file operation", "steps": []},
              "file_list": [
                {"path": "file1.cs", "operation": "create", "reason": "New feature"},
                {"path": "file2.cs", "operation": "modify", "reason": "Bug fix"},
                {"path": "file3.cs", "operation": "delete", "reason": "Cleanup"}
              ],
              "risk": {"level": "medium", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.FileList.Should().HaveCount(3);
        output.FileList[0].Operation.Should().Be("create");
        output.FileList[1].Operation.Should().Be("modify");
        output.FileList[2].Operation.Should().Be("delete");
    }

    [Fact]
    public void Deserialize_FileOperation_WithVeryLongPath_ParsesCorrectly()
    {
        // Arrange
        var longPath = string.Join("/", Enumerable.Repeat("folder", 50)) + "/file.cs";
        var json = $$"""
            {
              "plan": {"summary": "Test", "steps": []},
              "file_list": [
                {"path": "{{longPath}}", "operation": "create", "reason": "Deep nesting"}
              ],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.FileList[0].Path.Should().Contain("folder/folder/folder");
        output.FileList[0].Path.Should().EndWith("file.cs");
    }

    [Fact]
    public void Deserialize_FileList_Empty_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "No file changes", "steps": []},
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.FileList.Should().BeEmpty();
    }

    #endregion

    #region RiskAssessment Edge Cases

    [Fact]
    public void Deserialize_RiskAssessment_WithManyFactors_ParsesAll()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Complex operation", "steps": []},
              "file_list": [],
              "risk": {
                "level": "high",
                "factors": [
                  "Database changes",
                  "Breaking API changes",
                  "Performance impact",
                  "Security implications",
                  "Third-party dependencies"
                ],
                "mitigation": "Comprehensive testing and rollback plan"
              },
              "needs_approval": true,
              "approval_reason": "High risk operation"
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Risk.Factors.Should().HaveCount(5);
        output.Risk.Factors.Should().Contain("Security implications");
    }

    [Fact]
    public void Deserialize_RiskAssessment_WithNoFactors_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Simple operation", "steps": []},
              "file_list": [],
              "risk": {
                "level": "low",
                "factors": [],
                "mitigation": "None required"
              },
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Risk.Factors.Should().BeEmpty();
        output.Risk.Level.Should().Be("low");
    }

    [Fact]
    public void Deserialize_RiskAssessment_MediumLevel_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Moderate risk", "steps": []},
              "file_list": [],
              "risk": {
                "level": "medium",
                "factors": ["Refactoring existing code"],
                "mitigation": "Unit tests and code review"
              },
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Risk.Level.Should().Be("medium");
    }

    #endregion

    #region Approval Edge Cases

    [Fact]
    public void Deserialize_ApprovalReason_WhenNeedsApprovalFalse_CanBeNull()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Safe operation", "steps": []},
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.NeedsApproval.Should().BeFalse();
        output.ApprovalReason.Should().BeNull();
    }

    [Fact]
    public void Deserialize_ApprovalReason_WhenNeedsApprovalTrue_ContainsReason()
    {
        // Arrange
        var json = """
            {
              "plan": {"summary": "Dangerous operation", "steps": []},
              "file_list": [],
              "risk": {"level": "high", "factors": ["Data loss risk"], "mitigation": "Backup"},
              "needs_approval": true,
              "approval_reason": "Potential data loss"
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.NeedsApproval.Should().BeTrue();
        output.ApprovalReason.Should().Be("Potential data loss");
    }

    #endregion

    #region Serialization Round-Trip Tests

    [Fact]
    public void Serialize_ThenDeserialize_PreservesAllProperties()
    {
        // Arrange
        var original = new PlannerOutput
        {
            Plan = new PlanDetails
            {
                Summary = "Test plan",
                Steps = new List<PlanStep>
                {
                    new PlanStep
                    {
                        StepNumber = 1,
                        Description = "Create file",
                        FileTarget = "test.cs",
                        Agent = "coder",
                        EstimatedLoc = 100
                    }
                }
            },
            FileList = new List<FileOperation>
            {
                new FileOperation
                {
                    Path = "test.cs",
                    Operation = "create",
                    Reason = "Testing"
                }
            },
            Risk = new RiskAssessment
            {
                Level = "low",
                Factors = new List<string> { "Simple test" },
                Mitigation = "Unit tests"
            },
            Verify = new VerificationPlan
            {
                AcceptanceCriteria = new List<string> { "Tests pass" },
                TestCommands = new List<string> { "dotnet test" },
                ManualChecks = new List<string> { "Visual check" }
            },
            Rollback = new RollbackPlan
            {
                Strategy = "Git revert",
                Commands = new List<string> { "git revert HEAD" },
                Notes = "Easy rollback"
            },
            NeedsApproval = true,
            ApprovalReason = "Testing approval"
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Plan.Summary.Should().Be(original.Plan.Summary);
        deserialized.FileList.Should().HaveCount(original.FileList.Count);
        deserialized.Risk.Level.Should().Be(original.Risk.Level);
        deserialized.Verify.Should().NotBeNull();
        deserialized.Rollback.Should().NotBeNull();
        deserialized.NeedsApproval.Should().Be(original.NeedsApproval);
        deserialized.ApprovalReason.Should().Be(original.ApprovalReason);
    }

    [Fact]
    public void Serialize_PlanWithoutOptionalFields_IncludesNulls()
    {
        // Arrange
        var plan = new PlannerOutput
        {
            Plan = new PlanDetails
            {
                Summary = "Simple plan",
                Steps = new List<PlanStep>()
            },
            FileList = new List<FileOperation>(),
            Risk = new RiskAssessment
            {
                Level = "low",
                Factors = new List<string>(),
                Mitigation = "None"
            },
            NeedsApproval = false
        };

        // Act
        var json = JsonSerializer.Serialize(plan);

        // Assert - By default, System.Text.Json includes null values
        json.Should().Contain("\"verify\":null");
        json.Should().Contain("\"rollback\":null");
        json.Should().Contain("\"approval_reason\":null");
    }

    #endregion

    #region Complex Plan Tests

    [Fact]
    public void Deserialize_ComplexPlanWithAllFields_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
              "plan": {
                "summary": "Complete feature implementation",
                "steps": [
                  {
                    "step_number": 1,
                    "description": "Create domain model",
                    "file_target": "Models/User.cs",
                    "agent": "coder",
                    "estimated_loc": 150
                  },
                  {
                    "step_number": 2,
                    "description": "Create repository",
                    "file_target": "Repositories/UserRepository.cs",
                    "agent": "coder",
                    "estimated_loc": 200
                  },
                  {
                    "step_number": 3,
                    "description": "Create tests",
                    "file_target": "Tests/UserTests.cs",
                    "agent": "tester",
                    "estimated_loc": 300
                  }
                ]
              },
              "file_list": [
                {"path": "Models/User.cs", "operation": "create", "reason": "Domain model"},
                {"path": "Repositories/UserRepository.cs", "operation": "create", "reason": "Data access"},
                {"path": "Tests/UserTests.cs", "operation": "create", "reason": "Test coverage"}
              ],
              "risk": {
                "level": "medium",
                "factors": ["Database changes", "New API endpoints"],
                "mitigation": "Integration tests and staged deployment"
              },
              "verify": {
                "acceptance_criteria": ["All tests pass", "Code coverage > 80%", "No compiler warnings"],
                "test_commands": ["dotnet test --filter Category=Integration"],
                "manual_checks": ["API contract validation", "Database schema review"]
              },
              "rollback": {
                "strategy": "Database migration rollback and git revert",
                "commands": ["dotnet ef database update PreviousMigration", "git revert HEAD"],
                "notes": "Ensure database backup exists before execution"
              },
              "needs_approval": true,
              "approval_reason": "Database schema changes require DBA review"
            }
            """;

        // Act
        var output = JsonSerializer.Deserialize<PlannerOutput>(json);

        // Assert
        output.Should().NotBeNull();
        output!.Plan.Steps.Should().HaveCount(3);
        output.Plan.Steps.Sum(s => s.EstimatedLoc).Should().Be(650);
        output.FileList.Should().HaveCount(3);
        output.Risk.Level.Should().Be("medium");
        output.Risk.Factors.Should().HaveCount(2);
        output.Verify.Should().NotBeNull();
        output.Verify!.AcceptanceCriteria.Should().HaveCount(3);
        output.Rollback.Should().NotBeNull();
        output.Rollback!.Commands.Should().HaveCount(2);
        output.NeedsApproval.Should().BeTrue();
        output.ApprovalReason.Should().Contain("DBA review");
    }

    #endregion
}
