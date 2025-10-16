using DevPilot.Core;
using DevPilot.Orchestrator;
using FluentAssertions;

namespace DevPilot.Agents.Tests;

public sealed class PipelineTests
{
    [Fact]
    public async Task ExecuteAsync_CompletesSuccessfully_WhenAllAgentsSucceed()
    {
        // Arrange
        var agents = CreateMockAgents(allSucceed: true);
        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Create a calculator");

        // Assert
        result.Success.Should().BeTrue();
        result.FinalStage.Should().Be(PipelineStage.Completed);
        result.Context.Plan.Should().NotBeNullOrWhiteSpace();
        result.Context.Patch.Should().NotBeNullOrWhiteSpace();
        result.Context.Review.Should().NotBeNullOrWhiteSpace();
        result.Context.TestReport.Should().NotBeNullOrWhiteSpace();
        result.Context.Scores.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailed_WhenPlannerFails()
    {
        // Arrange
        var agents = CreateMockAgents(allSucceed: true);
        agents[PipelineStage.Planning] = new MockAgent("planner", succeeds: false, output: "Planning failed");
        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Create a calculator");

        // Assert
        result.Success.Should().BeFalse();
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Be("Planning failed");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailed_WhenCoderFails()
    {
        // Arrange
        var agents = CreateMockAgents(allSucceed: true);
        agents[PipelineStage.Coding] = new MockAgent("coder", succeeds: false, output: "Code generation failed");
        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Create a calculator");

        // Assert
        result.Success.Should().BeFalse();
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.Context.Plan.Should().NotBeNullOrWhiteSpace();
        result.ErrorMessage.Should().Be("Code generation failed");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailed_WhenReviewerRejects()
    {
        // Arrange
        var reviewerJson = """
            {
              "verdict": "REJECT",
              "issues": [
                {
                  "severity": "error",
                  "file": "Calculator.cs",
                  "line": 5,
                  "message": "Poor code quality",
                  "suggestion": "Refactor for better maintainability"
                }
              ],
              "summary": "Code does not meet quality standards",
              "metrics": {
                "complexity": 8,
                "maintainability": 3
              }
            }
            """;

        var agents = CreateMockAgents(allSucceed: true);
        agents[PipelineStage.Reviewing] = new MockAgent("reviewer", true, reviewerJson);
        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Create a calculator");

        // Assert
        result.Success.Should().BeFalse();
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.Context.Plan.Should().NotBeNullOrWhiteSpace();
        result.Context.Patch.Should().NotBeNullOrWhiteSpace();
        result.Context.Review.Should().NotBeNullOrWhiteSpace();
        result.ErrorMessage.Should().Contain("Reviewer rejected code");
        result.ErrorMessage.Should().Contain("REJECT");
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesToTesting_WhenReviewerApproves()
    {
        // Arrange
        var reviewerJson = """
            {
              "verdict": "APPROVE",
              "issues": [],
              "summary": "Code meets all quality standards",
              "metrics": {
                "complexity": 2,
                "maintainability": 9
              }
            }
            """;

        var agents = CreateMockAgents(allSucceed: true);
        agents[PipelineStage.Reviewing] = new MockAgent("reviewer", true, reviewerJson);
        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Create a calculator");

        // Assert
        result.Success.Should().BeTrue();
        result.FinalStage.Should().Be(PipelineStage.Completed);
        result.Context.Review.Should().NotBeNullOrWhiteSpace();
        result.Context.TestReport.Should().NotBeNullOrWhiteSpace();
        result.Context.Scores.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsArgumentException_WhenUserRequestIsEmpty()
    {
        // Arrange
        var agents = CreateMockAgents(allSucceed: true);
        var pipeline = new Pipeline(agents);

        // Act
        var act = async () => await pipeline.ExecuteAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsArgumentException_WhenUserRequestIsWhitespace()
    {
        // Arrange
        var agents = CreateMockAgents(allSucceed: true);
        var pipeline = new Pipeline(agents);

        // Act
        var act = async () => await pipeline.ExecuteAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteAsync_HandlesCancellation()
    {
        // Arrange
        var agents = CreateMockAgents(allSucceed: true);
        var pipeline = new Pipeline(agents);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await pipeline.ExecuteAsync("Create a calculator", cts.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("cancelled");
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenAgentsIsNull()
    {
        // Act
        var act = () => new Pipeline(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenMissingPlannerAgent()
    {
        // Arrange
        var agents = CreateMockAgents(allSucceed: true);
        agents.Remove(PipelineStage.Planning);

        // Act
        var act = () => new Pipeline(agents);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Planning*");
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenMissingCoderAgent()
    {
        // Arrange
        var agents = CreateMockAgents(allSucceed: true);
        agents.Remove(PipelineStage.Coding);

        // Act
        var act = () => new Pipeline(agents);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Coding*");
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenMissingMultipleAgents()
    {
        // Arrange
        var agents = CreateMockAgents(allSucceed: true);
        agents.Remove(PipelineStage.Planning);
        agents.Remove(PipelineStage.Testing);

        // Act
        var act = () => new Pipeline(agents);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Planning*Testing*");
    }

    [Fact]
    public async Task ExecuteAsync_RecordsStageHistory()
    {
        // Arrange
        var agents = CreateMockAgents(allSucceed: true);
        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Create a calculator");

        // Assert
        result.Context.StageHistory.Should().HaveCount(6);
        result.Context.StageHistory[0].Stage.Should().Be(PipelineStage.Planning);
        result.Context.StageHistory[5].Stage.Should().Be(PipelineStage.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_TracksDuration()
    {
        // Arrange
        var agents = CreateMockAgents(allSucceed: true);
        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Create a calculator");

        // Assert
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_RequiresApproval_WhenPlannerFlagsNeedsApproval()
    {
        // Arrange
        var plannerJson = """
            {
              "plan": {"summary": "Blocked", "steps": []},
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": true,
              "approval_reason": "Ambiguous requirements"
            }
            """;

        var agents = CreateMockAgents(allSucceed: true);
        agents[PipelineStage.Planning] = new MockAgent("planner", true, plannerJson);
        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Unclear request");

        // Assert
        result.Success.Should().BeFalse();
        result.RequiresApproval.Should().BeTrue();
        result.FinalStage.Should().Be(PipelineStage.AwaitingApproval);
        result.Context.ApprovalRequired.Should().BeTrue();
        result.Context.ApprovalReason.Should().Contain("needs_approval");
    }

    [Fact]
    public async Task ExecuteAsync_RequiresApproval_WhenPlanHasLocBreach()
    {
        // Arrange
        var plannerJson = """
            {
              "plan": {
                "summary": "Large change",
                "steps": [
                  {"step_number": 1, "description": "Big feature", "file_target": "Big.cs", "agent": "coder", "estimated_loc": 450}
                ]
              },
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false
            }
            """;

        var agents = CreateMockAgents(allSucceed: true);
        agents[PipelineStage.Planning] = new MockAgent("planner", true, plannerJson);
        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Create large feature");

        // Assert
        result.Success.Should().BeFalse();
        result.RequiresApproval.Should().BeTrue();
        result.Context.ApprovalReason.Should().Contain("LOC limit exceeded");
        result.Context.ApprovalReason.Should().Contain("450");
    }

    [Fact]
    public async Task ExecuteAsync_RevisesCode_WhenReviewerReturnsRevise()
    {
        // Arrange
        var reviseJson = """
            {
              "verdict": "REVISE",
              "issues": [
                {
                  "severity": "warning",
                  "file": "Calculator.cs",
                  "line": 3,
                  "message": "Missing XML documentation",
                  "suggestion": "Add /// <summary> comment"
                }
              ],
              "summary": "Minor improvements needed",
              "metrics": {"complexity": 2, "maintainability": 8}
            }
            """;

        var approveJson = """{"verdict": "APPROVE", "issues": [], "summary": "Improved", "metrics": {"complexity": 2, "maintainability": 9}}""";

        var agents = CreateMockAgents(allSucceed: true);
        var reviewerOutputs = new Queue<string>(new[] { reviseJson, approveJson });
        agents[PipelineStage.Reviewing] = new MultiOutputMockAgent("reviewer", reviewerOutputs);

        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Create a calculator");

        // Assert
        result.Success.Should().BeTrue("reviewer should approve after revision");
        result.FinalStage.Should().Be(PipelineStage.Completed);
        result.Context.RevisionIteration.Should().Be(1, "one revision loop should have occurred");
        result.Context.StageHistory.Should().Contain(e => e.Stage == PipelineStage.Coding);
    }

    [Fact]
    public async Task ExecuteAsync_FailsAfterMaxRevisions_WhenReviewerKeepsRequestingRevisions()
    {
        // Arrange
        var reviseJson = """
            {
              "verdict": "REVISE",
              "issues": [{"severity": "warning", "file": "Test.cs", "line": 1, "message": "Needs work", "suggestion": "Fix it"}],
              "summary": "Still needs work",
              "metrics": {"complexity": 5, "maintainability": 5}
            }
            """;

        var agents = CreateMockAgents(allSucceed: true);
        // Reviewer always returns REVISE (will hit max iterations)
        agents[PipelineStage.Reviewing] = new MockAgent("reviewer", true, reviseJson);

        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Create a calculator");

        // Assert
        result.Success.Should().BeFalse("should fail after max revisions");
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("Maximum revision iterations");
        result.ErrorMessage.Should().Contain("2");
        result.Context.RevisionIteration.Should().Be(2, "should reach max revisions");
    }

    [Fact]
    public async Task ExecuteAsync_FailsIfReviewerRejectsAfterRevision()
    {
        // Arrange
        var reviseJson = """{"verdict": "REVISE", "issues": [{"severity": "warning", "file": "Test.cs", "line": 1, "message": "Fix this", "suggestion": "Do better"}], "summary": "Needs work", "metrics": {"complexity": 3, "maintainability": 7}}""";
        var rejectJson = """{"verdict": "REJECT", "issues": [{"severity": "error", "file": "Test.cs", "line": 1, "message": "Critical issue", "suggestion": "Major refactor needed"}], "summary": "Still not acceptable", "metrics": {"complexity": 8, "maintainability": 2}}""";

        var agents = CreateMockAgents(allSucceed: true);
        var reviewerOutputs = new Queue<string>(new[] { reviseJson, rejectJson });
        agents[PipelineStage.Reviewing] = new MultiOutputMockAgent("reviewer", reviewerOutputs);

        var pipeline = new Pipeline(agents);

        // Act
        var result = await pipeline.ExecuteAsync("Create a calculator");

        // Assert
        result.Success.Should().BeFalse();
        result.FinalStage.Should().Be(PipelineStage.Failed);
        result.ErrorMessage.Should().Contain("Reviewer rejected revised code");
        result.Context.RevisionIteration.Should().Be(1);
    }

    private static Dictionary<PipelineStage, IAgent> CreateMockAgents(bool allSucceed)
    {
        var safePlanJson = """
            {
              "plan": {"summary": "Safe operation", "steps": [{"step_number": 1, "description": "Test", "file_target": null, "agent": "coder", "estimated_loc": 50}]},
              "file_list": [],
              "risk": {"level": "low", "factors": [], "mitigation": ""},
              "needs_approval": false
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

        return new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", allSucceed, safePlanJson),
            [PipelineStage.Coding] = new MockAgent("coder", allSucceed, validPatch),
            [PipelineStage.Reviewing] = new MockAgent("reviewer", allSucceed, "{\"verdict\": \"APPROVE\"}"),
            [PipelineStage.Testing] = new MockAgent("tester", allSucceed, "{\"pass\": true}"),
            [PipelineStage.Evaluating] = new MockAgent("evaluator", allSucceed, evaluatorJson)
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

    /// <summary>
    /// Mock agent that returns different outputs on successive calls (for testing revision loops).
    /// </summary>
    private sealed class MultiOutputMockAgent : IAgent
    {
        private readonly Queue<string> _outputs;

        public MultiOutputMockAgent(string name, Queue<string> outputs)
        {
            _outputs = outputs;
            Definition = new AgentDefinition
            {
                Name = name,
                Version = "1.0.0",
                Description = "Multi-output mock agent for testing",
                SystemPrompt = "Test prompt",
                Model = "sonnet"
            };
        }

        public AgentDefinition Definition { get; }

        public Task<AgentResult> ExecuteAsync(string input, AgentContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var output = _outputs.Count > 0 ? _outputs.Dequeue() : "{}";
            var result = AgentResult.CreateSuccess(Definition.Name, output);

            return Task.FromResult(result);
        }
    }
}
