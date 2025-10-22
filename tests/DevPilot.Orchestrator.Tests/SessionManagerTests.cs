using DevPilot.Core;
using DevPilot.Orchestrator;
using FluentAssertions;
using Xunit;

namespace DevPilot.Orchestrator.Tests;

/// <summary>
/// Comprehensive tests for SessionManager functionality.
/// Validates session lifecycle, activity recording, persistence, and retrieval.
/// </summary>
public sealed class SessionManagerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly SessionManager _sessionManager;

    public SessionManagerTests()
    {
        // Create temporary directory for test sessions
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"devpilot-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _sessionManager = new SessionManager(_tempDirectory);
    }

    public void Dispose()
    {
        // Clean up temporary directory after tests
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void StartSession_CreatesNewSession_WithCorrectProperties()
    {
        // Act
        var session = _sessionManager.StartSession(_tempDirectory);

        // Assert
        session.Should().NotBeNull();
        session.SessionId.Should().NotBeNullOrWhiteSpace();
        session.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        session.EndTime.Should().BeNull();
        session.Activities.Should().BeEmpty();
        session.WorkingDirectory.Should().Be(_tempDirectory);
        session.Summary.Should().BeNull();
    }

    [Fact]
    public void RecordPipelineExecution_WithValidParameters_AddsActivity()
    {
        // Arrange
        _sessionManager.StartSession(_tempDirectory);
        const string pipelineId = "test-pipeline-123";
        const string userRequest = "Add calculator class";
        const double qualityScore = 8.5;

        // Act
        _sessionManager.RecordPipelineExecution(pipelineId, userRequest, success: true, qualityScore);

        // Assert
        var session = _sessionManager.EndSessionAsync().Result;
        session.Activities.Should().HaveCount(1);
        session.Activities[0].Type.Should().Be(ActivityType.PipelineExecution);
        session.Activities[0].Description.Should().Contain("Add calculator class");
        session.Activities[0].Metadata["pipelineId"].Should().Be(pipelineId);
        session.Activities[0].Metadata["success"].Should().Be("True");
        session.Activities[0].Metadata["qualityScore"].Should().Be("8.5");
        session.Activities[0].Metadata["userRequest"].Should().Be(userRequest);
    }

    [Fact]
    public void RecordPipelineExecution_WithoutQualityScore_OmitsScoreMetadata()
    {
        // Arrange
        _sessionManager.StartSession(_tempDirectory);

        // Act
        _sessionManager.RecordPipelineExecution("pipeline-1", "Test request", success: false);

        // Assert
        var session = _sessionManager.EndSessionAsync().Result;
        session.Activities[0].Metadata.Should().NotContainKey("qualityScore");
        session.Activities[0].Metadata["success"].Should().Be("False");
    }

    [Fact]
    public void RecordPipelineExecution_WithLongRequest_TruncatesDescription()
    {
        // Arrange
        _sessionManager.StartSession(_tempDirectory);
        var longRequest = new string('A', 100); // 100 characters

        // Act
        _sessionManager.RecordPipelineExecution("pipeline-1", longRequest, success: true);

        // Assert
        var session = _sessionManager.EndSessionAsync().Result;
        session.Activities[0].Description.Should().HaveLength(63); // "Pipeline: " + 50 chars + "..."
        session.Activities[0].Description.Should().EndWith("...");
    }

    [Fact]
    public void RecordPipelineExecution_WithoutActiveSession_ThrowsException()
    {
        // Act & Assert
        var act = () => _sessionManager.RecordPipelineExecution("pipeline-1", "Test", true);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("No active session*");
    }

    [Fact]
    public void RecordGitCommit_WithValidParameters_AddsActivity()
    {
        // Arrange
        _sessionManager.StartSession(_tempDirectory);
        const string commitHash = "abc123";
        const string commitMessage = "feat: Add new feature";

        // Act
        _sessionManager.RecordGitCommit(commitHash, commitMessage);

        // Assert
        var session = _sessionManager.EndSessionAsync().Result;
        session.Activities.Should().HaveCount(1);
        session.Activities[0].Type.Should().Be(ActivityType.GitCommit);
        session.Activities[0].Description.Should().Contain("feat: Add new feature");
        session.Activities[0].Metadata["commitHash"].Should().Be(commitHash);
        session.Activities[0].Metadata["commitMessage"].Should().Be(commitMessage);
    }

    [Fact]
    public void RecordDecision_WithValidParameters_AddsActivity()
    {
        // Arrange
        _sessionManager.StartSession(_tempDirectory);
        const string decision = "Use Repository pattern";
        const string rationale = "Better separation of concerns";

        // Act
        _sessionManager.RecordDecision(decision, rationale);

        // Assert
        var session = _sessionManager.EndSessionAsync().Result;
        session.Activities.Should().HaveCount(1);
        session.Activities[0].Type.Should().Be(ActivityType.Decision);
        session.Activities[0].Description.Should().Be(decision);
        session.Activities[0].Metadata["rationale"].Should().Be(rationale);
    }

    [Fact]
    public void RecordIssue_WithDefaultSeverity_AddsErrorActivity()
    {
        // Arrange
        _sessionManager.StartSession(_tempDirectory);
        const string issue = "Null reference exception in Calculator";

        // Act
        _sessionManager.RecordIssue(issue);

        // Assert
        var session = _sessionManager.EndSessionAsync().Result;
        session.Activities.Should().HaveCount(1);
        session.Activities[0].Type.Should().Be(ActivityType.IssueEncountered);
        session.Activities[0].Description.Should().Be(issue);
        session.Activities[0].Metadata["severity"].Should().Be("error");
    }

    [Fact]
    public void RecordIssue_WithCustomSeverity_UsesProvidedSeverity()
    {
        // Arrange
        _sessionManager.StartSession(_tempDirectory);

        // Act
        _sessionManager.RecordIssue("Deprecated API usage", severity: "warning");

        // Assert
        var session = _sessionManager.EndSessionAsync().Result;
        session.Activities[0].Metadata["severity"].Should().Be("warning");
    }

    [Fact]
    public void RecordFix_WithRelatedIssue_IncludesRelationship()
    {
        // Arrange
        _sessionManager.StartSession(_tempDirectory);
        const string fix = "Added null check before dereferencing";
        const string relatedIssue = "Null reference exception";

        // Act
        _sessionManager.RecordFix(fix, relatedIssue);

        // Assert
        var session = _sessionManager.EndSessionAsync().Result;
        session.Activities.Should().HaveCount(1);
        session.Activities[0].Type.Should().Be(ActivityType.IssueFix);
        session.Activities[0].Description.Should().Be(fix);
        session.Activities[0].Metadata["relatedIssue"].Should().Be(relatedIssue);
    }

    [Fact]
    public void RecordFix_WithoutRelatedIssue_OmitsRelationship()
    {
        // Arrange
        _sessionManager.StartSession(_tempDirectory);

        // Act
        _sessionManager.RecordFix("Applied performance optimization");

        // Assert
        var session = _sessionManager.EndSessionAsync().Result;
        session.Activities[0].Metadata.Should().NotContainKey("relatedIssue");
    }

    [Fact]
    public async Task EndSessionAsync_SavesSessionToDisk_WithCorrectFormat()
    {
        // Arrange
        _sessionManager.StartSession(_tempDirectory);
        _sessionManager.RecordPipelineExecution("p1", "Test request", true, 9.0);
        const string summary = "Successfully completed test session";

        // Act
        var completedSession = await _sessionManager.EndSessionAsync(summary);

        // Assert
        completedSession.EndTime.Should().NotBeNull();
        completedSession.EndTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        completedSession.Summary.Should().Be(summary);

        // Verify file was saved
        var sessionsDir = Path.Combine(_tempDirectory, ".devpilot", "sessions");
        Directory.Exists(sessionsDir).Should().BeTrue();
        Directory.GetFiles(sessionsDir, "*.json").Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task LoadLastSessionAsync_ReturnsNullWhenNoSessions_Exist()
    {
        // Act
        var lastSession = await _sessionManager.LoadLastSessionAsync();

        // Assert
        lastSession.Should().BeNull();
    }

    [Fact]
    public async Task LoadLastSessionAsync_ReturnsMostRecentSession_WhenMultipleExist()
    {
        // Arrange
        _sessionManager.StartSession(_tempDirectory);
        _sessionManager.RecordPipelineExecution("p1", "First request", true);
        await _sessionManager.EndSessionAsync("First session");

        await Task.Delay(100); // Ensure different timestamps

        _sessionManager.StartSession(_tempDirectory);
        _sessionManager.RecordPipelineExecution("p2", "Second request", true);
        await _sessionManager.EndSessionAsync("Second session");

        // Act
        var lastSession = await _sessionManager.LoadLastSessionAsync();

        // Assert
        lastSession.Should().NotBeNull();
        lastSession!.Summary.Should().Be("Second session");
    }

    [Fact]
    public async Task ListSessionsAsync_ReturnsEmptyList_WhenNoSessionsExist()
    {
        // Act
        var sessions = await _sessionManager.ListSessionsAsync();

        // Assert
        sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task ListSessionsAsync_ReturnsAllSessions_OrderedByStartTime()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
        {
            _sessionManager.StartSession(_tempDirectory);
            _sessionManager.RecordPipelineExecution($"p{i}", $"Request {i}", true);
            await _sessionManager.EndSessionAsync($"Session {i}");
            await Task.Delay(50); // Ensure different timestamps
        }

        // Act
        var sessions = await _sessionManager.ListSessionsAsync();

        // Assert
        sessions.Should().HaveCount(3);
        sessions[0].Summary.Should().Be("Session 2"); // Newest first
        sessions[1].Summary.Should().Be("Session 1");
        sessions[2].Summary.Should().Be("Session 0");
    }

    [Fact]
    public async Task SearchSessionsAsync_FindsMatchingKeyword_InSummary()
    {
        // Arrange
        _sessionManager.StartSession(_tempDirectory);
        await _sessionManager.EndSessionAsync("Calculator feature implementation");

        _sessionManager.StartSession(_tempDirectory);
        await _sessionManager.EndSessionAsync("User authentication module");

        // Act
        var results = await _sessionManager.SearchSessionsAsync("calculator");

        // Assert
        results.Should().HaveCount(1);
        results[0].Summary.Should().Contain("Calculator");
    }

    [Fact]
    public async Task SessionMemory_CalculatesDurationCorrectly()
    {
        // Arrange
        _sessionManager.StartSession(_tempDirectory);
        await Task.Delay(100); // Session duration
        var session = await _sessionManager.EndSessionAsync();

        // Assert
        session.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(100));
        session.Duration.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SessionMemory_CountsPipelines_AndCommitsCorrectly()
    {
        // Arrange
        _sessionManager.StartSession(_tempDirectory);
        _sessionManager.RecordPipelineExecution("p1", "Request 1", true);
        _sessionManager.RecordPipelineExecution("p2", "Request 2", true);
        _sessionManager.RecordGitCommit("abc123", "Commit 1");
        _sessionManager.RecordGitCommit("def456", "Commit 2");
        _sessionManager.RecordGitCommit("ghi789", "Commit 3");
        _sessionManager.RecordDecision("Some decision", "rationale");

        // Act
        var session = _sessionManager.EndSessionAsync().Result;

        // Assert
        session.PipelineCount.Should().Be(2);
        session.CommitCount.Should().Be(3);
        session.Activities.Should().HaveCount(6);
    }
}
