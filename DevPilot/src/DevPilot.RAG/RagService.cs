namespace DevPilot.RAG;

/// <summary>
/// Implementation of <see cref="IRagService"/> for document indexing and retrieval.
/// </summary>
public sealed class RagService : IRagService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly DocumentChunker _chunker;
    private readonly RAGOptions _options;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagService"/> class.
    /// </summary>
    /// <param name="embeddingService">The embedding service for generating vectors.</param>
    /// <param name="vectorStore">The vector store for persisting embeddings.</param>
    /// <param name="options">Configuration options for RAG operations.</param>
    public RagService(
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        RAGOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(embeddingService);
        ArgumentNullException.ThrowIfNull(vectorStore);

        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _options = options ?? RAGOptions.Default;

        // ChunkSize is in tokens (approx), convert to characters (4 chars ≈ 1 token)
        int chunkSizeChars = _options.ChunkSize * 4;
        int overlapSizeChars = _options.OverlapSize * 4;

        _chunker = new DocumentChunker(chunkSizeChars, overlapSizeChars);
    }

    /// <inheritdoc />
    public async Task<int> IndexWorkspaceAsync(string workspacePath, string workspaceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        if (!Directory.Exists(workspacePath))
        {
            throw new DirectoryNotFoundException($"Workspace path does not exist: {workspacePath}");
        }

        // Initialize vector store
        await _vectorStore.InitializeAsync(cancellationToken);

        // Discover files
        var files = DiscoverFiles(workspacePath);

        // Process files and collect chunks
        var allChunks = new List<DocumentChunk>();
        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file, cancellationToken);
            var relativePath = Path.GetRelativePath(workspacePath, file);
            var chunks = _chunker.ChunkDocument(content, relativePath);
            allChunks.AddRange(chunks);
        }

        if (allChunks.Count == 0)
        {
            return 0;
        }

        // Generate embeddings in batches
        var chunkTexts = allChunks.Select(c => c.Text).ToList();
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunkTexts, cancellationToken);

        // Convert to VectorDocuments
        var documents = new List<VectorDocument>();
        for (int i = 0; i < allChunks.Count; i++)
        {
            var vectorDoc = allChunks[i].ToVectorDocument(embeddings[i], workspaceId);
            documents.Add(vectorDoc);
        }

        // Store in vector database
        await _vectorStore.AddDocumentsAsync(documents, cancellationToken);

        return documents.Count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VectorDocument>> QueryAsync(
        string query,
        string workspaceId,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), "topK must be greater than 0.");
        }

        // Generate embedding for query
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        // Search vector store with workspace filter
        var filter = new Dictionary<string, object>
        {
            ["workspace_id"] = workspaceId
        };

        var results = await _vectorStore.SearchAsync(queryEmbedding, topK, filter, cancellationToken);

        return results;
    }

    /// <inheritdoc />
    public string FormatContext(IReadOnlyList<VectorDocument> documents, int maxTokens = 8000)
    {
        ArgumentNullException.ThrowIfNull(documents);

        if (documents.Count == 0)
        {
            return string.Empty;
        }

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("# Relevant Context from Workspace");
        contextBuilder.AppendLine();

        int approximateTokens = 0;
        const int tokensPerChar = 4; // Rough estimate: 1 token ≈ 4 characters

        // Prioritize CLAUDE.md if present
        var prioritizedDocs = documents
            .OrderByDescending(d => d.Metadata.TryGetValue("file_path", out var path) && path.ToString()!.Contains("CLAUDE.md"))
            .ThenByDescending(d => d.Score)
            .ToList();

        foreach (var doc in prioritizedDocs)
        {
            var filePath = doc.Metadata.TryGetValue("file_path", out var path) ? path.ToString() : "unknown";
            var chunkIndex = doc.Metadata.TryGetValue("chunk_index", out var idx) ? idx : 0;
            var score = doc.Score;

            var chunkHeader = $"## {filePath} (chunk {chunkIndex}, relevance: {score:F3})";
            var chunkContent = doc.Text;

            // Estimate tokens for this chunk
            int chunkTokens = (chunkHeader.Length + chunkContent.Length) / tokensPerChar;

            // Stop if adding this chunk would exceed max tokens
            if (approximateTokens + chunkTokens > maxTokens)
            {
                break;
            }

            contextBuilder.AppendLine(chunkHeader);
            contextBuilder.AppendLine();
            contextBuilder.AppendLine(chunkContent);
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("---");
            contextBuilder.AppendLine();

            approximateTokens += chunkTokens;
        }

        return contextBuilder.ToString();
    }

    /// <inheritdoc />
    public async Task ClearWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        // Note: Current IVectorStore.ClearAsync() clears ALL data
        // Future enhancement: Add workspace-specific deletion
        await _vectorStore.ClearAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> GetIndexedChunkCountAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        // Note: Current IVectorStore.GetDocumentCountAsync() returns ALL documents
        // Future enhancement: Add workspace-specific counting
        return await _vectorStore.GetDocumentCountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _embeddingService?.Dispose();
        _vectorStore?.Dispose();
        _disposed = true;
    }

    // Private helper methods

    private List<string> DiscoverFiles(string workspacePath)
    {
        var files = new List<string>();

        foreach (var extension in _options.IncludeExtensions)
        {
            var pattern = $"*{extension}";
            var matchingFiles = Directory.GetFiles(workspacePath, pattern, SearchOption.AllDirectories);
            files.AddRange(matchingFiles);
        }

        // Filter out excluded patterns
        var filteredFiles = files.Where(file => !IsExcluded(file, workspacePath)).ToList();

        return filteredFiles;
    }

    private bool IsExcluded(string filePath, string workspacePath)
    {
        var relativePath = Path.GetRelativePath(workspacePath, filePath);

        foreach (var pattern in _options.ExcludePatterns)
        {
            // Simple glob pattern matching (supports **/pattern/**)
            var regexPattern = pattern
                .Replace("**", ".*")
                .Replace("*", "[^/\\\\]*")
                .Replace("/", "[/\\\\]");

            if (System.Text.RegularExpressions.Regex.IsMatch(relativePath, regexPattern))
            {
                return true;
            }
        }

        return false;
    }
}
