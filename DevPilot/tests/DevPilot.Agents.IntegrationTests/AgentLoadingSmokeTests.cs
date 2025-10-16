using DevPilot.Agents;
using DevPilot.Core;
using FluentAssertions;
using Xunit;

namespace DevPilot.Agents.IntegrationTests;

/// <summary>
/// Smoke tests to verify agent definitions can be loaded successfully.
/// These tests do not make API calls - they only verify infrastructure.
/// </summary>
public sealed class AgentLoadingSmokeTests
{
    private const string AgentsDirectory = "../../../../../.agents";

    [Theory]
    [InlineData("planner")]
    [InlineData("coder")]
    [InlineData("reviewer")]
    [InlineData("tester")]
    [InlineData("evaluator")]
    public async Task LoadAgent_AllAgents_SuccessfullyLoadsDefinition(string agentName)
    {
        // Arrange
        var loader = new AgentLoader(AgentsDirectory);

        // Act
        var definition = await loader.LoadAgentAsync(agentName);

        // Assert
        definition.Should().NotBeNull();
        definition.Name.Should().Be(agentName);
        definition.SystemPrompt.Should().NotBeNullOrWhiteSpace();
        definition.Model.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LoadAgent_Planner_HasMcpConfigPath()
    {
        // Arrange
        var loader = new AgentLoader(AgentsDirectory);

        // Act
        var definition = await loader.LoadAgentAsync("planner");

        // Assert
        definition.McpConfigPath.Should().NotBeNullOrWhiteSpace();
        definition.McpConfigPath.Should().Contain("mcp-planner");
    }

    [Theory]
    [InlineData("coder")]
    [InlineData("reviewer")]
    [InlineData("tester")]
    public async Task LoadAgent_NonPlannerAgents_NoMcpConfig(string agentName)
    {
        // Arrange
        var loader = new AgentLoader(AgentsDirectory);

        // Act
        var definition = await loader.LoadAgentAsync(agentName);

        // Assert
        definition.McpConfigPath.Should().BeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateClaudeCliAgent_AllAgents_SuccessfullyCreatesInstances()
    {
        // Arrange
        var loader = new AgentLoader(AgentsDirectory);
        var agentNames = new[] { "planner", "coder", "reviewer", "tester", "evaluator" };

        // Act & Assert
        foreach (var agentName in agentNames)
        {
            var definition = await loader.LoadAgentAsync(agentName);
            var agent = new ClaudeCliAgent(definition);

            agent.Should().NotBeNull();
            agent.Should().BeAssignableTo<IAgent>();
        }
    }
}
