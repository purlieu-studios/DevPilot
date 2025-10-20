using System;
using Xunit;

namespace MathLib.Tests;

/// <summary>
/// Unit tests for the ScientificCalculator class.
/// </summary>
public class ScientificCalculatorTests
{
    private readonly ScientificCalculator _calculator;

    public ScientificCalculatorTests()
    {
        _calculator = new ScientificCalculator();
    }

    #region Power Tests

    [Fact]
    public void Power_PositiveBaseAndExponent_ReturnsCorrectResult()
    {
        // Arrange
        double baseValue = 2;
        double exponent = 3;

        // Act
        var result = _calculator.Power(baseValue, exponent);

        // Assert
        Assert.Equal(8.0, result, precision: 5);
    }

    [Fact]
    public void Power_ZeroExponent_ReturnsOne()
    {
        // Arrange
        double baseValue = 5;
        double exponent = 0;

        // Act
        var result = _calculator.Power(baseValue, exponent);

        // Assert
        Assert.Equal(1.0, result, precision: 5);
    }

    [Fact]
    public void Power_NegativeExponent_ReturnsReciprocal()
    {
        // Arrange
        double baseValue = 2;
        double exponent = -2;

        // Act
        var result = _calculator.Power(baseValue, exponent);

        // Assert
        Assert.Equal(0.25, result, precision: 5);
    }

    [Fact]
    public void Power_FractionalExponent_ReturnsRoot()
    {
        // Arrange
        double baseValue = 4;
        double exponent = 0.5;

        // Act
        var result = _calculator.Power(baseValue, exponent);

        // Assert
        Assert.Equal(2.0, result, precision: 5);
    }

    [Fact]
    public void Power_ZeroBase_ReturnsZero()
    {
        // Arrange
        double baseValue = 0;
        double exponent = 5;

        // Act
        var result = _calculator.Power(baseValue, exponent);

        // Assert
        Assert.Equal(0.0, result, precision: 5);
    }

    #endregion

    #region SquareRoot Tests

    [Fact]
    public void SquareRoot_PositiveNumber_ReturnsCorrectResult()
    {
        // Arrange
        double value = 16;

        // Act
        var result = _calculator.SquareRoot(value);

        // Assert
        Assert.Equal(4.0, result, precision: 5);
    }

    [Fact]
    public void SquareRoot_Zero_ReturnsZero()
    {
        // Arrange
        double value = 0;

        // Act
        var result = _calculator.SquareRoot(value);

        // Assert
        Assert.Equal(0.0, result, precision: 5);
    }

