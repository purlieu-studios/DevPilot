using DevPilot.Agents;
using DevPilot.Core;
using DevPilot.Orchestrator;
using DevPilot.RAG;
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
            // Parse command-line arguments first to check for flags
            bool autoApprove = false;
            bool enableRag = false;
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

            // Generate pipeline ID early (before workspace creation)
            var pipelineId = Guid.NewGuid().ToString();

            // Create workspace and copy target repository files
            WorkspaceManager workspace;
            try
            {
                workspace = WorkspaceManager.CreateWorkspace(pipelineId);

                // Copy domain files (CLAUDE.md, .agents/, docs/, src/, tests/ + devpilot.json configured folders)
                var sourceRoot = Directory.GetCurrentDirectory();
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
            var pipeline = await BuildPipelineAsync(workspace, enableRag);

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

            // Prompt to apply changes if pipeline succeeded
            if (result.Success && result.Context.AppliedFiles?.Count > 0)
            {
                var applied = await PromptAndApplyChanges(result, workspace, autoApprove);
                if (!applied)
                {
                    AnsiConsole.MarkupLine("[yellow]Changes not applied. Workspace preserved for review.[/]");
                }
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
    /// <returns>The configured pipeline ready for execution.</returns>
    private static async Task<Pipeline> BuildPipelineAsync(WorkspaceManager workspace, bool enableRag)
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
        return new Pipeline(agents, workspace, sourceRoot, ragService);
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

            [dim]Note: State persistence not yet implemented.
            The pipeline cannot be resumed via CLI at this time.[/]
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
    private static async Task<bool> PromptAndApplyChanges(PipelineResult result, WorkspaceManager workspace, bool autoApprove)
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
            // Clean up workspace after user decision (apply or decline)
            if (Directory.Exists(result.Context.WorkspaceRoot))
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

            [bold]Examples:[/]
              devpilot "Create Calculator class with Add and Subtract methods"
              devpilot "Add error handling to UserService"
              devpilot "Refactor PaymentProcessor to use dependency injection"

            [bold]Pipeline Stages:[/]
              1. Planning    - Analyzes request, creates execution plan
              2. Coding      - Generates unified diff patch
              3. Reviewing   - Validates code quality and correctness
              4. Testing     - Executes tests and reports results
              5. Evaluating  - Scores overall quality and provides verdict
            """))
        {
            Header = new PanelHeader("DevPilot CLI", Justify.Center),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(usage);
    }
}
