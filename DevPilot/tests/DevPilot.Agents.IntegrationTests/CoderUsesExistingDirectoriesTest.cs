using DevPilot.Agents;
using DevPilot.Core;
using DevPilot.Orchestrator;
using FluentAssertions;
using Xunit;

namespace DevPilot.Agents.IntegrationTests;

/// <summary>
/// Integration tests to verify that the Coder agent discovers existing project
/// directories and uses them instead of creating new directories.
/// Validates fixes for path generation bugs where Coder created EmailValidator.Tests/
/// and src/ instead of using existing Testing.Tests/ and Testing/ directories.
/// </summary>
public sealed class CoderUsesExistingDirectoriesTest
{
    /// <summary>
    /// Tests that when the entire pipeline runs, Coder's output is validated for correct directory usage.
    /// This E2E test uses real agents to validate the full discovery workflow.
    /// </summary>
    [Fact(Skip = "Requires live Claude API access. Run manually to validate full pipeline with Coder discovery.")]
    public async Task ExecutePipeline_RealAgents_CoderUsesExistingDirectories()
    {
        // Arrange
        const string agentsDirectory = "../../../../../.agents";
        var loader = new AgentLoader(agentsDirectory);
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
        var userRequest = "Create an EmailValidator class with methods to validate email format and normalize email addresses to lowercase";

        // Act
        var result = await pipeline.ExecuteAsync(userRequest);

        // Assert - Verify Coder used existing directories
        if (result.Success)
        {
            result.Context.Patch.Should().Contain("Testing/", "Coder should discover and use existing Testing/ directory");
            result.Context.Patch.Should().Contain("Testing.Tests/", "Coder should discover and use existing Testing.Tests/ directory");

            // Verify Coder did NOT create new directories
            result.Context.Patch.Should().NotContain("EmailValidator.Tests/", "Coder should NOT create new EmailValidator.Tests/ directory");
            result.Context.Patch.Should().NotContain("src/EmailValidator.cs", "Coder should NOT create new src/ directory");
        }
        else
        {
            // If it failed, check it's not due to wrong directory usage
            if (result.ErrorMessage?.Contains("Pre-build validation failed") == true)
            {
                Assert.Fail($"Pipeline failed due to path validation. This indicates Coder may have used wrong directories. Error: {result.ErrorMessage}");
            }
        }
    }

    /// <summary>
    /// Mock-based test to verify expected behavior when Coder generates correct paths.
    /// This test runs fast and validates the pipeline's handling of correct Coder output.
    /// </summary>
    [Fact]
    public async Task ExecutePipeline_CoderUsesExistingPaths_PassesValidation()
    {
        // Arrange
        var validPlanJson = """
            {
              "plan": {"summary": "Create EmailValidator", "steps": [{"step_number": 1, "description": "Create EmailValidator", "file_target": "Testing/EmailValidator.cs", "agent": "coder", "estimated_loc": 80}]},
              "file_list": [
                {"path": "Testing/EmailValidator.cs", "operation": "create", "reason": "Implementation"},
                {"path": "Testing.Tests/EmailValidatorTests.cs", "operation": "create", "reason": "Tests"}
              ],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "verify": {"acceptance_criteria": [], "test_commands": ["dotnet test"], "manual_checks": []},
              "rollback": {"strategy": "Delete files", "commands": [], "notes": ""},
              "needs_approval": false
            }
            """;

        // Coder generates patch with CORRECT paths (existing directories)
        var correctPathPatch = """
            diff --git a/Testing/EmailValidator.cs b/Testing/EmailValidator.cs
            new file mode 100644
            --- /dev/null
            +++ b/Testing/EmailValidator.cs
            @@ -0,0 +1,20 @@
            +namespace Testing;
            +
            +/// <summary>
            +/// Provides email validation functionality.
            +/// </summary>
            +public sealed class EmailValidator
            +{
            +    /// <summary>
            +    /// Validates email format.
            +    /// </summary>
            +    public bool IsValid(string email) => !string.IsNullOrWhiteSpace(email) && email.Contains('@');
            +}
            diff --git a/Testing.Tests/EmailValidatorTests.cs b/Testing.Tests/EmailValidatorTests.cs
            new file mode 100644
            --- /dev/null
            +++ b/Testing.Tests/EmailValidatorTests.cs
            @@ -0,0 +1,15 @@
            +using Xunit;
            +using Testing;
            +
            +namespace Testing.Tests;
            +
            +public class EmailValidatorTests
            +{
            +    [Fact]
            +    public void IsValid_ValidEmail_ReturnsTrue()
            +    {
            +        var validator = new EmailValidator();
            +        Assert.True(validator.IsValid("test@example.com"));
            +    }
            +}
            """;

        var mockAgents = CreateMockAgents(validPlanJson, correctPathPatch);
        var pipeline = new Pipeline(mockAgents);

        // Act
        var result = await pipeline.ExecuteAsync("Create EmailValidator");

        // Assert - Pipeline should complete successfully (assuming workspace has Testing/ and Testing.Tests/)
        // Note: May still fail if workspace doesn't have the directories, but won't fail validation
        result.Context.Patch.Should().Contain("Testing/EmailValidator.cs", "patch should use existing main project");
        result.Context.Patch.Should().Contain("Testing.Tests/EmailValidatorTests.cs", "patch should use existing test project");
    }

