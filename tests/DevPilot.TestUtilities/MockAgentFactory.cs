using DevPilot.Core;

namespace DevPilot.TestUtilities;

/// <summary>
/// Factory for creating mock agents for testing.
/// Allows tests to control agent behavior without making real API calls.
/// </summary>
/// <example>
/// <code>
/// // Create an agent that always succeeds
/// var agent = MockAgentFactory.CreateSuccessAgent("planner", "Mock plan output");
///
/// // Create an agent that fails
/// var agent = MockAgentFactory.CreateFailureAgent("coder", "Failed to generate code");
///
/// // Create a custom agent
/// var agent = MockAgentFactory.Create("reviewer", async (input, context) =>
/// {
///     return AgentResult.CreateSuccess("reviewer", "LGTM!");
/// });
/// </code>
/// </example>
public static class MockAgentFactory
{
    /// <summary>
    /// Creates a mock agent that always succeeds with the specified output.
    /// </summary>
    /// <param name="agentName">The agent name (e.g., "planner", "coder")</param>
    /// <param name="output">The output to return</param>
    /// <param name="executionTime">Optional execution time to simulate</param>
    public static IAgent CreateSuccessAgent(
        string agentName,
        string output,
        TimeSpan? executionTime = null)
    {
        return new MockAgent(
            agentName,
            (input, context) => Task.FromResult(
                AgentResult.CreateSuccess(agentName, output, executionTime)));
    }

    /// <summary>
    /// Creates a mock agent that always fails with the specified error message.
    /// </summary>
    /// <param name="agentName">The agent name</param>
    /// <param name="errorMessage">The error message to return</param>
    /// <param name="executionTime">Optional execution time to simulate</param>
    public static IAgent CreateFailureAgent(
        string agentName,
        string errorMessage,
        TimeSpan? executionTime = null)
    {
        return new MockAgent(
            agentName,
            (input, context) => Task.FromResult(
                AgentResult.CreateFailure(agentName, errorMessage, executionTime)));
    }

    /// <summary>
    /// Creates a mock agent with custom execution logic.
    /// </summary>
    /// <param name="agentName">The agent name</param>
    /// <param name="executeFunc">The execution function</param>
    public static IAgent Create(
        string agentName,
        Func<string, AgentContext, Task<AgentResult>> executeFunc)
    {
        return new MockAgent(agentName, executeFunc);
    }

    /// <summary>
    /// Creates a mock agent that echoes the input as output.
    /// Useful for testing pipeline data flow.
    /// </summary>
    /// <param name="agentName">The agent name</param>
    public static IAgent CreateEchoAgent(string agentName)
    {
        return new MockAgent(
            agentName,
            (input, context) => Task.FromResult(
                AgentResult.CreateSuccess(agentName, $"Echo: {input}")));
    }

    /// <summary>
    /// Creates a mock agent that captures invocations for verification.
    /// </summary>
    /// <param name="agentName">The agent name</param>
    /// <param name="output">The output to return</param>
    public static (IAgent Agent, MockAgentInvocationTracker Tracker) CreateTrackedAgent(
        string agentName,
        string output)
    {
        var tracker = new MockAgentInvocationTracker();
        var agent = new MockAgent(
            agentName,
            (input, context) =>
            {
                tracker.RecordInvocation(input, context);
                return Task.FromResult(AgentResult.CreateSuccess(agentName, output));
            });

        return (agent, tracker);
    }

    private sealed class MockAgent : IAgent
    {
        private readonly Func<string, AgentContext, Task<AgentResult>> _executeFunc;

        public MockAgent(
            string agentName,
            Func<string, AgentContext, Task<AgentResult>> executeFunc)
        {
            _executeFunc = executeFunc;
            Definition = new AgentDefinition
            {
                Name = agentName,
                Version = "1.0.0",
                Description = $"Mock {agentName} agent for testing",
                SystemPrompt = $"You are a mock {agentName} agent.",
                Model = "mock-model"
            };
        }

        public AgentDefinition Definition { get; }

        public Task<AgentResult> ExecuteAsync(
            string input,
            AgentContext context,
            CancellationToken cancellationToken = default)
        {
            return _executeFunc(input, context);
        }
    }
}

/// <summary>
/// Tracks invocations of mock agents for verification in tests.
/// </summary>
public sealed class MockAgentInvocationTracker
{
    private readonly List<(string Input, AgentContext Context)> _invocations = new();

    /// <summary>
    /// Gets the number of times the agent was invoked.
    /// </summary>
    public int InvocationCount => _invocations.Count;

    /// <summary>
    /// Gets all invocations.
    /// </summary>
    public IReadOnlyList<(string Input, AgentContext Context)> Invocations =>
        _invocations.AsReadOnly();

    /// <summary>
    /// Gets the most recent invocation input.
    /// </summary>
    public string? LastInput => _invocations.Count > 0 ? _invocations[^1].Input : null;

    /// <summary>
    /// Gets the most recent invocation context.
    /// </summary>
    public AgentContext? LastContext => _invocations.Count > 0 ? _invocations[^1].Context : null;

    /// <summary>
    /// Records an invocation.
    /// </summary>
    internal void RecordInvocation(string input, AgentContext context)
    {
        _invocations.Add((input, context));
    }

    /// <summary>
    /// Clears all recorded invocations.
    /// </summary>
    public void Clear() => _invocations.Clear();
}
