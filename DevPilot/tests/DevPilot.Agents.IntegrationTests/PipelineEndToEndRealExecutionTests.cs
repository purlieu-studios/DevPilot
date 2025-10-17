using DevPilot.Agents;
using DevPilot.Core;
using DevPilot.Orchestrator;
using FluentAssertions;
using Xunit;

namespace DevPilot.Agents.IntegrationTests;

/// <summary>
/// End-to-end validation tests that execute the full pipeline with real Claude API calls.
/// These tests are skipped by default and should be run manually to validate infrastructure.
/// </summary>
public sealed class PipelineEndToEndRealExecutionTests
{
    private const string AgentsDirectory = "../../../../../.agents";

    /// <summary>
    /// Full end-to-end test with real Claude API calls across all 5 stages.
    /// This test takes 10-15 minutes to complete and requires live API access.
    ///
    /// Expected behavior:
    /// 1. Planner generates structured JSON via MCP tools (2-3 min)
    /// 2. Approval gate evaluates plan (instant)
    /// 3. Coder generates placeholder diff (2-3 min)
    /// 4. Reviewer provides placeholder verdict (2-3 min)
    /// 5. Tester provides placeholder report (2-3 min)
    /// 6. Evaluator provides placeholder scores (2-3 min)
    /// 7. Pipeline completes with Success result
    /// </summary>
    [Fact(Skip = "Requires live Claude API access and takes 10-15 minutes. Run manually for validation.")]
    public async Task ExecutePipeline_SimpleCalculatorRequest_CompletesAllStages()
    {
        // Arrange
        var loader = new AgentLoader(AgentsDirectory);
        var agentMappings = new Dictionary<string, PipelineStage>
        {
            ["planner"] = PipelineStage.Planning,
            ["coder"] = PipelineStage.Coding,
            ["reviewer"] = PipelineStage.Reviewing,
            ["tester"] = PipelineStage.Testing,
            ["evaluator"] = PipelineStage.Evaluating
        };

        var agents = new Dictionary<PipelineStage, IAgent>();
        foreach (var (agentName, stage) in agentMappings)
        {
            var definition = await loader.LoadAgentAsync(agentName, TestContext.Current.CancellationToken);
            var agent = new ClaudeCliAgent(definition);
            agents[stage] = agent;
        }

        var pipeline = new Pipeline(agents);
        var userRequest = "Create a Calculator class with Add and Subtract methods";

        // Act
        var result = await pipeline.ExecuteAsync(userRequest, TestContext.Current.CancellationToken);

        // Assert - Pipeline Infrastructure
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("pipeline should complete all stages without errors");
        result.RequiresApproval.Should().BeFalse("simple Calculator request should not trigger approval gates");
        result.FinalStage.Should().Be(PipelineStage.Completed);
        result.Duration.Should().BeGreaterThan(TimeSpan.FromMinutes(5), "all 5 agents should execute");
        result.Duration.Should().BeLessThan(TimeSpan.FromMinutes(20), "should complete within reasonable time");

        // Assert - PipelineContext State
        result.Context.Should().NotBeNull();
        result.Context.PipelineId.Should().NotBeNullOrEmpty();
        result.Context.UserRequest.Should().Be(userRequest);
        result.Context.CurrentStage.Should().Be(PipelineStage.Completed);

        // Assert - Stage Outputs (all should be populated)
        result.Context.Plan.Should().NotBeNullOrWhiteSpace("planner should generate plan JSON");
        result.Context.Patch.Should().NotBeNullOrWhiteSpace("coder should generate patch");
        result.Context.Review.Should().NotBeNullOrWhiteSpace("reviewer should generate verdict");
        result.Context.TestReport.Should().NotBeNullOrWhiteSpace("tester should generate report");
        result.Context.Scores.Should().NotBeNullOrWhiteSpace("evaluator should generate scores");

        // Assert - Stage History (should record all transitions)
        result.Context.StageHistory.Should().HaveCount(6, "should record NotStarted + 5 stage transitions");
        result.Context.StageHistory.Should().ContainSingle(e => e.Stage == PipelineStage.Planning);
        result.Context.StageHistory.Should().ContainSingle(e => e.Stage == PipelineStage.Coding);
        result.Context.StageHistory.Should().ContainSingle(e => e.Stage == PipelineStage.Reviewing);
        result.Context.StageHistory.Should().ContainSingle(e => e.Stage == PipelineStage.Testing);
        result.Context.StageHistory.Should().ContainSingle(e => e.Stage == PipelineStage.Evaluating);
        result.Context.StageHistory.Should().ContainSingle(e => e.Stage == PipelineStage.Completed);

        // Assert - Timestamps
        result.Context.StartedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(20));
        result.Context.CompletedAt.Should().NotBeNull();
        result.Context.CompletedAt.Should().BeAfter(result.Context.StartedAt);
    }

    /// <summary>
    /// Tests that approval gates correctly trigger for high-risk operations.
    /// This validates the ApprovalGate evaluation logic with real agent execution.
    ///
    /// Note: This test may not trigger approval if the planner doesn't flag the request as high-risk.
    /// Expected to complete after Planning stage only (2-3 min).
    /// </summary>
    [Fact(Skip = "Requires live Claude API access. Run manually for approval gate validation.")]
    public async Task ExecutePipeline_HighRiskRequest_TriggersApprovalGate()
    {
        // Arrange
        var loader = new AgentLoader(AgentsDirectory);
        var agentMappings = new Dictionary<string, PipelineStage>
        {
            ["planner"] = PipelineStage.Planning,
            ["coder"] = PipelineStage.Coding,
            ["reviewer"] = PipelineStage.Reviewing,
            ["tester"] = PipelineStage.Testing,
            ["evaluator"] = PipelineStage.Evaluating
        };

        var agents = new Dictionary<PipelineStage, IAgent>();
        foreach (var (agentName, stage) in agentMappings)
        {
            var definition = await loader.LoadAgentAsync(agentName, TestContext.Current.CancellationToken);
            var agent = new ClaudeCliAgent(definition);
            agents[stage] = agent;
        }

        var pipeline = new Pipeline(agents);
        var userRequest = "Delete all user authentication files and remove database migration scripts";

        // Act
        var result = await pipeline.ExecuteAsync(userRequest, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.FinalStage.Should().Be(PipelineStage.AwaitingApproval, "high-risk request should trigger approval");
        result.RequiresApproval.Should().BeTrue();
        result.Context.ApprovalReason.Should().NotBeNullOrWhiteSpace("should explain why approval is required");
        result.Context.Plan.Should().NotBeNullOrWhiteSpace("planner should still generate plan");
        result.Context.Patch.Should().BeNullOrWhiteSpace("coder should not run after approval gate");
    }

    /// <summary>
    /// Tests workspace creation, patch application, and cleanup with mocked agents.
    /// Validates infrastructure added in PRs #24-27 without requiring live API calls.
    /// </summary>
    [Fact]
    public async Task ExecutePipeline_MockedAgents_CreatesWorkspaceAndAppliesPatch()
    {
        // Arrange
        var safePlanJson = """
            {
              "plan": {"summary": "Create Calculator", "steps": [{"step_number": 1, "description": "Add Calculator", "file_target": "Calculator.cs", "agent": "coder", "estimated_loc": 50}]},
              "file_list": [{"path": "Calculator.cs", "operation": "create", "reason": "Implementation"}],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "verify": {"acceptance_criteria": [], "test_commands": ["dotnet test"], "manual_checks": []},
              "rollback": {"strategy": "Delete files", "commands": [], "notes": ""},
              "needs_approval": false
            }
            """;

        var validPatch = """
            diff --git a/Calculator.cs b/Calculator.cs
            new file mode 100644
            --- /dev/null
            +++ b/Calculator.cs
            @@ -0,0 +1,5 @@
            +public class Calculator
            +{
            +    public int Add(int a, int b) => a + b;
            +}
            """;

        var approvalReview = """{"verdict": "APPROVE", "issues": [], "summary": "Good code", "metrics": {"complexity": 1, "maintainability": 10}}""";
        var passingTests = """{"pass": true, "summary": "All tests passed", "test_results": [], "coverage": {"line_coverage_percent": 100}, "performance": {"total_duration_ms": 100, "total_tests": 1, "passed": 1, "failed": 0, "skipped": 0}}""";
        var highScores = """{"evaluation": {"overall_score": 9.0, "scores": {"plan_quality": 9.0, "code_quality": 9.0, "test_coverage": 9.0, "documentation": 9.0, "maintainability": 9.0}, "strengths": ["Good"], "weaknesses": [], "recommendations": [], "final_verdict": "ACCEPT", "justification": "Excellent"}}""";

        var agents = new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", true, safePlanJson),
            [PipelineStage.Coding] = new MockAgent("coder", true, validPatch),
            [PipelineStage.Reviewing] = new MockAgent("reviewer", true, approvalReview),
            [PipelineStage.Testing] = new MockAgent("tester", true, passingTests),
            [PipelineStage.Evaluating] = new MockAgent("evaluator", true, highScores)
        };

        var pipeline = new Pipeline(agents);
        var userRequest = "Create a Calculator class";

        // Act
        var result = await pipeline.ExecuteAsync(userRequest, TestContext.Current.CancellationToken);

        // Assert - Pipeline Success
        result.Success.Should().BeTrue();
        result.FinalStage.Should().Be(PipelineStage.Completed);

        // Assert - Workspace Infrastructure
        result.Context.WorkspaceRoot.Should().NotBeNullOrWhiteSpace("workspace should be created");
        result.Context.WorkspaceRoot.Should().Contain(".devpilot").And.Contain("workspaces").And.Contain(result.Context.PipelineId);

        // Assert - Patch Application
        result.Context.AppliedFiles.Should().NotBeNull();
        result.Context.AppliedFiles.Should().Contain("Calculator.cs", "patch should be applied to workspace");

        // Assert - Workspace Preservation (workspace should be preserved on success for user to apply changes)
        Directory.Exists(result.Context.WorkspaceRoot).Should().BeTrue("workspace should be preserved after successful completion");

        // Cleanup workspace after test
        if (Directory.Exists(result.Context.WorkspaceRoot))
        {
            Directory.Delete(result.Context.WorkspaceRoot, recursive: true);
        }
    }

    /// <summary>
    /// Tests that reviewer REJECT verdict fails the pipeline correctly.
    /// Validates verdict enforcement added in PR #26.
    /// </summary>
    [Fact]
    public async Task ExecutePipeline_ReviewerRejects_FailsPipelineAtReviewStage()
    {
        // Arrange
        var safePlanJson = """
            {
              "plan": {"summary": "Create Calculator", "steps": [{"step_number": 1, "description": "Add Calculator", "file_target": "Calculator.cs", "agent": "coder", "estimated_loc": 50}]},
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "verify": {"acceptance_criteria": [], "test_commands": ["dotnet test"], "manual_checks": []},
              "rollback": {"strategy": "Delete files", "commands": [], "notes": ""},
              "needs_approval": false
            }
            """;

        var validPatch = """
            diff --git a/Calculator.cs b/Calculator.cs
            new file mode 100644
            --- /dev/null
            +++ b/Calculator.cs
            @@ -0,0 +1,5 @@
            +public class Calculator
            +{
            +    public int Add(int a, int b) => a + b;
            +}
            """;

        var rejectReview = """
            {
              "verdict": "REJECT",
              "issues": [
                {"severity": "error", "file": "Calculator.cs", "line": 1, "message": "Poor code quality", "suggestion": "Refactor"}
              ],
              "summary": "Code does not meet standards",
              "metrics": {"complexity": 8, "maintainability": 2}
            }
            """;

        var agents = new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", true, safePlanJson),
            [PipelineStage.Coding] = new MockAgent("coder", true, validPatch),
            [PipelineStage.Reviewing] = new MockAgent("reviewer", true, rejectReview),
            [PipelineStage.Testing] = new MockAgent("tester", true, "{}"),
            [PipelineStage.Evaluating] = new MockAgent("evaluator", true, "{}")
        };

        var pipeline = new Pipeline(agents);
        var userRequest = "Create a Calculator class";

        // Act
        var result = await pipeline.ExecuteAsync(userRequest, TestContext.Current.CancellationToken);

        // Assert - Pipeline Failure
        result.Success.Should().BeFalse("reviewer REJECT verdict should fail pipeline");
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("Reviewer rejected code").And.Contain("REJECT");

        // Assert - Stages Completed Before Failure
        result.Context.Plan.Should().NotBeNullOrWhiteSpace("planner should complete");
        result.Context.Patch.Should().NotBeNullOrWhiteSpace("coder should complete");
        result.Context.Review.Should().NotBeNullOrWhiteSpace("reviewer should complete");
        result.Context.TestReport.Should().BeNullOrWhiteSpace("tester should not run after rejection");
        result.Context.Scores.Should().BeNullOrWhiteSpace("evaluator should not run after rejection");

        // Assert - Workspace Cleanup (even on failure)
        if (result.Context.WorkspaceRoot != null)
        {
            Directory.Exists(result.Context.WorkspaceRoot).Should().BeFalse("workspace should be cleaned up even on failure");
        }
    }

    /// <summary>
    /// Tests that evaluator REJECT verdict fails the pipeline correctly.
    /// Validates evaluator verdict enforcement added in PR #24.
    /// </summary>
    [Fact]
    public async Task ExecutePipeline_EvaluatorRejects_FailsPipelineAtEvaluationStage()
    {
        // Arrange
        var safePlanJson = """
            {
              "plan": {"summary": "Create Calculator", "steps": [{"step_number": 1, "description": "Add Calculator", "file_target": "Calculator.cs", "agent": "coder", "estimated_loc": 50}]},
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "verify": {"acceptance_criteria": [], "test_commands": ["dotnet test"], "manual_checks": []},
              "rollback": {"strategy": "Delete files", "commands": [], "notes": ""},
              "needs_approval": false
            }
            """;

        var validPatch = """
            diff --git a/Calculator.cs b/Calculator.cs
            new file mode 100644
            --- /dev/null
            +++ b/Calculator.cs
            @@ -0,0 +1,5 @@
            +public class Calculator
            +{
            +    public int Add(int a, int b) => a + b;
            +}
            """;

        var approvalReview = """{"verdict": "APPROVE", "issues": [], "summary": "Good", "metrics": {"complexity": 1, "maintainability": 10}}""";
        var passingTests = """{"pass": true, "summary": "All tests passed", "test_results": [], "coverage": {"line_coverage_percent": 100}, "performance": {"total_duration_ms": 100, "total_tests": 1, "passed": 1, "failed": 0, "skipped": 0}}""";
        var lowScores = """
            {
              "evaluation": {
                "overall_score": 5.0,
                "scores": {"plan_quality": 5.0, "code_quality": 5.0, "test_coverage": 5.0, "documentation": 5.0, "maintainability": 5.0},
                "strengths": [],
                "weaknesses": ["Poor quality"],
                "recommendations": ["Improve everything"],
                "final_verdict": "REJECT",
                "justification": "Does not meet standards"
              }
            }
            """;

        var agents = new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", true, safePlanJson),
            [PipelineStage.Coding] = new MockAgent("coder", true, validPatch),
            [PipelineStage.Reviewing] = new MockAgent("reviewer", true, approvalReview),
            [PipelineStage.Testing] = new MockAgent("tester", true, passingTests),
            [PipelineStage.Evaluating] = new MockAgent("evaluator", true, lowScores)
        };

        var pipeline = new Pipeline(agents);
        var userRequest = "Create a Calculator class";

        // Act
        var result = await pipeline.ExecuteAsync(userRequest, TestContext.Current.CancellationToken);

        // Assert - Pipeline Failure
        result.Success.Should().BeFalse("evaluator REJECT verdict should fail pipeline");
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("Evaluator rejected").And.Contain("5.0").And.Contain("REJECT");

        // Assert - All Stages Completed
        result.Context.Plan.Should().NotBeNullOrWhiteSpace();
        result.Context.Patch.Should().NotBeNullOrWhiteSpace();
        result.Context.Review.Should().NotBeNullOrWhiteSpace();
        result.Context.TestReport.Should().NotBeNullOrWhiteSpace();
        result.Context.Scores.Should().NotBeNullOrWhiteSpace("evaluator should complete before failing");

        // Assert - Workspace Cleanup
        if (result.Context.WorkspaceRoot != null)
        {
            Directory.Exists(result.Context.WorkspaceRoot).Should().BeFalse("workspace should be cleaned up even on failure");
        }
    }

    /// <summary>
    /// Mock agent implementation for E2E testing without live API calls.
    /// </summary>
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
                Description = "Mock agent for testing",
                SystemPrompt = "Test prompt",
                Model = "sonnet"
            };
        }

        public AgentDefinition Definition { get; }

        public Task<AgentResult> ExecuteAsync(string input, AgentContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = _succeeds
                ? AgentResult.CreateSuccess(Definition.Name, _output)
                : AgentResult.CreateFailure(Definition.Name, _output);

            return Task.FromResult(result);
        }
    }
}
