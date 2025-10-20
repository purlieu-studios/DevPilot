namespace MathLib.Tests;

using Xunit;

/// <summary>
/// Tests for the StatisticsCalculator class.
/// </summary>
public class StatisticsCalculatorTests
{
    private readonly StatisticsCalculator _calculator = new();

    #region Mean Tests

    [Fact]
    public void Mean_ValidArray_ReturnsCorrectAverage()
    {
        // Arrange
        double[] values = { 1.0, 2.0, 3.0, 4.0, 5.0 };

        // Act
        double result = _calculator.Mean(values);

        // Assert
        Assert.Equal(3.0, result, precision: 5);
    }

    [Fact]
    public void Mean_SingleElement_ReturnsElement()
    {
        // Arrange
        double[] values = { 42.0 };

        // Act
        double result = _calculator.Mean(values);

        // Assert
        Assert.Equal(42.0, result, precision: 5);
    }

    [Fact]
    public void Mean_NegativeValues_ReturnsCorrectAverage()
    {
        // Arrange
        double[] values = { -5.0, -10.0, -15.0 };

        // Act
        double result = _calculator.Mean(values);

        // Assert
        Assert.Equal(-10.0, result, precision: 5);
    }

    [Fact]
    public void Mean_NullArray_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _calculator.Mean(null!));
    }

    [Fact]
    public void Mean_EmptyArray_ThrowsArgumentException()
    {
        // Arrange
        double[] values = Array.Empty<double>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _calculator.Mean(values));
    }

    #endregion

    #region Median Tests

    [Fact]
    public void Median_OddLengthArray_ReturnsMiddleValue()
    {
        // Arrange
        double[] values = { 3.0, 1.0, 5.0, 2.0, 4.0 };

        // Act
        double result = _calculator.Median(values);

        // Assert
        Assert.Equal(3.0, result, precision: 5);
    }

    [Fact]
    public void Median_EvenLengthArray_ReturnsAverageOfMiddleTwo()
    {
        // Arrange
        double[] values = { 1.0, 2.0, 3.0, 4.0 };

        // Act
        double result = _calculator.Median(values);

        // Assert
        Assert.Equal(2.5, result, precision: 5);
    }

    [Fact]
    public void Median_SingleElement_ReturnsElement()
    {
        // Arrange
        double[] values = { 7.5 };

        // Act
        double result = _calculator.Median(values);

        // Assert
        Assert.Equal(7.5, result, precision: 5);
    }

    [Fact]
    public void Median_UnsortedArray_ReturnsSortedMedian()
    {
        // Arrange
        double[] values = { 9.0, 1.0, 5.0, 3.0, 7.0 };

        // Act
        double result = _calculator.Median(values);

        // Assert
        Assert.Equal(5.0, result, precision: 5);
    }

    [Fact]
    public void Median_NullArray_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _calculator.Median(null!));
    }

    [Fact]
    public void Median_EmptyArray_ThrowsArgumentException()
    {
        // Arrange
        double[] values = Array.Empty<double>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _calculator.Median(values));
    }

    #endregion

    #region StandardDeviation Tests

    [Fact]
    public void StandardDeviation_ValidArray_ReturnsCorrectValue()
    {
        // Arrange
        double[] values = { 2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0 };

        // Act
        double result = _calculator.StandardDeviation(values);

        // Assert
        Assert.Equal(2.0, result, precision: 5);
    }

    [Fact]
    public void StandardDeviation_SingleElement_ReturnsZero()
    {
        // Arrange
        double[] values = { 10.0 };

        // Act
        double result = _calculator.StandardDeviation(values);

        // Assert
        Assert.Equal(0.0, result, precision: 5);
    }

    [Fact]
    public void StandardDeviation_IdenticalValues_ReturnsZero()
    {
        // Arrange
        double[] values = { 5.0, 5.0, 5.0, 5.0 };

        // Act
        double result = _calculator.StandardDeviation(values);

        // Assert
        Assert.Equal(0.0, result, precision: 5);
    }

    [Fact]
    public void StandardDeviation_NullArray_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _calculator.StandardDeviation(null!));
    }

    [Fact]
    public void StandardDeviation_EmptyArray_ThrowsArgumentException()
    {
        // Arrange
        double[] values = Array.Empty<double>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _calculator.StandardDeviation(values));
    }

    #endregion

    #region Min Tests

    [Fact]
    public void Min_ValidArray_ReturnsSmallestValue()
    {
        // Arrange
        double[] values = { 5.0, 2.0, 8.0, 1.0, 9.0 };

        // Act
        double result = _calculator.Min(values);

        // Assert
        Assert.Equal(1.0, result, precision: 5);
    }

    [Fact]
    public void Min_NegativeValues_ReturnsSmallestNegative()
    {
        // Arrange
        double[] values = { -3.0, -10.0, -1.0, -5.0 };

        // Act
        double result = _calculator.Min(values);

        // Assert
        Assert.Equal(-10.0, result, precision: 5);
    }

    #endregion

    #region Max Tests

    [Fact]
    public void Max_ValidArray_ReturnsLargestValue()
    {
        // Arrange
        double[] values = { 5.0, 2.0, 8.0, 1.0, 9.0 };

        // Act
        double result = _calculator.Max(values);

        // Assert
        Assert.Equal(9.0, result, precision: 5);
    }

    [Fact]
    public void Max_NegativeValues_ReturnsLargestNegative()
    {
        // Arrange
        double[] values = { -10.0, -3.0, -5.0, -1.0 };

        // Act
        double result = _calculator.Max(values);

        // Assert
        Assert.Equal(-1.0, result, precision: 5);
    }

    #endregion
}