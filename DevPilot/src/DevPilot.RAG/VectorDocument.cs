namespace DevPilot.RAG;

/// <summary>
/// Represents a document chunk with its vector embedding and metadata.
/// </summary>
public sealed class VectorDocument
{
    /// <summary>
    /// Gets or sets the unique identifier for this document chunk.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the text content of this document chunk.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets or sets the vector embedding representation of the text.
    /// </summary>
    public required float[] Embedding { get; init; }

    /// <summary>
    /// Gets or sets metadata about the source of this document.
    /// </summary>
    /// <example>
    /// Metadata: { "source": "coder/system-prompt.md", "section": "Async/Await Patterns", "start_line": 150, "end_line": 200 }
    /// </example>
    public required Dictionary<string, object> Metadata { get; init; }

    /// <summary>
    /// Gets or sets the similarity score when retrieved from a query (0.0 to 1.0).
    /// </summary>
    /// <remarks>
    /// This is populated during retrieval and indicates how relevant this document is to the query.
    /// </remarks>
    public double Score { get; set; }
}
