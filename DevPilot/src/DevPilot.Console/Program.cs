using DevPilot.Agents;
using DevPilot.Core;
using DevPilot.Orchestrator;
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
            // Parse command-line arguments
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                DisplayUsage();
                return 1;
            }

            var userRequest = args[0];

            // Display header
            AnsiConsole.Write(
                new FigletText("DevPilot")
                    .Color(Color.Blue));

            AnsiConsole.MarkupLine("[dim]MASAI Pipeline - Automated Code Generation & Review[/]");
            AnsiConsole.WriteLine();

            // Load agents and build pipeline
            var pipeline = await BuildPipelineAsync();

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
                var applied = await PromptAndApplyChanges(result);
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
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Fatal error:[/] {ex.Message}");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    /// <summary>
    /// Finds the .agents directory, checking tool installation directory first, then current directory.
    /// </summary>
    private static string FindAgentsDirectory()
    {
        // Option 1: Tool installation directory (for global tool usage)
        var toolDirectory = AppContext.BaseDirectory;
        var toolAgentsPath = Path.Combine(toolDirectory, ".agents");
        if (Directory.Exists(toolAgentsPath))
        {
            return toolAgentsPath;
        }

        // Option 2: Current working directory (for running from source during development)
        var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), ".agents");
        if (Directory.Exists(currentDirPath))
        {
            return currentDirPath;
        }

        // If neither exists, throw helpful error
        throw new DirectoryNotFoundException(
            $".agents directory not found. Searched:\n" +
            $"  - Tool directory: {toolAgentsPath}\n" +
            $"  - Current directory: {currentDirPath}");
    }

    /// <summary>
    /// Builds the complete MASAI pipeline with all 5 agents.
    /// </summary>
    private static async Task<Pipeline> BuildPipelineAsync()
    {
        // Load agents from tool installation directory (for global tool usage)
        // Falls back to current directory if running from source
        var agentsDirectory = FindAgentsDirectory();

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

        return new Pipeline(agents);
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
        AnsiConsole.MarkupLine("[green bold]✓ Pipeline completed successfully[/]");
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
            catch
            {
                // If parsing fails, just show raw scores (use WriteLine to avoid markup parsing issues)
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
    private static async Task<bool> PromptAndApplyChanges(PipelineResult result)
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

            // Prompt for confirmation
            if (!AnsiConsole.Confirm("Apply these changes to your project?", defaultValue: true))
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
                catch
                {
                    // Ignore cleanup failures
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
