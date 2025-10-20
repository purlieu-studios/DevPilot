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
        var result = calc.Add(2.5, 3.5);

        // Assert
        Assert.Equal(6.0, result, precision: 5);
    }

    [Fact]
    public void Add_NegativeNumbers_ReturnsSum()
    {
        // Arrange
        var calc = new Calculator();

        // Act
        var result = calc.Add(-5.0, -3.0);

        // Assert
        Assert.Equal(-8.0, result, precision: 5);
    }

    [Fact]
    public void Subtract_TwoPositiveNumbers_ReturnsDifference()
    {
        // Arrange
        var calc = new Calculator();

        // Act
        var result = calc.Subtract(10.0, 4.0);

        // Assert
        Assert.Equal(6.0, result, precision: 5);
    }

    [Fact]
    public void Subtract_NegativeFromPositive_ReturnsDifference()
    {
        // Arrange
        var calc = new Calculator();

        // Act
        var result = calc.Subtract(5.0, -3.0);

        // Assert
        Assert.Equal(8.0, result, precision: 5);
    }
}
