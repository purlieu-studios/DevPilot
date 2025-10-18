namespace DevPilot.RAG;

/// <summary>
/// Interface for generating vector embeddings from text.
/// </summary>
public interface IEmbeddingService : IDisposable
{
    /// <summary>
    /// Gets the dimensionality of the embeddings produced by this service.
    /// </summary>
    /// <example>
    /// For all-MiniLM-L6-v2, this returns 384.
    /// For OpenAI text-embedding-ada-002, this returns 1536.
    /// </example>
    int EmbeddingDimension { get; }

    /// <summary>
    /// Generates a vector embedding for a single text string.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The vector embedding as an array of floats.</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates vector embeddings for multiple text strings in a batch.
    /// </summary>
    /// <param name="texts">The texts to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The vector embeddings, one per input text.</returns>
    /// <remarks>
    /// Batch processing is more efficient than calling GenerateEmbeddingAsync multiple times.
    /// </remarks>
    Task<float[][]> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
