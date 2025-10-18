using DevPilot.RAG;
using FluentAssertions;

namespace DevPilot.RAG.Tests;

public sealed class SqliteVectorStoreTests : IDisposable
{
    private readonly string _testDatabasePath;

    public SqliteVectorStoreTests()
    {
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.db");
    }

    public void Dispose()
    {
        // Give SQLite time to release file locks
        GC.Collect();
        GC.WaitForPendingFinalizers();

        try
        {
            if (File.Exists(_testDatabasePath))
            {
                File.Delete(_testDatabasePath);
            }
        }
        catch (IOException)
        {
            // Ignore file lock issues during cleanup
            // The temp file will be cleaned up by OS eventually
        }
    }

    [Fact]
    public async Task InitializeAsync_CreatesTablesAndIndexes()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);

        // Act
        await store.InitializeAsync();

        // Assert
        File.Exists(_testDatabasePath).Should().BeTrue();
        var count = await store.GetDocumentCountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task AddDocumentAsync_StoresDocumentAndEmbedding()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync();

        var document = new VectorDocument
        {
            Id = "test-1",
            Text = "This is a test document.",
            Embedding = new float[] { 0.1f, 0.2f, 0.3f },
            Metadata = new Dictionary<string, object>
            {
                ["workspace_id"] = "workspace-1",
                ["file_path"] = "test.cs"
            }
        };

        // Act
        await store.AddDocumentAsync(document);

        // Assert
        var count = await store.GetDocumentCountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task AddDocumentsAsync_StoresBatch()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync();

        var documents = new List<VectorDocument>
        {
            new()
            {
                Id = "doc-1",
                Text = "First document",
                Embedding = new float[] { 0.1f, 0.2f, 0.3f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            },
            new()
            {
                Id = "doc-2",
                Text = "Second document",
                Embedding = new float[] { 0.4f, 0.5f, 0.6f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            },
            new()
            {
                Id = "doc-3",
                Text = "Third document",
                Embedding = new float[] { 0.7f, 0.8f, 0.9f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            }
        };

        // Act
        await store.AddDocumentsAsync(documents);

        // Assert
        var count = await store.GetDocumentCountAsync();
        count.Should().Be(3);
    }

    [Fact]
    public async Task SearchAsync_FindsSimilarDocuments()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync();

        var documents = new List<VectorDocument>
        {
            new()
            {
                Id = "doc-1",
                Text = "Calculator class with Add method",
                Embedding = new float[] { 1.0f, 0.0f, 0.0f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            },
            new()
            {
                Id = "doc-2",
                Text = "User authentication service",
                Embedding = new float[] { 0.0f, 1.0f, 0.0f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            },
            new()
            {
                Id = "doc-3",
                Text = "Calculator class with Subtract method",
                Embedding = new float[] { 0.9f, 0.1f, 0.0f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            }
        };

        await store.AddDocumentsAsync(documents);

        // Act - Query with embedding similar to doc-1 and doc-3
        var queryEmbedding = new float[] { 0.95f, 0.05f, 0.0f };
        var results = await store.SearchAsync(queryEmbedding, topK: 2);

        // Assert
        results.Should().HaveCount(2);
        results[0].Id.Should().Be("doc-1"); // Most similar
        results[1].Id.Should().Be("doc-3"); // Second most similar
        results[0].Score.Should().BeGreaterThan(results[1].Score);
    }

    [Fact]
    public async Task SearchAsync_RespectsTopK()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync();

        var documents = Enumerable.Range(1, 10)
            .Select(i => new VectorDocument
            {
                Id = $"doc-{i}",
                Text = $"Document {i}",
                Embedding = new float[] { i / 10.0f, 0.5f, 0.5f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            })
            .ToList();

        await store.AddDocumentsAsync(documents);

        // Act
        var results = await store.SearchAsync(new float[] { 0.5f, 0.5f, 0.5f }, topK: 3);

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchAsync_WithFilter_ReturnsMatchingDocuments()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync();

        var documents = new List<VectorDocument>
        {
            new()
            {
                Id = "doc-1",
                Text = "Workspace 1 document",
                Embedding = new float[] { 1.0f, 0.0f, 0.0f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            },
            new()
            {
                Id = "doc-2",
                Text = "Workspace 2 document",
                Embedding = new float[] { 1.0f, 0.0f, 0.0f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-2" }
            },
            new()
            {
                Id = "doc-3",
                Text = "Another workspace 1 document",
                Embedding = new float[] { 0.9f, 0.1f, 0.0f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            }
        };

        await store.AddDocumentsAsync(documents);

        // Act - Search with workspace filter
        var filter = new Dictionary<string, object> { ["workspace_id"] = "ws-1" };
        var results = await store.SearchAsync(new float[] { 1.0f, 0.0f, 0.0f }, topK: 10, filter);

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(doc =>
        {
            var workspaceId = doc.Metadata["workspace_id"];
            // Metadata values might be JsonElement when deserialized
            var actualValue = workspaceId is System.Text.Json.JsonElement jsonElement
                ? jsonElement.GetString()
                : workspaceId.ToString();
            actualValue.Should().Be("ws-1");
        });
    }

    [Fact]
    public async Task GetDocumentCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync();

        var initialCount = await store.GetDocumentCountAsync();
        initialCount.Should().Be(0);

        // Act - Add documents
        await store.AddDocumentAsync(new VectorDocument
        {
            Id = "doc-1",
            Text = "Test",
            Embedding = new float[] { 0.1f, 0.2f, 0.3f },
            Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
        });

        await store.AddDocumentAsync(new VectorDocument
        {
            Id = "doc-2",
            Text = "Test 2",
            Embedding = new float[] { 0.4f, 0.5f, 0.6f },
            Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
        });

        // Assert
        var finalCount = await store.GetDocumentCountAsync();
        finalCount.Should().Be(2);
    }

    [Fact]
    public async Task ClearAsync_RemovesAllDocuments()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync();

        var documents = new List<VectorDocument>
        {
            new()
            {
                Id = "doc-1",
                Text = "Test 1",
                Embedding = new float[] { 0.1f, 0.2f, 0.3f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            },
            new()
            {
                Id = "doc-2",
                Text = "Test 2",
                Embedding = new float[] { 0.4f, 0.5f, 0.6f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            }
        };

        await store.AddDocumentsAsync(documents);
        var beforeCount = await store.GetDocumentCountAsync();
        beforeCount.Should().Be(2);

        // Act
        await store.ClearAsync();

        // Assert
        var afterCount = await store.GetDocumentCountAsync();
        afterCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync();

        // Act
        var results = await store.SearchAsync(new float[] { 1.0f, 0.0f, 0.0f }, topK: 5);

        // Assert
        results.Should().BeEmpty();
    }
}
