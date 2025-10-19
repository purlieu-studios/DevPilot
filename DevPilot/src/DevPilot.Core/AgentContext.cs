namespace DevPilot.Core;

/// <summary>
/// Manages state, memory, and context shared across agent executions.
/// </summary>
public sealed class AgentContext
{
    private readonly Dictionary<string, object> _state = new();
    private readonly List<AgentMessage> _history = new();

    /// <summary>
    /// Gets the unique identifier for this context.
    /// </summary>
    public string ContextId { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the workspace root directory (needed for CLAUDE.md file approach).
    /// </summary>
    public string? WorkspaceRoot { get; set; }

    /// <summary>
    /// Gets the conversation history.
    /// </summary>
    public IReadOnlyList<AgentMessage> History => _history.AsReadOnly();

    /// <summary>
    /// Adds a message to the conversation history.
    /// </summary>
    /// <param name="message">The message to add.</param>
    public void AddMessage(AgentMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _history.Add(message);
    }

    /// <summary>
    /// Stores a value in the context state.
    /// </summary>
    /// <typeparam name="T">The type of value to store.</typeparam>
    /// <param name="key">The key to store the value under.</param>
    /// <param name="value">The value to store.</param>
    public void SetValue<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _state[key] = value!;
    }

    /// <summary>
    /// Retrieves a value from the context state.
    /// </summary>
    /// <typeparam name="T">The type of value to retrieve.</typeparam>
    /// <param name="key">The key to retrieve the value for.</param>
    /// <returns>The value if found; otherwise, default.</returns>
    public T? GetValue<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _state.TryGetValue(key, out var value) ? (T)value : default;
    }

    /// <summary>
    /// Checks if a key exists in the context state.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key exists; otherwise, false.</returns>
    public bool ContainsKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _state.ContainsKey(key);
    }

    /// <summary>
    /// Clears the conversation history.
    /// </summary>
    public void ClearHistory() => _history.Clear();

    /// <summary>
    /// Clears all state.
    /// </summary>
    public void ClearState() => _state.Clear();

    /// <summary>
    /// Gets the most recent messages up to a specified count.
    /// </summary>
    /// <param name="count">The number of recent messages to retrieve.</param>
    /// <returns>The most recent messages.</returns>
    public IReadOnlyList<AgentMessage> GetRecentMessages(int count)
    {
        if (count <= 0)
        {
            return Array.Empty<AgentMessage>();
        }

        var startIndex = Math.Max(0, _history.Count - count);
        return _history.Skip(startIndex).ToList().AsReadOnly();
    }
}

/// <summary>
/// Represents a message in the agent conversation.
/// </summary>
public sealed class AgentMessage
{
    /// <summary>
    /// Gets the agent name that sent the message.
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// Gets the role of the message sender.
    /// </summary>
    public required MessageRole Role { get; init; }

    /// <summary>
    /// Gets the content of the message.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the timestamp when the message was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets optional metadata associated with the message.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Represents the role of a message sender.
/// </summary>
public enum MessageRole
{
    /// <summary>
    /// Message from the user.
    /// </summary>
    User,

    /// <summary>
    /// Message from an agent/assistant.
    /// </summary>
    Assistant,

    /// <summary>
    /// System message.
    /// </summary>
    System
}
