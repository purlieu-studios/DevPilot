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
}
