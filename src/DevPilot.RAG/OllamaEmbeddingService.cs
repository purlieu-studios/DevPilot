using Microsoft.Extensions.AI;
using OllamaSharp;

namespace DevPilot.RAG;

/// <summary>
/// Embedding service implementation using Ollama for local model inference.
/// </summary>
/// <remarks>
/// <para>
/// This service generates vector embeddings using locally-hosted models via Ollama.
/// Default model: mxbai-embed-large (1024 dimensions, 59.25% retrieval accuracy).
/// </para>
/// <para>
/// Requires Ollama to be installed and running:
/// <code>
/// curl -fsSL https://ollama.com/install.sh | sh
/// ollama pull mxbai-embed-large
/// </code>
/// </para>
/// </remarks>
public sealed class OllamaEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly int _embeddingDimension;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaEmbeddingService"/> class.
    /// </summary>
    /// <param name="endpoint">The Ollama API endpoint (default: http://localhost:11434).</param>
    /// <param name="modelName">The embedding model name (default: mxbai-embed-large).</param>
    /// <param name="embeddingDimension">The embedding dimension for the model (default: 1024 for mxbai-embed-large).</param>
    public OllamaEmbeddingService(
        string endpoint = "http://localhost:11434",
        string modelName = "mxbai-embed-large",
        int embeddingDimension = 1024)
    {
        var client = new OllamaApiClient(new Uri(endpoint))
        {
            SelectedModel = modelName
        };

        _generator = client;
        _embeddingDimension = embeddingDimension;
    }

    /// <inheritdoc />
    public int EmbeddingDimension => _embeddingDimension;

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be empty or whitespace.", nameof(text));
        }

        try
        {
            var result = await _generator.GenerateAsync([text], cancellationToken: cancellationToken);

            if (result == null || result.Count == 0)
            {
                throw new InvalidOperationException($"Ollama returned no embeddings for text: '{text[..Math.Min(50, text.Length)]}'");
            }

            var embedding = result[0];
            var vector = embedding.Vector.ToArray();

            if (vector.Length != _embeddingDimension)
            {
                throw new InvalidOperationException(
                    $"Expected embedding dimension {_embeddingDimension}, but got {vector.Length}. " +
                    $"Verify the model configuration is correct.");
            }

            return vector;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Failed to connect to Ollama. " +
                $"Ensure Ollama is running and the model is pulled.",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<float[][]> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        // Validate all texts
        for (int i = 0; i < texts.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(texts[i]))
            {
                throw new ArgumentException($"Text at index {i} is null, empty, or whitespace.", nameof(texts));
            }
        }

        try
        {
            var result = await _generator.GenerateAsync(texts, cancellationToken: cancellationToken);

            if (result == null || result.Count == 0)
            {
                throw new InvalidOperationException("Ollama returned no embeddings for batch operation.");
            }

            var embeddings = new float[result.Count][];
            for (int i = 0; i < result.Count; i++)
            {
                var embedding = result[i];
                if (embedding == null || embedding.Vector.Length == 0)
                {
                    throw new InvalidOperationException($"Ollama returned an empty embedding at index {i}.");
                }

                var vector = embedding.Vector.ToArray();

                if (vector.Length != _embeddingDimension)
                {
                    throw new InvalidOperationException(
                        $"Expected embedding dimension {_embeddingDimension}, but got {vector.Length} at index {i}.");
                }

                embeddings[i] = vector;
            }

            return embeddings;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                "Failed to connect to Ollama during batch embedding generation. " +
                "Ensure Ollama is running and the model is pulled.",
                ex);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_generator is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _disposed = true;
    }
}