    [Fact]
    public void SquareRoot_NegativeNumber_ThrowsArgumentException()
    {
        // Arrange
        double value = -4;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _calculator.SquareRoot(value));
    }

    [Fact]
    public void SquareRoot_LargeNumber_ReturnsCorrectResult()
    {
        // Arrange
        double value = 144;

        // Act
        var result = _calculator.SquareRoot(value);

        // Assert
        Assert.Equal(12.0, result, precision: 5);
    }

    [Fact]
    public void SquareRoot_FractionalNumber_ReturnsCorrectResult()
    {
        // Arrange
        double value = 0.25;

        // Act
        var result = _calculator.SquareRoot(value);

        // Assert
        Assert.Equal(0.5, result, precision: 5);
    }

    #endregion

    #region Log Tests

    [Fact]
    public void Log_EulerNumber_ReturnsOne()
    {
        // Arrange
        double value = Math.E;

        // Act
        var result = _calculator.Log(value);

        // Assert
        Assert.Equal(1.0, result, precision: 4);
    }

    [Fact]
    public void Log_One_ReturnsZero()
    {
        // Arrange
        double value = 1;

        // Act
        var result = _calculator.Log(value);

        // Assert
        Assert.Equal(0.0, result, precision: 4);
    }

    [Fact]
    public void Log_PositiveNumber_ReturnsCorrectResult()
    {
        // Arrange
        double value = 10;

        // Act
        var result = _calculator.Log(value);

        // Assert
        Assert.Equal(2.302585, result, precision: 4);
    }

    [Fact]
    public void Log_Zero_ThrowsArgumentException()
    {
        // Arrange
        double value = 0;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _calculator.Log(value));
    }

    [Fact]
    public void Log_NegativeNumber_ThrowsArgumentException()
    {
        // Arrange
        double value = -5;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _calculator.Log(value));
    }

    #endregion

    #region Sin Tests

    [Fact]
    public void Sin_Zero_ReturnsZero()
    {
        // Arrange
        double angle = 0;

        // Act
        var result = _calculator.Sin(angle);

        // Assert
        Assert.Equal(0.0, result, precision: 4);
    }

    [Fact]
    public void Sin_PiOverTwo_ReturnsOne()
    {
        // Arrange
        double angle = Math.PI / 2;

        // Act
        var result = _calculator.Sin(angle);

        // Assert
        Assert.Equal(1.0, result, precision: 4);
    }

    [Fact]
    public void Sin_Pi_ReturnsZero()
    {
        // Arrange
        double angle = Math.PI;

        // Act
        var result = _calculator.Sin(angle);

        // Assert
        Assert.Equal(0.0, result, precision: 4);
    }

    [Fact]
    public void Sin_NegativeAngle_ReturnsNegativeValue()
    {
        // Arrange
        double angle = -Math.PI / 6;

        // Act
        var result = _calculator.Sin(angle);

        // Assert
        Assert.Equal(-0.5, result, precision: 4);
    }

    [Fact]
    public void Sin_LargeAngle_ReturnsCorrectResult()
    {
        // Arrange
        double angle = 3 * Math.PI / 2;

        // Act
        var result = _calculator.Sin(angle);

        // Assert
        Assert.Equal(-1.0, result, precision: 4);
    }

    #endregion

    #region Cos Tests

    [Fact]
    public void Cos_Zero_ReturnsOne()
    {
        // Arrange
        double angle = 0;

        // Act
        var result = _calculator.Cos(angle);

        // Assert
        Assert.Equal(1.0, result, precision: 4);
    }

    [Fact]
    public void Cos_PiOverTwo_ReturnsZero()
    {
        // Arrange
        double angle = Math.PI / 2;

        // Act
        var result = _calculator.Cos(angle);

        // Assert
        Assert.Equal(0.0, result, precision: 4);
    }

    [Fact]
    public void Cos_Pi_ReturnsNegativeOne()
    {
        // Arrange
        double angle = Math.PI;

        // Act
        var result = _calculator.Cos(angle);

        // Assert
        Assert.Equal(-1.0, result, precision: 4);
    }

    [Fact]
    public void Cos_NegativeAngle_ReturnsSameAsPositive()
    {
        // Arrange
        double angle = -Math.PI / 3;

        // Act
        var result = _calculator.Cos(angle);

        // Assert
        Assert.Equal(0.5, result, precision: 4);
    }

    [Fact]
    public void Cos_TwoPi_ReturnsOne()
    {
        // Arrange
        double angle = 2 * Math.PI;

        // Act
        var result = _calculator.Cos(angle);

        // Assert
        Assert.Equal(1.0, result, precision: 4);
    }

    #endregion

    #region Tan Tests

    [Fact]
    public void Tan_Zero_ReturnsZero()
    {
        // Arrange
        double angle = 0;

        // Act
        var result = _calculator.Tan(angle);

        // Assert
        Assert.Equal(0.0, result, precision: 4);
    }

    [Fact]
    public void Tan_PiOverFour_ReturnsOne()
    {
        // Arrange
        double angle = Math.PI / 4;

        // Act
        var result = _calculator.Tan(angle);

        // Assert
        Assert.Equal(1.0, result, precision: 4);
    }

    [Fact]
    public void Tan_Pi_ReturnsZero()
    {
        // Arrange
        double angle = Math.PI;

        // Act
        var result = _calculator.Tan(angle);

        // Assert
        Assert.Equal(0.0, result, precision: 4);
    }

    [Fact]
    public void Tan_NegativeAngle_ReturnsNegativeValue()
    {
        // Arrange
        double angle = -Math.PI / 4;

        // Act
        var result = _calculator.Tan(angle);

        // Assert
        Assert.Equal(-1.0, result, precision: 4);
    }

    [Fact]
    public void Tan_SmallAngle_ReturnsCorrectResult()
    {
        // Arrange
        double angle = Math.PI / 6;

        // Act
        var result = _calculator.Tan(angle);

        // Assert
        Assert.Equal(0.57735, result, precision: 4);
    }

    #endregion
}