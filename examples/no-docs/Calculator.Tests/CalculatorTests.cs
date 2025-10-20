using Xunit;

namespace Calculator.Tests;

public class CalculatorTests
{
    [Fact]
    public void Add_TwoPositiveNumbers_ReturnsSum()
    {
        // Arrange
        var calc = new Calculator();

        // Act
        int result = calc.Add(5, 3);

        // Assert
        Assert.Equal(8, result);
    }

    [Fact]
    public void Subtract_TwoPositiveNumbers_ReturnsDifference()
    {
        // Arrange
        var calc = new Calculator();

        // Act
        int result = calc.Subtract(10, 3);

        // Assert
        Assert.Equal(7, result);
    }
}
