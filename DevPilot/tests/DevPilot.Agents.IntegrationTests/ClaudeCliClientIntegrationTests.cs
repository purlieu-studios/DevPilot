using DevPilot.Agents;
using FluentAssertions;

namespace DevPilot.Agents.IntegrationTests;

/// <summary>
/// Integration tests for ClaudeCliClient that make real API calls.
/// These tests are separated from unit tests to avoid API costs on every build.
/// They run on a schedule via GitHub Actions to detect Claude CLI breaking changes.
/// </summary>
public sealed class ClaudeCliClientIntegrationTests
{
    [Fact]
    public async Task ExecuteAsync_SuccessfulExecution()
    {
        // Arrange
        var client = new ClaudeCliClient();

        // Act
        var result = await client.ExecuteAsync(
            "Say 'Hello, DevPilot!' and nothing else.",
            "You are a helpful assistant. Respond concisely.",
            "sonnet",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNullOrWhiteSpace();
        result.Output.Should().Contain("DevPilot");
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesLongPrompts()
    {
        // Arrange
        var client = new ClaudeCliClient();
        var longPrompt = string.Join(" ", Enumerable.Repeat("test", 1000));

        // Act
        var result = await client.ExecuteAsync(
            $"Count the number of words in this text: {longPrompt}",
            "You are a helpful assistant.",
            "sonnet",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_RespectsSystemPrompt()
    {
        // Arrange
        var client = new ClaudeCliClient();

        // Act
        var result = await client.ExecuteAsync(
            "What is your role?",
            "You are a strict code reviewer who only speaks in bullet points.",
            "sonnet",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_RespectsModelParameter()
    {
        // Arrange
        var client = new ClaudeCliClient();

        // Act - Use "sonnet" alias
        var result = await client.ExecuteAsync(
            "Reply with 'OK'",
            "You are a helpful assistant.",
            "sonnet",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("OK");
    }

    [Fact]
    public async Task ExecuteAsync_HandlesSpecialCharactersInPrompt()
    {
        // Arrange
        var client = new ClaudeCliClient();

        // Act
        var result = await client.ExecuteAsync(
            "Echo this: \"Hello $USER\" and 'test' with <brackets>",
            "You are a helpful assistant.",
            "sonnet",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_HandlesMultilinePrompts()
    {
        // Arrange
        var client = new ClaudeCliClient();
        var multilinePrompt = """
            Line 1: Introduction
            Line 2: Details
            Line 3: Conclusion

            Please summarize this in one sentence.
            """;

        // Act
        var result = await client.ExecuteAsync(
            multilinePrompt,
            "You are a helpful assistant.",
            "sonnet",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNullOrWhiteSpace();
    }
}
