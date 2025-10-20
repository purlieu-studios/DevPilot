namespace DevPilot.RAG;

/// <summary>
/// Configuration options for the RAG (Retrieval Augmented Generation) system.
/// </summary>
public sealed class RAGOptions
{
    /// <summary>
    /// Gets or sets the Ollama API endpoint URL.
    /// </summary>
    /// <remarks>
    /// Default: http://localhost:11434
    /// </remarks>
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Gets or sets the embedding model name.
    /// </summary>
    /// <remarks>
    /// Recommended models:
    /// - "mxbai-embed-large" (1024 dims, 59.25% retrieval accuracy, default)
    /// - "nomic-embed-text" (768 dims, faster, better for long context)
    /// </remarks>
    public string EmbeddingModel { get; set; } = "mxbai-embed-large";

    /// <summary>
    /// Gets or sets the embedding dimension for the selected model.
    /// </summary>
    /// <remarks>
    /// - mxbai-embed-large: 1024
    /// - nomic-embed-text: 768
    /// </remarks>
    public int EmbeddingDimension { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the chunk size in tokens for document splitting.
    /// </summary>
    /// <remarks>
    /// Default: 512 tokens (approximately 400 words or 2000 characters)
    /// </remarks>
    public int ChunkSize { get; set; } = 512;

    /// <summary>
    /// Gets or sets the overlap size in tokens between consecutive chunks.
    /// </summary>
    /// <remarks>
    /// Default: 50 tokens (approximately 10% overlap for context continuity)
    /// </remarks>
    public int OverlapSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets the number of top results to retrieve during similarity search.
    /// </summary>
    /// <remarks>
    /// Default: 5 (top 5 most relevant document chunks)
    /// </remarks>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum total context size in tokens to inject into agent prompts.
    /// </summary>
    /// <remarks>
    /// Default: 8000 tokens (approximately 6000 words or 32,000 characters).
    /// Prevents overwhelming agent context windows while providing sufficient information.
    /// </remarks>
    public int MaxContextTokens { get; set; } = 8000;

    /// <summary>
    /// Gets or sets the database file path for the vector store.
    /// </summary>
    /// <remarks>
    /// Default: ".devpilot/rag/{workspace-id}.db"
    /// The {workspace-id} placeholder will be replaced at runtime.
    /// </remarks>
    public string DatabasePath { get; set; } = ".devpilot/rag/{workspace-id}.db";

    /// <summary>
    /// Gets or sets file glob patterns to exclude from indexing.
    /// </summary>
    /// <remarks>
    /// Default excludes: bin/, obj/, .git/, node_modules/, .devpilot/
    /// </remarks>
    public string[] ExcludePatterns { get; set; } = new[]
    {
        "**/bin/**",
        "**/obj/**",
        "**/.git/**",
        "**/.vs/**",
        "**/node_modules/**",
        "**/.devpilot/**",
        "**/packages/**"
    };

    /// <summary>
    /// Gets or sets file extensions to include in indexing.
    /// </summary>
    /// <remarks>
    /// Default includes code, documentation, and configuration files.
    /// </remarks>
    public string[] IncludeExtensions { get; set; } = new[]
    {
        ".cs",    // C# source files
        ".csproj", // C# project files
        ".md",    // Markdown documentation
        ".json",  // JSON configuration
        ".xml",   // XML configuration
        ".yaml",  // YAML configuration
        ".yml",   // YAML configuration
        ".txt"    // Plain text documentation
    };

    /// <summary>
    /// Gets the default configuration.
    /// </summary>
    public static RAGOptions Default => new();
}
