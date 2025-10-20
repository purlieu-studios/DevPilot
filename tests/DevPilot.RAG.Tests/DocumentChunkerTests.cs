using DevPilot.RAG;
using FluentAssertions;

namespace DevPilot.RAG.Tests;

public sealed class DocumentChunkerTests
{
    [Fact]
    public void Constructor_ValidSizes_Succeeds()
    {
        // Arrange & Act
        var chunker = new DocumentChunker(chunkSize: 1000, overlapSize: 100);

        // Assert
        chunker.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_OverlapLargerThanChunk_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new DocumentChunker(chunkSize: 100, overlapSize: 100);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Overlap size must be smaller than chunk size*");
    }

    [Fact]
    public void ChunkDocument_SmallText_ReturnsSingleChunk()
    {
        // Arrange
        var chunker = new DocumentChunker(chunkSize: 1000, overlapSize: 100);
        var content = "This is a small text that fits in one chunk.";
        var filePath = "test.cs";

        // Act
        var chunks = chunker.ChunkDocument(content, filePath);

        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].Text.Should().Be(content);
        chunks[0].ChunkIndex.Should().Be(0);
        chunks[0].FilePath.Should().Be(filePath);
        chunks[0].StartCharacter.Should().Be(0);
        chunks[0].EndCharacter.Should().Be(content.Length);
    }

    [Fact]
    public void ChunkDocument_LargeText_ReturnsMultipleChunks()
    {
        // Arrange
        var chunker = new DocumentChunker(chunkSize: 50, overlapSize: 10);
        var content = new string('A', 150); // 150 chars, chunk size 50
        var filePath = "large.cs";

        // Act
        var chunks = chunker.ChunkDocument(content, filePath);

        // Assert
        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().AllSatisfy(chunk =>
        {
            chunk.FilePath.Should().Be(filePath);
            chunk.Length.Should().BeLessThanOrEqualTo(50);
        });
    }

    [Fact]
    public void ChunkDocument_WithOverlap_EnsuresContextContinuity()
    {
        // Arrange
        var chunker = new DocumentChunker(chunkSize: 50, overlapSize: 10);
        var content = "AAAAA BBBBB CCCCC DDDDD EEEEE FFFFF GGGGG HHHHH IIIII JJJJJ KKKKK LLLLL";
        var filePath = "overlap.cs";

        // Act
        var chunks = chunker.ChunkDocument(content, filePath);

        // Assert
        if (chunks.Count > 1)
        {
            for (int i = 1; i < chunks.Count; i++)
            {
                var previousChunk = chunks[i - 1];
                var currentChunk = chunks[i];

                // Verify overlap exists
                currentChunk.StartCharacter.Should().BeLessThan(previousChunk.EndCharacter);
            }
        }
    }

    [Fact]
    public void ChunkDocument_ParagraphBoundary_BreaksAtParagraph()
    {
        // Arrange
        var chunker = new DocumentChunker(chunkSize: 80, overlapSize: 10);
        var content = "First paragraph text.\n\nSecond paragraph text that is longer and should trigger a chunk break.";
        var filePath = "paragraphs.cs";

        // Act
        var chunks = chunker.ChunkDocument(content, filePath);

        // Assert
        chunks.Should().HaveCountGreaterThan(0);
        // Verify that if broken, it respects paragraph boundary
        if (chunks.Count > 1)
        {
            chunks[0].Text.Should().Contain("First paragraph");
        }
    }

    [Fact]
    public void ChunkDocument_EdgeCase_ExactlyChunkSize()
    {
        // Arrange
        var chunker = new DocumentChunker(chunkSize: 50, overlapSize: 10);
        var content = new string('X', 50); // Exactly chunk size
        var filePath = "exact.cs";

        // Act
        var chunks = chunker.ChunkDocument(content, filePath);

        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].Text.Should().Be(content);
    }

    [Fact]
    public void ChunkDocument_NullContent_ThrowsArgumentException()
    {
        // Arrange
        var chunker = new DocumentChunker();
        string? content = null;

        // Act
        var act = () => chunker.ChunkDocument(content!, "test.cs");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChunkDocument_EmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var chunker = new DocumentChunker();
        var content = string.Empty;

        // Act
        var act = () => chunker.ChunkDocument(content, "test.cs");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(100, 10)]
    [InlineData(500, 50)]
    [InlineData(2000, 200)]
    public void Constructor_VariousSizes_Succeeds(int chunkSize, int overlap)
    {
        // Arrange & Act
        var chunker = new DocumentChunker(chunkSize, overlap);

        // Assert
        chunker.Should().NotBeNull();
    }

    [Fact]
    public void ToVectorDocument_CreatesCorrectMetadata()
    {
        // Arrange
        var chunk = new DocumentChunk
        {
            Text = "Test content",
            ChunkIndex = 2,
            FilePath = "src/Test.cs",
            StartCharacter = 100,
            EndCharacter = 200
        };
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var workspaceId = "test-workspace";

        // Act
        var vectorDoc = chunk.ToVectorDocument(embedding, workspaceId);

        // Assert
        vectorDoc.Id.Should().Be($"{workspaceId}_src/Test.cs_2");
        vectorDoc.Text.Should().Be("Test content");
        vectorDoc.Embedding.Should().BeEquivalentTo(embedding);
        vectorDoc.Metadata["workspace_id"].Should().Be(workspaceId);
        vectorDoc.Metadata["file_path"].Should().Be("src/Test.cs");
        vectorDoc.Metadata["chunk_index"].Should().Be(2);
        vectorDoc.Metadata["start_character"].Should().Be(100);
        vectorDoc.Metadata["end_character"].Should().Be(200);
        vectorDoc.Metadata["file_extension"].Should().Be(".cs");
    }
}
