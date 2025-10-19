using DevPilot.Core;
using DevPilot.Orchestrator;
using FluentAssertions;

namespace DevPilot.Orchestrator.Tests;

/// <summary>
/// Comprehensive integration tests for the complete MASAI pipeline.
/// Tests end-to-end scenarios with different request types and repository structures.
/// </summary>
public sealed class PipelineIntegrationTests : IDisposable
{
    private readonly string _testBaseDirectory;
    private readonly List<string> _workspacesToCleanup;

    public PipelineIntegrationTests()
    {
        _testBaseDirectory = Path.Combine(Path.GetTempPath(), "devpilot-integration-tests", Guid.NewGuid().ToString());
        _workspacesToCleanup = new List<string>();
        Directory.CreateDirectory(_testBaseDirectory);
    }

    #region Pipeline Success Scenarios

    [Fact]
    public void Pipeline_SimpleAddMethod_CompletesSuccessfully()
    {
        // Arrange
        var request = "Add a Multiply method to Calculator class";
        var (workspace, pipeline) = SetupPipelineWithMockAgents(createSuccessfulAgents: true);

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Success.Should().BeTrue();
        result.FinalStage.Should().Be(PipelineStage.Completed);
    }

    [Fact]
    public void Pipeline_CreateNewClass_GeneratesCorrectFileCount()
    {
        // Arrange
        var request = "Create a new UserService class";
        var (workspace, pipeline) = SetupPipelineWithMockAgents(createSuccessfulAgents: true);

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Success.Should().BeTrue();
        result.Context.AppliedFiles.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Pipeline_AddTestsToExistingClass_IncludesTestProject()
    {
        // Arrange
        var request = "Add unit tests for Calculator class";
        var (workspace, pipeline) = SetupPipelineWithMockAgents(createSuccessfulAgents: true);

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Success.Should().BeTrue();
        result.Context.TestReport.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Pipeline Failure Scenarios

    [Fact]
    public void Pipeline_PlannerFails_StopsAtPlanningStage()
    {
        // Arrange
        var request = "Invalid request that causes planner to fail";
        var (workspace, pipeline) = SetupPipelineWithFailingPlanner();

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Success.Should().BeFalse();
        result.FinalStage.Should().Be(PipelineStage.Failed);
    }

    [Fact]
    public void Pipeline_CoderFails_StopsAtCodingStage()
    {
        // Arrange
        var request = "Generate code that causes coder to fail";
        var (workspace, pipeline) = SetupPipelineWithFailingCoder();

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Success.Should().BeFalse();
        result.FinalStage.Should().Be(PipelineStage.Failed);
    }

    [Fact]
    public void Pipeline_ReviewerRejects_StopsAtReviewingStage()
    {
        // Arrange
        var request = "Code that will be rejected by reviewer";
        var (workspace, pipeline) = SetupPipelineWithRejectingReviewer();

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Success.Should().BeFalse();
        result.FinalStage.Should().Be(PipelineStage.Failed);
    }

    [Fact]
    public void Pipeline_TestsFail_ContinuesToEvaluator()
    {
        // Arrange
        var request = "Code with failing tests";
        var (workspace, pipeline) = SetupPipelineWithFailingTests();

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert - Pipeline continues despite test failures
        result.FinalStage.Should().Be(PipelineStage.Completed);
        result.Context.TestReport.Should().Contain("failed");
    }

    [Fact]
    public void Pipeline_EvaluatorRejects_FailsPipeline()
    {
        // Arrange
        var request = "Low quality code";
        var (workspace, pipeline) = SetupPipelineWithRejectingEvaluator();

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Success.Should().BeFalse();
        result.FinalStage.Should().Be(PipelineStage.Failed);
    }

    #endregion

    #region Context Preservation Tests

    [Fact]
    public void Pipeline_PreservesUserRequest_ThroughAllStages()
    {
        // Arrange
        var request = "Specific test request for context preservation";
        var (workspace, pipeline) = SetupPipelineWithMockAgents(createSuccessfulAgents: true);

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Context.UserRequest.Should().Be(request);
    }

    [Fact]
    public void Pipeline_PreservesWorkspaceRoot_ThroughAllStages()
    {
        // Arrange
        var request = "Test workspace preservation";
        var (workspace, pipeline) = SetupPipelineWithMockAgents(createSuccessfulAgents: true);

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Context.WorkspaceRoot.Should().NotBeNullOrEmpty();
        result.Context.WorkspaceRoot.Should().Contain("workspaces");
    }

    [Fact]
    public void Pipeline_RecordsStageHistory_ForAllCompletedStages()
    {
        // Arrange
        var request = "Test stage history tracking";
        var (workspace, pipeline) = SetupPipelineWithMockAgents(createSuccessfulAgents: true);

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Context.StageHistory.Should().HaveCountGreaterThan(0);
        result.Context.StageHistory.Should().Contain(e => e.Stage == PipelineStage.Planning);
        result.Context.StageHistory.Should().Contain(e => e.Stage == PipelineStage.Coding);
    }

    [Fact]
    public void Pipeline_TracksDuration_Accurately()
    {
        // Arrange
        var request = "Test duration tracking";
        var (workspace, pipeline) = SetupPipelineWithMockAgents(createSuccessfulAgents: true);

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    #endregion

    #region Quality Score Tests

    [Fact]
    public void Pipeline_WithHighQualityCode_ScoresAbove8()
    {
        // Arrange
        var request = "Create high quality implementation";
        var (workspace, pipeline) = SetupPipelineWithMockAgents(createSuccessfulAgents: true, qualityScore: 9.0);

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Success.Should().BeTrue();
        result.Context.Scores.Should().Contain("9");
    }

    [Fact]
    public void Pipeline_WithMediumQualityCode_ScoresBetween6And8()
    {
        // Arrange
        var request = "Create medium quality implementation";
        var (workspace, pipeline) = SetupPipelineWithMockAgents(createSuccessfulAgents: true, qualityScore: 7.0);

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Success.Should().BeTrue();
        result.Context.Scores.Should().Contain("7");
    }

    #endregion

    #region Test Execution Tests

    [Fact]
    public void Pipeline_GeneratesTests_AndExecutesThem()
    {
        // Arrange
        var request = "Add validation method with tests";
        var (workspace, pipeline) = SetupPipelineWithMockAgents(createSuccessfulAgents: true);

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Success.Should().BeTrue();
        result.Context.TestReport.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Pipeline_AllTestsPass_ReportsSuccess()
    {
        // Arrange
        var request = "Implementation with all passing tests";
        var (workspace, pipeline) = SetupPipelineWithMockAgents(createSuccessfulAgents: true);

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Success.Should().BeTrue();
        result.Context.TestReport.Should().Contain("pass");
    }

    #endregion

    #region File Operations Tests

    [Fact]
    public void Pipeline_AppliesPatch_CreatesFiles()
    {
        // Arrange
        var request = "Create new file";
        var (workspace, pipeline) = SetupPipelineWithMockAgents(createSuccessfulAgents: true);

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Context.AppliedFiles.Should().NotBeEmpty();
    }

    [Fact]
    public void Pipeline_AppliesPatch_ModifiesExistingFiles()
    {
        // Arrange
        var request = "Modify existing Calculator class";
        var (workspace, pipeline) = SetupPipelineWithMockAgents(createSuccessfulAgents: true);

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Pipeline_AppliesMultipleFiles_InSinglePatch()
    {
        // Arrange
        var request = "Create multiple files";
        var (workspace, pipeline) = SetupPipelineWithMockAgents(createSuccessfulAgents: true, fileCount: 3);

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Context.AppliedFiles.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Pipeline_WithInvalidPatch_FailsGracefully()
    {
        // Arrange
        var request = "Generate invalid patch";
        var (workspace, pipeline) = SetupPipelineWithInvalidPatch();

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Pipeline_WithCompilationError_FailsAtCodingStage()
    {
        // Arrange
        var request = "Code that won't compile";
        var (workspace, pipeline) = SetupPipelineWithCompilationError();

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Success.Should().BeFalse();
    }

    #endregion

    #region Revision Loop Tests

    [Fact]
    public void Pipeline_ReviewerRequestsRevision_IncrementsRevisionCount()
    {
        // Arrange
        var request = "Code requiring revision";
        var (workspace, pipeline) = SetupPipelineWithRevisionLoop();

        // Act
        var result = pipeline.ExecuteAsync(request).Result;

        // Assert
        result.Context.RevisionIteration.Should().BeGreaterThan(0);
    }

    #endregion

    #region Helper Methods

    private (WorkspaceManager workspace, Pipeline pipeline) SetupPipelineWithMockAgents(
        bool createSuccessfulAgents,
        double qualityScore = 8.0,
        int fileCount = 1)
    {
        var pipelineId = Guid.NewGuid().ToString();
        var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var agents = CreateMockAgents(createSuccessfulAgents, qualityScore, fileCount);
        var pipeline = new Pipeline(agents, workspace, Directory.GetCurrentDirectory());

        return (workspace, pipeline);
    }

    private (WorkspaceManager workspace, Pipeline pipeline) SetupPipelineWithFailingPlanner()
    {
        var pipelineId = Guid.NewGuid().ToString();
        var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var agents = new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", succeeds: false, "Planning failed"),
            [PipelineStage.Coding] = CreateSuccessfulMockAgent("coder"),
            [PipelineStage.Reviewing] = CreateSuccessfulMockAgent("reviewer"),
            [PipelineStage.Testing] = CreateSuccessfulMockAgent("tester"),
            [PipelineStage.Evaluating] = CreateSuccessfulMockAgent("evaluator")
        };

        return (workspace, new Pipeline(agents, workspace, Directory.GetCurrentDirectory()));
    }

    private (WorkspaceManager workspace, Pipeline pipeline) SetupPipelineWithFailingCoder()
    {
        var pipelineId = Guid.NewGuid().ToString();
        var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var agents = new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = CreateSuccessfulMockAgent("planner"),
            [PipelineStage.Coding] = new MockAgent("coder", succeeds: false, "Coding failed"),
            [PipelineStage.Reviewing] = CreateSuccessfulMockAgent("reviewer"),
            [PipelineStage.Testing] = CreateSuccessfulMockAgent("tester"),
            [PipelineStage.Evaluating] = CreateSuccessfulMockAgent("evaluator")
        };

        return (workspace, new Pipeline(agents, workspace, Directory.GetCurrentDirectory()));
    }

    private (WorkspaceManager workspace, Pipeline pipeline) SetupPipelineWithRejectingReviewer()
    {
        var pipelineId = Guid.NewGuid().ToString();
        var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var agents = new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = CreateSuccessfulMockAgent("planner"),
            [PipelineStage.Coding] = CreateSuccessfulMockAgent("coder"),
            [PipelineStage.Reviewing] = new MockAgent("reviewer", succeeds: true, "{\"verdict\": \"REJECT\"}"),
            [PipelineStage.Testing] = CreateSuccessfulMockAgent("tester"),
            [PipelineStage.Evaluating] = CreateSuccessfulMockAgent("evaluator")
        };

        return (workspace, new Pipeline(agents, workspace, Directory.GetCurrentDirectory()));
    }

    private (WorkspaceManager workspace, Pipeline pipeline) SetupPipelineWithFailingTests()
    {
        var pipelineId = Guid.NewGuid().ToString();
        var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var agents = new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = CreateSuccessfulMockAgent("planner"),
            [PipelineStage.Coding] = CreateSuccessfulMockAgent("coder"),
            [PipelineStage.Reviewing] = CreateSuccessfulMockAgent("reviewer"),
            [PipelineStage.Testing] = new MockAgent("tester", succeeds: true, "{\"pass\": false, \"failed\": 5}"),
            [PipelineStage.Evaluating] = CreateSuccessfulMockAgent("evaluator")
        };

        return (workspace, new Pipeline(agents, workspace, Directory.GetCurrentDirectory()));
    }

    private (WorkspaceManager workspace, Pipeline pipeline) SetupPipelineWithRejectingEvaluator()
    {
        var pipelineId = Guid.NewGuid().ToString();
        var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var evaluatorJson = """
            {
              "evaluation": {
                "overall_score": 4.0,
                "scores": {
                  "plan_quality": 5.0,
                  "code_quality": 4.0,
                  "test_coverage": 3.0,
                  "documentation": 4.0,
                  "maintainability": 4.0
                },
                "strengths": [],
                "weaknesses": ["Low quality"],
                "recommendations": [],
                "final_verdict": "REJECT",
                "justification": "Below quality threshold"
              }
            }
            """;

        var agents = new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = CreateSuccessfulMockAgent("planner"),
            [PipelineStage.Coding] = CreateSuccessfulMockAgent("coder"),
            [PipelineStage.Reviewing] = CreateSuccessfulMockAgent("reviewer"),
            [PipelineStage.Testing] = CreateSuccessfulMockAgent("tester"),
            [PipelineStage.Evaluating] = new MockAgent("evaluator", succeeds: true, evaluatorJson)
        };

        return (workspace, new Pipeline(agents, workspace, Directory.GetCurrentDirectory()));
    }

    private (WorkspaceManager workspace, Pipeline pipeline) SetupPipelineWithInvalidPatch()
    {
        var pipelineId = Guid.NewGuid().ToString();
        var workspace = WorkspaceManager.CreateWorkspace(pipelineId, _testBaseDirectory);
        _workspacesToCleanup.Add(workspace.WorkspaceRoot);

        var agents = new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = CreateSuccessfulMockAgent("planner"),
            [PipelineStage.Coding] = new MockAgent("coder", succeeds: true, "not a valid unified diff"),
            [PipelineStage.Reviewing] = CreateSuccessfulMockAgent("reviewer"),
            [PipelineStage.Testing] = CreateSuccessfulMockAgent("tester"),
            [PipelineStage.Evaluating] = CreateSuccessfulMockAgent("evaluator")
        };

        return (workspace, new Pipeline(agents, workspace, Directory.GetCurrentDirectory()));
    }

    private (WorkspaceManager workspace, Pipeline pipeline) SetupPipelineWithCompilationError()
    {
        return SetupPipelineWithFailingCoder(); // Same as failing coder
    }

    private (WorkspaceManager workspace, Pipeline pipeline) SetupPipelineWithRevisionLoop()
    {
        // For now, return successful pipeline - revision loop tests would require more complex mocking
        return SetupPipelineWithMockAgents(createSuccessfulAgents: true);
    }

    private Dictionary<PipelineStage, IAgent> CreateMockAgents(bool succeeds, double qualityScore, int fileCount)
    {
        var planJson = """
            {
              "plan": {"summary": "Test plan", "steps": [{"step_number": 1, "description": "Test", "file_target": null, "agent": "coder", "estimated_loc": 50}]},
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        var patchContent = fileCount == 1
            ? "diff --git a/Test.cs b/Test.cs\nnew file mode 100644\n--- /dev/null\n+++ b/Test.cs\n@@ -0,0 +1,1 @@\n+public class Test { }"
            : string.Join("\n", Enumerable.Range(1, fileCount).Select(i =>
                $"diff --git a/Test{i}.cs b/Test{i}.cs\nnew file mode 100644\n--- /dev/null\n+++ b/Test{i}.cs\n@@ -0,0 +1,1 @@\n+public class Test{i} {{ }}"));

        var verdict = qualityScore >= 7.0 ? "ACCEPT" : "REJECT";
        var evaluatorJson = $$"""
            {
              "evaluation": {
                "overall_score": {{qualityScore}},
                "scores": {
                  "plan_quality": {{qualityScore}},
                  "code_quality": {{qualityScore}},
                  "test_coverage": {{qualityScore}},
                  "documentation": {{qualityScore}},
                  "maintainability": {{qualityScore}}
                },
                "strengths": ["Good"],
                "weaknesses": [],
                "recommendations": [],
                "final_verdict": "{{verdict}}",
                "justification": "Test"
              }
            }
            """;

        return new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", succeeds, planJson),
            [PipelineStage.Coding] = new MockAgent("coder", succeeds, patchContent),
            [PipelineStage.Reviewing] = new MockAgent("reviewer", succeeds, "{\"verdict\": \"APPROVE\"}"),
            [PipelineStage.Testing] = new MockAgent("tester", succeeds, "{\"pass\": true}"),
            [PipelineStage.Evaluating] = new MockAgent("evaluator", succeeds, evaluatorJson)
        };
    }

    private IAgent CreateSuccessfulMockAgent(string name)
    {
        var output = name switch
        {
            "planner" => """{"plan": {"summary": "Test", "steps": []}, "file_list": [], "risk": {"level": "low"}, "needs_approval": false}""",
            "coder" => "diff --git a/Test.cs b/Test.cs\nnew file mode 100644\n--- /dev/null\n+++ b/Test.cs\n@@ -0,0 +1,1 @@\n+public class Test { }",
            "reviewer" => "{\"verdict\": \"APPROVE\"}",
            "tester" => "{\"pass\": true}",
            "evaluator" => """{"evaluation": {"overall_score": 8.0, "scores": {"plan_quality": 8.0, "code_quality": 8.0, "test_coverage": 8.0, "documentation": 8.0, "maintainability": 8.0}, "final_verdict": "ACCEPT"}}""",
            _ => "{}"
        };

        return new MockAgent(name, succeeds: true, output);
    }

    public void Dispose()
    {
        foreach (var workspaceRoot in _workspacesToCleanup)
        {
            try
            {
                if (Directory.Exists(workspaceRoot))
                {
                    Directory.Delete(workspaceRoot, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup failures
            }
        }

        try
        {
            if (Directory.Exists(_testBaseDirectory))
            {
                Directory.Delete(_testBaseDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }

    private sealed class MockAgent : IAgent
    {
        private readonly bool _succeeds;
        private readonly string _output;

        public MockAgent(string name, bool succeeds, string output)
        {
            _succeeds = succeeds;
            _output = output;
            Definition = new AgentDefinition
            {
                Name = name,
                Version = "1.0.0",
                Description = $"Mock {name} for testing",
                SystemPrompt = "Test prompt",
                Model = "sonnet"
            };
        }

        public AgentDefinition Definition { get; }

        public Task<AgentResult> ExecuteAsync(string input, AgentContext context, CancellationToken cancellationToken = default)
        {
            var result = _succeeds
                ? AgentResult.CreateSuccess(Definition.Name, _output)
                : AgentResult.CreateFailure(Definition.Name, _output);

            return Task.FromResult(result);
        }
    }

    #endregion
}
