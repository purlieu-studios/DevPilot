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
            var definition = await loader.LoadAgentAsync(agentName);
            var agent = new ClaudeCliAgent(definition);
            agents[stage] = agent;
        }

        var pipeline = new Pipeline(agents);
        var userRequest = "Create a Calculator class with Add and Subtract methods";

        // Act
        var result = await pipeline.ExecuteAsync(userRequest);

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
            var definition = await loader.LoadAgentAsync(agentName);
            var agent = new ClaudeCliAgent(definition);
            agents[stage] = agent;
        }

        var pipeline = new Pipeline(agents);
        var userRequest = "Delete all user authentication files and remove database migration scripts";

        // Act
        var result = await pipeline.ExecuteAsync(userRequest);

        // Assert
        result.Should().NotBeNull();
        result.FinalStage.Should().Be(PipelineStage.AwaitingApproval, "high-risk request should trigger approval");
        result.RequiresApproval.Should().BeTrue();
        result.Context.ApprovalReason.Should().NotBeNullOrWhiteSpace("should explain why approval is required");
        result.Context.Plan.Should().NotBeNullOrWhiteSpace("planner should still generate plan");
        result.Context.Patch.Should().BeNullOrWhiteSpace("coder should not run after approval gate");
    }
}
