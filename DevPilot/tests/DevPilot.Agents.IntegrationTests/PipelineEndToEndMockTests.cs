using DevPilot.Agents;
using DevPilot.Core;
using DevPilot.Orchestrator;
using FluentAssertions;
using Xunit;

namespace DevPilot.Agents.IntegrationTests;

/// <summary>
/// Comprehensive mock-based integration tests covering all pipeline failure scenarios.
/// These tests run fast without API calls, enabling CI/CD regression testing.
/// </summary>
public sealed class PipelineEndToEndMockTests
{
    [Fact]
    public async Task ExecutePipeline_TesterReportsFailed_FailsPipelineAtTestStage()
    {
        // Arrange
        var safePlanJson = """
            {"plan": {"summary": "Test", "steps": [{"step_number": 1, "description": "Add", "file_target": "Test.cs", "agent": "coder", "estimated_loc": 10}]}, "file_list": [], "risk": {"level": "low", "factors": [], "mitigation": ""}, "verify": {"acceptance_criteria": [], "test_commands": ["dotnet test"], "manual_checks": []}, "rollback": {"strategy": "Delete files", "commands": [], "notes": ""}, "needs_approval": false}
            """;
        var validPatch = """
            diff --git a/Test.cs b/Test.cs
            new file mode 100644
            --- /dev/null
            +++ b/Test.cs
            @@ -0,0 +1,3 @@
            +public class Test
            +{
            +}
            """;
        var approvalReview = """
            {"verdict": "APPROVE", "issues": [], "summary": "OK", "metrics": {"complexity": 1, "maintainability": 10}}
            """;
        var failingTests = """
            {"pass": false, "summary": "2 of 5 tests failed", "test_results": [{"test_name": "Test1", "status": "failed", "duration_ms": 50, "message": "Expected 5 but got 3"}], "coverage": null, "performance": {"total_duration_ms": 200}}
            """;

        var agents = CreateMockAgents(safePlanJson, validPatch, approvalReview, failingTests, "{}");
        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Test request", TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse("failing tests should fail pipeline");
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.Context.TestReport.Should().Contain("pass\": false");
    }

