using DevPilot.Agents;
using DevPilot.Core;
using DevPilot.Orchestrator;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace DevPilot.Agents.IntegrationTests;

/// <summary>
/// Integration tests to verify that the Planner agent uses MCP planning tools
/// and returns valid JSON with all required properties.
/// Validates fixes from PR fixing MCP/Planner timeout issue.
/// </summary>
public sealed class PlannerUsesMcpToolsTest
{
    [Fact]
    public async Task ExecutePipeline_PlannerAgent_ReturnsValidJsonStructure()
    {
        // Arrange - Valid plan JSON with all required properties
        var validPlanJson = """
            {
              "plan": {
                "summary": "Create Calculator class",
                "steps": [
                  {
                    "step_number": 1,
                    "description": "Create Calculator.cs with Add and Subtract methods",
                    "file_target": "src/Calculator.cs",
                    "agent": "coder",
                    "estimated_loc": 45
                  }
                ]
              },
              "file_list": [
                {"path": "src/Calculator.cs", "operation": "create", "reason": "Implementation"}
              ],
              "risk": {
                "level": "low",
                "factors": ["New isolated class", "Simple arithmetic"],
                "mitigation": "Comprehensive unit tests"
              },
              "verify": {
                "acceptance_criteria": ["Add method works", "Subtract method works"],
                "test_commands": ["dotnet test"],
                "manual_checks": []
              },
              "rollback": {
                "strategy": "Delete created files",
                "commands": ["git restore src/Calculator.cs"],
                "notes": "No dependencies to clean up"
              },
              "needs_approval": false
            }
            """;

        var mockAgents = CreateMinimalMockAgents(validPlanJson);
        var pipeline = new Pipeline(mockAgents);

        // Act
        var result = await pipeline.ExecuteAsync("Create Calculator class", TestContext.Current.CancellationToken);

        // Assert - Planning stage should complete successfully
        result.Context.Plan.Should().NotBeNullOrWhiteSpace("planner should generate plan");
        result.Context.Plan.Should().Contain("plan", "output should contain plan property");
        result.Context.Plan.Should().Contain("file_list", "output should contain file_list property");
        result.Context.Plan.Should().Contain("risk", "output should contain risk property");

        // Parse and validate JSON structure
        var planDoc = JsonDocument.Parse(result.Context.Plan);
        var root = planDoc.RootElement;

        root.TryGetProperty("plan", out _).Should().BeTrue("plan property is required");
        root.TryGetProperty("file_list", out _).Should().BeTrue("file_list property is required");
        root.TryGetProperty("risk", out _).Should().BeTrue("risk property is required");
        root.TryGetProperty("verify", out _).Should().BeTrue("verify property is required");
        root.TryGetProperty("rollback", out _).Should().BeTrue("rollback property is required");
    }

    [Fact]
    public async Task ExecutePipeline_InvalidPlannerOutput_FailsValidation()
    {
        // Arrange - Invalid plan JSON missing required properties
        var invalidPlanJson = """
            {
              "summary": "This is wrong - missing required properties"
            }
            """;

        var mockAgents = CreateMinimalMockAgents(invalidPlanJson);
        var pipeline = new Pipeline(mockAgents);

        // Act
        var result = await pipeline.ExecuteAsync("Invalid request", TestContext.Current.CancellationToken);

        // Assert - Pipeline should fail at planning stage with validation error
        result.Success.Should().BeFalse("invalid plan structure should fail validation");
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("missing required properties", "should explain validation failure");
    }

    [Fact]
    public async Task ExecutePipeline_EmptyPlannerOutput_FailsValidation()
    {
        // Arrange - Empty output
        var mockAgents = CreateMinimalMockAgents("");
        var pipeline = new Pipeline(mockAgents);

        // Act
        var result = await pipeline.ExecuteAsync("Empty response test", TestContext.Current.CancellationToken);

        // Assert - Pipeline should fail with empty output error
        result.Success.Should().BeFalse("empty output should fail validation");
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("empty output", "should explain empty output failure");
    }

    [Fact]
    public async Task ExecutePipeline_NonJsonPlannerOutput_FailsValidation()
    {
        // Arrange - Plain text instead of JSON
        var nonJsonOutput = "This is plain text, not JSON";

        var mockAgents = CreateMinimalMockAgents(nonJsonOutput);
        var pipeline = new Pipeline(mockAgents);

        // Act
        var result = await pipeline.ExecuteAsync("Non-JSON test", TestContext.Current.CancellationToken);

        // Assert - Pipeline should fail with JSON parsing error
        result.Success.Should().BeFalse("non-JSON output should fail validation");
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("did not return valid JSON", "should explain JSON parsing failure");
    }

    [Fact]
    public async Task ExecutePipeline_PlanMissingFileList_FailsValidation()
    {
        // Arrange - Plan missing file_list property
        var partialPlanJson = """
            {
              "plan": {"summary": "Test", "steps": []},
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "verify": {"acceptance_criteria": [], "test_commands": [], "manual_checks": []},
              "rollback": {"strategy": "", "commands": [], "notes": ""}
            }
            """;

        var mockAgents = CreateMinimalMockAgents(partialPlanJson);
        var pipeline = new Pipeline(mockAgents);

        // Act
        var result = await pipeline.ExecuteAsync("Partial plan test", TestContext.Current.CancellationToken);

        // Assert - Pipeline should fail due to missing file_list
        result.Success.Should().BeFalse("plan missing file_list should fail validation");
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("file_list", "should mention missing property");
    }

    /// <summary>
    /// Creates minimal mock agents for testing planner output validation.
    /// Only the planner agent is configured; other stages are minimal mocks.
    /// </summary>
    private static Dictionary<PipelineStage, IAgent> CreateMinimalMockAgents(string plannerOutput)
    {
        return new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", true, plannerOutput),
            [PipelineStage.Coding] = new MockAgent("coder", true, "diff --git a/Test.cs b/Test.cs\n"),
            [PipelineStage.Reviewing] = new MockAgent("reviewer", true, """{"verdict": "APPROVE", "issues": [], "summary": "OK", "metrics": {"complexity": 1, "maintainability": 10}}"""),
            [PipelineStage.Testing] = new MockAgent("tester", true, """{"pass": true, "summary": "OK", "test_results": [], "coverage": null, "performance": {"total_duration_ms": 100}}"""),
            [PipelineStage.Evaluating] = new MockAgent("evaluator", true, """{"evaluation": {"overall_score": 9.0, "scores": {}, "strengths": [], "weaknesses": [], "recommendations": [], "final_verdict": "ACCEPT", "justification": "Good"}}""")
        };
    }

    /// <summary>
    /// Mock agent implementation for testing.
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
