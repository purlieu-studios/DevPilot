using System;

namespace MathLib;

/// <summary>
/// Provides basic arithmetic operations including addition, subtraction, multiplication, and division.
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
}