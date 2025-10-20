namespace DevPilot.RAG;

/// <summary>
/// Interface for storing and retrieving vector embeddings.
/// </summary>
public interface IVectorStore : IDisposable
{
    /// <summary>
    /// Initializes the vector store (creates tables, indexes, etc.).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a vector document in the database.
    /// </summary>
    /// <param name="document">The document to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddDocumentAsync(VectorDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores multiple vector documents in a batch operation.
    /// </summary>
    /// <param name="documents">The documents to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddDocumentsAsync(IEnumerable<VectorDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for the most similar documents to the query vector.
    /// </summary>
    /// <param name="queryEmbedding">The query vector to search for.</param>
    /// <param name="topK">The number of top results to return.</param>
    /// <param name="filter">Optional metadata filter (e.g., source file, section).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The top K most similar documents, ordered by similarity score (highest first).</returns>
    Task<IReadOnlyList<VectorDocument>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        Dictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total number of documents stored in the vector store.
    /// </summary>
    /// <param name="workspaceId">Optional workspace ID to filter by. If null, returns count for all workspaces.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of stored documents.</returns>
    Task<long> GetDocumentCountAsync(string? workspaceId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all documents from the vector store.
    /// </summary>
    /// <param name="workspaceId">Optional workspace ID to filter by. If null, clears all workspaces.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ClearAsync(string? workspaceId = null, CancellationToken cancellationToken = default);
}
