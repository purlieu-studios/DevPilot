namespace DevPilot.RAG;

/// <summary>
/// Provides document chunking functionality for splitting large documents into smaller, overlapping chunks.
/// </summary>
/// <remarks>
/// <para>
/// Chunking is essential for RAG systems because:
/// 1. Embedding models have token limits
/// 2. Smaller chunks provide more precise context matching
/// 3. Overlap ensures context continuity across chunk boundaries
/// </para>
/// <para>
/// Chunking strategy:
/// - Text files: Split by sentences/paragraphs, then by character count
/// - Code files: Attempt to preserve method/class boundaries (future enhancement)
/// </para>
/// </remarks>
public sealed class DocumentChunker
{
    private readonly int _chunkSize;
    private readonly int _overlapSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentChunker"/> class.
    /// </summary>
    /// <param name="chunkSize">The maximum chunk size in characters (default: 2000).</param>
    /// <param name="overlapSize">The overlap size in characters between chunks (default: 200).</param>
    public DocumentChunker(int chunkSize = 2000, int overlapSize = 200)
    {
        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be greater than 0.");
        }

        if (overlapSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapSize), "Overlap size cannot be negative.");
        }

        if (overlapSize >= chunkSize)
        {
            throw new ArgumentException("Overlap size must be smaller than chunk size.");
        }

        _chunkSize = chunkSize;
        _overlapSize = overlapSize;
    }

    /// <summary>
    /// Splits a document into overlapping chunks.
    /// </summary>
    /// <param name="content">The document content to chunk.</param>
    /// <param name="filePath">The file path for metadata (used in chunk metadata).</param>
    /// <returns>A list of document chunks with metadata.</returns>
    public List<DocumentChunk> ChunkDocument(string content, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var chunks = new List<DocumentChunk>();

        // If content is smaller than chunk size, return as single chunk
        if (content.Length <= _chunkSize)
        {
            chunks.Add(new DocumentChunk
            {
                Text = content,
                ChunkIndex = 0,
                FilePath = filePath,
                StartCharacter = 0,
                EndCharacter = content.Length
            });

            return chunks;
        }

        // Split into overlapping chunks
        int chunkIndex = 0;
        int position = 0;

        while (position < content.Length)
        {
            int endPosition = Math.Min(position + _chunkSize, content.Length);

            // Try to find a natural boundary (newline, sentence end, etc.)
            if (endPosition < content.Length)
            {
                endPosition = FindNaturalBoundary(content, endPosition);
            }

            var chunkText = content[position..endPosition];

            chunks.Add(new DocumentChunk
            {
                Text = chunkText,
                ChunkIndex = chunkIndex,
                FilePath = filePath,
                StartCharacter = position,
                EndCharacter = endPosition
            });

            chunkIndex++;

            // Move position forward, accounting for overlap
            position = endPosition - _overlapSize;

            // Ensure we make progress even if overlap is large
            if (position <= chunks[^1].StartCharacter)
            {
                position = endPosition;
            }
        }

        return chunks;
    }

    /// <summary>
    /// Finds a natural boundary near the target position for cleaner chunk splits.
    /// </summary>
    /// <param name="content">The content to search.</param>
    /// <param name="targetPosition">The target position to search near.</param>
    /// <returns>The adjusted position at a natural boundary.</returns>
    private int FindNaturalBoundary(string content, int targetPosition)
    {
        const int searchWindow = 100; // Look 100 chars back from target
        int searchStart = Math.Max(0, targetPosition - searchWindow);

        // Look for natural boundaries in order of preference:
        // 1. Paragraph break (double newline)
        int paragraphBreak = content.LastIndexOf("\n\n", targetPosition, targetPosition - searchStart);
        if (paragraphBreak >= searchStart)
        {
            return paragraphBreak + 2; // Include both newlines
        }

        // 2. Newline (single line break)
        int lineBreak = content.LastIndexOf('\n', targetPosition, targetPosition - searchStart);
        if (lineBreak >= searchStart)
        {
            return lineBreak + 1; // Include the newline
        }

        // 3. Sentence end (period, exclamation, question mark followed by space)
        for (int i = targetPosition - 1; i >= searchStart; i--)
        {
            char c = content[i];
            if ((c == '.' || c == '!' || c == '?') && i + 1 < content.Length && content[i + 1] == ' ')
            {
                return i + 2; // Include punctuation and space
            }
        }

        // 4. Whitespace
        int whitespace = content.LastIndexOfAny(new[] { ' ', '\t' }, targetPosition, targetPosition - searchStart);
        if (whitespace >= searchStart)
        {
            return whitespace + 1; // Include the whitespace
        }

        // No natural boundary found, use target position
        return targetPosition;
    }
}

/// <summary>
/// Represents a chunk of a document with metadata about its position.
/// </summary>
public sealed class DocumentChunk
{
    /// <summary>
    /// Gets or sets the text content of this chunk.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets or sets the index of this chunk within the document (0-based).
    /// </summary>
    public required int ChunkIndex { get; init; }

    /// <summary>
    /// Gets or sets the file path of the source document.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets or sets the starting character position in the original document.
    /// </summary>
    public required int StartCharacter { get; init; }

    /// <summary>
    /// Gets or sets the ending character position in the original document.
    /// </summary>
    public required int EndCharacter { get; init; }

    /// <summary>
    /// Gets the length of this chunk in characters.
    /// </summary>
    public int Length => Text.Length;

    /// <summary>
    /// Converts this chunk to a <see cref="VectorDocument"/> with the specified embedding and workspace ID.
    /// </summary>
    /// <param name="embedding">The vector embedding for this chunk.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <returns>A <see cref="VectorDocument"/> ready for storage.</returns>
    public VectorDocument ToVectorDocument(float[] embedding, string workspaceId)
    {
        return new VectorDocument
        {
            Id = $"{workspaceId}_{FilePath}_{ChunkIndex}",
            Text = Text,
            Embedding = embedding,
            Metadata = new Dictionary<string, object>
            {
                ["workspace_id"] = workspaceId,
                ["file_path"] = FilePath,
                ["chunk_index"] = ChunkIndex,
                ["start_character"] = StartCharacter,
                ["end_character"] = EndCharacter,
                ["file_extension"] = Path.GetExtension(FilePath)
            }
        };
    }
}
