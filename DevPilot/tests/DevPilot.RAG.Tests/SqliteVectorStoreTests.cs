using DevPilot.RAG;
using FluentAssertions;
using Xunit;

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
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        // Assert
        File.Exists(_testDatabasePath).Should().BeTrue();
        var count = await store.GetDocumentCountAsync(cancellationToken: TestContext.Current.CancellationToken);
        count.Should().Be(0);
    }

    [Fact]
    public async Task AddDocumentAsync_StoresDocumentAndEmbedding()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

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
        await store.AddDocumentAsync(document, TestContext.Current.CancellationToken);

        // Assert
        var count = await store.GetDocumentCountAsync(cancellationToken: TestContext.Current.CancellationToken);
        count.Should().Be(1);
    }

    [Fact]
    public async Task AddDocumentsAsync_StoresBatch()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

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
        await store.AddDocumentsAsync(documents, TestContext.Current.CancellationToken);

        // Assert
        var count = await store.GetDocumentCountAsync(cancellationToken: TestContext.Current.CancellationToken);
        count.Should().Be(3);
    }

    [Fact]
    public async Task SearchAsync_FindsSimilarDocuments()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

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

        await store.AddDocumentsAsync(documents, TestContext.Current.CancellationToken);

        // Act - Query with embedding similar to doc-1 and doc-3
        var queryEmbedding = new float[] { 0.95f, 0.05f, 0.0f };
        var results = await store.SearchAsync(queryEmbedding, topK: 2, null, TestContext.Current.CancellationToken);

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
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var documents = Enumerable.Range(1, 10)
            .Select(i => new VectorDocument
            {
                Id = $"doc-{i}",
                Text = $"Document {i}",
                Embedding = new float[] { i / 10.0f, 0.5f, 0.5f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            })
            .ToList();

        await store.AddDocumentsAsync(documents, TestContext.Current.CancellationToken);

        // Act
        var results = await store.SearchAsync(new float[] { 0.5f, 0.5f, 0.5f }, topK: 3, null, TestContext.Current.CancellationToken);

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchAsync_WithFilter_ReturnsMatchingDocuments()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

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

        await store.AddDocumentsAsync(documents, TestContext.Current.CancellationToken);

        // Act - Search with workspace filter
        var filter = new Dictionary<string, object> { ["workspace_id"] = "ws-1" };
        var results = await store.SearchAsync(new float[] { 1.0f, 0.0f, 0.0f }, topK: 10, filter, TestContext.Current.CancellationToken);

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
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var initialCount = await store.GetDocumentCountAsync(cancellationToken: TestContext.Current.CancellationToken);
        initialCount.Should().Be(0);

        // Act - Add documents
        await store.AddDocumentAsync(new VectorDocument
        {
            Id = "doc-1",
            Text = "Test",
            Embedding = new float[] { 0.1f, 0.2f, 0.3f },
            Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
        }, TestContext.Current.CancellationToken);

        await store.AddDocumentAsync(new VectorDocument
        {
            Id = "doc-2",
            Text = "Test 2",
            Embedding = new float[] { 0.4f, 0.5f, 0.6f },
            Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
        }, TestContext.Current.CancellationToken);

        // Assert
        var finalCount = await store.GetDocumentCountAsync(cancellationToken: TestContext.Current.CancellationToken);
        finalCount.Should().Be(2);
    }

    [Fact]
    public async Task ClearAsync_RemovesAllDocuments()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

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

        await store.AddDocumentsAsync(documents, TestContext.Current.CancellationToken);
        var beforeCount = await store.GetDocumentCountAsync(cancellationToken: TestContext.Current.CancellationToken);
        beforeCount.Should().Be(2);

        // Act
        await store.ClearAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var afterCount = await store.GetDocumentCountAsync(cancellationToken: TestContext.Current.CancellationToken);
        afterCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        // Act
        var results = await store.SearchAsync(new float[] { 1.0f, 0.0f, 0.0f }, topK: 5, null, TestContext.Current.CancellationToken);

        // Assert
        results.Should().BeEmpty();
    }

    #region Multi-Workspace Isolation Tests

    [Fact]
    public async Task ClearAsync_WithWorkspaceId_OnlyClearsSpecifiedWorkspace()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        // Add documents to workspace 1
        var workspace1Docs = new List<VectorDocument>
        {
            new()
            {
                Id = "ws1-doc1",
                Text = "Workspace 1 Document 1",
                Embedding = new float[] { 0.1f, 0.2f, 0.3f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            },
            new()
            {
                Id = "ws1-doc2",
                Text = "Workspace 1 Document 2",
                Embedding = new float[] { 0.2f, 0.3f, 0.4f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            },
            new()
            {
                Id = "ws1-doc3",
                Text = "Workspace 1 Document 3",
                Embedding = new float[] { 0.3f, 0.4f, 0.5f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            }
        };

        // Add documents to workspace 2
        var workspace2Docs = new List<VectorDocument>
        {
            new()
            {
                Id = "ws2-doc1",
                Text = "Workspace 2 Document 1",
                Embedding = new float[] { 0.4f, 0.5f, 0.6f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-2" }
            },
            new()
            {
                Id = "ws2-doc2",
                Text = "Workspace 2 Document 2",
                Embedding = new float[] { 0.5f, 0.6f, 0.7f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-2" }
            },
            new()
            {
                Id = "ws2-doc3",
                Text = "Workspace 2 Document 3",
                Embedding = new float[] { 0.6f, 0.7f, 0.8f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-2" }
            }
        };

        await store.AddDocumentsAsync(workspace1Docs, TestContext.Current.CancellationToken);
        await store.AddDocumentsAsync(workspace2Docs, TestContext.Current.CancellationToken);

        // Verify initial state
        var initialWs1Count = await store.GetDocumentCountAsync("ws-1", TestContext.Current.CancellationToken);
        var initialWs2Count = await store.GetDocumentCountAsync("ws-2", TestContext.Current.CancellationToken);
        initialWs1Count.Should().Be(3);
        initialWs2Count.Should().Be(3);

        // Act - Clear only workspace 1
        await store.ClearAsync("ws-1", TestContext.Current.CancellationToken);

        // Assert
        var ws1CountAfterClear = await store.GetDocumentCountAsync("ws-1", TestContext.Current.CancellationToken);
        var ws2CountAfterClear = await store.GetDocumentCountAsync("ws-2", TestContext.Current.CancellationToken);

        ws1CountAfterClear.Should().Be(0, "workspace 1 should be cleared");
        ws2CountAfterClear.Should().Be(3, "workspace 2 should remain untouched");
    }

    [Fact]
    public async Task ClearAsync_WithoutWorkspaceId_ClearsAllWorkspaces()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        // Add documents to multiple workspaces
        var documents = new List<VectorDocument>
        {
            new()
            {
                Id = "ws1-doc",
                Text = "Workspace 1",
                Embedding = new float[] { 0.1f, 0.2f, 0.3f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            },
            new()
            {
                Id = "ws2-doc",
                Text = "Workspace 2",
                Embedding = new float[] { 0.4f, 0.5f, 0.6f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-2" }
            },
            new()
            {
                Id = "ws3-doc",
                Text = "Workspace 3",
                Embedding = new float[] { 0.7f, 0.8f, 0.9f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-3" }
            }
        };

        await store.AddDocumentsAsync(documents, TestContext.Current.CancellationToken);

        // Verify documents exist
        var totalCount = await store.GetDocumentCountAsync(cancellationToken: TestContext.Current.CancellationToken);
        totalCount.Should().Be(3);

        // Act - Clear all workspaces (null workspace ID)
        await store.ClearAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert - All workspaces should be cleared
        var ws1Count = await store.GetDocumentCountAsync("ws-1", TestContext.Current.CancellationToken);
        var ws2Count = await store.GetDocumentCountAsync("ws-2", TestContext.Current.CancellationToken);
        var ws3Count = await store.GetDocumentCountAsync("ws-3", TestContext.Current.CancellationToken);
        var totalCountAfter = await store.GetDocumentCountAsync(cancellationToken: TestContext.Current.CancellationToken);

        ws1Count.Should().Be(0);
        ws2Count.Should().Be(0);
        ws3Count.Should().Be(0);
        totalCountAfter.Should().Be(0);
    }

    [Fact]
    public async Task GetDocumentCountAsync_WithWorkspaceId_ReturnsCorrectCount()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        // Add 5 documents to workspace 1
        var workspace1Docs = Enumerable.Range(1, 5)
            .Select(i => new VectorDocument
            {
                Id = $"ws1-doc{i}",
                Text = $"Workspace 1 Document {i}",
                Embedding = new float[] { i / 10.0f, 0.5f, 0.5f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1" }
            })
            .ToList();

        // Add 7 documents to workspace 2
        var workspace2Docs = Enumerable.Range(1, 7)
            .Select(i => new VectorDocument
            {
                Id = $"ws2-doc{i}",
                Text = $"Workspace 2 Document {i}",
                Embedding = new float[] { 0.5f, i / 10.0f, 0.5f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-2" }
            })
            .ToList();

        await store.AddDocumentsAsync(workspace1Docs, TestContext.Current.CancellationToken);
        await store.AddDocumentsAsync(workspace2Docs, TestContext.Current.CancellationToken);

        // Act & Assert
        var ws1Count = await store.GetDocumentCountAsync("ws-1", TestContext.Current.CancellationToken);
        var ws2Count = await store.GetDocumentCountAsync("ws-2", TestContext.Current.CancellationToken);
        var totalCount = await store.GetDocumentCountAsync(cancellationToken: TestContext.Current.CancellationToken);

        ws1Count.Should().Be(5, "workspace 1 should have 5 documents");
        ws2Count.Should().Be(7, "workspace 2 should have 7 documents");
        totalCount.Should().Be(12, "total count should be 12 (5 + 7)");
    }

    [Fact]
    public async Task SearchAsync_WithWorkspaceFilter_OnlyReturnsWorkspaceDocuments()
    {
        // Arrange
        using var store = new SqliteVectorStore(_testDatabasePath);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        // Add documents to workspace 1
        var workspace1Docs = new List<VectorDocument>
        {
            new()
            {
                Id = "ws1-doc1",
                Text = "Calculator class implementation",
                Embedding = new float[] { 1.0f, 0.0f, 0.0f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1", ["file_path"] = "Calculator.cs" }
            },
            new()
            {
                Id = "ws1-doc2",
                Text = "Calculator test cases",
                Embedding = new float[] { 0.9f, 0.1f, 0.0f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-1", ["file_path"] = "CalculatorTests.cs" }
            }
        };

        // Add documents to workspace 2 with similar embeddings
        var workspace2Docs = new List<VectorDocument>
        {
            new()
            {
                Id = "ws2-doc1",
                Text = "Calculator class implementation",
                Embedding = new float[] { 0.95f, 0.05f, 0.0f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-2", ["file_path"] = "Calculator.cs" }
            },
            new()
            {
                Id = "ws2-doc2",
                Text = "Calculator test cases",
                Embedding = new float[] { 0.85f, 0.15f, 0.0f },
                Metadata = new Dictionary<string, object> { ["workspace_id"] = "ws-2", ["file_path"] = "CalculatorTests.cs" }
            }
        };

        await store.AddDocumentsAsync(workspace1Docs, TestContext.Current.CancellationToken);
        await store.AddDocumentsAsync(workspace2Docs, TestContext.Current.CancellationToken);

        // Act - Search with workspace filter
        var filter = new Dictionary<string, object> { ["workspace_id"] = "ws-1" };
        var results = await store.SearchAsync(
            new float[] { 1.0f, 0.0f, 0.0f },
            topK: 10,
            filter,
            TestContext.Current.CancellationToken);

        // Assert
        results.Should().HaveCount(2, "only workspace 1 documents should be returned");
        results.Should().AllSatisfy(doc =>
        {
            var workspaceId = doc.Metadata["workspace_id"];
            var actualValue = workspaceId is System.Text.Json.JsonElement jsonElement
                ? jsonElement.GetString()
                : workspaceId.ToString();
            actualValue.Should().Be("ws-1");
        });

        // Verify documents are from workspace 1
        results.Should().Contain(doc => doc.Id == "ws1-doc1");
        results.Should().Contain(doc => doc.Id == "ws1-doc2");
        results.Should().NotContain(doc => doc.Id.StartsWith("ws2-"));
    }

    #endregion
}
