using DevPilot.Agents;
using FluentAssertions;
using Xunit;

namespace DevPilot.Agents.IntegrationTests;

/// <summary>
/// Validates that the Coder agent system prompt contains all required sections
/// for discovery, self-verification, and quality standards.
/// These tests ensure prompt regressions don't silently break agent behavior.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CoderPromptValidationTests
{
    private const string CoderAgentPath = "../../../../../.agents/coder";

    /// <summary>
    /// Validates that the Coder prompt contains the mandatory discovery section.
    /// This prevents regressions where discovery guidance is accidentally removed.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_ContainsMandatoryDiscoverySection()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - Check for mandatory discovery section
        coderDefinition.SystemPrompt.Should().Contain("MANDATORY FIRST STEP: DISCOVER PROJECT STRUCTURE",
            "Coder prompt must emphasize discovery as first step");

        coderDefinition.SystemPrompt.Should().Contain("BEFORE WRITING ANY CODE, YOU MUST DISCOVER THE WORKSPACE STRUCTURE USING TOOLS",
            "Coder must be instructed to use discovery tools");

        coderDefinition.SystemPrompt.Should().Contain("Tool: Glob",
            "Coder must be shown how to use Glob for discovery");

        coderDefinition.SystemPrompt.Should().Contain("Pattern: \"**/*.csproj\"",
            "Coder must know to search for .csproj files");
    }

    /// <summary>
    /// Validates that the Coder prompt contains the discovery verification checklist.
    /// Ensures agents verify discovery was performed before generating patches.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_ContainsDiscoveryVerificationChecklist()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - Check for verification checklist items
        coderDefinition.SystemPrompt.Should().Contain("Discovery Verification Checklist",
            "Coder must have checklist for verifying discovery");

        coderDefinition.SystemPrompt.Should().Contain("I used Glob to discover .csproj files in the workspace",
            "Checklist must include Glob usage verification");

        coderDefinition.SystemPrompt.Should().Contain("I identified main project directory",
            "Checklist must include main project identification");

        coderDefinition.SystemPrompt.Should().Contain("I identified test project directory",
            "Checklist must include test project identification");

        coderDefinition.SystemPrompt.Should().Contain("All file paths in my patch use ACTUAL discovered directories",
            "Checklist must verify paths use discovered directories");
    }

    /// <summary>
    /// Validates that the Coder prompt contains discovery examples showing correct vs wrong paths.
    /// Examples help agents understand expected behavior.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_ContainsDiscoveryExamples()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - Check for examples
        coderDefinition.SystemPrompt.Should().Contain("Example Discovery Output",
            "Coder must have concrete discovery examples");

        coderDefinition.SystemPrompt.Should().Contain("Your patches MUST use",
            "Coder must be shown correct path usage");

        coderDefinition.SystemPrompt.Should().Contain("NOT:",
            "Coder must be shown incorrect path patterns to avoid");

        coderDefinition.SystemPrompt.Should().Contain("WRONG! No src/ directory exists",
            "Examples must highlight common mistakes");
    }

    /// <summary>
    /// Validates that the Coder prompt contains the comprehensive self-check checklist.
    /// Self-checks ensure quality before finalizing patches.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_ContainsSelfCheckChecklist()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - Check for self-check section
        coderDefinition.SystemPrompt.Should().Contain("SELF-CHECK BEFORE FINALIZING PATCH",
            "Coder must have self-check section before finalization");

        coderDefinition.SystemPrompt.Should().Contain("**MANDATORY**: Before outputting your unified diff patch, verify ALL of these criteria",
            "Self-check must be emphasized as mandatory");
    }

    /// <summary>
    /// Validates that the self-check checklist includes discovery verification.
    /// Ensures discovery is re-verified as final check before patch output.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_SelfCheckIncludesDiscoveryVerification()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - Discovery verification in self-check
        coderDefinition.SystemPrompt.Should().Contain("1. Discovery Verification",
            "Self-check must have discovery as first item");

        coderDefinition.SystemPrompt.Should().Contain("I used Glob to discover .csproj files",
            "Self-check must verify Glob usage");

        coderDefinition.SystemPrompt.Should().Contain("All file paths in my patch use ACTUAL discovered directories (not assumptions)",
            "Self-check must verify paths use discoveries not assumptions");
    }

    /// <summary>
    /// Validates that the self-check includes file path correctness verification.
    /// Prevents orphan test files and wrong directory usage.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_SelfCheckIncludesFilePathCorrectness()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - File path verification in self-check
        coderDefinition.SystemPrompt.Should().Contain("2. File Path Correctness",
            "Self-check must have file path section");

        coderDefinition.SystemPrompt.Should().Contain("New implementation files use main project directory",
            "Self-check must verify main project usage");

        coderDefinition.SystemPrompt.Should().Contain("New test files use test project directory",
            "Self-check must verify test project usage");

        coderDefinition.SystemPrompt.Should().Contain("No orphan test files in directories without `.csproj` files",
            "Self-check must prevent orphan test files");
    }

    /// <summary>
    /// Validates that the self-check includes test coverage verification.
    /// Ensures comprehensive test generation (fixes 2/10 coverage scores).
    /// </summary>
    [Fact]
    public async Task CoderPrompt_SelfCheckIncludesTestCoverageVerification()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - Test coverage verification in self-check
        coderDefinition.SystemPrompt.Should().Contain("4. Test Coverage Verification",
            "Self-check must have test coverage section");

        coderDefinition.SystemPrompt.Should().Contain("Every public method has at least 3-5 test cases",
            "Self-check must verify comprehensive test count");

        coderDefinition.SystemPrompt.Should().Contain("Edge cases covered (null, empty, zero, negative, max values)",
            "Self-check must verify edge case coverage");

        coderDefinition.SystemPrompt.Should().Contain("Exception cases tested with `Assert.Throws<>()`",
            "Self-check must verify exception testing");

        coderDefinition.SystemPrompt.Should().Contain("Floating-point comparisons use reasonable precision (precision: 4-7, NOT 10+)",
            "Self-check must prevent overly strict float precision");
    }

    /// <summary>
    /// Validates that the self-check includes common failure modes to avoid.
    /// Learning from past validation failures prevents regressions.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_SelfCheckIncludesCommonFailureModes()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - Common failure modes listed
        coderDefinition.SystemPrompt.Should().Contain("### Common Failure Modes to Avoid",
            "Self-check must list common mistakes");

        coderDefinition.SystemPrompt.Should().Contain("❌ **File path assumptions**: Assuming `src/` or `tests/` without discovery",
            "Must warn against path assumptions");

        coderDefinition.SystemPrompt.Should().Contain("❌ **Orphan test files**: Creating `tests/ClassTests.cs` instead of `ProjectName.Tests/ClassTests.cs`",
            "Must warn against orphan test files");

        coderDefinition.SystemPrompt.Should().Contain("❌ **Overly strict float precision**: Using `precision: 10` for Math.Sqrt() results",
            "Must warn against overly strict precision");

        coderDefinition.SystemPrompt.Should().Contain("❌ **Incomplete test coverage**: Only testing happy path, skipping edge cases",
            "Must warn against incomplete coverage");
    }

    /// <summary>
    /// Validates that the self-check has a strong failure consequence statement.
    /// Ensures agents take checklist seriously.
    /// </summary>
    [Fact]
    public async Task CoderPrompt_SelfCheckHasFailureConsequence()
    {
        // Arrange
        var loader = new AgentLoader(Path.GetDirectoryName(CoderAgentPath)!);
        var coderDefinition = await loader.LoadAgentAsync("coder", TestContext.Current.CancellationToken);

        // Assert - Strong consequence statement
        coderDefinition.SystemPrompt.Should().Contain("If ANY checklist item fails, DO NOT finalize the patch",
            "Self-check must have strong failure consequence");

        coderDefinition.SystemPrompt.Should().Contain("Fix the issue first",
            "Must instruct to fix before proceeding");
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
        var discoveryIndex = coderDefinition.SystemPrompt.IndexOf("MANDATORY FIRST STEP: DISCOVER PROJECT STRUCTURE", StringComparison.Ordinal);
        var responsibilitiesIndex = coderDefinition.SystemPrompt.IndexOf("## Responsibilities", StringComparison.Ordinal);
        var selfCheckIndex = coderDefinition.SystemPrompt.IndexOf("SELF-CHECK BEFORE FINALIZING PATCH", StringComparison.Ordinal);
        var outputFormatIndex = coderDefinition.SystemPrompt.IndexOf("## Output Format - Unified Diff", StringComparison.Ordinal);

        discoveryIndex.Should().BeGreaterThan(0, "Discovery section must exist");
        responsibilitiesIndex.Should().BeGreaterThan(discoveryIndex, "Responsibilities should come after discovery");
        selfCheckIndex.Should().BeGreaterThan(responsibilitiesIndex, "Self-check should come before output format");
        outputFormatIndex.Should().BeGreaterThan(selfCheckIndex, "Output format should be last major section");
    }
}
