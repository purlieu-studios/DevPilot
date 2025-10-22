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

    #region PipelineCount Tests

    [Fact]
    public void PipelineCount_WithNoPipelines_ReturnsZero()
    {
        // Arrange
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>
            {
                new SessionActivity { Type = ActivityType.GitCommit, Timestamp = DateTime.UtcNow, Description = "Commit" },
                new SessionActivity { Type = ActivityType.Decision, Timestamp = DateTime.UtcNow, Description = "Decision" }
            }
        };

        // Act
        var count = session.PipelineCount;

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void PipelineCount_WithOnlyPipelineActivities_ReturnsCorrectCount()
    {
        // Arrange
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>
            {
                new SessionActivity { Type = ActivityType.PipelineExecution, Timestamp = DateTime.UtcNow, Description = "Pipeline 1" },
                new SessionActivity { Type = ActivityType.PipelineExecution, Timestamp = DateTime.UtcNow, Description = "Pipeline 2" },
                new SessionActivity { Type = ActivityType.PipelineExecution, Timestamp = DateTime.UtcNow, Description = "Pipeline 3" }
            }
        };

        // Act
        var count = session.PipelineCount;

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public void PipelineCount_WithMixedActivities_CountsOnlyPipelines()
    {
        // Arrange
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>
            {
                new SessionActivity { Type = ActivityType.PipelineExecution, Timestamp = DateTime.UtcNow, Description = "Pipeline 1" },
                new SessionActivity { Type = ActivityType.GitCommit, Timestamp = DateTime.UtcNow, Description = "Commit" },
                new SessionActivity { Type = ActivityType.PipelineExecution, Timestamp = DateTime.UtcNow, Description = "Pipeline 2" },
                new SessionActivity { Type = ActivityType.Decision, Timestamp = DateTime.UtcNow, Description = "Decision" }
            }
        };

        // Act
        var count = session.PipelineCount;

        // Assert
        count.Should().Be(2);
    }

    #endregion

    #region CommitCount Tests

    [Fact]
    public void CommitCount_WithNoCommits_ReturnsZero()
    {
        // Arrange
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>
            {
                new SessionActivity { Type = ActivityType.PipelineExecution, Timestamp = DateTime.UtcNow, Description = "Pipeline" },
                new SessionActivity { Type = ActivityType.Note, Timestamp = DateTime.UtcNow, Description = "Note" }
            }
        };

        // Act
        var count = session.CommitCount;

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void CommitCount_WithOnlyGitCommits_ReturnsCorrectCount()
    {
        // Arrange
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>
            {
                new SessionActivity { Type = ActivityType.GitCommit, Timestamp = DateTime.UtcNow, Description = "Commit 1" },
                new SessionActivity { Type = ActivityType.GitCommit, Timestamp = DateTime.UtcNow, Description = "Commit 2" }
            }
        };

        // Act
        var count = session.CommitCount;

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void CommitCount_WithMixedActivities_CountsOnlyCommits()
    {
        // Arrange
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>
            {
                new SessionActivity { Type = ActivityType.GitCommit, Timestamp = DateTime.UtcNow, Description = "Commit 1" },
                new SessionActivity { Type = ActivityType.PipelineExecution, Timestamp = DateTime.UtcNow, Description = "Pipeline" },
                new SessionActivity { Type = ActivityType.GitCommit, Timestamp = DateTime.UtcNow, Description = "Commit 2" },
                new SessionActivity { Type = ActivityType.GitCommit, Timestamp = DateTime.UtcNow, Description = "Commit 3" }
            }
        };

        // Act
        var count = session.CommitCount;

        // Assert
        count.Should().Be(3);
    }

    #endregion

    #region SessionActivity Tests

    [Fact]
    public void SessionActivity_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var activity = new SessionActivity
        {
            Type = ActivityType.PipelineExecution,
            Timestamp = DateTime.UtcNow,
            Description = "Executed planning stage"
        };

        // Assert
        activity.Type.Should().Be(ActivityType.PipelineExecution);
        activity.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        activity.Description.Should().Be("Executed planning stage");
    }

    [Fact]
    public void SessionActivity_Metadata_DefaultsToEmptyDictionary()
    {
        // Arrange & Act
        var activity = new SessionActivity
        {
            Type = ActivityType.GitCommit,
            Timestamp = DateTime.UtcNow,
            Description = "Committed changes"
        };

        // Assert
        activity.Metadata.Should().NotBeNull();
        activity.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void SessionActivity_Metadata_CanStoreKeyValuePairs()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            { "commitHash", "abc123" },
            { "branch", "main" }
        };

        // Act
        var activity = new SessionActivity
        {
            Type = ActivityType.GitCommit,
            Timestamp = DateTime.UtcNow,
            Description = "Committed changes",
            Metadata = metadata
        };

        // Assert
        activity.Metadata.Should().NotBeNull();
        activity.Metadata.Should().HaveCount(2);
        activity.Metadata!["commitHash"].Should().Be("abc123");
    }

    [Fact]
    public void SessionActivity_WithVeryLongDescription_StoresCorrectly()
    {
        // Arrange
        var longDescription = new string('A', 10000);

        // Act
        var activity = new SessionActivity
        {
            Type = ActivityType.Note,
            Timestamp = DateTime.UtcNow,
            Description = longDescription
        };

        // Assert
        activity.Description.Should().HaveLength(10000);
    }

    #endregion

    #region ActivityType Enum Tests

    [Fact]
    public void ActivityType_HasExpectedValues()
    {
        // Assert
        Enum.GetNames(typeof(ActivityType)).Should().Contain(new[]
        {
            nameof(ActivityType.PipelineExecution),
            nameof(ActivityType.GitCommit),
            nameof(ActivityType.Decision),
            nameof(ActivityType.IssueEncountered),
            nameof(ActivityType.IssueFix),
            nameof(ActivityType.Note)
        });
    }

    [Fact]
    public void ActivityType_CanBeCompared()
    {
        // Arrange
        var type1 = ActivityType.GitCommit;
        var type2 = ActivityType.PipelineExecution;

        // Assert
        type1.Should().NotBe(type2);
        type1.Should().Be(ActivityType.GitCommit);
    }

    [Fact]
    public void ActivityType_AllValuesAreDistinct()
    {
        // Arrange
        var values = Enum.GetValues(typeof(ActivityType)).Cast<ActivityType>().ToList();

        // Assert
        values.Should().OnlyHaveUniqueItems();
        values.Should().HaveCount(6);
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void SessionMemory_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var startTime = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var activities = new List<SessionActivity>();
        var tags = new List<string>();

        var session1 = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = startTime,
            EndTime = endTime,
            Activities = activities,
            Tags = tags
        };

        var session2 = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = startTime,
            EndTime = endTime,
            Activities = activities,
            Tags = tags
        };

        // Assert - Records with same values (including same list instances) should be equal
        session1.Should().Be(session2);
        (session1 == session2).Should().BeTrue();
    }

    [Fact]
    public void SessionMemory_WithDifferentSessionId_AreNotEqual()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var activities = new List<SessionActivity>();

        var session1 = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = startTime,
            Activities = activities
        };

        var session2 = new SessionMemory
        {
            SessionId = "session-456",
            StartTime = startTime,
            Activities = activities
        };

        // Assert
        session1.Should().NotBe(session2);
    }

    [Fact]
    public void SessionActivity_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var timestamp = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var metadata = new Dictionary<string, string>();

        var activity1 = new SessionActivity
        {
            Type = ActivityType.GitCommit,
            Timestamp = timestamp,
            Description = "Committed changes",
            Metadata = metadata
        };

        var activity2 = new SessionActivity
        {
            Type = ActivityType.GitCommit,
            Timestamp = timestamp,
            Description = "Committed changes",
            Metadata = metadata
        };

        // Assert - Records with same values (including same Metadata instance) should be equal
        activity1.Should().Be(activity2);
        (activity1 == activity2).Should().BeTrue();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void SessionMemory_WithVeryLongSessionId_StoresCorrectly()
    {
        // Arrange
        var longId = new string('A', 1000);

        // Act
        var session = new SessionMemory
        {
            SessionId = longId,
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>()
        };

        // Assert
        session.SessionId.Should().HaveLength(1000);
    }

    [Fact]
    public void SessionMemory_WithHundredsOfActivities_CalculatesCountsCorrectly()
    {
        // Arrange
        var activities = new List<SessionActivity>();
        for (int i = 0; i < 50; i++)
        {
            activities.Add(new SessionActivity
            {
                Type = ActivityType.PipelineExecution,
                Timestamp = DateTime.UtcNow,
                Description = $"Pipeline {i}"
            });
        }
        for (int i = 0; i < 30; i++)
        {
            activities.Add(new SessionActivity
            {
                Type = ActivityType.GitCommit,
                Timestamp = DateTime.UtcNow,
                Description = $"Commit {i}"
            });
        }

        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = activities
        };

        // Act & Assert
        session.PipelineCount.Should().Be(50);
        session.CommitCount.Should().Be(30);
        session.Activities.Should().HaveCount(80);
    }

    [Fact]
    public void SessionMemory_WithManyTags_StoresAll()
    {
        // Arrange
        var manyTags = Enumerable.Range(1, 100).Select(i => $"tag-{i}").ToList();

        // Act
        var session = new SessionMemory
        {
            SessionId = "session-123",
            StartTime = DateTime.UtcNow,
            Activities = new List<SessionActivity>(),
            Tags = manyTags
        };

        // Assert
        session.Tags.Should().HaveCount(100);
        session.Tags.Should().Contain("tag-50");
    }

    #endregion
}
