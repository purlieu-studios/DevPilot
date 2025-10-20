using System.Numerics.Tensors;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace DevPilot.RAG;

/// <summary>
/// SQLite-based implementation of <see cref="IVectorStore"/> for persistent vector storage.
/// </summary>
/// <remarks>
/// <para>
/// Stores vector embeddings in a SQLite database with support for similarity search using cosine similarity.
/// Database location: .devpilot/rag/{workspace-id}.db
/// </para>
/// <para>
/// Schema:
/// - documents: Stores document metadata and text content
/// - embeddings: Stores vector embeddings with foreign key to documents
/// </para>
/// </remarks>
public sealed class SqliteVectorStore : IVectorStore
{
    private readonly string _databasePath;
    private readonly SqliteConnection _connection;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteVectorStore"/> class.
    /// </summary>
    /// <param name="databasePath">The file path to the SQLite database.</param>
    public SqliteVectorStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        _databasePath = databasePath;

        // Ensure directory exists
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new SqliteConnection($"Data Source={databasePath}");
        _connection.Open();
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        const string createDocumentsTable = """
            CREATE TABLE IF NOT EXISTS documents (
                id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL,
                file_path TEXT NOT NULL,
                content TEXT NOT NULL,
                metadata TEXT NOT NULL,
                created_at INTEGER NOT NULL
            );
            """;

        const string createEmbeddingsTable = """
            CREATE TABLE IF NOT EXISTS embeddings (
                id TEXT PRIMARY KEY,
                document_id TEXT NOT NULL,
                embedding BLOB NOT NULL,
                FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE
            );
            """;

        const string createWorkspaceIndex = """
            CREATE INDEX IF NOT EXISTS idx_workspace ON documents(workspace_id);
            """;

        const string createDocumentIdIndex = """
            CREATE INDEX IF NOT EXISTS idx_document_id ON embeddings(document_id);
            """;

        await using var command = _connection.CreateCommand();

        command.CommandText = createDocumentsTable;
        await command.ExecuteNonQueryAsync(cancellationToken);

        command.CommandText = createEmbeddingsTable;
        await command.ExecuteNonQueryAsync(cancellationToken);

        command.CommandText = createWorkspaceIndex;
        await command.ExecuteNonQueryAsync(cancellationToken);

