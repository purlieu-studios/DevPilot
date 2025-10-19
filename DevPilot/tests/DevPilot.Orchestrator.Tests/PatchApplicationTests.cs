using DevPilot.Core;
using DevPilot.Orchestrator;
using FluentAssertions;

namespace DevPilot.Orchestrator.Tests;

/// <summary>
/// Integration tests for patch application within the pipeline.
/// </summary>
public sealed class PatchApplicationTests
{
    [Fact]
    public async Task Pipeline_AppliesPatch_ToWorkspace()
    {
        // Arrange
        var coderPatch = @"diff --git a/Calculator.cs b/Calculator.cs
new file mode 100644
--- /dev/null
+++ b/Calculator.cs
@@ -0,0 +1,5 @@
+public class Calculator
+{
+    public int Add(int a, int b) => a + b;
+}
";

        var agents = CreateMockAgentsWithPatch(coderPatch);
        var workspace = WorkspaceManager.CreateWorkspace(Guid.NewGuid().ToString());
        var pipeline = new Pipeline(agents, workspace, Directory.GetCurrentDirectory());

        // Act
        var result = await pipeline.ExecuteAsync("Create a calculator");

        // Assert
        result.Success.Should().BeTrue();
        result.Context.WorkspaceRoot.Should().NotBeNullOrEmpty();
        result.Context.AppliedFiles.Should().NotBeNull();
        result.Context.AppliedFiles.Should().Contain("Calculator.cs");
    }

    [Fact]
    public async Task Pipeline_CreatesWorkspace_WithPipelineId()
    {
        // Arrange
        var coderPatch = @"diff --git a/Test.cs b/Test.cs
new file mode 100644
--- /dev/null
+++ b/Test.cs
@@ -0,0 +1,1 @@
+public class Test { }";

        var agents = CreateMockAgentsWithPatch(coderPatch);
        var workspace = WorkspaceManager.CreateWorkspace(Guid.NewGuid().ToString());
        var pipeline = new Pipeline(agents, workspace, Directory.GetCurrentDirectory());

        // Act
        var result = await pipeline.ExecuteAsync("Create a test class");

        // Assert
        result.Context.WorkspaceRoot.Should().Contain(result.Context.PipelineId);
        Directory.Exists(result.Context.WorkspaceRoot).Should().BeFalse(); // Should be cleaned up
    }

    [Fact]
    public async Task Pipeline_AppliesMultipleFiles_InSinglePatch()
    {
        // Arrange
        var coderPatch = @"diff --git a/File1.cs b/File1.cs
new file mode 100644
--- /dev/null
+++ b/File1.cs
@@ -0,0 +1,1 @@
+public class File1 { }
diff --git a/File2.cs b/File2.cs
new file mode 100644
--- /dev/null
+++ b/File2.cs
@@ -0,0 +1,1 @@
+public class File2 { }";

        var agents = CreateMockAgentsWithPatch(coderPatch);
        var workspace = WorkspaceManager.CreateWorkspace(Guid.NewGuid().ToString());
        var pipeline = new Pipeline(agents, workspace, Directory.GetCurrentDirectory());

        // Act
        var result = await pipeline.ExecuteAsync("Create two classes");

        // Assert
        result.Success.Should().BeTrue();
        result.Context.AppliedFiles.Should().HaveCount(2);
        result.Context.AppliedFiles.Should().Contain("File1.cs");
        result.Context.AppliedFiles.Should().Contain("File2.cs");
    }

    [Fact]
    public async Task Pipeline_FailsGracefully_WhenPatchIsInvalid()
    {
        // Arrange
        var invalidPatch = "This is not a valid unified diff";

        var agents = CreateMockAgentsWithPatch(invalidPatch);
        var workspace = WorkspaceManager.CreateWorkspace(Guid.NewGuid().ToString());
        var pipeline = new Pipeline(agents, workspace, Directory.GetCurrentDirectory());

        // Act
        var result = await pipeline.ExecuteAsync("Invalid request");

        // Assert
        result.Success.Should().BeFalse();
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("Failed to apply patch");
    }

