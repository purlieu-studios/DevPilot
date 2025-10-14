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
        definition.Model.Should().Be("sonnet");
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
        definition.Description.Should().Be("A minimal test agent without optional fields");
        definition.SystemPrompt.Should().Contain("Minimal Agent");
        definition.Model.Should().Be("haiku");
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
            "{\"agent_name\":\"broken\",\"version\":\"1.0.0\",\"description\":\"test\",\"model\":\"sonnet\"}");

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
