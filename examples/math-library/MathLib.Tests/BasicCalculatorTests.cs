using Xunit;
using System;

namespace MathLib.Tests;

/// <summary>
/// Unit tests for the BasicCalculator class.
/// </summary>
public class BasicCalculatorTests
{
    private readonly BasicCalculator _calculator;

    public BasicCalculatorTests()
    {
        _calculator = new BasicCalculator();
    }

    #region Add Tests

    [Fact]
    public void Add_PositiveNumbers_ReturnsSum()
    {
        // Arrange
        double a = 5.5;
        double b = 3.2;

        // Act
        double result = _calculator.Add(a, b);

        // Assert
        Assert.Equal(8.7, result, precision: 5);
    }

    [Fact]
    public void Add_NegativeNumbers_ReturnsSum()
    {
        // Arrange
        double a = -5.5;
        double b = -3.2;

        // Act
        double result = _calculator.Add(a, b);

        // Assert
        Assert.Equal(-8.7, result, precision: 5);
    }

    [Fact]
    public void Add_ZeroAndNumber_ReturnsNumber()
    {
        // Arrange
        double a = 0;
        double b = 7.5;

        // Act
        double result = _calculator.Add(a, b);

        // Assert
        Assert.Equal(7.5, result, precision: 5);
    }

    [Fact]
    public void Add_PositiveAndNegative_ReturnsCorrectSum()
    {
        // Arrange
        double a = 10.0;
        double b = -3.5;

        // Act
        double result = _calculator.Add(a, b);

        // Assert
        Assert.Equal(6.5, result, precision: 5);
    }

    [Fact]
    public void Add_LargeNumbers_ReturnsSum()
    {
        // Arrange
        double a = 1e15;
        double b = 2e15;

        // Act
        double result = _calculator.Add(a, b);

        // Assert
        Assert.Equal(3e15, result, precision: 5);
    }

    #endregion

    #region Subtract Tests

    [Fact]
    public void Subtract_PositiveNumbers_ReturnsDifference()
    {
        // Arrange
        double a = 10.5;
        double b = 3.2;

        // Act
        double result = _calculator.Subtract(a, b);

        // Assert
        Assert.Equal(7.3, result, precision: 5);
    }

    [Fact]
    public void Subtract_NegativeNumbers_ReturnsDifference()
    {
        // Arrange
        double a = -5.5;
        double b = -3.2;

        // Act
        double result = _calculator.Subtract(a, b);

        // Assert
        Assert.Equal(-2.3, result, precision: 5);
    }

    [Fact]
    public void Subtract_ZeroFromNumber_ReturnsNumber()
    {
        // Arrange
        double a = 7.5;
        double b = 0;

        // Act
        double result = _calculator.Subtract(a, b);

        // Assert
        Assert.Equal(7.5, result, precision: 5);
    }

    [Fact]
    public void Subtract_NumberFromZero_ReturnsNegative()
    {
        // Arrange
        double a = 0;
        double b = 5.5;

        // Act
        double result = _calculator.Subtract(a, b);

        // Assert
        Assert.Equal(-5.5, result, precision: 5);
    }

    [Fact]
    public void Subtract_SameNumbers_ReturnsZero()
    {
        // Arrange
        double a = 7.5;
        double b = 7.5;

        // Act
        double result = _calculator.Subtract(a, b);

        // Assert
        Assert.Equal(0, result, precision: 5);
    }

    #endregion

    #region Multiply Tests

    [Fact]
    public void Multiply_PositiveNumbers_ReturnsProduct()
    {
        // Arrange
        double a = 4.5;
        double b = 2.0;

        // Act
        double result = _calculator.Multiply(a, b);

        // Assert
        Assert.Equal(9.0, result, precision: 5);
    }

    [Fact]
    public void Multiply_NegativeNumbers_ReturnsPositiveProduct()
    {
        // Arrange
        double a = -3.0;
        double b = -4.0;

        // Act
        double result = _calculator.Multiply(a, b);

        // Assert
        Assert.Equal(12.0, result, precision: 5);
    }

    [Fact]
    public void Multiply_ByZero_ReturnsZero()
    {
        // Arrange
        double a = 7.5;
        double b = 0;

        // Act
        double result = _calculator.Multiply(a, b);

        // Assert
        Assert.Equal(0, result, precision: 5);
    }

