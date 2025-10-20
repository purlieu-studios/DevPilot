using DevPilot.RAG;
using FluentAssertions;

namespace DevPilot.RAG.Tests;

/// <summary>
/// Tests for RAGOptions - RAG system configuration.
/// </summary>
public sealed class RAGOptionsTests
{
    #region Default Values Tests

    [Fact]
    public void Default_HasCorrectOllamaEndpoint()
    {
        // Act
        var options = RAGOptions.Default;

        // Assert
        options.OllamaEndpoint.Should().Be("http://localhost:11434");
    }

    [Fact]
    public void Default_HasCorrectEmbeddingModel()
    {
        // Act
        var options = RAGOptions.Default;

        // Assert
        options.EmbeddingModel.Should().Be("mxbai-embed-large");
        options.EmbeddingDimension.Should().Be(1024);
    }

    #endregion

    #region Custom Configuration Tests

    [Fact]
    public void CustomOptions_CanSetChunkSizeAndOverlap()
    {
        // Arrange & Act
        var options = new RAGOptions
        {
            ChunkSize = 1024,
            OverlapSize = 100
        };

        // Assert
        options.ChunkSize.Should().Be(1024);
        options.OverlapSize.Should().Be(100);
    }

    [Fact]
    public void CustomOptions_CanSetIncludeExcludePatterns()
    {
        // Arrange & Act
        var options = new RAGOptions
        {
            IncludeExtensions = new[] { ".cs", ".ts", ".py" },
            ExcludePatterns = new[] { "**/bin/**", "**/node_modules/**" }
        };

        // Assert
        options.IncludeExtensions.Should().Equal(".cs", ".ts", ".py");
        options.ExcludePatterns.Should().Equal("**/bin/**", "**/node_modules/**");
    }

    [Fact]
    public void CustomOptions_CanSetEmbeddingModelAndEndpoint()
    {
        // Arrange & Act
        var options = new RAGOptions
        {
            EmbeddingModel = "nomic-embed-text",
            EmbeddingDimension = 768,
            OllamaEndpoint = "http://custom-server:11434"
        };

        // Assert
        options.EmbeddingModel.Should().Be("nomic-embed-text");
        options.EmbeddingDimension.Should().Be(768);
        options.OllamaEndpoint.Should().Be("http://custom-server:11434");
    }

    #endregion
}
