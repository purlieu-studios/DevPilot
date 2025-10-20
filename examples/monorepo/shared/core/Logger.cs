namespace Shared.Core;

/// <summary>
/// Shared logging utility for all apps in the monorepo.
/// </summary>
public static class Logger
{
    /// <summary>
    /// Logs a message to the console.
    /// </summary>
    public static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