    [Fact]
    public async Task ExecutePipeline_BuildFails_FailsAtTestStage()
    {
        // Arrange - TestingAgent will fail if it can't find workspace
        var safePlanJson = """
            {"plan": {"summary": "Test", "steps": [{"step_number": 1, "description": "Add", "file_target": "Test.cs", "agent": "coder", "estimated_loc": 10}]}, "file_list": [], "risk": {"level": "low", "factors": [], "mitigation": ""}, "verify": {"acceptance_criteria": [], "test_commands": ["dotnet test"], "manual_checks": []}, "rollback": {"strategy": "Delete files", "commands": [], "notes": ""}, "needs_approval": false}
            """;
        var validPatch = """
            diff --git a/Test.cs b/Test.cs
            new file mode 100644
            --- /dev/null
            +++ b/Test.cs
            @@ -0,0 +1,3 @@
            +public class Test { }
            """;
        var approvalReview = """
            {"verdict": "APPROVE", "issues": [], "summary": "OK", "metrics": {"complexity": 1, "maintainability": 10}}
            """;

        var agents = new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", true, safePlanJson),
            [PipelineStage.Coding] = new MockAgent("coder", true, validPatch),
            [PipelineStage.Reviewing] = new MockAgent("reviewer", true, approvalReview),
            [PipelineStage.Testing] = new MockAgent("tester", false, "Build failed: CS1234 Missing semicolon"),
            [PipelineStage.Evaluating] = new MockAgent("evaluator", true, "{}")
        };

        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Test request", TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse("build failures should fail pipeline");
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("Build failed");
    }

    [Fact]
    public async Task ExecutePipeline_ReviewerRevises_LoopsBackToCoder()
    {
        // Arrange
        var safePlanJson = """
            {"plan": {"summary": "Test", "steps": [{"step_number": 1, "description": "Add", "file_target": "Test.cs", "agent": "coder", "estimated_loc": 10}]}, "file_list": [], "risk": {"level": "low", "factors": [], "mitigation": ""}, "verify": {"acceptance_criteria": [], "test_commands": ["dotnet test"], "manual_checks": []}, "rollback": {"strategy": "Delete files", "commands": [], "notes": ""}, "needs_approval": false}
            """;
        var validPatch = """
            diff --git a/Test.cs b/Test.cs
            new file mode 100644
            --- /dev/null
            +++ b/Test.cs
            @@ -0,0 +1,3 @@
            +public class Test { }
            """;
        var reviseReview = """
            {"verdict": "REVISE", "issues": [{"severity": "warning", "file": "Test.cs", "line": 1, "message": "Add docs", "suggestion": "Add XML comments"}], "summary": "Needs docs", "metrics": {"complexity": 1, "maintainability": 8}}
            """;
        var approveReview = """
            {"verdict": "APPROVE", "issues": [], "summary": "Good", "metrics": {"complexity": 1, "maintainability": 10}}
            """;
        var passingTests = """
            {"pass": true, "summary": "All passed", "test_results": [], "coverage": null, "performance": {"total_duration_ms": 100}}
            """;
        var highScores = """
            {"evaluation": {"overall_score": 9.0, "scores": {"plan_quality": 9.0, "code_quality": 9.0, "test_coverage": 9.0, "documentation": 9.0, "maintainability": 9.0}, "strengths": [], "weaknesses": [], "recommendations": [], "final_verdict": "ACCEPT", "justification": "Good"}}
            """;

        var reviewerOutputs = new Queue<string>(new[] { reviseReview, approveReview });

        var agents = new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", true, safePlanJson),
            [PipelineStage.Coding] = new MockAgent("coder", true, validPatch),
            [PipelineStage.Reviewing] = new MultiOutputMockAgent("reviewer", reviewerOutputs),
            [PipelineStage.Testing] = new MockAgent("tester", true, passingTests),
            [PipelineStage.Evaluating] = new MockAgent("evaluator", true, highScores)
        };

        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Test request", TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue("should succeed after revision");
        result.FinalStage.Should().Be(PipelineStage.Completed);
        result.Context.RevisionIteration.Should().Be(1, "one revision should have occurred");
    }

    [Fact]
    public async Task ExecutePipeline_HighRiskPlan_TriggersApprovalGate()
    {
        // Arrange - Plan with high risk level
        var highRiskPlan = """
            {"plan": {"summary": "Delete files", "steps": [{"step_number": 1, "description": "Delete auth", "file_target": "Auth.cs", "agent": "coder", "estimated_loc": 50}]}, "file_list": [{"path": "Auth.cs", "operation": "delete", "reason": "Remove"}], "risk": {"level": "high", "factors": ["File deletion", "Auth changes"], "mitigation": "Backup"}, "verify": {"acceptance_criteria": [], "test_commands": ["dotnet test"], "manual_checks": []}, "rollback": {"strategy": "Delete files", "commands": [], "notes": ""}, "needs_approval": false}
            """;

        var agents = new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", true, highRiskPlan),
            [PipelineStage.Coding] = new MockAgent("coder", true, "patch"),
            [PipelineStage.Reviewing] = new MockAgent("reviewer", true, "{}"),
            [PipelineStage.Testing] = new MockAgent("tester", true, "{}"),
            [PipelineStage.Evaluating] = new MockAgent("evaluator", true, "{}")
        };

        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Delete auth files", TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse("high-risk should trigger approval");
        result.FinalStage.Should().Be(PipelineStage.AwaitingApproval);
        result.RequiresApproval.Should().BeTrue();
        result.Context.ApprovalReason.Should().ContainEquivalentOf("high");
    }

    [Fact]
    public async Task ExecutePipeline_CoderFails_FailsAtCodingStage()
    {
        // Arrange
        var safePlanJson = """
            {"plan": {"summary": "Test", "steps": [{"step_number": 1, "description": "Add", "file_target": "Test.cs", "agent": "coder", "estimated_loc": 10}]}, "file_list": [], "risk": {"level": "low", "factors": [], "mitigation": ""}, "verify": {"acceptance_criteria": [], "test_commands": ["dotnet test"], "manual_checks": []}, "rollback": {"strategy": "Delete files", "commands": [], "notes": ""}, "needs_approval": false}
            """;

        var agents = new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", true, safePlanJson),
            [PipelineStage.Coding] = new MockAgent("coder", false, "Failed to generate code"),
            [PipelineStage.Reviewing] = new MockAgent("reviewer", true, "{}"),
            [PipelineStage.Testing] = new MockAgent("tester", true, "{}"),
            [PipelineStage.Evaluating] = new MockAgent("evaluator", true, "{}")
        };

        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Test request", TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse("coder failure should fail pipeline");
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.Context.Patch.Should().BeNullOrWhiteSpace("coder should not produce patch on failure");
    }

    [Fact]
    public async Task ExecutePipeline_InvalidPatch_FailsAtPatchApplication()
    {
        // Arrange - Invalid patch syntax
        var safePlanJson = """
            {"plan": {"summary": "Test", "steps": [{"step_number": 1, "description": "Add", "file_target": "Test.cs", "agent": "coder", "estimated_loc": 10}]}, "file_list": [], "risk": {"level": "low", "factors": [], "mitigation": ""}, "verify": {"acceptance_criteria": [], "test_commands": ["dotnet test"], "manual_checks": []}, "rollback": {"strategy": "Delete files", "commands": [], "notes": ""}, "needs_approval": false}
            """;
        var invalidPatch = "This is not a valid diff format at all";

        var agents = CreateMockAgents(safePlanJson, invalidPatch, "{}", "{}", "{}");
        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Test request", TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse("invalid patch should fail");
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("patch", "should mention patch failure");
    }

    private static Dictionary<PipelineStage, IAgent> CreateMockAgents(string planJson, string patch, string review, string testReport, string scores)
    {
        return new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", true, planJson),
            [PipelineStage.Coding] = new MockAgent("coder", true, patch),
            [PipelineStage.Reviewing] = new MockAgent("reviewer", true, review),
            [PipelineStage.Testing] = new MockAgent("tester", true, testReport),
            [PipelineStage.Evaluating] = new MockAgent("evaluator", true, scores)
        };
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
                Description = "Mock agent",
                SystemPrompt = "Test",
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

    private sealed class MultiOutputMockAgent : IAgent
    {
        private readonly Queue<string> _outputs;

        public MultiOutputMockAgent(string name, Queue<string> outputs)
        {
            _outputs = outputs;
            Definition = new AgentDefinition { Name = name, Version = "1.0.0", Description = "Multi-output mock", SystemPrompt = "Test", Model = "sonnet" };
        }

        public AgentDefinition Definition { get; }

        public Task<AgentResult> ExecuteAsync(string input, AgentContext context, CancellationToken cancellationToken = default)
        {
            var output = _outputs.Count > 0 ? _outputs.Dequeue() : "{}";
            return Task.FromResult(AgentResult.CreateSuccess(Definition.Name, output));
        }
    }
}
