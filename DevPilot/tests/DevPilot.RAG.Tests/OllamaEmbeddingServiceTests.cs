using DevPilot.RAG;
using FluentAssertions;

namespace DevPilot.RAG.Tests;

public sealed class OllamaEmbeddingServiceTests
{
    [Fact]
    public void Constructor_DefaultParameters_Succeeds()
    {
        // Arrange & Act
        using var service = new OllamaEmbeddingService();

        // Assert
        service.Should().NotBeNull();
        service.EmbeddingDimension.Should().Be(1024);
    }

    [Fact]
    public void Constructor_CustomParameters_Succeeds()
    {
        // Arrange & Act
        using var service = new OllamaEmbeddingService(
            endpoint: "http://localhost:11434",
            modelName: "mxbai-embed-large",
            embeddingDimension: 1024);

        // Assert
        service.Should().NotBeNull();
        service.EmbeddingDimension.Should().Be(1024);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_NullText_ThrowsArgumentException()
    {
        // Arrange
        using var service = new OllamaEmbeddingService();
        string? text = null;

        // Act
        var act = async () => await service.GenerateEmbeddingAsync(text!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_EmptyText_ThrowsArgumentException()
    {
        // Arrange
        using var service = new OllamaEmbeddingService();
        var text = string.Empty;

        // Act
        var act = async () => await service.GenerateEmbeddingAsync(text);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_NullTexts_ThrowsArgumentException()
    {
        // Arrange
        using var service = new OllamaEmbeddingService();
        List<string>? texts = null;

        // Act
        var act = async () => await service.GenerateEmbeddingsAsync(texts!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_EmptyList_ReturnsEmptyArray()
    {
        // Arrange
        using var service = new OllamaEmbeddingService();
        var texts = new List<string>();

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        result.Should().BeEmpty();
    }

    // NOTE: Integration tests that require Ollama running are in RagServiceIntegrationTests
    // Those tests verify actual embedding generation when Ollama is available
}
