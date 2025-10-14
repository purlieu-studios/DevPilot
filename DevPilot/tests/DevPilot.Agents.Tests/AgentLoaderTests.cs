using DevPilot.Agents;
using FluentAssertions;

namespace DevPilot.Agents.Tests;

public sealed class AgentLoaderTests
{
    private static string GetTestDataPath() =>
        Path.Combine(AppContext.BaseDirectory, "TestData", ".agents");

    [Fact]
    public void DiscoverAgents_ReturnsAllAgents_WhenDirectoryExists()
    {
        // Arrange
        var loader = new AgentLoader(GetTestDataPath());

        // Act
        var agents = loader.DiscoverAgents();

        // Assert
        agents.Should().NotBeEmpty();
        agents.Should().Contain("test-agent");
        agents.Should().Contain("minimal-agent");
    }

    [Fact]
    public void DiscoverAgents_ReturnsEmpty_WhenDirectoryDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var loader = new AgentLoader(nonExistentPath);

        // Act
        var agents = loader.DiscoverAgents();

        // Assert
        agents.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAgentAsync_LoadsSuccessfully_WithFullAgent()
    {
        // Arrange
        var loader = new AgentLoader(GetTestDataPath());

        // Act
        var definition = await loader.LoadAgentAsync("test-agent");

        // Assert
        definition.Should().NotBeNull();
        definition.Name.Should().Be("test-agent");
        definition.Version.Should().Be("1.0.0");
        definition.Description.Should().Be("A test agent for unit testing");
        definition.SystemPrompt.Should().Contain("Test Agent System Prompt");

        definition.Model.Should().NotBeNull();
        definition.Model.Provider.Should().Be("anthropic");
        definition.Model.ModelName.Should().Be("claude-sonnet-4-5-20250929");
        definition.Model.Temperature.Should().Be(0.5);
        definition.Model.MaxTokens.Should().Be(2048);

        definition.Model.Reasoning.Should().NotBeNull();
        definition.Model.Reasoning!.Enabled.Should().BeTrue();
        definition.Model.Reasoning.Type.Should().Be("extended");

        definition.Capabilities.Should().HaveCount(2);
        definition.Capabilities.Should().Contain("test_capability_1");
        definition.Capabilities.Should().Contain("test_capability_2");

        definition.Tools.Should().NotBeNull();
        definition.Tools.Should().HaveCount(1);
        definition.Tools![0].Name.Should().Be("test_tool");
        definition.Tools[0].Description.Should().Be("A test tool for unit testing");

        definition.RetryPolicy.Should().NotBeNull();
        definition.RetryPolicy!.MaxRetries.Should().Be(3);
        definition.RetryPolicy.RetryDelayMs.Should().Be(1000);
        definition.RetryPolicy.ExponentialBackoff.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAgentAsync_LoadsSuccessfully_WithMinimalAgent()
    {
        // Arrange
        var loader = new AgentLoader(GetTestDataPath());

        // Act
        var definition = await loader.LoadAgentAsync("minimal-agent");

        // Assert
        definition.Should().NotBeNull();
        definition.Name.Should().Be("minimal-agent");
        definition.SystemPrompt.Should().Contain("Minimal Agent");

        definition.Model.Reasoning.Should().BeNull();
        definition.Tools.Should().BeNull();
        definition.RetryPolicy.Should().BeNull();
        definition.Capabilities.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAgentAsync_ThrowsException_WhenAgentDirectoryMissing()
    {
        // Arrange
        var loader = new AgentLoader(GetTestDataPath());

        // Act
        var act = () => loader.LoadAgentAsync("non-existent-agent");

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*Agent directory not found*");
    }

    [Fact]
    public async Task LoadAgentAsync_ThrowsException_WhenConfigMissing()
    {
        // Arrange
        var testPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), ".agents");
        Directory.CreateDirectory(Path.Combine(testPath, "broken-agent"));
        File.WriteAllText(
            Path.Combine(testPath, "broken-agent", "system-prompt.md"),
            "# Broken Agent");

        var loader = new AgentLoader(testPath);

        try
        {
            // Act
            var act = () => loader.LoadAgentAsync("broken-agent");

            // Assert
            await act.Should().ThrowAsync<FileNotFoundException>()
                .WithMessage("*config not found*");
        }
        finally
        {
            Directory.Delete(testPath, true);
        }
    }

    [Fact]
    public async Task LoadAgentAsync_ThrowsException_WhenSystemPromptMissing()
    {
        // Arrange
        var testPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), ".agents");
        Directory.CreateDirectory(Path.Combine(testPath, "broken-agent"));
        File.WriteAllText(
            Path.Combine(testPath, "broken-agent", "config.json"),
            "{\"agent_name\":\"broken\",\"version\":\"1.0.0\",\"description\":\"test\",\"model\":{\"provider\":\"test\",\"model_name\":\"test\",\"temperature\":0.5,\"max_tokens\":100},\"capabilities\":[]}");

        var loader = new AgentLoader(testPath);

        try
        {
            // Act
            var act = () => loader.LoadAgentAsync("broken-agent");

            // Assert
            await act.Should().ThrowAsync<FileNotFoundException>()
                .WithMessage("*system prompt not found*");
        }
        finally
        {
            Directory.Delete(testPath, true);
        }
    }

    [Fact]
    public async Task LoadAgentAsync_ThrowsArgumentException_WhenAgentNameEmpty()
    {
        // Arrange
        var loader = new AgentLoader(GetTestDataPath());

        // Act
        var act = () => loader.LoadAgentAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LoadAgentAsync_ThrowsArgumentException_WhenAgentNameWhitespace()
    {
        // Arrange
        var loader = new AgentLoader(GetTestDataPath());

        // Act
        var act = () => loader.LoadAgentAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
