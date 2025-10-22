using DevPilot.Core;
using FluentAssertions;

namespace DevPilot.Core.Tests;

public sealed class SessionMemoryTests
{
    #region SessionMemory Construction Tests

    [Fact]
    public void SessionMemory_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>()
        };

        // Assert
        session.SessionId.Should().Be("session-123");
        session.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        session.Activities.Should().NotBeNull();
        session.Activities.Should().BeEmpty();
    }

    [Fact]
    public void SessionMemory_EndTime_CanBeNull()
    {
        // Arrange & Act
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>()
        };

        // Assert
        session.EndTime.Should().BeNull();
    }

    [Fact]
    public void SessionMemory_EndTime_CanBeSet()
    {
        // Arrange
        var endTime = DateTime.UtcNow.AddHours(2);

        // Act
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            EndTime = endTime,
            Activities = new List<SessionActivity>()
        };

        // Assert
        session.EndTime.Should().Be(endTime);
    }

    [Fact]
    public void SessionMemory_Tags_DefaultsToEmptyList()
    {
        // Arrange & Act
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>()
        };

        // Assert
        session.Tags.Should().NotBeNull();
        session.Tags.Should().BeEmpty();
    }

    [Fact]
    public void SessionMemory_Tags_CanBeSet()
    {
        // Arrange
        var tags = new List<string> { "feature", "bugfix", "refactoring" };

        // Act
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>(),
            Tags = tags
        };

        // Assert
        session.Tags.Should().HaveCount(3);
        session.Tags.Should().Contain("feature");
    }

    [Fact]
    public void SessionMemory_WorkingDirectory_CanBeNull()
    {
        // Arrange & Act
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>()
        };

        // Assert
        session.WorkingDirectory.Should().BeNull();
    }

    [Fact]
    public void SessionMemory_WorkingDirectory_CanBeSet()
    {
        // Arrange & Act
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>(),
            WorkingDirectory = "C:\\DevPilot"
        };

        // Assert
        session.WorkingDirectory.Should().Be("C:\\DevPilot");
    }

    [Fact]
    public void SessionMemory_Summary_CanBeNull()
    {
        // Arrange & Act
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>()
        };

        // Assert
        session.Summary.Should().BeNull();
    }

    [Fact]
    public void SessionMemory_Summary_CanBeSet()
    {
        // Arrange & Act
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>(),
            Summary = "Implemented calculator feature with tests"
        };

        // Assert
        session.Summary.Should().Be("Implemented calculator feature with tests");
    }

    [Fact]
    public void SessionMemory_WithActivities_StoresCorrectly()
    {
        // Arrange
        var activities = new List<SessionActivity>
        {
            new SessionActivity
            {
                Type = ActivityType.PipelineExecution,
                Timestamp = DateTime.UtcNow,
                Description = "Ran planning stage"
            },
            new SessionActivity
            {
                Type = ActivityType.GitCommit,
                Timestamp = DateTime.UtcNow,
                Description = "Committed changes"
            }
        };

        // Act
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = activities
        };

        // Assert
        session.Activities.Should().HaveCount(2);
        session.Activities[0].Type.Should().Be(ActivityType.PipelineExecution);
        session.Activities[1].Type.Should().Be(ActivityType.GitCommit);
    }

    #endregion

    #region Duration Calculation Tests

    [Fact]
    public void Duration_WhenEndTimeIsNull_UsesCurrentTime()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-1);

        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = startTime,
            Activities = new List<SessionActivity>()
        };

        // Act
        var duration = session.Duration;

        // Assert
        duration.Should().BeCloseTo(TimeSpan.FromHours(1), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Duration_WhenEndTimeIsSet_CalculatesCorrectDuration()
    {
        // Arrange
        var startTime = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2025, 1, 1, 12, 30, 0, DateTimeKind.Utc);

        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = startTime,
            EndTime = endTime,
            Activities = new List<SessionActivity>()
        };

        // Act
        var duration = session.Duration;

        // Assert
        duration.Should().Be(TimeSpan.FromHours(2.5));
    }

    [Fact]
    public void Duration_VeryShortSession_CalculatesCorrectly()
    {
        // Arrange
        var startTime = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2025, 1, 1, 10, 0, 5, DateTimeKind.Utc);

        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = startTime,
            EndTime = endTime,
            Activities = new List<SessionActivity>()
        };

        // Act
        var duration = session.Duration;

        // Assert
        duration.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Duration_VeryLongSession_CalculatesCorrectly()
    {
        // Arrange
        var startTime = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2025, 1, 3, 14, 30, 0, DateTimeKind.Utc);

        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = startTime,
            EndTime = endTime,
            Activities = new List<SessionActivity>()
        };

        // Act
        var duration = session.Duration;

        // Assert
        duration.TotalHours.Should().BeApproximately(52.5, 0.01);
    }

    #endregion
}
