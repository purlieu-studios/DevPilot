namespace MyApplication;

/// <summary>
/// Provides string manipulation utilities.
/// </summary>
public static class StringHelper
{
    /// <summary>
    /// Reverses the input string.
    /// </summary>
    public static string Reverse(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        char[] chars = input.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    /// <summary>
    /// Concatenates two strings with a space separator.
    /// </summary>
    /// <param name="first">The first string.</param>
    /// <param name="second">The second string.</param>
    /// <returns>A string containing both inputs separated by a space.</returns>
    public static string Concatenate(string first, string second)
    {
        if (first == null) first = string.Empty;
        if (second == null) second = string.Empty;

        return $"{first} {second}".Trim();
    }
}
