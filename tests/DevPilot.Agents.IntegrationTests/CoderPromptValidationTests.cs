using DevPilot.Agents;
using FluentAssertions;
using Xunit;

namespace DevPilot.Agents.IntegrationTests;

/// <summary>
/// Validates that the Coder agent system prompt contains all required sections
/// for MCP file operation usage and quality standards.
/// These tests ensure prompt regressions don't silently break agent behavior.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CoderPromptValidationTests
{
    private const string CoderAgentPath = "../../../../../.agents/coder";

    /// <summary>
    /// Validates that the Coder prompt enforces MCP-only tool usage.
    /// This prevents regressions where agents use Edit/Glob/Read instead of MCP tools.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_EnforcesMcpOnlyToolUsage()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - Strong warning against using non-MCP tools
        coderDefinition.SystemPrompt.Should().Contain("If you use ANY other tool (Write, Edit, Bash, Task, Glob, Grep, Read, etc.), the **pipeline will FAIL**",
            "Coder must be warned against using standard tools");

        coderDefinition.SystemPrompt.Should().Contain("FORBIDDEN - DO NOT USE",
            "Must have explicit FORBIDDEN section");

        coderDefinition.SystemPrompt.Should().Contain("You MUST use ONLY the MCP file operation tools",
            "Must emphasize MCP-only requirement");
    }

    /// <summary>
    /// Validates that the Coder prompt lists all MCP tools with correct prefixes.
    /// Tool prefixes are critical for Claude to invoke them correctly.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_ListsAllMcpToolsWithPrefixes()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - All 5 MCP tools with mcp__pipeline-tools__ prefix
        coderDefinition.SystemPrompt.Should().Contain("mcp__pipeline-tools__create_file",
            "Must list create_file with correct prefix");

        coderDefinition.SystemPrompt.Should().Contain("mcp__pipeline-tools__modify_file",
            "Must list modify_file with correct prefix");

        coderDefinition.SystemPrompt.Should().Contain("mcp__pipeline-tools__delete_file",
            "Must list delete_file with correct prefix");

        coderDefinition.SystemPrompt.Should().Contain("mcp__pipeline-tools__rename_file",
            "Must list rename_file with correct prefix");

        coderDefinition.SystemPrompt.Should().Contain("mcp__pipeline-tools__finalize_file_operations",
            "Must list finalize_file_operations with correct prefix");
    }

    /// <summary>
    /// Validates that the Coder prompt explains when to use create vs modify.
    /// This prevents "file already exists" errors.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_ExplainsCreateVsModify()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - Clear guidance on create vs modify
        coderDefinition.SystemPrompt.Should().Contain("ONLY use when the plan says \"Create\" or you're creating a wholly new file",
            "Must explain when to use create_file");

        coderDefinition.SystemPrompt.Should().Contain("Use when the plan says \"Add\", \"Update\", \"Fix\", or you're modifying existing code",
            "Must explain when to use modify_file");

        coderDefinition.SystemPrompt.Should().Contain("Read the plan carefully to understand which files are new vs existing",
            "Must instruct to read plan for file status");
    }

    /// <summary>
    /// Validates that the Coder prompt requires finalize_file_operations as final step.
    /// Missing finalization causes pipeline failures.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_RequiresFinalization()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - Finalization requirement emphasized
        coderDefinition.SystemPrompt.Should().Contain("MUST BE LAST",
            "Must emphasize finalize_file_operations is last step");

        coderDefinition.SystemPrompt.Should().Contain("If you do not call `mcp__pipeline-tools__finalize_file_operations`, the pipeline will FAIL",
            "Must warn about failure if finalization is skipped");

        coderDefinition.SystemPrompt.Should().Contain("Output ONLY the JSON returned by finalize_file_operations",
            "Must instruct to output finalize result");
    }

    /// <summary>
    /// Validates that the Coder prompt includes C# best practices.
    /// Quality standards ensure generated code meets expectations.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_IncludesCSharpBestPractices()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - C# best practices included
        coderDefinition.SystemPrompt.Should().Contain("## C# Best Practices",
            "Must have C# best practices section");

        coderDefinition.SystemPrompt.Should().Contain("Use modern C# features",
            "Must encourage modern C# usage");

        coderDefinition.SystemPrompt.Should().Contain("Add comprehensive XML documentation",
            "Must require XML documentation");

        coderDefinition.SystemPrompt.Should().Contain("Write comprehensive unit tests using xUnit",
            "Must specify xUnit for testing");
    }

    /// <summary>
    /// Validates that the Coder prompt includes MCP tool usage examples.
    /// Examples help agents understand correct tool invocation.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_IncludesMcpToolExamples()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - Examples for create_file and modify_file
        coderDefinition.SystemPrompt.Should().Contain("## MCP Tool Examples",
            "Must have MCP examples section");

        coderDefinition.SystemPrompt.Should().Contain("### Example 1: Create New File",
            "Must have create_file example");

        coderDefinition.SystemPrompt.Should().Contain("### Example 2: Modify Existing File",
            "Must have modify_file example");

        coderDefinition.SystemPrompt.Should().Contain("### Example 3: Modify Existing Method",
            "Must have modify existing method example");

        coderDefinition.SystemPrompt.Should().Contain("### Example 4: Finalize (REQUIRED)",
            "Must have finalize example");
    }

    /// <summary>
    /// Validates that critical sections appear in the expected order.
    /// Order matters for LLM attention and comprehension.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_HasCorrectSectionOrdering()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - Sections appear in logical order
        var forbiddenIndex = coderDefinition.SystemPrompt.IndexOf("If you use ANY other tool", StringComparison.Ordinal);
        var toolsIndex = coderDefinition.SystemPrompt.IndexOf("### The 5 Required MCP Tools:", StringComparison.Ordinal);
        var workflowIndex = coderDefinition.SystemPrompt.IndexOf("## Required Workflow", StringComparison.Ordinal);
        var bestPracticesIndex = coderDefinition.SystemPrompt.IndexOf("## C# Best Practices", StringComparison.Ordinal);

        forbiddenIndex.Should().BeGreaterThan(0, "FORBIDDEN warning must exist near top");
        toolsIndex.Should().BeGreaterThan(forbiddenIndex, "Tools list should come after warning");
        workflowIndex.Should().BeGreaterThan(toolsIndex, "Workflow should come after tools");
        bestPracticesIndex.Should().BeGreaterThan(workflowIndex, "Best practices should come after workflow");
    }

    /// <summary>
    /// Validates that the prompt emphasizes error prevention.
    /// Common mistakes listed help agents avoid failures.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_ListsCommonMistakes()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - Common mistakes section exists
        coderDefinition.SystemPrompt.Should().Contain("## Common Patterns",
            "Must have common patterns section for guidance");
    }
}
