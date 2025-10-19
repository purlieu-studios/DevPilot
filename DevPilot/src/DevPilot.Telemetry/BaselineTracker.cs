using System.Data;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace DevPilot.Telemetry;

/// <summary>
/// Tracks pipeline quality baselines and detects regressions by comparing current runs against historical data.
/// </summary>
public sealed class BaselineTracker : IDisposable
{
    private readonly string _databasePath;
    private readonly SqliteConnection _connection;
    private bool _disposed;

    private BaselineTracker(string databasePath)
    {
        _databasePath = databasePath;
        _connection = new SqliteConnection($"Data Source={databasePath}");
        _connection.Open();
        InitializeDatabase();
    }

    /// <summary>
    /// Creates or opens a baseline tracker database.
    /// </summary>
    /// <param name="databasePath">Path to SQLite database file. If null, uses default location (.devpilot/telemetry/baselines.db).</param>
    public static BaselineTracker Create(string? databasePath = null)
    {
        databasePath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".devpilot", "telemetry", "baselines.db");

        var directory = Path.GetDirectoryName(databasePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return new BaselineTracker(databasePath);
    }

    /// <summary>
    /// Records pipeline metrics for baseline tracking.
    /// </summary>
    public void RecordMetrics(PipelineMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        using var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO pipeline_metrics (
                pipeline_id, timestamp, user_request, success, overall_score,
                plan_quality, code_quality, test_coverage, documentation, maintainability,
                tests_generated, tests_passed, tests_failed, duration_seconds,
                final_stage, rag_enabled, files_modified, repository_structure
            ) VALUES (
                @pipeline_id, @timestamp, @user_request, @success, @overall_score,
                @plan_quality, @code_quality, @test_coverage, @documentation, @maintainability,
                @tests_generated, @tests_passed, @tests_failed, @duration_seconds,
                @final_stage, @rag_enabled, @files_modified, @repository_structure
            )";

        command.Parameters.AddWithValue("@pipeline_id", metrics.PipelineId);
        command.Parameters.AddWithValue("@timestamp", metrics.Timestamp.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("@user_request", metrics.UserRequest);
        command.Parameters.AddWithValue("@success", metrics.Success ? 1 : 0);
        command.Parameters.AddWithValue("@overall_score", metrics.OverallScore);
        command.Parameters.AddWithValue("@plan_quality", metrics.PlanQuality);
        command.Parameters.AddWithValue("@code_quality", metrics.CodeQuality);
        command.Parameters.AddWithValue("@test_coverage", metrics.TestCoverage);
        command.Parameters.AddWithValue("@documentation", metrics.Documentation);
        command.Parameters.AddWithValue("@maintainability", metrics.Maintainability);
        command.Parameters.AddWithValue("@tests_generated", metrics.TestsGenerated);
        command.Parameters.AddWithValue("@tests_passed", metrics.TestsPassed);
        command.Parameters.AddWithValue("@tests_failed", metrics.TestsFailed);
        command.Parameters.AddWithValue("@duration_seconds", metrics.Duration.TotalSeconds);
        command.Parameters.AddWithValue("@final_stage", metrics.FinalStage);
        command.Parameters.AddWithValue("@rag_enabled", metrics.RagEnabled ? 1 : 0);
        command.Parameters.AddWithValue("@files_modified", metrics.FilesModified);
        command.Parameters.AddWithValue("@repository_structure", metrics.RepositoryStructure ?? (object)DBNull.Value);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Compares current metrics against rolling baseline (last 30 days).
    /// </summary>
    public RegressionReport CompareAgainstBaseline(PipelineMetrics current)
    {
        ArgumentNullException.ThrowIfNull(current);

        var baseline = GetRollingBaseline(days: 30);
        if (baseline == null)
        {
            // No baseline yet - first run
            return new RegressionReport
            {
                HasRegression = false,
                Message = "No baseline available yet (first run)"
            };
        }

        var regressions = new List<string>();

        // Check for score drops > 1.0 point
        if (current.OverallScore < baseline.OverallScore - 1.0)
        {
            regressions.Add($"Overall score dropped: {baseline.OverallScore:F1} → {current.OverallScore:F1} (-{baseline.OverallScore - current.OverallScore:F1})");
        }

        if (current.TestCoverage < baseline.TestCoverage - 1.0)
        {
            regressions.Add($"Test coverage dropped: {baseline.TestCoverage:F1} → {current.TestCoverage:F1} (-{baseline.TestCoverage - current.TestCoverage:F1})");
        }

        if (current.CodeQuality < baseline.CodeQuality - 1.0)
        {
            regressions.Add($"Code quality dropped: {baseline.CodeQuality:F1} → {current.CodeQuality:F1} (-{baseline.CodeQuality - current.CodeQuality:F1})");
        }

        // Check test pass rate drop > 10%
        if (current.TestPassRate < baseline.TestPassRate - 0.10)
        {
            regressions.Add($"Test pass rate dropped: {baseline.TestPassRate:P0} → {current.TestPassRate:P0} (-{(baseline.TestPassRate - current.TestPassRate):P0})");
        }

        return new RegressionReport
        {
            HasRegression = regressions.Count > 0,
            Regressions = regressions,
            BaselineScore = baseline.OverallScore,
            CurrentScore = current.OverallScore,
            Message = regressions.Count > 0
                ? $"⚠ Quality regression detected ({regressions.Count} issue{(regressions.Count > 1 ? "s" : "")})"
                : "✓ No regressions detected (quality within baseline)"
        };
    }

    /// <summary>
    /// Gets baseline statistics from recent successful pipeline runs.
    /// </summary>
    private BaselineStatistics? GetRollingBaseline(int days)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT
                AVG(overall_score) as avg_overall,
                AVG(plan_quality) as avg_plan,
                AVG(code_quality) as avg_code,
                AVG(test_coverage) as avg_test,
                AVG(documentation) as avg_docs,
                AVG(maintainability) as avg_maint,
                AVG(CAST(tests_passed AS REAL) / NULLIF(tests_generated, 0)) as avg_pass_rate,
                COUNT(*) as sample_count
            FROM pipeline_metrics
            WHERE
                timestamp >= @cutoff_time
                AND success = 1
                AND overall_score >= 7.0";

        var cutoffTime = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeSeconds();
        command.Parameters.AddWithValue("@cutoff_time", cutoffTime);

        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(0))
        {
            return null; // No baseline data
        }

        var sampleCount = reader.GetInt32(7);
        if (sampleCount < 3)
        {
            return null; // Need at least 3 samples for reliable baseline
        }

        return new BaselineStatistics
        {
            OverallScore = reader.GetDouble(0),
            PlanQuality = reader.GetDouble(1),
            CodeQuality = reader.GetDouble(2),
            TestCoverage = reader.GetDouble(3),
            Documentation = reader.GetDouble(4),
            Maintainability = reader.GetDouble(5),
            TestPassRate = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
            SampleCount = sampleCount
        };
    }