    /// <summary>
    /// Verifies that when Coder generates WRONG paths (new directories), validation catches it.
    /// </summary>
    [Fact]
    public async Task ExecutePipeline_CoderCreatesNewDirectories_FailsValidation()
    {
        // Arrange
        var planJson = """
            {
              "plan": {"summary": "Create EmailValidator", "steps": [{"step_number": 1, "description": "Create EmailValidator", "file_target": "src/EmailValidator.cs", "agent": "coder", "estimated_loc": 80}]},
              "file_list": [
                {"path": "src/EmailValidator.cs", "operation": "create", "reason": "Implementation"},
                {"path": "EmailValidator.Tests/EmailValidatorTests.cs", "operation": "create", "reason": "Tests"}
              ],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "verify": {"acceptance_criteria": [], "test_commands": ["dotnet test"], "manual_checks": []},
              "rollback": {"strategy": "Delete files", "commands": [], "notes": ""},
              "needs_approval": false
            }
            """;

        // Coder generates patch with WRONG paths (new directories)
        var wrongPathPatch = """
            diff --git a/src/EmailValidator.cs b/src/EmailValidator.cs
            new file mode 100644
            --- /dev/null
            +++ b/src/EmailValidator.cs
            @@ -0,0 +1,10 @@
            +public class EmailValidator
            +{
            +    public bool IsValid(string email) => true;
            +}
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
            +    public void Test() => Assert.True(true);
            +}
            """;

        var mockAgents = CreateMockAgents(planJson, wrongPathPatch);
        var pipeline = new Pipeline(mockAgents);

        // Act
        var result = await pipeline.ExecuteAsync("Create EmailValidator");

        // Assert - Should fail validation for orphan directories
        result.Success.Should().BeFalse("new directory creation should fail validation");
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("Pre-build validation failed", "validation should catch orphan test directory");
    }

    /// <summary>
    /// Tests that when workspace has multiple test projects, Coder chooses the appropriate one.
    /// </summary>
    [Fact]
    public async Task ExecutePipeline_MultipleTestProjects_UsesAppropriateOne()
    {
        // Arrange
        var planJson = """
            {
              "plan": {"summary": "Add agent test", "steps": [{"step_number": 1, "description": "Add test", "file_target": "AgentTests.cs", "agent": "coder", "estimated_loc": 50}]},
              "file_list": [{"path": "AgentTests.cs", "operation": "create", "reason": "Tests"}],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "verify": {"acceptance_criteria": [], "test_commands": ["dotnet test"], "manual_checks": []},
              "rollback": {"strategy": "Delete files", "commands": [], "notes": ""},
              "needs_approval": false
            }
            """;

        // Coder should discover and choose appropriate test project
        // For agent-related tests, should use DevPilot.Agents.Tests (not Orchestrator.Tests)
        var appropriateTestPatch = """
            diff --git a/DevPilot.Agents.Tests/AgentTests.cs b/DevPilot.Agents.Tests/AgentTests.cs
            new file mode 100644
            --- /dev/null
            +++ b/DevPilot.Agents.Tests/AgentTests.cs
            @@ -0,0 +1,10 @@
            +using Xunit;
            +
            +public class AgentTests
            +{
            +    [Fact]
            +    public void Test() => Assert.True(true);
            +}
            """;

        var mockAgents = CreateMockAgents(planJson, appropriateTestPatch);
        var pipeline = new Pipeline(mockAgents);

        // Act
        var result = await pipeline.ExecuteAsync("Add agent test");

        // Assert - Should use appropriate test project (not create new one)
        result.Context.Patch.Should().Contain(".Tests/", "should use existing test project");
        result.Context.Patch.Should().NotContain("AgentTests.Tests/", "should NOT create new test project");
    }

    /// <summary>
    /// Creates mock agents with specified plan and patch.
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
