namespace DevPilot.RAG;

/// <summary>
/// Service for Retrieval Augmented Generation (RAG) operations.
/// </summary>
/// <remarks>
/// Orchestrates document indexing, embedding generation, and similarity search
/// to provide relevant context for agent prompts.
/// </remarks>
public interface IRagService : IDisposable
{
    /// <summary>
    /// Indexes all relevant files in a workspace directory.
    /// </summary>
    /// <param name="workspacePath">The path to the workspace directory.</param>
    /// <param name="workspaceId">The unique workspace identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of document chunks indexed.</returns>
    Task<int> IndexWorkspaceAsync(string workspacePath, string workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the indexed workspace for relevant context.
    /// </summary>
    /// <param name="query">The query text to search for.</param>
    /// <param name="workspaceId">The workspace identifier to search within.</param>
    /// <param name="topK">The number of top results to return (default: 5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of relevant document chunks with similarity scores.</returns>
    Task<IReadOnlyList<VectorDocument>> QueryAsync(string query, string workspaceId, int topK = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Formats retrieved documents as context text for agent prompts.
    /// </summary>
    /// <param name="documents">The retrieved documents.</param>
    /// <param name="maxTokens">The maximum number of tokens to include (approximate).</param>
    /// <returns>Formatted context string ready for injection into prompts.</returns>
    string FormatContext(IReadOnlyList<VectorDocument> documents, int maxTokens = 8000);

    /// <summary>
    /// Clears all indexed data for a specific workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier to clear.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total number of indexed document chunks for a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of indexed chunks.</returns>
    Task<long> GetIndexedChunkCountAsync(string workspaceId, CancellationToken cancellationToken = default);
}