    /// <summary>
    /// Gets recent pipeline execution history.
    /// </summary>
    public List<PipelineMetrics> GetRecentMetrics(int count = 10)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT
                pipeline_id, timestamp, user_request, success, overall_score,
                plan_quality, code_quality, test_coverage, documentation, maintainability,
                tests_generated, tests_passed, tests_failed, duration_seconds,
                final_stage, rag_enabled, files_modified, repository_structure
            FROM pipeline_metrics
            ORDER BY timestamp DESC
            LIMIT @count";

        command.Parameters.AddWithValue("@count", count);

        var results = new List<PipelineMetrics>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new PipelineMetrics
            {
                PipelineId = reader.GetString(0),
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1)),
                UserRequest = reader.GetString(2),
                Success = reader.GetInt32(3) == 1,
                OverallScore = reader.GetDouble(4),
                PlanQuality = reader.GetDouble(5),
                CodeQuality = reader.GetDouble(6),
                TestCoverage = reader.GetDouble(7),
                Documentation = reader.GetDouble(8),
                Maintainability = reader.GetDouble(9),
                TestsGenerated = reader.GetInt32(10),
                TestsPassed = reader.GetInt32(11),
                TestsFailed = reader.GetInt32(12),
                Duration = TimeSpan.FromSeconds(reader.GetDouble(13)),
                FinalStage = reader.GetString(14),
                RagEnabled = reader.GetInt32(15) == 1,
                FilesModified = reader.GetInt32(16),
                RepositoryStructure = reader.IsDBNull(17) ? null : reader.GetString(17)
            });
        }

        return results;
    }

    private void InitializeDatabase()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS pipeline_metrics (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                pipeline_id TEXT NOT NULL,
                timestamp INTEGER NOT NULL,
                user_request TEXT NOT NULL,
                success INTEGER NOT NULL,
                overall_score REAL NOT NULL,
                plan_quality REAL NOT NULL,
                code_quality REAL NOT NULL,
                test_coverage REAL NOT NULL,
                documentation REAL NOT NULL,
                maintainability REAL NOT NULL,
                tests_generated INTEGER NOT NULL,
                tests_passed INTEGER NOT NULL,
                tests_failed INTEGER NOT NULL,
                duration_seconds REAL NOT NULL,
                final_stage TEXT NOT NULL,
                rag_enabled INTEGER NOT NULL,
                files_modified INTEGER NOT NULL,
                repository_structure TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_timestamp ON pipeline_metrics(timestamp);
            CREATE INDEX IF NOT EXISTS idx_success ON pipeline_metrics(success);";

        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _connection?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Statistical baseline calculated from recent successful pipeline runs.
/// </summary>
internal sealed class BaselineStatistics
{
    public required double OverallScore { get; init; }
    public required double PlanQuality { get; init; }
    public required double CodeQuality { get; init; }
    public required double TestCoverage { get; init; }
    public required double Documentation { get; init; }
    public required double Maintainability { get; init; }
    public required double TestPassRate { get; init; }
    public required int SampleCount { get; init; }
}

/// <summary>
/// Report detailing any quality regressions detected.
/// </summary>
public sealed class RegressionReport
{
    /// <summary>
    /// Gets whether a regression was detected.
    /// </summary>
    public required bool HasRegression { get; init; }

    /// <summary>
    /// Gets the list of specific regressions found.
    /// </summary>
    public List<string> Regressions { get; init; } = new();

    /// <summary>
    /// Gets the baseline overall score.
    /// </summary>
    public double? BaselineScore { get; init; }

    /// <summary>
    /// Gets the current overall score.
    /// </summary>
    public double? CurrentScore { get; init; }

    /// <summary>
    /// Gets a human-readable message describing the regression status.
    /// </summary>
    public required string Message { get; init; }
}
