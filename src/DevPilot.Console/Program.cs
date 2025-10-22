using DevPilot.Agents;
using DevPilot.Core;
using DevPilot.Diagnostics;
using DevPilot.Orchestrator;
using DevPilot.Orchestrator.State;
using DevPilot.RAG;
using DevPilot.Telemetry;
using Spectre.Console;

namespace DevPilot.Console;

/// <summary>
/// Main entry point for the DevPilot CLI application.
/// </summary>
internal sealed class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            // Check for diagnostic and cleanup commands first
            if (args.Length > 0 && args[0] == "diagnose")
            {
                return await HandleDiagnoseCommandAsync(args);
            }

            if (args.Length > 0 && args[0] == "cleanup")
            {
                return await HandleCleanupCommandAsync(args);
            }

            if (args.Length > 0 && args[0] == "install-hook")
            {
                return HandleInstallHookCommand(args);
            }

            if (args.Length > 0 && args[0] == "uninstall-hook")
            {
                return HandleUninstallHookCommand(args);
            }

            // State management commands
            if (args.Length > 0 && args[0] == "list")
            {
                return await HandleListCommandAsync(args);
            }

            if (args.Length > 0 && args[0] == "review")
            {
                return await HandleReviewCommandAsync(args);
            }

            if (args.Length > 0 && args[0] == "resume")
            {
                return await HandleResumeCommandAsync(args);
            }

            if (args.Length > 0 && args[0] == "approve")
            {
                return await HandleApproveCommandAsync(args);
            }

            if (args.Length > 0 && args[0] == "reject")
            {
                return await HandleRejectCommandAsync(args);
            }

            if (args.Length > 0 && args[0] == "cleanup-states")
            {
                return await HandleCleanupStatesCommandAsync(args);
            }

            // Parse command-line arguments first to check for flags
            bool autoApprove = false;
            bool enableRag = false;
            bool preserveWorkspace = false;
            string? userRequest = null;

            foreach (var arg in args)
            {
                if (arg == "--yes" || arg == "-y")
                {
                    autoApprove = true;
                }
                else if (arg == "--enable-rag")
                {
                    enableRag = true;
                }
                else if (arg == "--preserve-workspace")
                {
                    preserveWorkspace = true;
                }
                else if (!string.IsNullOrWhiteSpace(arg))
                {
                    userRequest = arg;
                }
            }

            if (string.IsNullOrWhiteSpace(userRequest))
            {
                DisplayUsage();
                return 1;
            }

            // Check if running in interactive mode (required for user prompts unless --yes provided)
            if (!autoApprove && !AnsiConsole.Profile.Capabilities.Interactive)
            {
                System.Console.WriteLine("ERROR: DevPilot requires an interactive terminal.");
                System.Console.WriteLine();
                System.Console.WriteLine("DevPilot is designed to run interactively and will prompt you to");
                System.Console.WriteLine("review and apply code changes after pipeline execution.");
                System.Console.WriteLine();
                System.Console.WriteLine("Please run DevPilot directly in a terminal (not piped or redirected):");
                System.Console.WriteLine("  devpilot \"your request here\"");
                System.Console.WriteLine();
                System.Console.WriteLine("Or use --yes flag to skip prompts and auto-apply changes:");
                System.Console.WriteLine("  devpilot --yes \"your request here\"");
                return 1;
            }

            // Display header
            AnsiConsole.Write(
                new FigletText("DevPilot")
                    .Color(Color.Blue));

            AnsiConsole.MarkupLine("[dim]MASAI Pipeline - Automated Code Generation & Review[/]");
            AnsiConsole.WriteLine();

            // Create session manager and show last session context (automatic, no config needed)
            var sourceRoot = Directory.GetCurrentDirectory();
            var sessionManager = new SessionManager(sourceRoot);
            var lastSession = await sessionManager.LoadLastSessionAsync();
            if (lastSession != null && lastSession.Activities.Any())
            {
                var lastActivity = lastSession.Activities.LastOrDefault(a => a.Type == ActivityType.PipelineExecution);
                if (lastActivity != null)
                {
                    var qualityScore = lastActivity.Metadata.ContainsKey("qualityScore")
                        ? lastActivity.Metadata["qualityScore"]
                        : null;
                    var timeAgo = DateTime.UtcNow - lastActivity.Timestamp;
                    var timeAgoStr = timeAgo.TotalMinutes < 1 ? "just now" :
                                     timeAgo.TotalMinutes < 60 ? $"{(int)timeAgo.TotalMinutes}m ago" :
                                     timeAgo.TotalHours < 24 ? $"{(int)timeAgo.TotalHours}h ago" :
                                     $"{(int)timeAgo.TotalDays}d ago";
                    var scoreStr = qualityScore != null ? $" ({qualityScore}/10)" : "";
                    AnsiConsole.MarkupLine($"[dim]Last: \"{Markup.Escape(lastActivity.Metadata.GetValueOrDefault("userRequest", "Unknown"))}\"{scoreStr} - {timeAgoStr}[/]");
                    AnsiConsole.WriteLine();
                }
            }

            // Pre-flight validation: Check environment before starting
            var validator = new PreFlightValidator();
            var validationResult = validator.Validate(sourceRoot);

            if (!validationResult.IsValid)
            {
                AnsiConsole.MarkupLine($"[red]✗ Pre-flight validation failed:[/] {validationResult.ErrorMessage}");
                if (validationResult.Suggestion != null)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[yellow]💡 Suggestion:[/]");
                    AnsiConsole.MarkupLine($"[dim]{Markup.Escape(validationResult.Suggestion)}[/]");
                }
                return 1;
            }

            if (validationResult.WarningMessage != null)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ Warning:[/] {validationResult.WarningMessage}");
                AnsiConsole.WriteLine();
            }

            // Generate pipeline ID early (before workspace creation)
            var pipelineId = Guid.NewGuid().ToString();

            // Create workspace and copy target repository files
            WorkspaceManager workspace;
            try
            {
                workspace = WorkspaceManager.CreateWorkspace(pipelineId);

                // Copy domain files (CLAUDE.md, .agents/, docs/, src/, tests/ + devpilot.json configured folders)
                workspace.CopyDomainFiles(sourceRoot);

                // Copy project files (.csproj, .sln, config files)
                workspace.CopyProjectFiles(sourceRoot);
            }
            catch (IOException ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Failed to create workspace: {ex.Message}");
                return 1;
            }

            // Load agents and build pipeline with workspace context
            var pipeline = await BuildPipelineAsync(workspace, sessionManager, enableRag, preserveWorkspace);

            // Start new session (automatic, tracks context between runs)
            sessionManager.StartSession(sourceRoot);

            // Execute pipeline
            AnsiConsole.MarkupLine($"[bold]Request:[/] {userRequest}");
            AnsiConsole.WriteLine();

            var result = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Executing pipeline...", async ctx =>
                {
                    return await pipeline.ExecuteAsync(userRequest);
                });

            // Display results
            DisplayResults(result);

            // End session with auto-generated summary (automatic, no user input needed)
            var sessionSummary = result.Success
                ? $"✓ {userRequest}"
                : $"✗ {userRequest} (failed)";
            await sessionManager.EndSessionAsync(sessionSummary);

            // Track metrics and check for regressions
            TrackMetricsAndCheckRegressions(result, enableRag);

            // Prompt to apply changes if pipeline succeeded
            if (result.Success && result.Context.AppliedFiles?.Count > 0)
            {
                var applied = await PromptAndApplyChanges(result, workspace, autoApprove, preserveWorkspace);
                if (!applied)
                {
                    AnsiConsole.MarkupLine("[yellow]Changes not applied. Workspace preserved for review.[/]");
                }
            }
            else if (!preserveWorkspace)
            {
                // Clean up workspace for failed or no-change pipelines (unless preserveWorkspace is set)
                workspace.Dispose();
            }

            if (preserveWorkspace)
            {
                AnsiConsole.MarkupLine($"[dim]Workspace preserved at:[/] [cyan]{Markup.Escape(workspace.WorkspaceRoot)}[/]");
            }

            return result.Success ? 0 : 1;
        }
        catch (FileNotFoundException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            AnsiConsole.MarkupLine("[yellow]Hint:[/] Ensure all agent definitions exist in .agents/ directory");
            return 1;
        }
        catch (DirectoryNotFoundException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"[red]I/O error:[/] {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Invalid operation:[/] {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Finds the .agents directory, checking workspace first, then DevPilot defaults.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root path containing potential custom agents.</param>
    /// <param name="usingCustomAgents">Output parameter indicating if custom agents from workspace are being used.</param>
    /// <returns>The path to the .agents directory to use.</returns>
    private static string FindAgentsDirectory(string workspaceRoot, out bool usingCustomAgents)
    {
        // Priority 1: Workspace .agents/ (target repository custom agents)
        var workspaceAgentsPath = Path.Combine(workspaceRoot, ".agents");
        if (Directory.Exists(workspaceAgentsPath))
        {
            usingCustomAgents = true;
            AnsiConsole.MarkupLine("[yellow]Using custom agents from target repository[/]");
            return workspaceAgentsPath;
        }

        // Priority 2: Tool installation directory (DevPilot defaults)
        var toolDirectory = AppContext.BaseDirectory;
        var toolAgentsPath = Path.Combine(toolDirectory, ".agents");
        if (Directory.Exists(toolAgentsPath))
        {
            usingCustomAgents = false;
            AnsiConsole.MarkupLine("[dim]Using default DevPilot agents[/]");
            return toolAgentsPath;
        }

        // Priority 3: Current working directory (for running from source during development)
        var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), ".agents");
        if (Directory.Exists(currentDirPath))
        {
            usingCustomAgents = false;
            AnsiConsole.MarkupLine("[dim]Using DevPilot agents from current directory (development mode)[/]");
            return currentDirPath;
        }

        // If none exist, throw helpful error
        throw new DirectoryNotFoundException(
            $".agents directory not found. Searched:\n" +
            $"  - Workspace: {workspaceAgentsPath}\n" +
            $"  - Tool directory: {toolAgentsPath}\n" +
            $"  - Current directory: {currentDirPath}");
    }

    /// <summary>
    /// Builds the complete MASAI pipeline with all 5 agents from workspace or DevPilot defaults.
    /// </summary>
    /// <param name="workspace">The workspace manager containing the target repository files.</param>
    /// <param name="enableRag">Whether to enable RAG (Retrieval Augmented Generation) context retrieval.</param>
    /// <param name="preserveWorkspace">Whether to preserve workspace on failure for debugging.</param>
    /// <returns>The configured pipeline ready for execution.</returns>
    private static async Task<Pipeline> BuildPipelineAsync(WorkspaceManager workspace, SessionManager sessionManager, bool enableRag, bool preserveWorkspace)
    {
        // Find agents directory (workspace custom agents or DevPilot defaults)
        var agentsDirectory = FindAgentsDirectory(workspace.WorkspaceRoot, out var usingCustomAgents);

        var loader = new AgentLoader(agentsDirectory);

        // Agent name to pipeline stage mapping
        var agentMappings = new Dictionary<string, PipelineStage>
        {
            ["planner"] = PipelineStage.Planning,
            ["coder"] = PipelineStage.Coding,
            ["reviewer"] = PipelineStage.Reviewing,
            ["tester"] = PipelineStage.Testing,
            ["evaluator"] = PipelineStage.Evaluating
        };

        var agents = new Dictionary<PipelineStage, IAgent>();

        // Load all agent definitions and create agent instances
        foreach (var (agentName, stage) in agentMappings)
        {
            // If using custom agents, validate all 5 agents exist (all-or-nothing)
            if (usingCustomAgents && !Directory.Exists(Path.Combine(agentsDirectory, agentName)))
            {
                throw new DirectoryNotFoundException(
                    $"Custom agents must define all 5 agents. Missing: .agents/{agentName}/\n\n" +
                    $"Target repository has .agents/ directory but is missing required agent.\n" +
                    $"Either define all 5 agents (planner, coder, reviewer, tester, evaluator) or remove .agents/ to use DevPilot defaults.");
            }

            IAgent agent;

            // Use TestingAgent for Testing stage (real test execution)
            if (stage == PipelineStage.Testing)
            {
                agent = new TestingAgent();
            }
            else
            {
                // Use ClaudeCliAgent for all other stages (LLM-based)
                var definition = await loader.LoadAgentAsync(agentName);
                agent = new ClaudeCliAgent(definition);
            }

            agents[stage] = agent;
        }

        // Create RAG service if enabled
        IRagService? ragService = null;
        if (enableRag)
        {
            try
            {
                var ragOptions = RAGOptions.Default;
                var embeddingService = new OllamaEmbeddingService(
                    ragOptions.OllamaEndpoint,
                    ragOptions.EmbeddingModel,
                    ragOptions.EmbeddingDimension);

                var databasePath = Path.Combine(
                    workspace.WorkspaceRoot,
                    ".devpilot",
                    "rag",
                    $"{Path.GetFileName(workspace.WorkspaceRoot)}.db");

                var vectorStore = new SqliteVectorStore(databasePath);
                ragService = new RagService(embeddingService, vectorStore, ragOptions);

                AnsiConsole.MarkupLine("[green]✓ RAG enabled[/] [dim](Ollama endpoint: {0})[/]", ragOptions.OllamaEndpoint);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
            {
                AnsiConsole.MarkupLine("[yellow]⚠ RAG disabled:[/] {0}", ex.Message);
                AnsiConsole.MarkupLine("[dim]Install Ollama: https://ollama.com[/]");
                AnsiConsole.MarkupLine("[dim]Pull model: ollama pull mxbai-embed-large[/]");
                ragService = null;
            }
        }

        var sourceRoot = Directory.GetCurrentDirectory();
        var stateManager = new StateManager(sourceRoot);
        return new Pipeline(agents, workspace, sourceRoot, ragService, stateManager, sessionManager, preserveWorkspace);
    }

    /// <summary>
    /// Displays the results of the pipeline execution.
    /// </summary>
    private static void DisplayResults(PipelineResult result)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold]Pipeline Result[/]"));
        AnsiConsole.WriteLine();

        if (result.RequiresApproval)
        {
            DisplayAwaitingApproval(result);
        }
        else if (result.Success)
        {
            DisplaySuccess(result);
        }
        else
        {
            DisplayFailure(result);
        }

        // Display execution details
        AnsiConsole.WriteLine();
        var detailsTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Detail")
            .AddColumn("Value");

        detailsTable.AddRow("Pipeline ID", result.Context.PipelineId);
        detailsTable.AddRow("Duration", $"{result.Duration.TotalSeconds:F1}s");
        detailsTable.AddRow("Final Stage", result.FinalStage.ToString());
        detailsTable.AddRow("Started", result.Context.StartedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"));

        AnsiConsole.Write(detailsTable);
    }

    /// <summary>
    /// Displays results for a successful pipeline execution.
    /// </summary>
    private static void DisplaySuccess(PipelineResult result)
    {
        // Check if pipeline has warnings (test failures)
        if (result.Context.HasTestFailures)
        {
            AnsiConsole.MarkupLine("[yellow bold]⚠ Pipeline completed with warnings[/]");
            var warningMsg = Markup.Escape(result.ErrorMessage ?? "Unknown warning");
            AnsiConsole.MarkupLine($"[yellow]{warningMsg}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[green bold]✓ Pipeline completed successfully[/]");
        }
        AnsiConsole.WriteLine();

        // Try to parse and display evaluation scores if available
        if (!string.IsNullOrEmpty(result.Context.Scores))
        {
            try
            {
                var scores = System.Text.Json.JsonDocument.Parse(result.Context.Scores);
                var evaluation = scores.RootElement.GetProperty("evaluation");

                var overallScore = evaluation.GetProperty("overall_score").GetDouble();
                var verdict = evaluation.GetProperty("final_verdict").GetString() ?? "UNKNOWN";

                var panel = new Panel(new Markup($"""
                    [bold]Overall Score:[/] {overallScore:F1}/10
                    [bold]Final Verdict:[/] [green]{verdict}[/]
                    """))
                {
                    Header = new PanelHeader("Quality Assessment"),
                    Border = BoxBorder.Rounded
                };

                AnsiConsole.Write(panel);
                AnsiConsole.WriteLine();

                // Display dimension scores
                var scoresObj = evaluation.GetProperty("scores");
                var scoresTable = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("Dimension")
                    .AddColumn(new TableColumn("Score").RightAligned());

                scoresTable.AddRow("Plan Quality", $"{scoresObj.GetProperty("plan_quality").GetDouble():F1}");
                scoresTable.AddRow("Code Quality", $"{scoresObj.GetProperty("code_quality").GetDouble():F1}");
                scoresTable.AddRow("Test Coverage", $"{scoresObj.GetProperty("test_coverage").GetDouble():F1}");
                scoresTable.AddRow("Documentation", $"{scoresObj.GetProperty("documentation").GetDouble():F1}");
                scoresTable.AddRow("Maintainability", $"{scoresObj.GetProperty("maintainability").GetDouble():F1}");

                AnsiConsole.Write(scoresTable);
            }
            catch (System.Text.Json.JsonException)
            {
                // If parsing fails, just show raw scores (use WriteLine to avoid markup parsing issues)
                AnsiConsole.WriteLine($"Scores: {result.Context.Scores}");
            }
            catch (KeyNotFoundException)
            {
                // Missing expected JSON properties, show raw scores
                AnsiConsole.WriteLine($"Scores: {result.Context.Scores}");
            }
        }
    }

    /// <summary>
    /// Displays results for a pipeline awaiting approval.
    /// </summary>
    private static void DisplayAwaitingApproval(PipelineResult result)
    {
        AnsiConsole.MarkupLine("[yellow bold]⏸ Pipeline awaiting approval[/]");
        AnsiConsole.WriteLine();

        var panel = new Panel(new Markup($"""
            [bold]Reason:[/] {result.Context.ApprovalReason ?? "Unknown"}
            [bold]Pipeline ID:[/] {result.Context.PipelineId}

            [dim]Pipeline state has been saved. Use the following commands:[/]
            [cyan]devpilot review {result.Context.PipelineId}[/] - Review changes
            [cyan]devpilot approve {result.Context.PipelineId}[/] - Apply changes
            [cyan]devpilot reject {result.Context.PipelineId}[/] - Discard changes
            """))
        {
            Header = new PanelHeader("Approval Required", Justify.Left),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Yellow)
        };

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Displays results for a failed pipeline execution.
    /// </summary>
    private static void DisplayFailure(PipelineResult result)
    {
        AnsiConsole.MarkupLine("[red bold]✗ Pipeline failed[/]");
        AnsiConsole.WriteLine();

        var errorMessage = Markup.Escape(result.ErrorMessage ?? "Unknown error");
        var panel = new Panel(new Markup($"""
            [bold]Stage:[/] {result.FinalStage}
            [bold]Error:[/] {errorMessage}
            """))
        {
            Header = new PanelHeader("Failure Details", Justify.Left),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Red)
        };

        AnsiConsole.Write(panel);

        // Display workspace path for inspection
        if (!string.IsNullOrEmpty(result.Context.WorkspaceRoot))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Workspace preserved for inspection at:[/] [cyan]{Markup.Escape(result.Context.WorkspaceRoot)}[/]");
        }

        // Display stage history
        if (result.Context.StageHistory.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Stage History:[/]");

            var historyTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Stage")
                .AddColumn("Entered At");

            foreach (var entry in result.Context.StageHistory)
            {
                historyTable.AddRow(
                    entry.Stage.ToString(),
                    entry.EnteredAt.ToString("HH:mm:ss.fff"));
            }

            AnsiConsole.Write(historyTable);
        }
    }

    /// <summary>
    /// Prompts the user to apply changes and copies files from workspace to project.
    /// </summary>
    /// <param name="result">The pipeline result containing changes to apply.</param>
    /// <param name="workspace">The workspace manager containing the files.</param>
    /// <param name="autoApprove">If true, skips prompts and auto-applies changes.</param>
    /// <param name="preserveWorkspace">If true, skips workspace cleanup after applying changes.</param>
    private static async Task<bool> PromptAndApplyChanges(PipelineResult result, WorkspaceManager workspace, bool autoApprove, bool preserveWorkspace)
    {
        if (result.Context.WorkspaceRoot == null || result.Context.AppliedFiles == null)
        {
            return false;
        }

        try
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold]Apply Changes[/]"));
            AnsiConsole.WriteLine();

            // Display file change summary
            var changeTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("File").Width(60))
                .AddColumn(new TableColumn("Status").RightAligned());

            foreach (var file in result.Context.AppliedFiles)
            {
                var fullPath = Path.Combine(result.Context.WorkspaceRoot, file);
                var exists = File.Exists(file);
                var status = exists ? "[yellow]Modified[/]" : "[green]Created[/]";
                changeTable.AddRow(file, status);
            }

            AnsiConsole.Write(changeTable);
            AnsiConsole.WriteLine();

            // Prompt to view diffs (skip if auto-approve)
            var viewDiffs = autoApprove ? false : AnsiConsole.Confirm("View diffs before applying?", defaultValue: true);

            if (viewDiffs)
            {
                var sourceRoot = Directory.GetCurrentDirectory();

                foreach (var file in result.Context.AppliedFiles)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(new Rule($"[bold cyan]{file}[/]"));

                    var diff = workspace.GenerateFileDiff(file, sourceRoot);
                    if (diff != null)
                    {
                        // Display diff in a panel with syntax highlighting
                        var diffPanel = new Panel(Markup.Escape(diff))
                        {
                            Border = BoxBorder.Rounded,
                            BorderStyle = new Style(Color.Grey),
                            Padding = new Padding(1, 0, 1, 0)
                        };

                        AnsiConsole.Write(diffPanel);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[dim](file not found in workspace)[/]");
                    }
                }

                AnsiConsole.WriteLine();
            }

            // Prompt for confirmation (auto-approve if --yes flag provided)
            bool shouldApply;
            if (autoApprove)
            {
                shouldApply = true;
            }
            else
            {
                try
                {
                    shouldApply = AnsiConsole.Confirm("Apply these changes to your project?", defaultValue: true);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("non-interactive"))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[red]ERROR:[/] Cannot prompt for confirmation in non-interactive mode.");
                    AnsiConsole.MarkupLine("[yellow]Hint:[/] Run DevPilot directly in a terminal (not piped or redirected).");
                    return false;
                }
            }

            if (!shouldApply)
            {
                return false;
            }

            // Copy files from workspace to project directory
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Applying changes...", async ctx =>
                {
                    foreach (var file in result.Context.AppliedFiles)
                    {
                        var sourcePath = Path.Combine(result.Context.WorkspaceRoot, file);
                        var destPath = file;

                        // Ensure destination directory exists
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        // Copy file
                        File.Copy(sourcePath, destPath, overwrite: true);
                        ctx.Status($"Copied {file}");
                    }

                    await Task.CompletedTask;
                });

            AnsiConsole.MarkupLine("[green]✓ Changes applied successfully[/]");
            AnsiConsole.WriteLine();

            return true;
        }
        finally
        {
            // Clean up workspace after user decision (apply or decline) unless preserveWorkspace is set
            if (!preserveWorkspace && Directory.Exists(result.Context.WorkspaceRoot))
            {
                try
                {
                    Directory.Delete(result.Context.WorkspaceRoot, recursive: true);
                }
                catch (IOException)
                {
                    // Directory in use or locked
                    AnsiConsole.MarkupLine($"[dim]Note: Failed to clean up workspace at {result.Context.WorkspaceRoot}[/]");
                }
                catch (UnauthorizedAccessException)
                {
                    // Insufficient permissions
                    AnsiConsole.MarkupLine($"[dim]Note: Failed to clean up workspace at {result.Context.WorkspaceRoot}[/]");
                }
            }
        }
    }

    /// <summary>
    /// Tracks pipeline metrics and checks for quality regressions.
    /// </summary>
    private static void TrackMetricsAndCheckRegressions(PipelineResult result, bool ragEnabled)
    {
        try
        {
            using var tracker = BaselineTracker.Create();

            // Extract metrics from pipeline result
            var metrics = MetricsCollector.ExtractMetrics(result, ragEnabled);

            // Record and check for regressions
            var report = MetricsCollector.RecordAndCheck(metrics, tracker);

            // Display regression alert if detected
            if (report.HasRegression)
            {
                AnsiConsole.WriteLine();
                var panel = new Panel(new Markup($"""
                    [bold yellow]{report.Message}[/]

                    {string.Join("\n", report.Regressions.Select(r => $"[yellow]•[/] {r}"))}

                    [dim]Baseline score: {report.BaselineScore:F1}/10 (last 30 days)[/]
                    [dim]Current score:  {report.CurrentScore:F1}/10[/]

                    [dim italic]💡 Review recent changes that may have impacted pipeline quality.[/]
                    """))
                {
                    Header = new PanelHeader("⚠ Regression Detection", Justify.Left),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Yellow)
                };

                AnsiConsole.Write(panel);
            }
            else if (!string.IsNullOrEmpty(report.Message))
            {
                // Show positive feedback
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]{report.Message}[/]");
            }
        }
        catch (Exception ex)
        {
            // Telemetry is non-critical - don't fail pipeline if it errors
            AnsiConsole.MarkupLine($"[dim yellow]Note: Failed to track metrics ({ex.Message})[/]");
        }
    }

    /// <summary>
    /// Handles the diagnose command.
    /// </summary>
    private static async Task<int> HandleDiagnoseCommandAsync(string[] args)
    {
        // Display header
        AnsiConsole.Write(
            new FigletText("DevPilot")
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[dim]Diagnostic Tools[/]");
        AnsiConsole.WriteLine();

        if (args.Length < 2)
        {
            DisplayDiagnoseUsage();
            return 1;
        }

        var diagnosticType = args[1].ToLowerInvariant();

        DiagnosticResult result;

        try
        {
            result = diagnosticType switch
            {
                "tests" or "test" => await TestDiagnostics.RunAsync(),
                "build" => await BuildDiagnostics.RunAsync(),
                "workspace" or "workspaces" => await WorkspaceDiagnostics.RunAsync(),
                "ci" => await CiDiagnostics.RunAsync(),
                _ => throw new ArgumentException($"Unknown diagnostic type: {diagnosticType}")
            };

            DiagnosticFormatter.Display(result);
            return result.HasIssues ? 1 : 0;
        }
        catch (ArgumentException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            AnsiConsole.WriteLine();
            DisplayDiagnoseUsage();
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Diagnostic failed:[/] {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the cleanup command.
    /// </summary>
    private static async Task<int> HandleCleanupCommandAsync(string[] args)
    {
        // Display header
        AnsiConsole.Write(
            new FigletText("DevPilot")
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[dim]Workspace Cleanup[/]");
        AnsiConsole.WriteLine();

        // Parse flags
        bool force = args.Contains("--force") || args.Contains("-f");
        bool dryRun = args.Contains("--dry-run") || args.Contains("-d");

        try
        {
            var result = await WorkspaceCleanup.RunAsync(
                force: force,
                dryRun: dryRun);

            // Display result
            var color = result.DryRun ? "yellow" : result.WorkspacesDeleted > 0 ? "green" : "blue";
            var icon = result.DryRun ? "👁" : result.WorkspacesDeleted > 0 ? "✓" : "ℹ";

            AnsiConsole.MarkupLine($"[{color}]{icon} {Markup.Escape(result.Message)}[/]");
            AnsiConsole.WriteLine();

            if (result.Errors?.Count > 0)
            {
                AnsiConsole.MarkupLine("[red]Errors encountered:[/]");
                foreach (var error in result.Errors)
                {
                    AnsiConsole.MarkupLine($"  [red]•[/] {Markup.Escape(error)}");
                }
                AnsiConsole.WriteLine();
            }

            // Show summary
            var summaryTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Metric")
                .AddColumn(new TableColumn("Value").RightAligned());

            summaryTable.AddRow("Workspaces " + (dryRun ? "to delete" : "deleted"), result.WorkspacesDeleted.ToString());
            summaryTable.AddRow("Processes terminated", result.ProcessesTerminated.ToString());
            summaryTable.AddRow("Space " + (dryRun ? "to free" : "freed"), $"{result.BytesFreed / (1024 * 1024)} MB");
            summaryTable.AddRow("Duration", $"{result.Duration.TotalSeconds:F1}s");

            AnsiConsole.Write(summaryTable);

            return result.WorkspacesDeleted > 0 || result.DryRun ? 0 : 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Cleanup failed:[/] {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Displays usage information for the diagnose command.
    /// </summary>
    private static void DisplayDiagnoseUsage()
    {
        var usage = new Panel(new Markup("""
            [bold]Usage:[/]
              devpilot diagnose <type>

            [bold]Diagnostic Types:[/]
              tests      - Analyze test failures and identify patterns
              build      - Categorize and prioritize build errors
              workspace  - Identify locked files and orphaned workspaces
              ci         - Parse GitHub Actions logs for failures

            [bold]Examples:[/]
              devpilot diagnose tests      # Analyze test failures
              devpilot diagnose build      # Check for build errors
              devpilot diagnose workspace  # Find locked/orphaned workspaces
              devpilot diagnose ci         # Check latest CI run failures
            """))
        {
            Header = new PanelHeader("Diagnose Command", Justify.Center),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(usage);
    }

    /// <summary>
    /// Displays usage information.
    /// </summary>
    private static void DisplayUsage()
    {
        AnsiConsole.Write(
            new FigletText("DevPilot")
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[dim]MASAI Pipeline - Automated Code Generation & Review[/]");
        AnsiConsole.WriteLine();

        var usage = new Panel(new Markup("""
            [bold]Usage:[/]
              devpilot "<request>"
              devpilot diagnose <type>
              devpilot cleanup [[--force]] [[--dry-run]]
              devpilot install-hook [[--force]]
              devpilot uninstall-hook

            [bold]State Management:[/]
              devpilot list                 - List all saved pipeline states
              devpilot review <pipeline-id> - Review changes without applying
              devpilot resume <pipeline-id> - Resume a paused pipeline
              devpilot approve <pipeline-id> - Approve and apply changes
              devpilot reject <pipeline-id>  - Reject and discard changes
              devpilot cleanup-states [[days]] - Delete old states (default: 7 days)

            [bold]Examples:[/]
              devpilot "Create Calculator class with Add and Subtract methods"
              devpilot "Add error handling to UserService"
              devpilot "Refactor PaymentProcessor to use dependency injection"
              devpilot list                 # View all pipeline states
              devpilot review abc123        # Review changes for pipeline abc123
              devpilot approve abc123       # Apply changes from pipeline abc123

            [bold]Pipeline Stages:[/]
              1. Planning    - Analyzes request, creates execution plan
              2. Coding      - Generates unified diff patch
              3. Reviewing   - Validates code quality and correctness
              4. Testing     - Executes tests and reports results
              5. Evaluating  - Scores overall quality and provides verdict

            [bold]Quality Tools:[/]
              diagnose tests      - Analyze test failures
              diagnose workspace  - Check workspace health
              cleanup             - Remove orphaned workspaces
              install-hook        - Add pre-commit quality checks
            """))
        {
            Header = new PanelHeader("DevPilot CLI", Justify.Center),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(usage);
    }

    /// <summary>
    /// Handles the 'install-hook' command.
    /// </summary>
    private static int HandleInstallHookCommand(string[] args)
    {
        try
        {
            // Display header
            AnsiConsole.Write(new FigletText("DevPilot").Color(Color.Blue));
            AnsiConsole.MarkupLine("[dim]Git Hook Installation[/]");
            AnsiConsole.WriteLine();

            // Parse flags
            bool force = args.Contains("--force") || args.Contains("-f");

            // Install hook
            var result = HookInstaller.Install(force: force);

            if (result.Success)
            {
                AnsiConsole.MarkupLine($"[green]✓ {Markup.Escape(result.Message)}[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]The pre-commit hook will run 'devpilot diagnose tests' before each commit.[/]");
                AnsiConsole.MarkupLine("[dim]To skip the hook, use 'git commit --no-verify'[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(result.Message)}[/]");
                return 1;
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error installing hook: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Handles the 'uninstall-hook' command.
    /// </summary>
    private static int HandleUninstallHookCommand(string[] args)
    {
        try
        {
            // Display header
            AnsiConsole.Write(new FigletText("DevPilot").Color(Color.Blue));
            AnsiConsole.MarkupLine("[dim]Git Hook Removal[/]");
            AnsiConsole.WriteLine();

            // Uninstall hook
            var result = HookInstaller.Uninstall();

            if (result.Success)
            {
                AnsiConsole.MarkupLine($"[green]✓ {Markup.Escape(result.Message)}[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(result.Message)}[/]");
                return 1;
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error removing hook: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Handles the 'list' command - displays all saved pipeline states.
    /// </summary>
    private static async Task<int> HandleListCommandAsync(string[] args)
    {
        try
        {
            // Display header
            AnsiConsole.Write(new FigletText("DevPilot").Color(Color.Blue));
            AnsiConsole.MarkupLine("[dim]Pipeline States[/]");
            AnsiConsole.WriteLine();

            var stateManager = new StateManager();
            var states = await stateManager.ListStatesAsync();

            if (states.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No saved pipeline states found.[/]");
                return 0;
            }

            // Display states in a table
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Pipeline ID")
                .AddColumn("Request")
                .AddColumn("Status")
                .AddColumn("Stage")
                .AddColumn("Timestamp");

            foreach (var state in states)
            {
                var statusColor = state.Status switch
                {
                    PipelineStatus.Completed => "green",
                    PipelineStatus.Approved => "cyan",
                    PipelineStatus.AwaitingApproval => "yellow",
                    PipelineStatus.Failed => "red",
                    PipelineStatus.Rejected => "grey",
                    PipelineStatus.Running => "blue",
                    _ => "white"
                };

                // Truncate request if too long
                var request = state.UserRequest.Length > 50
                    ? state.UserRequest.Substring(0, 47) + "..."
                    : state.UserRequest;

                table.AddRow(
                    state.PipelineId.Substring(0, 8),
                    Markup.Escape(request),
                    $"[{statusColor}]{state.Status}[/]",
                    state.CurrentStage.ToString(),
                    state.Timestamp.ToString("yyyy-MM-dd HH:mm"));
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Total: {states.Count} pipeline(s)[/]");

            return 0;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error listing states: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Handles the 'review' command - displays patch diff without applying changes.
    /// </summary>
    private static async Task<int> HandleReviewCommandAsync(string[] args)
    {
        try
        {
            // Display header
            AnsiConsole.Write(new FigletText("DevPilot").Color(Color.Blue));
            AnsiConsole.MarkupLine("[dim]Review Pipeline Changes[/]");
            AnsiConsole.WriteLine();

            if (args.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Pipeline ID required");
                AnsiConsole.MarkupLine("[dim]Usage: devpilot review <pipeline-id>[/]");
                return 1;
            }

            var pipelineId = args[1];
            var stateManager = new StateManager();
            var state = await stateManager.LoadStateAsync(pipelineId);

            if (state == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Pipeline state not found: {pipelineId}");
                return 1;
            }

            // Display pipeline info
            var infoPanel = new Panel(new Markup($"""
                [bold]Request:[/] {Markup.Escape(state.UserRequest)}
                [bold]Status:[/] {state.Status}
                [bold]Stage:[/] {state.CurrentStage}
                [bold]Started:[/] {state.Timestamp:yyyy-MM-dd HH:mm:ss}
                """))
            {
                Header = new PanelHeader("Pipeline Information"),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(infoPanel);
            AnsiConsole.WriteLine();

            // Display patch if available
            if (!string.IsNullOrEmpty(state.Patch))
            {
                AnsiConsole.Write(new Rule("[bold]Patch Preview[/]"));
                AnsiConsole.WriteLine();

                var patchPanel = new Panel(Markup.Escape(state.Patch))
                {
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Grey),
                    Padding = new Padding(1, 0, 1, 0)
                };

                AnsiConsole.Write(patchPanel);
                AnsiConsole.WriteLine();
            }

            // Display scores if available
            if (!string.IsNullOrEmpty(state.Scores))
            {
                AnsiConsole.Write(new Rule("[bold]Quality Scores[/]"));
                AnsiConsole.WriteLine();

                try
                {
                    var scores = System.Text.Json.JsonDocument.Parse(state.Scores);
                    var evaluation = scores.RootElement.GetProperty("evaluation");
                    var overallScore = evaluation.GetProperty("overall_score").GetDouble();
                    var verdict = evaluation.GetProperty("final_verdict").GetString() ?? "UNKNOWN";

                    var scoresPanel = new Panel(new Markup($"""
                        [bold]Overall Score:[/] {overallScore:F1}/10
                        [bold]Final Verdict:[/] [green]{verdict}[/]
                        """))
                    {
                        Header = new PanelHeader("Quality Assessment"),
                        Border = BoxBorder.Rounded
                    };

                    AnsiConsole.Write(scoresPanel);
                }
                catch (System.Text.Json.JsonException)
                {
                    AnsiConsole.WriteLine($"Scores: {state.Scores}");
                }
            }

            return 0;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error reviewing state: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Handles the 'resume' command - loads saved state and continues pipeline execution.
    /// </summary>
    private static async Task<int> HandleResumeCommandAsync(string[] args)
    {
        try
        {
            // Display header
            AnsiConsole.Write(new FigletText("DevPilot").Color(Color.Blue));
            AnsiConsole.MarkupLine("[dim]Resume Pipeline Execution[/]");
            AnsiConsole.WriteLine();

            if (args.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Pipeline ID required");
                AnsiConsole.MarkupLine("[dim]Usage: devpilot resume <pipeline-id>[/]");
                return 1;
            }

            var pipelineId = args[1];
            var stateManager = new StateManager();
            var state = await stateManager.LoadStateAsync(pipelineId);

            if (state == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Pipeline state not found: {pipelineId}");
                return 1;
            }

            // Check if state can be resumed
            if (state.Status == PipelineStatus.Completed)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] Pipeline already completed");
                return 0;
            }

            if (state.Status == PipelineStatus.Approved)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] Pipeline already approved and applied");
                return 0;
            }

            if (state.Status == PipelineStatus.Rejected)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] Pipeline was rejected");
                return 0;
            }

            if (state.Status == PipelineStatus.Failed)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] Pipeline failed and cannot be resumed");
                AnsiConsole.MarkupLine($"[dim]Error: {Markup.Escape(state.ErrorMessage ?? "Unknown error")}[/]");
                return 1;
            }

            // TODO: Implement pipeline resume logic
            // This requires:
            // 1. Reconstructing PipelineContext from saved state
            // 2. Reloading workspace from WorkspacePath
            // 3. Continuing from CurrentStage
            AnsiConsole.MarkupLine("[yellow]⚠ Resume functionality not yet implemented[/]");
            AnsiConsole.MarkupLine("[dim]This feature requires reconstructing pipeline context and continuing from last stage.[/]");
            AnsiConsole.MarkupLine($"[dim]Pipeline ID: {state.PipelineId}[/]");
            AnsiConsole.MarkupLine($"[dim]Last Stage: {state.CurrentStage}[/]");

            return 1;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error resuming pipeline: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Handles the 'approve' command - applies patch to source repository and marks state as approved.
    /// </summary>
    private static async Task<int> HandleApproveCommandAsync(string[] args)
    {
        try
        {
            // Display header
            AnsiConsole.Write(new FigletText("DevPilot").Color(Color.Blue));
            AnsiConsole.MarkupLine("[dim]Approve Pipeline Changes[/]");
            AnsiConsole.WriteLine();

            if (args.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Pipeline ID required");
                AnsiConsole.MarkupLine("[dim]Usage: devpilot approve <pipeline-id>[/]");
                return 1;
            }

            var pipelineId = args[1];
            var stateManager = new StateManager();
            var state = await stateManager.LoadStateAsync(pipelineId);

            if (state == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Pipeline state not found: {pipelineId}");
                return 1;
            }

            // Check if state can be approved
            if (state.Status != PipelineStatus.AwaitingApproval)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Pipeline is not awaiting approval (status: {state.Status})");
                return 1;
            }

            // Display patch summary
            AnsiConsole.MarkupLine($"[bold]Request:[/] {Markup.Escape(state.UserRequest)}");
            AnsiConsole.WriteLine();

            if (!string.IsNullOrEmpty(state.Patch))
            {
                var patchPanel = new Panel(Markup.Escape(state.Patch.Length > 500
                    ? state.Patch.Substring(0, 500) + "\n... (truncated)"
                    : state.Patch))
                {
                    Header = new PanelHeader("Patch Preview"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Grey)
                };

                AnsiConsole.Write(patchPanel);
                AnsiConsole.WriteLine();
            }

            // TODO: Implement patch application logic
            // This requires:
            // 1. Applying patch from workspace to source repository
            // 2. Copying files from WorkspacePath to SourceRoot
            // 3. Cleaning up workspace
            AnsiConsole.MarkupLine("[yellow]⚠ Approve functionality not yet implemented[/]");
            AnsiConsole.MarkupLine("[dim]This feature requires applying workspace changes to source repository.[/]");
            AnsiConsole.MarkupLine($"[dim]Workspace: {Markup.Escape(state.WorkspacePath)}[/]");
            AnsiConsole.MarkupLine($"[dim]Source: {Markup.Escape(state.SourceRoot ?? "Unknown")}[/]");

            return 1;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error approving pipeline: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Handles the 'reject' command - marks state as rejected and cleans up workspace.
    /// </summary>
    private static async Task<int> HandleRejectCommandAsync(string[] args)
    {
        try
        {
            // Display header
            AnsiConsole.Write(new FigletText("DevPilot").Color(Color.Blue));
            AnsiConsole.MarkupLine("[dim]Reject Pipeline Changes[/]");
            AnsiConsole.WriteLine();

            if (args.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Pipeline ID required");
                AnsiConsole.MarkupLine("[dim]Usage: devpilot reject <pipeline-id>[/]");
                return 1;
            }

            var pipelineId = args[1];
            var stateManager = new StateManager();
            var state = await stateManager.LoadStateAsync(pipelineId);

            if (state == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Pipeline state not found: {pipelineId}");
                return 1;
            }

            // Check if state can be rejected
            if (state.Status != PipelineStatus.AwaitingApproval)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Pipeline is not awaiting approval (status: {state.Status})");
                return 1;
            }

            // Confirm rejection
            var confirmed = AnsiConsole.Confirm($"Reject pipeline '{state.UserRequest.Substring(0, Math.Min(50, state.UserRequest.Length))}'?", defaultValue: false);

            if (!confirmed)
            {
                AnsiConsole.MarkupLine("[yellow]Rejection cancelled[/]");
                return 0;
            }

            // Update status to rejected
            var updated = await stateManager.UpdateStatusAsync(
                pipelineId,
                PipelineStatus.Rejected,
                DateTime.UtcNow);

            if (!updated)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Failed to update pipeline status");
                return 1;
            }

            // Clean up workspace if it exists
            if (Directory.Exists(state.WorkspacePath))
            {
                try
                {
                    Directory.Delete(state.WorkspacePath, recursive: true);
                    AnsiConsole.MarkupLine($"[green]✓ Workspace deleted:[/] [dim]{Markup.Escape(state.WorkspacePath)}[/]");
                }
                catch (IOException ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠ Failed to delete workspace: {Markup.Escape(ex.Message)}[/]");
                }
            }

            AnsiConsole.MarkupLine("[green]✓ Pipeline rejected successfully[/]");

            return 0;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error rejecting pipeline: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Handles the 'cleanup-states' command - deletes pipeline states older than specified days.
    /// </summary>
    private static async Task<int> HandleCleanupStatesCommandAsync(string[] args)
    {
        try
        {
            // Display header
            AnsiConsole.Write(new FigletText("DevPilot").Color(Color.Blue));
            AnsiConsole.MarkupLine("[dim]Cleanup Old Pipeline States[/]");
            AnsiConsole.WriteLine();

            // Parse max age (default: 7 days)
            int maxAgeDays = 7;
            if (args.Length > 1 && int.TryParse(args[1], out var parsedDays))
            {
                maxAgeDays = parsedDays;
            }

            var stateManager = new StateManager();
            var deletedCount = await stateManager.CleanupOldStatesAsync(maxAgeDays);

            if (deletedCount > 0)
            {
                AnsiConsole.MarkupLine($"[green]✓ Deleted {deletedCount} pipeline state(s) older than {maxAgeDays} days[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[blue]No pipeline states older than {maxAgeDays} days found[/]");
            }

            return 0;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error cleaning up states: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
#pragma warning restore CA1031
    }
}
