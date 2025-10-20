namespace MathLib;

/// <summary>
/// Provides statistical operations on arrays of double values.
/// </summary>
/// <remarks>
/// This calculator includes common statistical functions such as mean, median,
/// standard deviation, and finding minimum and maximum values.
/// </remarks>
/// <example>
/// <code>
/// var calculator = new StatisticsCalculator();
/// double[] data = { 1.0, 2.0, 3.0, 4.0, 5.0 };
/// double mean = calculator.Mean(data);  // Returns 3.0
/// double median = calculator.Median(data);  // Returns 3.0
/// </code>
/// </example>
public class StatisticsCalculator
{
    /// <summary>
    /// Calculates the arithmetic mean (average) of an array of values.
    /// </summary>
    /// <param name="values">The array of values to calculate the mean for.</param>
    /// <returns>The arithmetic mean of the values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="values"/> is empty.</exception>
    /// <remarks>
    /// The mean is calculated as the sum of all values divided by the count of values.
    /// </remarks>
    /// <example>
    /// <code>
    /// var calculator = new StatisticsCalculator();
    /// double[] data = { 1.0, 2.0, 3.0, 4.0, 5.0 };
    /// double mean = calculator.Mean(data);  // Returns 3.0
    /// </code>
    /// </example>
    public double Mean(double[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            throw new ArgumentException("Array cannot be empty.", nameof(values));

        return values.Average();
    }

    /// <summary>
    /// Calculates the median (middle value) of an array of values.
    /// </summary>
    /// <param name="values">The array of values to calculate the median for.</param>
    /// <returns>
    /// For odd-length arrays, returns the middle value.
    /// For even-length arrays, returns the average of the two middle values.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="values"/> is empty.</exception>
    /// <remarks>
    /// The input array is sorted before finding the median. The original array is not modified.
    /// </remarks>
    /// <example>
    /// <code>
    /// var calculator = new StatisticsCalculator();
    /// double[] oddData = { 3.0, 1.0, 5.0 };
    /// double median1 = calculator.Median(oddData);  // Returns 3.0
    /// 
    /// double[] evenData = { 1.0, 2.0, 3.0, 4.0 };
    /// double median2 = calculator.Median(evenData);  // Returns 2.5
    /// </code>
    /// </example>
    public double Median(double[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            throw new ArgumentException("Array cannot be empty.", nameof(values));

        double[] sorted = values.Order().ToArray();
        int n = sorted.Length;

        if (n % 2 == 1)
        {
            return sorted[n / 2];
        }
        else
        {
            return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
        }
    }

    /// <summary>
    /// Calculates the population standard deviation of an array of values.
    /// </summary>
    /// <param name="values">The array of values to calculate the standard deviation for.</param>
    /// <returns>The population standard deviation of the values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="values"/> is empty.</exception>
    /// <remarks>
    /// This method calculates the population standard deviation using the formula:
    /// sqrt(sum((x - mean)^2) / n).
    /// For a single value, the standard deviation is 0.
    /// </remarks>
    /// <example>
    /// <code>
    /// var calculator = new StatisticsCalculator();
    /// double[] data = { 2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0 };
    /// double stdDev = calculator.StandardDeviation(data);  // Returns 2.0
    /// </code>
    /// </example>
    public double StandardDeviation(double[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            throw new ArgumentException("Array cannot be empty.", nameof(values));

        double mean = Mean(values);
        double sumOfSquaredDifferences = values.Sum(value => Math.Pow(value - mean, 2));
        return Math.Sqrt(sumOfSquaredDifferences / values.Length);
    }

    /// <summary>
    /// Finds the minimum value in an array.
    /// </summary>
    /// <param name="values">The array of values to search.</param>
    /// <returns>The smallest value in the array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="values"/> is empty.</exception>
    public double Min(double[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Min();
    }

    /// <summary>
    /// Finds the maximum value in an array.
    /// </summary>
    /// <param name="values">The array of values to search.</param>
    /// <returns>The largest value in the array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="values"/> is empty.</exception>
    public double Max(double[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Max();
    }
}