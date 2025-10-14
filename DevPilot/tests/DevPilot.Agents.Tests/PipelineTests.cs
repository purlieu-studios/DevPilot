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

    private static Dictionary<PipelineStage, IAgent> CreateMockAgents(bool allSucceed)
    {
        return new Dictionary<PipelineStage, IAgent>
        {
            [PipelineStage.Planning] = new MockAgent("planner", allSucceed, "{\"steps\": []}"),
            [PipelineStage.Coding] = new MockAgent("coder", allSucceed, "diff --git..."),
            [PipelineStage.Reviewing] = new MockAgent("reviewer", allSucceed, "{\"verdict\": \"APPROVE\"}"),
            [PipelineStage.Testing] = new MockAgent("tester", allSucceed, "{\"pass\": true}"),
            [PipelineStage.Evaluating] = new MockAgent("evaluator", allSucceed, "{\"score\": 9}")
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
                Model = new ModelConfiguration
                {
                    Provider = "test",
                    ModelName = "test-model",
                    Temperature = 0.0,
                    MaxTokens = 1000
                },
                Capabilities = Array.Empty<string>(),
                Tools = Array.Empty<AgentTool>()
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
