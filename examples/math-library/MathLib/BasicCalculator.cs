using System;

namespace MathLib;

/// <summary>
/// Provides basic arithmetic operations including addition, subtraction, multiplication, division, modulo, and absolute value.
/// </summary>
/// <remarks>
/// This calculator uses double-precision floating-point arithmetic for all operations.
/// Division by zero will throw a <see cref="DivideByZeroException"/>.
/// </remarks>
/// <example>
/// <code>
/// var calculator = new BasicCalculator();
/// double sum = calculator.Add(5.0, 3.0);        // Returns 8.0
/// double difference = calculator.Subtract(10.0, 4.0);  // Returns 6.0
/// double product = calculator.Multiply(3.0, 4.0);      // Returns 12.0
/// double quotient = calculator.Divide(15.0, 3.0);      // Returns 5.0
/// </code>
/// double remainder = calculator.Modulo(10.0, 3.0);     // Returns 1.0
/// double absolute = calculator.AbsoluteValue(-7.5);    // Returns 7.5
/// </example>
public class BasicCalculator
{
    /// <summary>
    /// Adds two numbers together.
    /// </summary>
    /// <param name="a">The first number to add.</param>
    /// <param name="b">The second number to add.</param>
    /// <returns>The sum of <paramref name="a"/> and <paramref name="b"/>.</returns>
    /// <example>
    /// <code>
    /// var calculator = new BasicCalculator();
    /// double result = calculator.Add(5.5, 3.2);  // Returns 8.7
    /// </code>
    /// </example>
    public double Add(double a, double b) => a + b;

    /// <summary>
    /// Subtracts the second number from the first number.
    /// </summary>
    /// <param name="a">The number to subtract from (minuend).</param>
    /// <param name="b">The number to subtract (subtrahend).</param>
    /// <returns>The difference of <paramref name="a"/> minus <paramref name="b"/>.</returns>
    /// <example>
    /// <code>
    /// var calculator = new BasicCalculator();
    /// double result = calculator.Subtract(10.5, 3.2);  // Returns 7.3
    /// </code>
    /// </example>
    public double Subtract(double a, double b) => a - b;

    /// <summary>
    /// Multiplies two numbers together.
    /// </summary>
    /// <param name="a">The first number to multiply (multiplicand).</param>
    /// <param name="b">The second number to multiply (multiplier).</param>
    /// <returns>The product of <paramref name="a"/> and <paramref name="b"/>.</returns>
    /// <example>
    /// <code>
    /// var calculator = new BasicCalculator();
    /// double result = calculator.Multiply(4.5, 2.0);  // Returns 9.0
    /// </code>
    /// </example>
    public double Multiply(double a, double b) => a * b;

    /// <summary>
    /// Divides the first number by the second number.
    /// </summary>
    /// <param name="a">The number to be divided (dividend).</param>
    /// <param name="b">The number to divide by (divisor).</param>
    /// <returns>The quotient of <paramref name="a"/> divided by <paramref name="b"/>.</returns>
    /// <exception cref="DivideByZeroException">
    /// Thrown when <paramref name="b"/> is zero.
    /// </exception>
    /// <remarks>
    /// Division by zero is not allowed and will result in a <see cref="DivideByZeroException"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var calculator = new BasicCalculator();
    /// double result = calculator.Divide(10.0, 2.0);  // Returns 5.0
    /// // calculator.Divide(10.0, 0);  // Throws DivideByZeroException
    /// </code>
    /// </example>
    public double Divide(double a, double b) =>
        b == 0 ? throw new DivideByZeroException("Cannot divide by zero.") : a / b;

    /// <summary>
    /// Computes the remainder after dividing the first number by the second number.
    /// </summary>
    /// <param name="a">The dividend (number to be divided).</param>
    /// <param name="b">The divisor (number to divide by).</param>
    /// <returns>The remainder of <paramref name="a"/> divided by <paramref name="b"/>.</returns>
    /// <exception cref="DivideByZeroException">
    /// Thrown when <paramref name="b"/> is zero.
    /// </exception>
    /// <remarks>
    /// The modulo operation returns the remainder after division. For example, 10 % 3 = 1.
    /// The result has the same sign as the dividend.
    /// </remarks>
    /// <example>
    /// <code>
    /// var calculator = new BasicCalculator();
    /// double result = calculator.Modulo(10.0, 3.0);  // Returns 1.0
    /// double result2 = calculator.Modulo(-10.0, 3.0); // Returns -1.0
    /// // calculator.Modulo(10.0, 0);  // Throws DivideByZeroException
    /// </code>
    /// </example>
    public double Modulo(double a, double b) =>
        b == 0 ? throw new DivideByZeroException("Cannot calculate modulo with zero divisor.") : a % b;

    /// <summary>
    /// Returns the absolute value of a number (distance from zero).
    /// </summary>
    /// <param name="value">The number to get the absolute value of.</param>
    /// <returns>The non-negative absolute value of <paramref name="value"/>.</returns>
    /// <remarks>
    /// The absolute value is always non-negative. For example, both Abs(-5) and Abs(5) return 5.
    /// </remarks>
    /// <example>
    /// <code>
    /// var calculator = new BasicCalculator();
    /// double result = calculator.AbsoluteValue(-7.5);  // Returns 7.5
    /// double result2 = calculator.AbsoluteValue(3.2);  // Returns 3.2
    /// double result3 = calculator.AbsoluteValue(0);    // Returns 0
    /// </code>
    /// </example>
    public double AbsoluteValue(double value) => Math.Abs(value);
}