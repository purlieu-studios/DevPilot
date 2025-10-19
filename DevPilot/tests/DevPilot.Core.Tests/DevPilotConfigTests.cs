using DevPilot.Core;
using FluentAssertions;
using System.Text.Json;

namespace DevPilot.Core.Tests;

/// <summary>
/// Tests for DevPilotConfig - repository-specific configuration.
/// </summary>
public sealed class DevPilotConfigTests
{
    #region JSON Deserialization Tests

    [Fact]
    public void Deserialize_ValidJsonWithAllProperties_ParsesCorrectly()
    {
        // Arrange
        var json = @"{
  ""folders"": [""migrations"", ""config"", ""scripts""],
  ""copyAllFiles"": true
}";

        // Act
        var config = JsonSerializer.Deserialize<DevPilotConfig>(json);

        // Assert
        config.Should().NotBeNull();
        config!.Folders.Should().Equal("migrations", "config", "scripts");
        config.CopyAllFiles.Should().BeTrue();
    }

    [Fact]
    public void Deserialize_MissingFolders_UsesNull()
    {
        // Arrange
        var json = @"{
  ""copyAllFiles"": false
}";

        // Act
        var config = JsonSerializer.Deserialize<DevPilotConfig>(json);

        // Assert
        config.Should().NotBeNull();
        config!.Folders.Should().BeNull();
        config.CopyAllFiles.Should().BeFalse();
    }

    [Fact]
    public void Deserialize_MissingCopyAllFiles_UsesNull()
    {
        // Arrange
        var json = @"{
  ""folders"": [""shared""]
}";

        // Act
        var config = JsonSerializer.Deserialize<DevPilotConfig>(json);

        // Assert
        config.Should().NotBeNull();
        config!.Folders.Should().Equal("shared");
        config.CopyAllFiles.Should().BeNull();
    }

    [Fact]
    public void Deserialize_EmptyJson_CreatesDefaultConfig()
    {
        // Arrange
        var json = "{}";

        // Act
        var config = JsonSerializer.Deserialize<DevPilotConfig>(json);

        // Assert
        config.Should().NotBeNull();
        config!.Folders.Should().BeNull();
        config.CopyAllFiles.Should().BeNull();
    }

    [Fact]
    public void Deserialize_NullValues_ParsesGracefully()
    {
        // Arrange
        var json = @"{
  ""folders"": null,
  ""copyAllFiles"": null
}";

        // Act
        var config = JsonSerializer.Deserialize<DevPilotConfig>(json);

        // Assert
        config.Should().NotBeNull();
        config!.Folders.Should().BeNull();
        config.CopyAllFiles.Should().BeNull();
    }

    #endregion

    #region Configuration Behavior Tests

    [Fact]
    public void Default_HasNullFoldersAndFalseCopyAllFiles()
    {
        // Act
        var config = DevPilotConfig.Default;

        // Assert
        config.Folders.Should().BeNull();
        config.CopyAllFiles.Should().BeFalse();
    }

    [Fact]
    public void Folders_EmptyArray_IsValid()
    {
        // Arrange
        var json = @"{
  ""folders"": []
}";

        // Act
        var config = JsonSerializer.Deserialize<DevPilotConfig>(json);

        // Assert
        config.Should().NotBeNull();
        config!.Folders.Should().BeEmpty();
    }

    [Fact]
    public void Folders_WithSpecialCharacters_ParsesCorrectly()
    {
        // Arrange
        var json = @"{
  ""folders"": [""my-lib"", ""3rd_party"", ""lib/core""]
}";

        // Act
        var config = JsonSerializer.Deserialize<DevPilotConfig>(json);

        // Assert
        config.Should().NotBeNull();
        config!.Folders.Should().Equal("my-lib", "3rd_party", "lib/core");
    }

    [Fact]
    public void Properties_AreInitOnly_CannotBeModifiedAfterConstruction()
    {
        // Arrange & Act
        var config = new DevPilotConfig
        {
            Folders = new[] { "shared" },
            CopyAllFiles = true
        };

        // Assert
        config.Folders.Should().Equal("shared");
        config.CopyAllFiles.Should().BeTrue();
    }

    [Fact]
    public void Serialize_RoundTrip_PreservesValues()
    {
        // Arrange
        var original = new DevPilotConfig
        {
            Folders = new[] { "migrations", "scripts" },
            CopyAllFiles = true
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<DevPilotConfig>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Folders.Should().Equal(original.Folders);
        deserialized.CopyAllFiles.Should().Be(original.CopyAllFiles);
    }

    #endregion
}
