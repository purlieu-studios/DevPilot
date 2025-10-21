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

    [Fact]
    public void Concatenate_TwoValidStrings_ReturnsConcatenatedWithSpace()
    {
        // Arrange
        string first = "Hello";
        string second = "World";

        // Act
        string result = StringHelper.Concatenate(first, second);

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void Concatenate_FirstStringEmpty_ReturnsSecondString()
    {
        // Act
        string result = StringHelper.Concatenate("", "World");

        // Assert
        Assert.Equal("World", result);
    }

    [Fact]
    public void Concatenate_SecondStringEmpty_ReturnsFirstString()
    {
        // Act
        string result = StringHelper.Concatenate("Hello", "");

        // Assert
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void Concatenate_BothStringsEmpty_ReturnsEmptyString()
    {
        // Act
        string result = StringHelper.Concatenate("", "");

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void Concatenate_FirstStringNull_ReturnsSecondString()
    {
        // Act
        string result = StringHelper.Concatenate(null, "World");

        // Assert
        Assert.Equal("World", result);
    }

    [Fact]
    public void Concatenate_SecondStringNull_ReturnsFirstString()
    {
        // Act
        string result = StringHelper.Concatenate("Hello", null);

        // Assert
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void Concatenate_BothStringsNull_ReturnsEmptyString()
    {
        // Act
        string result = StringHelper.Concatenate(null, null);

        // Assert
        Assert.Equal("", result);
    }
}