    [Fact]
    public void Multiply_PositiveAndNegative_ReturnsNegativeProduct()
    {
        // Arrange
        double a = 5.0;
        double b = -3.0;

        // Act
        double result = _calculator.Multiply(a, b);

        // Assert
        Assert.Equal(-15.0, result, precision: 5);
    }

    [Fact]
    public void Multiply_Fractions_ReturnsProduct()
    {
        // Arrange
        double a = 0.5;
        double b = 0.25;

        // Act
        double result = _calculator.Multiply(a, b);

        // Assert
        Assert.Equal(0.125, result, precision: 5);
    }

    #endregion

    #region Divide Tests

    [Fact]
    public void Divide_PositiveNumbers_ReturnsQuotient()
    {
        // Arrange
        double a = 10.0;
        double b = 2.0;

        // Act
        double result = _calculator.Divide(a, b);

        // Assert
        Assert.Equal(5.0, result, precision: 5);
    }

    [Fact]
    public void Divide_NegativeNumbers_ReturnsPositiveQuotient()
    {
        // Arrange
        double a = -10.0;
        double b = -2.0;

        // Act
        double result = _calculator.Divide(a, b);

        // Assert
        Assert.Equal(5.0, result, precision: 5);
    }

    [Fact]
    public void Divide_ByZero_ThrowsDivideByZeroException()
    {
        // Arrange
        double a = 10.0;
        double b = 0;

        // Act & Assert
        Assert.Throws<DivideByZeroException>(() => _calculator.Divide(a, b));
    }

    [Fact]
    public void Divide_ZeroByNumber_ReturnsZero()
    {
        // Arrange
        double a = 0;
        double b = 5.0;

        // Act
        double result = _calculator.Divide(a, b);

        // Assert
        Assert.Equal(0, result, precision: 5);
    }

    [Fact]
    public void Divide_PositiveAndNegative_ReturnsNegativeQuotient()
    {
        // Arrange
        double a = 15.0;
        double b = -3.0;

        // Act
        double result = _calculator.Divide(a, b);

        // Assert
        Assert.Equal(-5.0, result, precision: 5);
    }

    #endregion

    #region Modulo Tests

    [Fact]
    public void Modulo_PositiveNumbers_ReturnsRemainder()
    {
        // Arrange
        double a = 10.0;
        double b = 3.0;

        // Act
        double result = _calculator.Modulo(a, b);

        // Assert
        Assert.Equal(1.0, result, precision: 5);
    }

    [Fact]
    public void Modulo_NegativeDividend_ReturnsNegativeRemainder()
    {
        // Arrange
        double a = -10.0;
        double b = 3.0;

        // Act
        double result = _calculator.Modulo(a, b);

        // Assert
        Assert.Equal(-1.0, result, precision: 5);
    }

    [Fact]
    public void Modulo_ByZero_ThrowsDivideByZeroException()
    {
        // Arrange
        double a = 10.0;
        double b = 0;

        // Act & Assert
        Assert.Throws<DivideByZeroException>(() => _calculator.Modulo(a, b));
    }

    [Fact]
    public void Modulo_DividendSmallerThanDivisor_ReturnsDividend()
    {
        // Arrange
        double a = 2.0;
        double b = 5.0;

        // Act
        double result = _calculator.Modulo(a, b);

        // Assert
        Assert.Equal(2.0, result, precision: 5);
    }

    [Fact]
    public void Modulo_EvenlyDivisible_ReturnsZero()
    {
        // Arrange
        double a = 15.0;
        double b = 5.0;

        // Act
        double result = _calculator.Modulo(a, b);

        // Assert
        Assert.Equal(0, result, precision: 5);
    }

    #endregion

    #region AbsoluteValue Tests

    [Fact]
    public void AbsoluteValue_PositiveNumber_ReturnsNumber()
    {
        // Arrange
        double value = 7.5;

        // Act
        double result = _calculator.AbsoluteValue(value);

        // Assert
        Assert.Equal(7.5, result, precision: 5);
    }

    [Fact]
    public void AbsoluteValue_NegativeNumber_ReturnsPositive()
    {
        // Arrange
        double value = -7.5;

        // Act
        double result = _calculator.AbsoluteValue(value);

        // Assert
        Assert.Equal(7.5, result, precision: 5);
    }

    [Fact]
    public void AbsoluteValue_Zero_ReturnsZero()
    {
        // Arrange
        double value = 0;

        // Act
        double result = _calculator.AbsoluteValue(value);

        // Assert
        Assert.Equal(0, result, precision: 5);
    }

    #endregion
}