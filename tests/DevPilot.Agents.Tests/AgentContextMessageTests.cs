using DevPilot.Core;
using FluentAssertions;

namespace DevPilot.Agents.Tests;

public sealed class AgentContextMessageTests
{
    [Fact]
    public void AddMessage_AddsToHistory()
    {
        // Arrange
        var context = new AgentContext();
        var message = new AgentMessage
        {
            AgentName = "test-agent",
            Role = MessageRole.Assistant,
            Content = "Hello, World!"
        };

        // Act
        context.AddMessage(message);

        // Assert
        context.History.Should().HaveCount(1);
        context.History[0].Should().Be(message);
    }

    [Fact]
    public void AddMessage_ThrowsArgumentNullException_WhenMessageIsNull()
    {
        // Arrange
        var context = new AgentContext();

        // Act
        var act = () => context.AddMessage(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void History_ReturnsMessagesInOrderAdded()
    {
        // Arrange
        var context = new AgentContext();
        var message1 = new AgentMessage
        {
            AgentName = "agent1",
            Role = MessageRole.User,
            Content = "First message"
        };
        var message2 = new AgentMessage
        {
            AgentName = "agent2",
            Role = MessageRole.Assistant,
            Content = "Second message"
        };

        // Act
        context.AddMessage(message1);
        context.AddMessage(message2);

        // Assert
        context.History.Should().HaveCount(2);
        context.History[0].Should().Be(message1);
        context.History[1].Should().Be(message2);
    }

    [Fact]
    public void History_IsReadOnly()
    {
        // Arrange
        var context = new AgentContext();

        // Act
        var history = context.History;

        // Assert
        history.Should().BeAssignableTo<IReadOnlyList<AgentMessage>>();
    }

    [Fact]
    public void ClearHistory_RemovesAllMessages()
    {
        // Arrange
        var context = new AgentContext();
        context.AddMessage(new AgentMessage
        {
            AgentName = "agent1",
            Role = MessageRole.User,
            Content = "Message 1"
        });
        context.AddMessage(new AgentMessage
        {
            AgentName = "agent2",
            Role = MessageRole.Assistant,
            Content = "Message 2"
        });

        // Act
        context.ClearHistory();

        // Assert
        context.History.Should().BeEmpty();
    }

    [Fact]
    public void GetRecentMessages_ReturnsEmptyArray_WhenCountIsZero()
    {
        // Arrange
        var context = new AgentContext();
        context.AddMessage(new AgentMessage
        {
            AgentName = "agent",
            Role = MessageRole.User,
            Content = "Message"
        });

        // Act
        var recent = context.GetRecentMessages(0);

        // Assert
        recent.Should().BeEmpty();
    }

    [Fact]
    public void GetRecentMessages_ReturnsEmptyArray_WhenCountIsNegative()
    {
        // Arrange
        var context = new AgentContext();
        context.AddMessage(new AgentMessage
        {
            AgentName = "agent",
            Role = MessageRole.User,
            Content = "Message"
        });

        // Act
        var recent = context.GetRecentMessages(-5);

        // Assert
        recent.Should().BeEmpty();
    }

    [Fact]
    public void GetRecentMessages_ReturnsCorrectNumberOfMessages()
    {
        // Arrange
        var context = new AgentContext();
        for (int i = 1; i <= 5; i++)
        {
            context.AddMessage(new AgentMessage
            {
                AgentName = $"agent{i}",
                Role = MessageRole.User,
                Content = $"Message {i}"
            });
        }

        // Act
        var recent = context.GetRecentMessages(3);

        // Assert
        recent.Should().HaveCount(3);
        recent[0].Content.Should().Be("Message 3");
        recent[1].Content.Should().Be("Message 4");
        recent[2].Content.Should().Be("Message 5");
    }

    [Fact]
    public void GetRecentMessages_ReturnsAllMessages_WhenCountExceedsHistorySize()
    {
        // Arrange
        var context = new AgentContext();
        context.AddMessage(new AgentMessage
        {
            AgentName = "agent1",
            Role = MessageRole.User,
            Content = "Message 1"
        });
        context.AddMessage(new AgentMessage
        {
            AgentName = "agent2",
            Role = MessageRole.Assistant,
            Content = "Message 2"
        });

        // Act
        var recent = context.GetRecentMessages(10);

        // Assert
        recent.Should().HaveCount(2);
        recent[0].Content.Should().Be("Message 1");
        recent[1].Content.Should().Be("Message 2");
    }

    [Fact]
    public void AgentMessage_RequiredPropertiesAreSet()
    {
        // Arrange & Act
        var message = new AgentMessage
        {
            AgentName = "test-agent",
            Role = MessageRole.System,
            Content = "Test content"
        };

        // Assert
        message.AgentName.Should().Be("test-agent");
        message.Role.Should().Be(MessageRole.System);
        message.Content.Should().Be("Test content");
    }

    [Fact]
    public void AgentMessage_TimestampIsSetAutomatically()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var message = new AgentMessage
        {
            AgentName = "test-agent",
            Role = MessageRole.User,
            Content = "Test"
        };

        var after = DateTimeOffset.UtcNow;

        // Assert
        message.Timestamp.Should().BeOnOrAfter(before);
        message.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void AgentMessage_MetadataIsOptional()
    {
        // Arrange & Act
        var messageWithoutMetadata = new AgentMessage
        {
            AgentName = "agent1",
            Role = MessageRole.Assistant,
            Content = "Without metadata"
        };

        var messageWithMetadata = new AgentMessage
        {
            AgentName = "agent2",
            Role = MessageRole.User,
            Content = "With metadata",
            Metadata = new Dictionary<string, object>
            {
                ["key1"] = "value1",
                ["key2"] = 42
            }
        };

        // Assert
        messageWithoutMetadata.Metadata.Should().BeNull();
        messageWithMetadata.Metadata.Should().NotBeNull();
        messageWithMetadata.Metadata!["key1"].Should().Be("value1");
        messageWithMetadata.Metadata["key2"].Should().Be(42);
    }
}
