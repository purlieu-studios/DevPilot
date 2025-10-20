namespace DevPilot.Core;

/// <summary>
/// Represents the detected project structure of a target repository.
/// Used to inform agents about actual directory names and project organization.
/// </summary>
public sealed class ProjectStructureInfo
{
    /// <summary>
    /// Gets the main production project directory (e.g., "src/", "Testing/", "MyApp/").
    /// Null if no main project detected.
    /// </summary>
    public string? MainProject { get; init; }

    /// <summary>
    /// Gets the list of test project directories (e.g., ["tests/", "Testing.Tests/"]).
    /// Empty list if no test projects detected.
    /// </summary>
    public IReadOnlyList<string> TestProjects { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of all project directories found in the repository.
    /// </summary>
    public IReadOnlyList<string> AllProjects { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets whether the repository has a documentation directory (docs/).
    /// </summary>
    public bool HasDocs { get; init; }

    /// <summary>
    /// Gets whether the repository has custom agent definitions (.agents/).
    /// </summary>
    public bool HasAgents { get; init; }

    /// <summary>
    /// Gets whether the repository has a CLAUDE.md file with project instructions.
    /// </summary>
    public bool HasClaudeMd { get; init; }

    /// <summary>
    /// Creates a default structure info with no detected projects.
    /// </summary>
    public static ProjectStructureInfo Empty => new ProjectStructureInfo
    {
        MainProject = null,
        TestProjects = Array.Empty<string>(),
        AllProjects = Array.Empty<string>(),
        HasDocs = false,
        HasAgents = false,
        HasClaudeMd = false
    };

    /// <summary>
    /// Formats the structure info as a human-readable string for passing to agents.
    /// </summary>
    public string ToAgentContext()
    {
        var context = "Repository Structure:\n";

        if (MainProject != null)
        {
            context += $"- Main Project: {MainProject}\n";
        }
        else
        {
            context += "- Main Project: (not detected)\n";
        }

        if (TestProjects.Count > 0)
        {
            context += $"- Test Projects: {string.Join(", ", TestProjects)}\n";
        }
        else
        {
            context += "- Test Projects: (none detected)\n";
        }

        if (HasDocs)
        {
            context += "- Documentation: docs/\n";
        }

        if (HasClaudeMd)
        {
            context += "- Project Instructions: CLAUDE.md (read this for context)\n";
        }

        context += "\nIMPORTANT: Use the ACTUAL project directories listed above when generating file paths.\n";
        context += "Do NOT assume standard names like 'src/' or 'tests/' unless they are listed above.\n";

        return context;
    }
}
