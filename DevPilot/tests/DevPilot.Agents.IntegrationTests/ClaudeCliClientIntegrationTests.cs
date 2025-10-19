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

    [Fact]
    public async Task ExecuteAsync_PreservesPreExistingClaudeMd()
    {
        // Arrange
        var client = new ClaudeCliClient();
        var tempDir = Path.Combine(Path.GetTempPath(), $"devpilot-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create a pre-existing CLAUDE.md with specific content
            var preExistingContent = "# Pre-existing CLAUDE.md\n\nThis file should NOT be deleted.";
            var claudeMdPath = Path.Combine(tempDir, "CLAUDE.md");
            await File.WriteAllTextAsync(claudeMdPath, preExistingContent, TestContext.Current.CancellationToken);

            // Create a large system prompt (>30KB) to trigger CLAUDE.md file approach
            var largeSystemPrompt = string.Join("\n", Enumerable.Repeat("This is a test system prompt line. ", 1000));

            // Act
            var result = await client.ExecuteAsync(
                "Say 'OK' and nothing else.",
                largeSystemPrompt,
                "sonnet",
                workingDirectory: tempDir,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeTrue();

            // Verify pre-existing CLAUDE.md was NOT deleted
            File.Exists(claudeMdPath).Should().BeTrue("pre-existing CLAUDE.md should be preserved");

            // Verify content was NOT overwritten (ClaudeCliClient should detect it exists and use it as-is)
            var actualContent = await File.ReadAllTextAsync(claudeMdPath, TestContext.Current.CancellationToken);
            actualContent.Should().Be(preExistingContent, "pre-existing CLAUDE.md should not be overwritten");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_CleansUpCreatedClaudeMd()
    {
        // Arrange
        var client = new ClaudeCliClient();
        var tempDir = Path.Combine(Path.GetTempPath(), $"devpilot-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var claudeMdPath = Path.Combine(tempDir, "CLAUDE.md");

            // Verify CLAUDE.md does NOT exist before execution
            File.Exists(claudeMdPath).Should().BeFalse("CLAUDE.md should not exist before test");

            // Create a large system prompt (>30KB) to trigger CLAUDE.md file approach
            var largeSystemPrompt = string.Join("\n", Enumerable.Repeat("This is a test system prompt line. ", 1000));

            // Act
            var result = await client.ExecuteAsync(
                "Say 'OK' and nothing else.",
                largeSystemPrompt,
                "sonnet",
                workingDirectory: tempDir,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeTrue();

            // Verify CLAUDE.md was cleaned up after execution
            File.Exists(claudeMdPath).Should().BeFalse("ClaudeCliClient should delete CLAUDE.md it created");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
