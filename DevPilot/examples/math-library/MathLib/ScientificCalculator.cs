using System;

namespace MathLib;

/// <summary>
/// Provides advanced mathematical functions including power, roots, logarithms, and trigonometry.
/// </summary>
/// <remarks>
/// This calculator uses the System.Math library for all operations.
/// All trigonometric functions expect angles in radians.
/// </remarks>
public class ScientificCalculator
{
    /// <summary>
    /// Calculates the value of a number raised to a specified power.
    /// </summary>
    /// <param name="x">The base number.</param>
    /// <param name="y">The exponent to raise the base to.</param>
    /// <returns>The value of <paramref name="x"/> raised to the power <paramref name="y"/>.</returns>
    /// <remarks>
    /// Special cases:
    /// - If x is NaN or y is NaN, the result is NaN.
    /// - If x is 0 and y is negative, the result is positive infinity.
    /// - If x is negative and y is not an integer, the result is NaN.
    /// </remarks>
    /// <example>
    /// <code>
    /// var calculator = new ScientificCalculator();
    /// var result = calculator.Power(2, 3);
    /// // Returns: 8.0
    /// </code>
    /// </example>
    public double Power(double x, double y) => Math.Pow(x, y);

    /// <summary>
    /// Calculates the square root of a specified number.
    /// </summary>
    /// <param name="x">The number whose square root is to be found.</param>
    /// <returns>The positive square root of <paramref name="x"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="x"/> is negative.</exception>
    /// <remarks>
    /// The square root of a negative number is undefined in real numbers.
    /// For x = 0, the result is 0.
    /// </remarks>
    /// <example>
    /// <code>
    /// var calculator = new ScientificCalculator();
    /// var result = calculator.SquareRoot(16);
    /// // Returns: 4.0
    /// </code>
    /// </example>
    public double SquareRoot(double x)
    {
        if (x < 0)
            throw new ArgumentException("Cannot calculate square root of a negative number.", nameof(x));
        
        return Math.Sqrt(x);
    }

    /// <summary>
    /// Calculates the natural logarithm (base e) of a specified number.
    /// </summary>
    /// <param name="x">The number whose logarithm is to be found.</param>
    /// <returns>The natural logarithm of <paramref name="x"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="x"/> is less than or equal to zero.</exception>
    /// <remarks>
    /// The natural logarithm uses Euler's number (e ≈ 2.71828) as its base.
    /// Logarithm is undefined for non-positive numbers.
    /// </remarks>
    /// <example>
    /// <code>
    /// var calculator = new ScientificCalculator();
    /// var result = calculator.Log(Math.E);
    /// // Returns: 1.0
    /// </code>
    /// </example>
    public double Log(double x)
    {
        if (x <= 0)
            throw new ArgumentException("Cannot calculate logarithm of a non-positive number.", nameof(x));
        
        return Math.Log(x);
    }

    /// <summary>
    /// Calculates the sine of the specified angle.
    /// </summary>
    /// <param name="x">An angle, measured in radians.</param>
    /// <returns>The sine of <paramref name="x"/>. The value is between -1 and 1.</returns>
    /// <remarks>
    /// The angle must be in radians. To convert degrees to radians, multiply by π/180.
    /// </remarks>
    /// <example>
    /// <code>
    /// var calculator = new ScientificCalculator();
    /// var result = calculator.Sin(Math.PI / 2);
    /// // Returns: 1.0
    /// </code>
    /// </example>
    public double Sin(double x) => Math.Sin(x);

    /// <summary>
    /// Calculates the cosine of the specified angle.
    /// </summary>
    /// <param name="x">An angle, measured in radians.</param>
    /// <returns>The cosine of <paramref name="x"/>. The value is between -1 and 1.</returns>
    /// <remarks>
    /// The angle must be in radians. To convert degrees to radians, multiply by π/180.
    /// </remarks>
    /// <example>
    /// <code>
    /// var calculator = new ScientificCalculator();
    /// var result = calculator.Cos(0);
    /// // Returns: 1.0
    /// </code>
    /// </example>
    public double Cos(double x) => Math.Cos(x);

    /// <summary>
    /// Calculates the tangent of the specified angle.
    /// </summary>
    /// <param name="x">An angle, measured in radians.</param>
    /// <returns>The tangent of <paramref name="x"/>.</returns>
    /// <remarks>
    /// The angle must be in radians. To convert degrees to radians, multiply by π/180.
    /// Tangent is undefined at odd multiples of π/2 (90°, 270°, etc.), where it approaches infinity.
    /// </remarks>
    /// <example>
    /// <code>
    /// var calculator = new ScientificCalculator();
    /// var result = calculator.Tan(Math.PI / 4);
    /// // Returns: 1.0
    /// </code>
    /// </example>
    public double Tan(double x) => Math.Tan(x);
}