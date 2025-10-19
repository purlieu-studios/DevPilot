namespace Calculator;

/// <summary>
/// Provides basic arithmetic operations.
/// </summary>
public class Calculator
{
    /// <summary>
    /// Adds two numbers.
    /// </summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>The sum of a and b.</returns>
    public double Add(double a, double b) => a + b;

    /// <summary>
    /// Subtracts the second number from the first.
    /// </summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number to subtract.</param>
    /// <returns>The difference of a minus b.</returns>
    public double Subtract(double a, double b) => a - b;
}