    [Fact]
    public async Task Pipeline_CleansUpWorkspace_AfterExecution()
    {
        // Arrange
        var coderPatch = @"diff --git a/Test.cs b/Test.cs
new file mode 100644
--- /dev/null
+++ b/Test.cs
@@ -0,0 +1,1 @@
+public class Test { }";

        var agents = CreateMockAgentsWithPatch(coderPatch);
        var workspace = WorkspaceManager.CreateWorkspace(Guid.NewGuid().ToString());
        var pipeline = new Pipeline(agents, workspace, Directory.GetCurrentDirectory());

        // Act
        var result = await pipeline.ExecuteAsync("Create a test");
        var workspaceRoot = result.Context.WorkspaceRoot;

        // Assert
        workspaceRoot.Should().NotBeNullOrEmpty();
        Directory.Exists(workspaceRoot).Should().BeFalse(); // Workspace should be cleaned up
    }

    [Fact]
    public async Task Pipeline_CleansUpWorkspace_EvenOnFailure()
    {
        // Arrange
        var invalidPatch = "invalid patch content";

        var agents = CreateMockAgentsWithPatch(invalidPatch);
        var workspace = WorkspaceManager.CreateWorkspace(Guid.NewGuid().ToString());
        var pipeline = new Pipeline(agents, workspace, Directory.GetCurrentDirectory());

        // Act
        var result = await pipeline.ExecuteAsync("This will fail");
        var workspaceRoot = result.Context.WorkspaceRoot;

        // Assert
        result.Success.Should().BeFalse();
        workspaceRoot.Should().NotBeNullOrEmpty();
        Directory.Exists(workspaceRoot).Should().BeFalse(); // Workspace cleaned up even on failure
    }

    [Fact]
    public async Task Pipeline_PreservesWorkspaceContext_ThroughAllStages()
    {
        // Arrange
        var coderPatch = @"diff --git a/Calculator.cs b/Calculator.cs
new file mode 100644
--- /dev/null
+++ b/Calculator.cs
@@ -0,0 +1,3 @@
+public class Calculator {
+    public int Add(int a, int b) => a + b;
+}";

        var agents = CreateMockAgentsWithPatch(coderPatch);
        var workspace = WorkspaceManager.CreateWorkspace(Guid.NewGuid().ToString());
        var pipeline = new Pipeline(agents, workspace, Directory.GetCurrentDirectory());

        // Act
        var result = await pipeline.ExecuteAsync("Create calculator");

        // Assert
        result.Success.Should().BeTrue();
        result.Context.WorkspaceRoot.Should().NotBeNullOrEmpty();
        result.Context.AppliedFiles.Should().NotBeNull();
        result.Context.AppliedFiles.Should().Contain("Calculator.cs");

        // Verify all stages completed
        result.FinalStage.Should().Be(PipelineStage.Completed);
        result.Context.Plan.Should().NotBeNullOrEmpty();
        result.Context.Patch.Should().NotBeNullOrEmpty();
        result.Context.Review.Should().NotBeNullOrEmpty();
        result.Context.TestReport.Should().NotBeNullOrEmpty();
        result.Context.Scores.Should().NotBeNullOrEmpty();
    }

    private static Dictionary<PipelineStage, IAgent> CreateMockAgentsWithPatch(string coderPatch)
    {
        var safePlanJson = """
            {
              "plan": {"summary": "Safe operation", "steps": [{"step_number": 1, "description": "Test", "file_target": null, "agent": "coder", "estimated_loc": 50}]},
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false,
              "verify": {"acceptance_criteria": [], "test_commands": [], "manual_checks": []},
              "rollback": {"strategy": "Safe to rollback", "commands": [], "notes": ""}
            }
            """;

        var evaluatorJson = """
            {
              "evaluation": {
                "overall_score": 9.0,
                "scores": {
                  "plan_quality": 9.0,
                  "code_quality": 9.0,
                  "test_coverage": 9.0,
                  "documentation": 9.0,
                  "maintainability": 9.0
                },
                "strengths": ["Good"],
                "weaknesses": [],
                "recommendations": [],
                "final_verdict": "ACCEPT",
                "justification": "Meets quality standards"
              }
            }
            """;

        return new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", true, safePlanJson),
            [PipelineStage.Coding] = new MockAgent("coder", true, coderPatch),
            [PipelineStage.Reviewing] = new MockAgent("reviewer", true, "{\"verdict\": \"APPROVE\"}"),
            [PipelineStage.Testing] = new MockAgent("tester", true, "{\"pass\": true}"),
            [PipelineStage.Evaluating] = new MockAgent("evaluator", true, evaluatorJson)
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
