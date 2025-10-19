using DevPilot.Agents;
using DevPilot.Core;
using DevPilot.Orchestrator;
using FluentAssertions;
using Xunit;

namespace DevPilot.Agents.IntegrationTests;

/// <summary>
/// Integration tests to verify that pre-build validation (CodeValidator) catches
/// path issues before compilation, such as test files in orphan directories.
/// Validates pre-build validation infrastructure from PR #30.
/// </summary>
public sealed class ValidationCatchesPathIssuesTest
{
    [Fact]
    public async Task ExecutePipeline_OrphanTestDirectory_FailsPreBuildValidation()
    {
        // Arrange - Patch creates test file in directory WITHOUT .csproj
        var validPlanJson = """
            {
              "plan": {"summary": "Test", "steps": [{"step_number": 1, "description": "Add test", "file_target": "tests/CalculatorTests.cs", "agent": "coder", "estimated_loc": 50}]},
              "file_list": [{"path": "tests/CalculatorTests.cs", "operation": "create", "reason": "Tests"}],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "verify": {"acceptance_criteria": [], "test_commands": ["dotnet test"], "manual_checks": []},
              "rollback": {"strategy": "Delete files", "commands": [], "notes": ""},
              "needs_approval": false
            }
            """;

        // Patch creates orphan test directory (no .csproj)
        var orphanTestPatch = """
            diff --git a/tests/CalculatorTests.cs b/tests/CalculatorTests.cs
            new file mode 100644
            --- /dev/null
            +++ b/tests/CalculatorTests.cs
            @@ -0,0 +1,10 @@
            +using Xunit;
            +
            +public class CalculatorTests
            +{
            +    [Fact]
            +    public void Add_ReturnsSum()
            +    {
            +        Assert.Equal(5, 2 + 3);
            +    }
            +}
            """;

        var mockAgents = CreateMockAgents(validPlanJson, orphanTestPatch);
        var workspace = WorkspaceManager.CreateWorkspace(Guid.NewGuid().ToString());
        var pipeline = new Pipeline(mockAgents, workspace, workspace.WorkspaceRoot);

        // Act
        var result = await pipeline.ExecuteAsync("Add calculator tests", TestContext.Current.CancellationToken);

        // Assert - Should fail at testing stage during pre-build validation
        result.Success.Should().BeFalse("orphan test directory should fail validation");
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("Pre-build validation failed", "should explain validation failure");
        result.ErrorMessage.Should().Contain(".csproj", "should mention missing .csproj file");
    }


    [Fact]
    public async Task ExecutePipeline_NewDirectoryCreation_FailsValidation()
    {
        // Arrange - Coder creates new directory structure instead of using existing
        var validPlanJson = """
            {
              "plan": {"summary": "Email validator", "steps": [{"step_number": 1, "description": "Add validator", "file_target": "EmailValidator.Tests/Tests.cs", "agent": "coder", "estimated_loc": 50}]},
              "file_list": [{"path": "EmailValidator.Tests/Tests.cs", "operation": "create", "reason": "Tests"}],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "verify": {"acceptance_criteria": [], "test_commands": ["dotnet test"], "manual_checks": []},
              "rollback": {"strategy": "Delete files", "commands": [], "notes": ""},
              "needs_approval": false
            }
            """;

        // Patch creates new project directory (wrong - should use existing Testing.Tests)
        var newDirectoryPatch = """
            diff --git a/EmailValidator.Tests/EmailValidatorTests.cs b/EmailValidator.Tests/EmailValidatorTests.cs
            new file mode 100644
            --- /dev/null
            +++ b/EmailValidator.Tests/EmailValidatorTests.cs
            @@ -0,0 +1,10 @@
            +using Xunit;
            +
            +public class EmailValidatorTests
            +{
            +    [Fact]
            +    public void Validate_ValidEmail_ReturnsTrue()
            +    {
            +        Assert.True(true);
            +    }
            +}
            """;

        var mockAgents = CreateMockAgents(validPlanJson, newDirectoryPatch);
        var workspace = WorkspaceManager.CreateWorkspace(Guid.NewGuid().ToString());
        var pipeline = new Pipeline(mockAgents, workspace, workspace.WorkspaceRoot);

        // Act
        var result = await pipeline.ExecuteAsync("Create EmailValidator", TestContext.Current.CancellationToken);

        // Assert - Should fail validation for creating new directory
        result.Success.Should().BeFalse("new directory creation should fail validation");
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("Pre-build validation failed");
        result.ErrorMessage.Should().Contain(".csproj");
    }

    /// <summary>
    /// Creates mock agents with specified planner output and coder patch.
    /// </summary>
    private static Dictionary<PipelineStage, IAgent> CreateMockAgents(string planJson, string patch)
    {
        return new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", true, planJson),
            [PipelineStage.Coding] = new MockAgent("coder", true, patch),
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