        command.CommandText = createDocumentIdIndex;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddDocumentAsync(VectorDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        await using var transaction = (SqliteTransaction)await _connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // Insert document metadata
            const string insertDocument = """
                INSERT OR REPLACE INTO documents (id, workspace_id, file_path, content, metadata, created_at)
                VALUES (@id, @workspace_id, @file_path, @content, @metadata, @created_at);
                """;

            await using (var command = _connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = insertDocument;

                command.Parameters.AddWithValue("@id", document.Id);
                command.Parameters.AddWithValue("@workspace_id", document.Metadata.TryGetValue("workspace_id", out var wsId) ? wsId : "default");
                command.Parameters.AddWithValue("@file_path", document.Metadata.TryGetValue("file_path", out var path) ? path : "unknown");
                command.Parameters.AddWithValue("@content", document.Text);
                command.Parameters.AddWithValue("@metadata", JsonSerializer.Serialize(document.Metadata));
                command.Parameters.AddWithValue("@created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            // Insert embedding
            const string insertEmbedding = """
                INSERT OR REPLACE INTO embeddings (id, document_id, embedding)
                VALUES (@id, @document_id, @embedding);
                """;

            await using (var command = _connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = insertEmbedding;

                command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                command.Parameters.AddWithValue("@document_id", document.Id);
                command.Parameters.AddWithValue("@embedding", SerializeEmbedding(document.Embedding));

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AddDocumentsAsync(IEnumerable<VectorDocument> documents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var documentList = documents.ToList();
        if (documentList.Count == 0)
        {
            return;
        }

        await using var transaction = (SqliteTransaction)await _connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var document in documentList)
            {
                await AddDocumentInternalAsync(document, transaction, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VectorDocument>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        Dictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);

        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), "topK must be greater than 0.");
        }

        const string selectQuery = """
            SELECT d.id, d.file_path, d.content, d.metadata, e.embedding
            FROM documents d
            INNER JOIN embeddings e ON d.id = e.document_id
            """;

        await using var command = _connection.CreateCommand();
        command.CommandText = selectQuery;

        var results = new List<(VectorDocument Document, float Similarity)>();

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetString(0);
                var filePath = reader.GetString(1);
                var content = reader.GetString(2);
                var metadataJson = reader.GetString(3);
                var embeddingBytes = (byte[])reader["embedding"];

                var embedding = DeserializeEmbedding(embeddingBytes);
                var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson) ?? new Dictionary<string, object>();

                // Apply filter if specified
                if (filter != null && !MatchesFilter(metadata, filter))
                {
                    continue;
                }

                var similarity = CosineSimilarity(queryEmbedding, embedding);

                var document = new VectorDocument
                {
                    Id = id,
                    Text = content,
                    Embedding = embedding,
                    Metadata = metadata,
                    Score = similarity
                };

                results.Add((document, similarity));
            }
        }

        // Sort by similarity (highest first) and take top K
        return results
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .Select(r => r.Document)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<long> GetDocumentCountAsync(string? workspaceId = null, CancellationToken cancellationToken = default)
    {
        string countQuery;
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            // Count all documents
            countQuery = "SELECT COUNT(*) FROM documents;";
        }
        else
        {
            // Count documents for specific workspace
            countQuery = "SELECT COUNT(*) FROM documents WHERE json_extract(metadata, '$.workspace_id') = @workspaceId;";
        }

        await using var command = _connection.CreateCommand();
        command.CommandText = countQuery;

        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            command.Parameters.AddWithValue("@workspaceId", workspaceId);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt64(result) : 0;
    }

    /// <inheritdoc />
    public async Task ClearAsync(string? workspaceId = null, CancellationToken cancellationToken = default)
    {
        await using var transaction = (SqliteTransaction)await _connection.BeginTransactionAsync(cancellationToken);

        try
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
            {
                // Clear all documents (all workspaces)
                await using (var command = _connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM embeddings;";
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var command = _connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM documents;";
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            else
            {
                // Delete only documents for specific workspace
                // First, get document IDs for the workspace
                var documentIds = new List<long>();
                await using (var command = _connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "SELECT id FROM documents WHERE json_extract(metadata, '$.workspace_id') = @workspaceId;";
                    command.Parameters.AddWithValue("@workspaceId", workspaceId);

                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        documentIds.Add(reader.GetInt64(0));
                    }
                }

                // Delete embeddings for these documents
                if (documentIds.Count > 0)
                {
                    var idsParam = string.Join(",", documentIds);
                    await using (var command = _connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = $"DELETE FROM embeddings WHERE document_id IN ({idsParam});";
                        await command.ExecuteNonQueryAsync(cancellationToken);
                    }

                    // Delete documents
                    await using (var command = _connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM documents WHERE json_extract(metadata, '$.workspace_id') = @workspaceId;";
                        command.Parameters.AddWithValue("@workspaceId", workspaceId);
                        await command.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _connection?.Dispose();
        _disposed = true;
    }

    // Private helper methods

    private async Task AddDocumentInternalAsync(VectorDocument document, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        const string insertDocument = """
            INSERT OR REPLACE INTO documents (id, workspace_id, file_path, content, metadata, created_at)
            VALUES (@id, @workspace_id, @file_path, @content, @metadata, @created_at);
            """;

        await using (var command = _connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = insertDocument;

            command.Parameters.AddWithValue("@id", document.Id);
            command.Parameters.AddWithValue("@workspace_id", document.Metadata.TryGetValue("workspace_id", out var wsId) ? wsId : "default");
            command.Parameters.AddWithValue("@file_path", document.Metadata.TryGetValue("file_path", out var path) ? path : "unknown");
            command.Parameters.AddWithValue("@content", document.Text);
            command.Parameters.AddWithValue("@metadata", JsonSerializer.Serialize(document.Metadata));
            command.Parameters.AddWithValue("@created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertEmbedding = """
            INSERT OR REPLACE INTO embeddings (id, document_id, embedding)
            VALUES (@id, @document_id, @embedding);
            """;

        await using (var command = _connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = insertEmbedding;

            command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("@document_id", document.Id);
            command.Parameters.AddWithValue("@embedding", SerializeEmbedding(document.Embedding));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static byte[] SerializeEmbedding(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] DeserializeEmbedding(byte[] bytes)
    {
        var embedding = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, embedding, 0, bytes.Length);
        return embedding;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors must have the same dimension.");
        }

        return TensorPrimitives.CosineSimilarity(a, b);
    }

    private static bool MatchesFilter(Dictionary<string, object> metadata, Dictionary<string, object> filter)
    {
        foreach (var (key, value) in filter)
        {
            if (!metadata.TryGetValue(key, out var metadataValue))
            {
                return false;
            }

            // Handle JsonElement comparison (when deserialized from JSON)
            if (metadataValue is JsonElement jsonElement)
            {
                var stringValue = value.ToString();
                var metadataStringValue = jsonElement.ValueKind switch
                {
                    JsonValueKind.String => jsonElement.GetString(),
                    JsonValueKind.Number => jsonElement.GetRawText(),
                    JsonValueKind.True => "True",
                    JsonValueKind.False => "False",
                    _ => jsonElement.GetRawText()
                };

                if (!string.Equals(metadataStringValue, stringValue, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            else if (!metadataValue.Equals(value))
            {
                return false;
            }
        }

        return true;
    }
}
