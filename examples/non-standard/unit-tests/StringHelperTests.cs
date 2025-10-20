using MyApplication;
using Xunit;

namespace MyApplication.UnitTests;

public class StringHelperTests
{
    [Fact]
    public void Reverse_ValidString_ReturnsReversedString()
    {
        // Arrange
        string input = "hello";

        // Act
        string result = StringHelper.Reverse(input);

        // Assert
        Assert.Equal("olleh", result);
    }

    [Fact]
    public void Reverse_EmptyString_ReturnsEmptyString()
    {
        // Act
        string result = StringHelper.Reverse("");

        // Assert
        Assert.Equal("", result);
    }
}
