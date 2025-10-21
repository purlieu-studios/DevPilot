using DevPilot.Core;
using DevPilot.Orchestrator.CodeAnalysis;
using DevPilot.Orchestrator.State;
using DevPilot.Orchestrator.Validation;
using DevPilot.RAG;
using Spectre.Console;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DevPilot.Orchestrator;

/// <summary>
/// Orchestrates execution of the 5-agent MASAI pipeline: Planner → Coder → Reviewer → Tester → Evaluator.
/// </summary>
public sealed class Pipeline
{
    private const int MaxRevisionIterations = 2;
    private readonly IReadOnlyDictionary<PipelineStage, IAgent> _agents;
    private readonly WorkspaceManager _workspace;
    private readonly IRagService? _ragService;
    private readonly StateManager _stateManager;
    private readonly CodeAnalyzer _codeAnalyzer;
    private readonly string _sourceRoot;
    private readonly bool _preserveWorkspace;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pipeline"/> class.
    /// </summary>
    /// <param name="agents">Dictionary mapping pipeline stages to agent implementations.</param>
    /// <param name="workspace">The workspace manager for this pipeline execution.</param>
    /// <param name="sourceRoot">The source repository root directory (where DevPilot was executed from).</param>
    /// <param name="ragService">Optional RAG service for context retrieval (null to disable RAG).</param>
    /// <param name="stateManager">Optional state manager for persisting pipeline state (null to disable state persistence).</param>
    /// <param name="preserveWorkspace">If true, preserves workspace on failure for debugging (default: false).</param>
    public Pipeline(
        IReadOnlyDictionary<PipelineStage, IAgent> agents,
        WorkspaceManager workspace,
        string sourceRoot,
        IRagService? ragService = null,
        StateManager? stateManager = null,
        bool preserveWorkspace = false)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);

        ValidateAgents(agents);
        _agents = agents;
        _workspace = workspace;
        _sourceRoot = sourceRoot;
        _ragService = ragService;
        _stateManager = stateManager ?? new StateManager(sourceRoot);
        _codeAnalyzer = new CodeAnalyzer();
        _preserveWorkspace = preserveWorkspace;
    }

    /// <summary>
    /// Executes the complete MASAI pipeline for a user request.
    /// </summary>
    /// <param name="userRequest">The user's request to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final pipeline result.</returns>
    public async Task<PipelineResult> ExecuteAsync(string userRequest, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userRequest);

        var stopwatch = Stopwatch.StartNew();
        var context = new PipelineContext
        {
            UserRequest = userRequest,
            PipelineId = _workspace.PipelineId
        };
        bool success = false;

        try
        {
            // Use the workspace and source root provided in constructor
            context.SetWorkspaceRoot(_workspace.WorkspaceRoot);
            context.SetSourceRoot(_sourceRoot);

            // Analyze project structure for agent context
            var projectStructure = _workspace.AnalyzeProjectStructure();
            context.SetProjectStructure(projectStructure);

            // Index workspace and retrieve RAG context (if RAG enabled)
            if (_ragService != null)
            {
                try
                {
                    // Index all workspace files
                    AnsiConsole.MarkupLine("[cyan]📚 Indexing workspace for RAG...[/]");
                    var indexStopwatch = Stopwatch.StartNew();
                    var chunkCount = await _ragService.IndexWorkspaceAsync(
                        _workspace.WorkspaceRoot,
                        context.PipelineId,
                        cancellationToken);
                    indexStopwatch.Stop();
                    AnsiConsole.MarkupLine($"[green]✓[/] Indexed {chunkCount} chunks in {indexStopwatch.Elapsed.TotalSeconds:F1}s");

                    // Query for relevant context based on user request
                    var ragResults = await _ragService.QueryAsync(
                        userRequest,
                        context.PipelineId,
                        topK: 5,
                        cancellationToken);

                    // Format and set RAG context for agents
                    if (ragResults.Count > 0)
                    {
                        var formattedContext = _ragService.FormatContext(ragResults, maxTokens: 8000);
                        context.SetRAGContext(formattedContext);

                        // Display retrieved chunks for observability
                        AnsiConsole.MarkupLine($"[cyan]🔍 Retrieved {ragResults.Count} relevant chunks:[/]");
                        foreach (var doc in ragResults.Take(5))
                        {
                            var filePath = doc.Metadata.TryGetValue("file_path", out var path) ? path.ToString() : "unknown";
                            var chunkIndex = doc.Metadata.TryGetValue("chunk_index", out var idx) ? idx.ToString() : "?";
                            AnsiConsole.MarkupLine($"[dim]  - {filePath} (chunk {chunkIndex}, score: {doc.Score:F3})[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]⚠[/] [dim]No relevant chunks found for query[/]");
                    }

                    // Record RAG metrics in pipeline context
                    context.SetRAGMetrics(chunkCount, ragResults.Count, indexStopwatch.Elapsed);
                }
                catch (InvalidOperationException ex)
                {
                    // RAG failed (Ollama not running, model not found, etc.)
                    // Continue pipeline without RAG context
                    AnsiConsole.MarkupLine("[yellow]⚠ RAG unavailable:[/] {0}", ex.Message);
                    AnsiConsole.MarkupLine("[dim]Pipeline continuing without RAG context[/]");
                }
                catch (HttpRequestException)
                {
                    // Network error connecting to Ollama
                    // Continue pipeline without RAG context
                    AnsiConsole.MarkupLine("[yellow]⚠ RAG unavailable:[/] Failed to connect to Ollama");
                    AnsiConsole.MarkupLine("[dim]Pipeline continuing without RAG context[/]");
                }
            }

            // Execute all stages sequentially
            var stages = new[]
            {
                PipelineStage.Planning,
                PipelineStage.Coding,
                PipelineStage.Reviewing,
                PipelineStage.Testing,
                PipelineStage.Evaluating
            };

            foreach (var stage in stages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var agentResult = await RunStageAsync(stage, context, cancellationToken);

                if (!agentResult.Success)
                {
                    context.AdvanceToStage(PipelineStage.Failed, agentResult.ErrorMessage ?? "Unknown error");
                    stopwatch.Stop();
                    return PipelineResult.CreateFailure(context, stopwatch.Elapsed, agentResult.ErrorMessage ?? "Unknown error");
                }

                // Validate Planner output contains required JSON structure
                if (stage == PipelineStage.Planning)
                {
                    var validationError = ValidatePlannerOutput(agentResult.Output);
                    if (validationError != null)
                    {
                        var detailedError = $"{validationError}\n\nPlanner returned:\n{TruncateOutput(agentResult.Output, 500)}";
                        context.AdvanceToStage(PipelineStage.Failed, detailedError);
                        stopwatch.Stop();
                        return PipelineResult.CreateFailure(context, stopwatch.Elapsed, detailedError);
                    }

                    // Copy CLAUDE.md after Planning succeeds to make it available for subsequent stages
                    // (it was excluded during initial workspace creation to reduce Planner context)
                    _workspace.CopyClaudeMd(context.SourceRoot!);
                }

                context.AdvanceToStage(stage, agentResult.Output);

                // Save pipeline state after each stage completes
                await SavePipelineStateAsync(context, null, PipelineStatus.Running, cancellationToken);

                // Apply patch to workspace after Coding stage
                if (stage == PipelineStage.Coding)
                {
                    try
                    {
                        // Try parsing as MCP file operations first
                        var fileOpsResult = ParseAndApplyFileOperations(agentResult.Output);

                        if (fileOpsResult.Success)
                        {
                            // MCP parsing succeeded - use the file operations
                            context.SetAppliedFiles(fileOpsResult.FilesModified);
                        }
                        else
                        {
                            // MCP parsing failed - fall back to unified diff patch (backward compatibility)
                            var patchResult = _workspace.ApplyPatch(agentResult.Output);
                            if (!patchResult.Success)
                            {
                                var errorMsg = $"Failed to apply patch: {patchResult.ErrorMessage}";
                                context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                                stopwatch.Stop();
                                return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                            }

                            context.SetAppliedFiles(_workspace.AppliedFiles);
                        }

                        // Validate only modified files to prevent false positives from existing code (always run)
                        var validator = new CodeValidator();
                        var validationResult = validator.ValidateModifiedFiles(_workspace.WorkspaceRoot, _workspace.AppliedFiles);
                        if (!validationResult.Success)
                        {
                            var errorMsg = $"Pre-build validation failed: {validationResult.Summary}\n\n{validationResult.Details}";
                            context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                            stopwatch.Stop();
                            return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                        }

                        // Skip compilation validation for test workspaces
                        // Only run compilation validation for production workspaces
                        if (_workspace.Type == WorkspaceType.Production)
                        {
                            // Copy project files (.csproj and .sln) to workspace for compilation
                            _workspace.CopyProjectFiles(context.SourceRoot!);

                            // NEW: Validate compilation after syntax validation
                            var compilationResult = await ValidateCompilationAsync(cancellationToken);

                        if (!compilationResult.Success)
                        {
                            // Attempt automatic fix for missing using directives (with multiple iterations)
                            AnsiConsole.MarkupLine("[yellow]⚠️  Compilation failed. Attempting to fix using directive errors...[/]");

                            const int maxAutoFixAttempts = 5;
                            var currentErrors = compilationResult.Errors;
                            var totalFixedFiles = new List<string>();

                            for (int attempt = 1; attempt <= maxAutoFixAttempts; attempt++)
                            {
                                var fixResult = await TryAutoFixUsingDirectives(currentErrors, cancellationToken);

                                if (fixResult.Fixed)
                                {
                                    totalFixedFiles.AddRange(fixResult.FixedFiles);
                                    AnsiConsole.MarkupLine($"[green]✓ Auto-fixed {fixResult.FixedFiles.Count} file(s): {string.Join(", ", fixResult.FixedFiles)}[/]");

                                    // Re-validate compilation after fix
                                    var retryCompilationResult = await ValidateCompilationAsync(cancellationToken);
                                    if (retryCompilationResult.Success)
                                    {
                                        AnsiConsole.MarkupLine($"[green]✓ Compilation successful after {attempt} auto-fix iteration(s)[/]");
                                        break;
                                    }

                                    // If still failing, update currentErrors for next iteration
                                    currentErrors = retryCompilationResult.Errors;
                                }
                                else
                                {
                                    // No more files can be auto-fixed
                                    if (totalFixedFiles.Count > 0)
                                    {
                                        var errorMsg = $"Code still does not compile after auto-fixing {totalFixedFiles.Count} file(s):\n\n{currentErrors}";
                                        context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                                        stopwatch.Stop();
                                        return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                                    }
                                    else
                                    {
                                        var errorMsg = $"Compilation failed with errors that could not be auto-fixed:\n\n{compilationResult.Errors}";
                                        context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                                        stopwatch.Stop();
                                        return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                                    }
                                }
                            }

                            // Check final compilation status
                            var finalCompilationResult = await ValidateCompilationAsync(cancellationToken);
                            if (!finalCompilationResult.Success)
                            {
                                var errorMsg = $"Code still does not compile after {maxAutoFixAttempts} auto-fix attempts:\n\n{finalCompilationResult.Errors}";
                                context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                                stopwatch.Stop();
                                return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                            }
                        }
                        } // End if (!isTestWorkspace)
                    }
                    catch (PatchApplicationException ex)
                    {
                        var errorMsg = $"Patch application failed: {ex.Message}";
                        context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                        stopwatch.Stop();
                        return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                    }
                }

                // Check approval gates after Planning stage
                if (stage == PipelineStage.Planning)
                {
                    var decision = ApprovalGate.Evaluate(agentResult.Output);
                    if (decision.Required)
                    {
                        context.RequestApproval(decision.Reason);
                    }
                }

                // Check reviewer verdict after Reviewing stage
                if (stage == PipelineStage.Reviewing)
                {
                    var verdict = ParseReviewerVerdict(agentResult.Output);
                    if (verdict == "REJECT")
                    {
                        var errorMsg = $"Reviewer rejected code (verdict: {verdict})";
                        context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                        stopwatch.Stop();
                        return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                    }
                    else if (verdict == "REVISE")
                    {
                        // Revision loop: continue until APPROVE, REJECT, or max iterations
                        while (verdict == "REVISE" && context.RevisionIteration < MaxRevisionIterations)
                        {
                            context.IncrementRevisionIteration();
                            var reviewerFeedback = ExtractReviewerFeedback(agentResult.Output);
                            var coderInput = BuildCoderRevisionInput(context, reviewerFeedback);
                            var coderResult = await RunStageAsync(PipelineStage.Coding, context, cancellationToken, coderInput);

                            if (!coderResult.Success)
                            {
                                context.AdvanceToStage(PipelineStage.Failed, coderResult.ErrorMessage ?? "Unknown error");
                                stopwatch.Stop();
                                return PipelineResult.CreateFailure(context, stopwatch.Elapsed, coderResult.ErrorMessage ?? "Unknown error");
                            }

                            context.AdvanceToStage(PipelineStage.Coding, coderResult.Output);

                            // Rollback and re-apply revised patch
                            try
                            {
                                _workspace.Rollback();
                                var patchResult = _workspace.ApplyPatch(coderResult.Output);
                                if (!patchResult.Success)
                                {
                                    var errorMsg = $"Failed to apply revised patch: {patchResult.ErrorMessage}";
                                    context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                                    stopwatch.Stop();
                                    return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                                }
                                context.SetAppliedFiles(_workspace.AppliedFiles);

                                // Copy project files (.csproj and .sln) to workspace for compilation
                                _workspace.CopyProjectFiles(context.SourceRoot!);

                                // Validate revised code before building
                                var validator = new CodeValidator();
                                var validationResult = validator.ValidateWorkspace(_workspace.WorkspaceRoot);
                                if (!validationResult.Success)
                                {
                                    var errorMsg = $"Pre-build validation failed on revised code: {validationResult.Summary}\n\n{validationResult.Details}";
                                    context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                                    stopwatch.Stop();
                                    return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                                }
                            }
                            catch (PatchApplicationException ex)
                            {
                                var errorMsg = $"Revised patch application failed: {ex.Message}";
                                context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                                stopwatch.Stop();
                                return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                            }

                            // Re-run Reviewer
                            var reviewerResult = await RunStageAsync(PipelineStage.Reviewing, context, cancellationToken);
                            if (!reviewerResult.Success)
                            {
                                context.AdvanceToStage(PipelineStage.Failed, reviewerResult.ErrorMessage ?? "Unknown error");
                                stopwatch.Stop();
                                return PipelineResult.CreateFailure(context, stopwatch.Elapsed, reviewerResult.ErrorMessage ?? "Unknown error");
                            }

                            context.AdvanceToStage(PipelineStage.Reviewing, reviewerResult.Output);
                            verdict = ParseReviewerVerdict(reviewerResult.Output);
                            agentResult = reviewerResult; // Update for next iteration

                            if (verdict == "REJECT")
                            {
                                var errorMsg = $"Reviewer rejected revised code (verdict: {verdict})";
                                context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                                stopwatch.Stop();
                                return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                            }
                        }

                        // If still REVISE after loop, max iterations exceeded
                        if (verdict == "REVISE")
                        {
                            var errorMsg = $"Maximum revision iterations ({MaxRevisionIterations}) exceeded. Code still needs improvement.";
                            context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                            stopwatch.Stop();
                            return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                        }
                    }
                }

                // Track test failures after Testing stage (but don't stop pipeline)
                if (stage == PipelineStage.Testing)
                {
                    var testFailureCount = ParseTestFailureCount(agentResult.Output);
                    if (testFailureCount > 0)
                    {
                        context.SetTestFailures(testFailureCount);
                        // DON'T transition to Failed - continue to Evaluating
                        // Test failures will be reflected in PassedWithWarnings outcome
                    }
                }

                // Check evaluator verdict after Evaluating stage
                if (stage == PipelineStage.Evaluating)
                {
                    var (score, verdict) = ParseEvaluatorVerdict(agentResult.Output);
                    if (verdict == "REJECT" || score < 7.0)
                    {
                        var errorMsg = $"Evaluator rejected pipeline (score: {score:F1}/10, verdict: {verdict})";
                        context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                        stopwatch.Stop();
                        return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                    }
                }

                if (!ShouldContinue(context))
                {
                    stopwatch.Stop();

                    if (context.ApprovalRequired)
                    {
                        var awaitingResult = PipelineResult.CreateAwaitingApproval(context, stopwatch.Elapsed);
                        await SavePipelineStateAsync(context, awaitingResult, PipelineStatus.AwaitingApproval, cancellationToken);
                        return awaitingResult;
                    }

                    var failureResult = PipelineResult.CreateFailure(context, stopwatch.Elapsed, "Pipeline stopped unexpectedly");
                    await SavePipelineStateAsync(context, failureResult, PipelineStatus.Failed, cancellationToken);
                    return failureResult;
                }
            }

            // All stages completed successfully
            context.AdvanceToStage(PipelineStage.Completed, "Pipeline completed successfully");
            context.CompletedAt = DateTimeOffset.UtcNow;
            stopwatch.Stop();

            // Return PassedWithWarnings if tests failed, otherwise Success
            if (context.HasTestFailures)
            {
                var warningMsg = $"{context.TestFailureCount} test(s) failed";
                success = true; // Preserve workspace on success (even with warnings)
                var warningResult = PipelineResult.CreatePassedWithWarnings(context, stopwatch.Elapsed, warningMsg);
                await SavePipelineStateAsync(context, warningResult, PipelineStatus.Completed, cancellationToken);
                return warningResult;
            }

            success = true; // Preserve workspace on success
            var successResult = PipelineResult.CreateSuccess(context, stopwatch.Elapsed);
            await SavePipelineStateAsync(context, successResult, PipelineStatus.Completed, cancellationToken);
            return successResult;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            context.AdvanceToStage(PipelineStage.Failed, "Pipeline execution was cancelled");
            return PipelineResult.CreateFailure(context, stopwatch.Elapsed, "Pipeline execution was cancelled");
        }
        catch (IOException ex)
        {
            stopwatch.Stop();
            context.AdvanceToStage(PipelineStage.Failed, $"I/O error: {ex.Message}");
            return PipelineResult.CreateFailure(context, stopwatch.Elapsed, $"Pipeline I/O error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            stopwatch.Stop();
            context.AdvanceToStage(PipelineStage.Failed, $"Access denied: {ex.Message}");
            return PipelineResult.CreateFailure(context, stopwatch.Elapsed, $"Pipeline access denied: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            context.AdvanceToStage(PipelineStage.Failed, $"Invalid operation: {ex.Message}");
            return PipelineResult.CreateFailure(context, stopwatch.Elapsed, $"Pipeline invalid operation: {ex.Message}");
        }
        finally
        {
            // Clean up workspace on failure (no point keeping failed attempts)
            // Preserve workspace on success (so user can review and apply changes)
            // Also preserve if --preserve-workspace flag is set (for debugging)
            if (!success && !_preserveWorkspace)
            {
                _workspace.Dispose();
            }
        }
    }

    /// <summary>
    /// Executes a single pipeline stage using the corresponding agent.
    /// </summary>
    /// <param name="stage">The stage to execute.</param>
    /// <param name="context">The pipeline context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent execution result.</returns>
    private async Task<AgentResult> RunStageAsync(PipelineStage stage, PipelineContext context, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(stage, out var agent))
        {
            return AgentResult.CreateFailure($"Agent for stage {stage}", $"No agent configured for stage: {stage}");
        }

        var input = BuildStageInput(stage, context);
        var agentContext = new AgentContext
        {
            WorkspaceRoot = context.WorkspaceRoot
        };
        agentContext.SetValue("PipelineId", context.PipelineId);
        agentContext.SetValue("CurrentStage", stage);

        return await agent.ExecuteAsync(input, agentContext, cancellationToken);
    }

    /// <summary>
    /// Determines if the pipeline should continue executing.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <returns>True if execution should continue; otherwise, false.</returns>
    private static bool ShouldContinue(PipelineContext context)
    {
        return context.CurrentStage != PipelineStage.Failed
               && context.CurrentStage != PipelineStage.AwaitingApproval
               && !context.ApprovalRequired;
    }

    /// <summary>
    /// Builds the input for a specific pipeline stage.
    /// </summary>
    /// <param name="stage">The stage to build input for.</param>
    /// <param name="context">The pipeline context.</param>
    /// <returns>The input string for the stage.</returns>
    private string BuildStageInput(PipelineStage stage, PipelineContext context)
    {
        var baseInput = stage switch
        {
            PipelineStage.Planning => context.UserRequest,
            PipelineStage.Coding => BuildCoderInput(context),
            PipelineStage.Reviewing => BuildReviewerInput(context),
            PipelineStage.Testing => BuildTesterInput(context),
            PipelineStage.Evaluating => BuildEvaluatorInput(context),
            _ => string.Empty
        };

        var contextParts = new List<string>();

        // Prepend project structure context for stages that need it
        if (context.ProjectStructure != null && ShouldIncludeStructureContext(stage))
        {
            contextParts.Add(context.ProjectStructure.ToAgentContext());
        }

        // Prepend RAG-retrieved context for all stages (if available)
        if (!string.IsNullOrWhiteSpace(context.RAGContext) && ShouldIncludeRAGContext(stage))
        {
            contextParts.Add(context.RAGContext);
        }

        // Combine all context parts with the base input
        if (contextParts.Count > 0)
        {
            contextParts.Add(baseInput);
            return string.Join("\n\n", contextParts);
        }

        return baseInput;
    }

    /// <summary>
    /// Determines if a pipeline stage should receive project structure context.
    /// </summary>
    /// <param name="stage">The pipeline stage.</param>
    /// <returns>True if structure context should be included; otherwise, false.</returns>
    private static bool ShouldIncludeStructureContext(PipelineStage stage)
    {
        // Planning and Coding stages need structure context to generate correct file paths
        // Other stages work with existing outputs that already reference the correct paths
        return stage is PipelineStage.Planning or PipelineStage.Coding;
    }

    /// <summary>
    /// Determines if a pipeline stage should receive RAG-retrieved context.
    /// </summary>
    /// <param name="stage">The pipeline stage.</param>
    /// <returns>True if RAG context should be included; otherwise, false.</returns>
    private static bool ShouldIncludeRAGContext(PipelineStage stage)
    {
        // RAG context is valuable for creative/analytical stages:
        // - Planning: Domain knowledge, architectural patterns
        // - Reviewing: Project-specific review guidelines
        // - Testing: Test patterns, existing test structure
        // - Evaluating: Quality standards from documentation
        //
        // Coding stage is EXCLUDED because:
        // - Coder needs EXACT, COMPLETE file content (via Read tool)
        // - RAG provides partial chunks (~512 tokens) which cause incorrect patches
        // - Planner already incorporates RAG patterns into the plan
        return stage is PipelineStage.Planning
            or PipelineStage.Reviewing
            or PipelineStage.Testing
            or PipelineStage.Evaluating;
    }

    /// <summary>
    /// Builds the input for the coder agent with the plan to implement using MCP file operation tools.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <returns>The coder input with plan.</returns>
    private static string BuildCoderInput(PipelineContext context)
    {
        return $"""
            Implement the following plan using MCP file operation tools.

            Plan:
            {context.Plan ?? string.Empty}
            """;
    }

    /// <summary>
    /// Builds the input for the reviewer agent, enriched with Roslyn analyzer diagnostics.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <returns>The reviewer input with diagnostics and patch.</returns>
    private string BuildReviewerInput(PipelineContext context)
    {
        var patch = context.Patch ?? string.Empty;

        // Run Roslyn analysis on modified files if available
        if (context.AppliedFiles != null && context.AppliedFiles.Count > 0 && _workspace.WorkspaceRoot != null)
        {
            try
            {
                var analysisResult = _codeAnalyzer.AnalyzeWorkspaceAsync(
                    _workspace.WorkspaceRoot,
                    context.AppliedFiles,
                    CancellationToken.None).GetAwaiter().GetResult();

                if (analysisResult.TotalCount > 0)
                {
                    // Prepend diagnostics to patch
                    return FormatAnalysisForReviewer(analysisResult) + "\n\n" + patch;
                }
            }
            catch (Exception ex)
            {
                // Analysis failed - just return patch without diagnostics
                System.Console.Error.WriteLine($"Roslyn analysis failed: {ex.Message}");
            }
        }

        return patch;
    }

    /// <summary>
    /// Formats Roslyn analysis results for the Reviewer agent.
    /// </summary>
    /// <param name="analysis">The analysis results.</param>
    /// <returns>Formatted diagnostics string.</returns>
    private static string FormatAnalysisForReviewer(AnalysisResult analysis)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Roslyn Analyzer Diagnostics\n");

        var counts = analysis.GetCountsBySeverity();
        foreach (var (severity, count) in counts.OrderByDescending(x => (int)x.Key))
        {
            sb.AppendLine($"### {severity} ({count}):\n");

            foreach (var diagnostic in analysis.Diagnostics.Where(d => d.Severity == severity))
            {
                sb.AppendLine($"- {diagnostic.FilePath}:{diagnostic.LineNumber} [{diagnostic.RuleId}]: {diagnostic.Message}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---\n");
        sb.AppendLine("## Unified Diff Patch\n");

        return sb.ToString();
    }

    /// <summary>
    /// Builds the comprehensive input for the evaluator agent.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <returns>The evaluator input combining all stage outputs.</returns>
    private static string BuildEvaluatorInput(PipelineContext context)
    {
        return $"""
            User Request: {context.UserRequest}
            Plan: {context.Plan}
            Patch: {context.Patch}
            Review: {context.Review}
            Test Report: {context.TestReport}
            """;
    }

    /// <summary>
    /// Builds the input for the tester agent with workspace information.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <returns>The tester input with workspace path and applied files.</returns>
    private static string BuildTesterInput(PipelineContext context)
    {
        var appliedFiles = context.AppliedFiles != null && context.AppliedFiles.Count > 0
            ? string.Join(", ", context.AppliedFiles)
            : "None";

        return $"""
            Workspace Path: {context.WorkspaceRoot ?? "Not set"}
            Applied Files: {appliedFiles}

            Please run tests in the workspace directory.
            Steps:
            1. Navigate to workspace: cd "{context.WorkspaceRoot}"
            2. Build the solution: dotnet build
            3. Run tests: dotnet test --logger "trx"
            4. Parse and report results
            """;
    }

    /// <summary>
    /// Validates that all required agents are configured.
    /// </summary>
    /// <param name="agents">The agent dictionary to validate.</param>
    /// <exception cref="ArgumentException">Thrown when required agents are missing.</exception>
    private static void ValidateAgents(IReadOnlyDictionary<PipelineStage, IAgent> agents)
    {
        var requiredStages = new[]
        {
            PipelineStage.Planning,
            PipelineStage.Coding,
            PipelineStage.Reviewing,
            PipelineStage.Testing,
            PipelineStage.Evaluating
        };

        var missingStages = requiredStages.Where(stage => !agents.ContainsKey(stage)).ToList();

        if (missingStages.Any())
        {
            var stageNames = string.Join(", ", missingStages);
            throw new ArgumentException($"Missing required agents for stages: {stageNames}", nameof(agents));
        }
    }

    /// <summary>
    /// Parses the evaluator's JSON output to extract the verdict and overall score.
    /// </summary>
    /// <param name="evaluatorOutput">The JSON output from the evaluator agent.</param>
    /// <returns>A tuple containing (score, verdict). Returns (0.0, "UNKNOWN") if parsing fails.</returns>
    private static (double Score, string Verdict) ParseEvaluatorVerdict(string evaluatorOutput)
    {
        try
        {
            using var doc = JsonDocument.Parse(evaluatorOutput);
            var evaluation = doc.RootElement.GetProperty("evaluation");
            var score = evaluation.GetProperty("overall_score").GetDouble();
            var verdict = evaluation.GetProperty("final_verdict").GetString() ?? "UNKNOWN";
            return (score, verdict);
        }
        catch (JsonException)
        {
            // If JSON parsing fails, treat as unknown/failed evaluation
            return (0.0, "UNKNOWN");
        }
        catch (KeyNotFoundException)
        {
            // If required properties are missing, treat as unknown/failed evaluation
            return (0.0, "UNKNOWN");
        }
    }

    /// <summary>
    /// Parses the reviewer's JSON output to extract the verdict.
    /// </summary>
    /// <param name="reviewerOutput">The JSON output from the reviewer agent.</param>
    /// <returns>The verdict string (APPROVE, REJECT, REVISE). Returns "UNKNOWN" if parsing fails.</returns>
    private static string ParseReviewerVerdict(string reviewerOutput)
    {
        try
        {
            using var doc = JsonDocument.Parse(reviewerOutput);
            var verdict = doc.RootElement.GetProperty("verdict").GetString() ?? "UNKNOWN";
            return verdict;
        }
        catch (JsonException)
        {
            // If JSON parsing fails, treat as unknown (will not fail pipeline)
            return "UNKNOWN";
        }
        catch (KeyNotFoundException)
        {
            // If verdict property is missing, treat as unknown (will not fail pipeline)
            return "UNKNOWN";
        }
    }

    /// <summary>
    /// Executes a single pipeline stage using custom input (for revision loops).
    /// </summary>
    /// <param name="stage">The stage to execute.</param>
    /// <param name="context">The pipeline context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="customInput">Custom input to pass to the agent.</param>
    /// <returns>The agent execution result.</returns>
    private async Task<AgentResult> RunStageAsync(PipelineStage stage, PipelineContext context, CancellationToken cancellationToken, string customInput)
    {
        if (!_agents.TryGetValue(stage, out var agent))
        {
            return AgentResult.CreateFailure($"Agent for stage {stage}", $"No agent configured for stage: {stage}");
        }

        var agentContext = new AgentContext
        {
            WorkspaceRoot = context.WorkspaceRoot
        };
        agentContext.SetValue("PipelineId", context.PipelineId);
        agentContext.SetValue("CurrentStage", stage);
        agentContext.SetValue("RevisionIteration", context.RevisionIteration);

        return await agent.ExecuteAsync(customInput, agentContext, cancellationToken);
    }

    /// <summary>
    /// Extracts reviewer feedback from the review JSON output.
    /// </summary>
    /// <param name="reviewerOutput">The JSON output from the reviewer.</param>
    /// <returns>Formatted feedback string with issues and suggestions.</returns>
    private static string ExtractReviewerFeedback(string reviewerOutput)
    {
        try
        {
            using var doc = JsonDocument.Parse(reviewerOutput);
            var root = doc.RootElement;

            var summary = root.GetProperty("summary").GetString() ?? "No summary provided";
            var issues = root.GetProperty("issues");

            var feedback = $"Reviewer Feedback:\n\nSummary: {summary}\n\nIssues to address:\n";

            foreach (var issue in issues.EnumerateArray())
            {
                var severity = issue.GetProperty("severity").GetString();
                var file = issue.GetProperty("file").GetString();
                var line = issue.GetProperty("line").GetInt32();
                var message = issue.GetProperty("message").GetString();
                var suggestion = issue.GetProperty("suggestion").GetString();

                feedback += $"\n- [{severity}] {file}:{line}\n";
                feedback += $"  Problem: {message}\n";
                feedback += $"  Suggestion: {suggestion}\n";
            }

            return feedback;
        }
        catch (JsonException)
        {
            return "Could not parse reviewer feedback. Please review the code carefully.";
        }
        catch (KeyNotFoundException)
        {
            return "Reviewer feedback incomplete. Please improve code quality.";
        }
    }

    /// <summary>
    /// Builds the input for the Coder agent when revising code based on reviewer feedback.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="reviewerFeedback">The formatted feedback from the reviewer.</param>
    /// <returns>The input string for the Coder with revision instructions.</returns>
    private static string BuildCoderRevisionInput(PipelineContext context, string reviewerFeedback)
    {
        return $"""
            You are revising code based on reviewer feedback. This is revision iteration {context.RevisionIteration + 1}.

            Original Plan:
            {context.Plan}

            Previous Code (requires improvement):
            {context.Patch}

            {reviewerFeedback}

            Please generate an improved unified diff patch that addresses all the reviewer's concerns while maintaining the original intent of the plan.
            """;
    }

    /// <summary>
    /// Builds input for Coder to fix compilation errors in the current workspace state.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <param name="compilationErrors">The compilation errors from the build.</param>
    /// <param name="currentFileContents">The ACTUAL current content of modified files from the workspace.</param>
    /// <returns>Input text for Coder to generate a fix patch.</returns>
    private static string BuildCoderFixInput(PipelineContext context, string compilationErrors, Dictionary<string, string> currentFileContents)
    {
        // Parse compilation error to identify which file has the error
        var errorFileMatch = System.Text.RegularExpressions.Regex.Match(
            compilationErrors,
            @"([^\\:]+\.cs)\((\d+),(\d+)\):\s*error\s+CS\d+:\s*(.+?)(?:\[|$)",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        string errorFilePath = string.Empty;
        string errorLine = string.Empty;
        string errorMessage = string.Empty;
        string? fileTopLines = null;

        if (errorFileMatch.Success)
        {
            errorFilePath = errorFileMatch.Groups[1].Value;
            errorLine = errorFileMatch.Groups[2].Value;
            errorMessage = errorFileMatch.Groups[4].Value.Trim();

            // Find the matching file in currentFileContents
            var matchingEntry = currentFileContents.FirstOrDefault(kvp =>
                kvp.Key.Contains(errorFilePath, StringComparison.OrdinalIgnoreCase));

            if (matchingEntry.Key != null)
            {
                // Show first 10 lines of the file to see existing using directives
                var lines = matchingEntry.Value.Split('\n');
                fileTopLines = string.Join("\n", lines.Take(10));
                errorFilePath = matchingEntry.Key; // Use the full relative path
            }
        }

        var filesSummary = string.Join(", ", currentFileContents.Keys);

        return $"""
            COMPILATION FIX REQUIRED

            ERROR LOCATION: {errorFilePath} (line {errorLine})
            ERROR MESSAGE: {errorMessage}

            CURRENT FILE HEADER (first 10 lines of {errorFilePath}):
            ```
            {fileTopLines ?? "File not found in current changes"}
            ```

            FULL COMPILATION OUTPUT:
            {compilationErrors}

            FILES MODIFIED IN THIS RUN: {filesSummary}

            ====================
            TASK: Generate a MINIMAL unified diff patch to add the missing using directive.

            MOST COMMON FIX:
            If error says "type or namespace name 'DivideByZeroException' could not be found":
            → Add "using System;" at the TOP of {errorFilePath}

            Other common using directives:
            - ArgumentException, ArgumentNullException → using System;
            - List<T>, Dictionary<K,V> → using System.Collections.Generic;
            - Task<T> → using System.Threading.Tasks;

            CRITICAL OUTPUT FORMAT:
            1. Output ONLY a unified diff patch in git format
            2. Do NOT include explanations or conversation
            3. Start directly with: diff --git a/{errorFilePath} b/{errorFilePath}
            4. The patch must be MINIMAL - only add the missing using directive
            5. Include enough context lines (3-5 lines) for the patch to apply correctly

            EXAMPLE OUTPUT (adapt to actual file):
            diff --git a/{errorFilePath} b/{errorFilePath}
            --- a/{errorFilePath}
            +++ b/{errorFilePath}
            @@ -1,4 +1,6 @@
            +using System;
            +
             using Xunit;

             namespace Calculator.Tests;

            Now generate the fix patch (ONLY the patch, no other text):
            """;
    }

    /// <summary>
    /// Saves the current pipeline state for resume/approve/reject workflows.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="result">The pipeline result (if available).</param>
    /// <param name="status">The pipeline status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task SavePipelineStateAsync(
        PipelineContext context,
        PipelineResult? result,
        PipelineStatus status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var state = PipelineState.FromContext(context, result, status);
            await _stateManager.SaveStateAsync(state, cancellationToken);
        }
        catch (Exception ex)
        {
            // Don't fail the pipeline if state saving fails - just log
            AnsiConsole.MarkupLine($"[yellow]⚠[/] [dim]Failed to save pipeline state: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// Validates that the Planner output contains the required JSON structure.
    /// </summary>
    /// <param name="plannerOutput">The output from the Planner agent.</param>
    /// <returns>Error message if validation fails; null if valid.</returns>
    private static string? ValidatePlannerOutput(string plannerOutput)
    {
        if (string.IsNullOrWhiteSpace(plannerOutput))
        {
            return "ERROR: Planner returned empty output. Did you use the MCP planning tools (mcp__pipeline-tools__*)?";
        }

        try
        {
            using var doc = JsonDocument.Parse(plannerOutput);
            var root = doc.RootElement;

            // Check for required top-level properties
            var requiredProps = new[] { "plan", "file_list", "risk", "verify", "rollback" };
            var missingProps = new List<string>();

            foreach (var prop in requiredProps)
            {
                if (!root.TryGetProperty(prop, out _))
                {
                    missingProps.Add(prop);
                }
            }

            if (missingProps.Count > 0)
            {
                var missing = string.Join(", ", missingProps);
                return $"ERROR: Planner output is missing required properties: {missing}\n\n" +
                       "Did you use the MCP planning tools? Your first tool call MUST be mcp__pipeline-tools__plan_init,\n" +
                       "and your last tool call MUST be mcp__pipeline-tools__finalize_plan.\n\n" +
                       "DO NOT use Write, Bash, Edit, or any other tools - ONLY use mcp__pipeline-tools__* tools.";
            }

            return null; // Valid
        }
        catch (JsonException)
        {
            return "ERROR: Planner did not return valid JSON.\n\n" +
                   "Did you use the MCP planning tools? You must use mcp__pipeline-tools__finalize_plan to get the final JSON.\n\n" +
                   "DO NOT write JSON directly or use conversational responses.";
        }
    }

    /// <summary>
    /// Truncates output to a maximum length for error messages.
    /// </summary>
    /// <param name="output">The output to truncate.</param>
    /// <param name="maxLength">Maximum length.</param>
    /// <returns>Truncated output with ellipsis if needed.</returns>
    private static string TruncateOutput(string output, int maxLength)
    {
        if (string.IsNullOrEmpty(output) || output.Length <= maxLength)
        {
            return output;
        }

        return output.Substring(0, maxLength) + "\n... (truncated)";
    }

    /// <summary>
    /// Parses test failure count from Testing stage JSON output.
    /// </summary>
    /// <param name="testOutput">The JSON output from the Testing agent.</param>
    /// <returns>The number of failed tests, or 0 if no failures or parsing error.</returns>
    private static int ParseTestFailureCount(string testOutput)
    {
        try
        {
            using var doc = JsonDocument.Parse(testOutput);
            if (!doc.RootElement.GetProperty("pass").GetBoolean())
            {
                // Count failed tests from test_results array
                var testResults = doc.RootElement.GetProperty("test_results");
                return testResults.EnumerateArray()
                    .Count(t => t.GetProperty("status").GetString() == "failed");
            }
            return 0;
        }
        catch (JsonException)
        {
            return 0; // If parsing fails, assume no failures
        }
        catch (KeyNotFoundException)
        {
            return 0; // If required properties missing, assume no failures
        }
    }

    /// <summary>
    /// Validates that the generated code compiles successfully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing success status and compilation errors (if any).</returns>
    private async Task<(bool Success, string Errors)> ValidateCompilationAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Find the solution file to build (handle multiple .sln files)
            var slnFiles = Directory.GetFiles(_workspace.WorkspaceRoot, "*.sln", SearchOption.TopDirectoryOnly);

            string buildTarget;

            if (slnFiles.Length == 1)
            {
                // Use the single solution file
                buildTarget = Path.GetFileName(slnFiles[0]);
            }
            else if (slnFiles.Length > 1)
            {
                // Multiple solution files - prefer non-DevPilot.sln (original solution from source)
                var preferredSln = slnFiles.FirstOrDefault(s => !s.EndsWith("DevPilot.sln"));
                buildTarget = Path.GetFileName(preferredSln ?? slnFiles[0]);
            }
            else
            {
                // No solution file - just use default dotnet build
                buildTarget = string.Empty;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = string.IsNullOrEmpty(buildTarget)
                    ? "build"
                    : $"build \"{buildTarget}\"",
                WorkingDirectory = _workspace.WorkspaceRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errors = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                return (true, string.Empty);
            }

            // Extract compilation errors from output
            var compilationErrors = output + "\n" + errors;
            return (false, compilationErrors);
        }
        catch (Exception ex)
        {
            return (false, $"Compilation validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Result of automatic using directive fix attempt.
    /// </summary>
    private record AutoFixResult(bool Fixed, List<string> FixedFiles);

    /// <summary>
    /// Attempts to automatically fix compilation errors caused by missing using directives.
    /// Parses CS0246 errors, determines required namespaces, and injects them into files.
    /// </summary>
    /// <param name="compilationErrors">The compilation error output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating whether fixes were applied and which files were modified.</returns>
    private async Task<AutoFixResult> TryAutoFixUsingDirectives(string compilationErrors, CancellationToken cancellationToken)
    {
        // Map common type names to their required namespaces
        var typeToNamespace = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // System namespace
            ["ArgumentException"] = "System",
            ["ArgumentNullException"] = "System",
            ["ArgumentOutOfRangeException"] = "System",
            ["DivideByZeroException"] = "System",
            ["InvalidOperationException"] = "System",
            ["NotImplementedException"] = "System",
            ["NotSupportedException"] = "System",
            ["NullReferenceException"] = "System",
            ["IndexOutOfRangeException"] = "System",
            ["FormatException"] = "System",
            ["OverflowException"] = "System",
            ["TimeoutException"] = "System",
            ["Guid"] = "System",
            ["DateTime"] = "System",
            ["DateTimeOffset"] = "System",
            ["TimeSpan"] = "System",
            ["Math"] = "System",
            ["Convert"] = "System",
            ["Console"] = "System",
            ["Environment"] = "System",
            ["Exception"] = "System",
            ["Tuple"] = "System",
            ["ValueTuple"] = "System",

            // System.Collections.Generic
            ["List"] = "System.Collections.Generic",
            ["Dictionary"] = "System.Collections.Generic",
            ["HashSet"] = "System.Collections.Generic",
            ["Queue"] = "System.Collections.Generic",
            ["Stack"] = "System.Collections.Generic",
            ["IEnumerable"] = "System.Collections.Generic",
            ["ICollection"] = "System.Collections.Generic",
            ["IList"] = "System.Collections.Generic",
            ["IDictionary"] = "System.Collections.Generic",
            ["LinkedList"] = "System.Collections.Generic",
            ["SortedDictionary"] = "System.Collections.Generic",
            ["SortedList"] = "System.Collections.Generic",
            ["KeyValuePair"] = "System.Collections.Generic",

            // System.Threading.Tasks
            ["Task"] = "System.Threading.Tasks",

            // System.Linq
            ["IQueryable"] = "System.Linq",
            ["IOrderedQueryable"] = "System.Linq",

            // System.Text
            ["StringBuilder"] = "System.Text",
            ["Encoding"] = "System.Text",

            // System.Text.RegularExpressions
            ["Regex"] = "System.Text.RegularExpressions",
            ["Match"] = "System.Text.RegularExpressions",

            // System.IO
            ["File"] = "System.IO",
            ["Directory"] = "System.IO",
            ["Path"] = "System.IO",
            ["FileStream"] = "System.IO",
            ["StreamReader"] = "System.IO",
            ["StreamWriter"] = "System.IO",
            ["IOException"] = "System.IO",
            ["FileNotFoundException"] = "System.IO",
            ["DirectoryNotFoundException"] = "System.IO"
        };

        // Parse CS0246 errors: "The type or namespace name 'TypeName' could not be found"
        var cs0246Pattern = new System.Text.RegularExpressions.Regex(
            @"([^:]+\.cs)\(\d+,\d+\):\s*error\s+CS0246:\s*The type or namespace name\s+'([^']+)'\s+could not be found",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        var matches = cs0246Pattern.Matches(compilationErrors);
        if (matches.Count == 0)
        {
            return new AutoFixResult(false, new List<string>());
        }

        // Group errors by file
        var fileToMissingTypes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var fileName = match.Groups[1].Value;
            var typeName = match.Groups[2].Value;

            // Find the full file path in the workspace
            var fullPath = FindFileInWorkspace(fileName);
            if (fullPath == null) continue;

            if (!fileToMissingTypes.ContainsKey(fullPath))
            {
                fileToMissingTypes[fullPath] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            fileToMissingTypes[fullPath].Add(typeName);
        }

        var fixedFiles = new List<string>();

        // Fix each file
        foreach (var (filePath, missingTypes) in fileToMissingTypes)
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            var requiredNamespaces = new HashSet<string>();

            // Determine which namespaces are needed
            foreach (var typeName in missingTypes)
            {
                if (typeToNamespace.TryGetValue(typeName, out var ns))
                {
                    requiredNamespaces.Add(ns);
                }
            }

            if (requiredNamespaces.Count == 0) continue;

            // Check which using directives are already present
            var existingUsings = ExtractExistingUsings(content);
            var usingsToAdd = requiredNamespaces.Except(existingUsings).ToList();

            if (usingsToAdd.Count == 0) continue;

            // Inject missing using directives at the top of the file
            var updatedContent = InjectUsingDirectives(content, usingsToAdd);
            await File.WriteAllTextAsync(filePath, updatedContent, cancellationToken);

            fixedFiles.Add(Path.GetRelativePath(_workspace.WorkspaceRoot, filePath));
        }

        return new AutoFixResult(fixedFiles.Count > 0, fixedFiles);
    }

    /// <summary>
    /// Finds a file in the workspace by its file name (handles relative paths).
    /// </summary>
    private string? FindFileInWorkspace(string fileName)
    {
        var searchPath = Path.Combine(_workspace.WorkspaceRoot, fileName);
        return File.Exists(searchPath) ? searchPath : null;
    }

    /// <summary>
    /// Extracts existing using directives from C# source code.
    /// </summary>
    private static HashSet<string> ExtractExistingUsings(string content)
    {
        var usings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usingPattern = new System.Text.RegularExpressions.Regex(@"^\s*using\s+([^;]+)\s*;", System.Text.RegularExpressions.RegexOptions.Multiline);
        var matches = usingPattern.Matches(content);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var usingDirective = match.Groups[1].Value.Trim();
            // Handle both "using System;" and "using static System.Math;"
            if (!usingDirective.StartsWith("static ", StringComparison.Ordinal))
            {
                usings.Add(usingDirective);
            }
        }

        return usings;
    }

    /// <summary>
    /// Injects using directives at the top of the file (after any existing usings).
    /// </summary>
    private static string InjectUsingDirectives(string content, List<string> namespacesToAdd)
    {
        var lines = content.Split('\n');
        var insertIndex = 0;

        // Find the last existing using directive
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimStart();
            if (line.StartsWith("using ", StringComparison.Ordinal))
            {
                insertIndex = i + 1;
            }
            else if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("//"))
            {
                // Stop at the first non-using, non-comment, non-blank line
                break;
            }
        }

        // If no using directives found, insert at the very top
        if (insertIndex == 0)
        {
            insertIndex = 0;
        }

        // Sort the namespaces to add (System first, then alphabetically)
        var sortedNamespaces = namespacesToAdd.OrderBy(ns => ns == "System" ? "0" : ns).ToList();

        // Build the new using directives
        var newUsings = sortedNamespaces.Select(ns => $"using {ns};").ToList();

        // Insert the new using directives
        var updatedLines = lines.ToList();
        updatedLines.InsertRange(insertIndex, newUsings);

        // If there were no existing usings, add a blank line after the new ones
        if (insertIndex == 0 && lines.Length > 0)
        {
            updatedLines.Insert(newUsings.Count, string.Empty);
        }

        return string.Join("\n", updatedLines);
    }

    /// <summary>
    /// Parse MCP file operations output and apply to workspace.
    /// </summary>
    private MCPFileOperationResult ParseAndApplyFileOperations(string mcpOutput)
    {
        try
        {
            // Parse finalize_file_operations result from MCP output
            var json = JsonDocument.Parse(mcpOutput);

            // Handle MCP tool response format: { "success": true, "file_operations": {...} }
            // vs direct format: { "file_operations": {...} }
            JsonElement fileOpsElement;
            if (json.RootElement.TryGetProperty("success", out var successProp))
            {
                // MCP tool response wrapper - extract file_operations from nested level
                if (!json.RootElement.TryGetProperty("file_operations", out fileOpsElement))
                {
                    return new MCPFileOperationResult
                    {
                        Success = false,
                        ErrorMessage = "MCP tool response missing 'file_operations' property"
                    };
                }
            }
            else if (!json.RootElement.TryGetProperty("file_operations", out fileOpsElement))
            {
                // Direct format - file_operations at root level
                return new MCPFileOperationResult
                {
                    Success = false,
                    ErrorMessage = "MCP output missing 'file_operations' property"
                };
            }

            if (!fileOpsElement.TryGetProperty("operations", out var opsArray))
            {
                return new MCPFileOperationResult
                {
                    Success = false,
                    ErrorMessage = "MCP file_operations missing 'operations' array"
                };
            }

            var operations = new List<MCPFileOperation>();

            foreach (var op in opsArray.EnumerateArray())
            {
                var type = op.GetProperty("type").GetString();
                operations.Add(type switch
                {
                    "create" => new MCPFileOperation
                    {
                        Type = MCPFileOperationType.Create,
                        Path = op.GetProperty("path").GetString(),
                        Content = op.GetProperty("content").GetString(),
                        Reason = op.TryGetProperty("reason", out var r) ? r.GetString() : null
                    },
                    "modify" => new MCPFileOperation
                    {
                        Type = MCPFileOperationType.Modify,
                        Path = op.GetProperty("path").GetString(),
                        Changes = ParseLineChanges(op.GetProperty("changes"))
                    },
                    "delete" => new MCPFileOperation
                    {
                        Type = MCPFileOperationType.Delete,
                        Path = op.GetProperty("path").GetString(),
                        Reason = op.TryGetProperty("reason", out var r) ? r.GetString() : null
                    },
                    "rename" => new MCPFileOperation
                    {
                        Type = MCPFileOperationType.Rename,
                        OldPath = op.GetProperty("old_path").GetString(),
                        NewPath = op.GetProperty("new_path").GetString(),
                        Reason = op.TryGetProperty("reason", out var r) ? r.GetString() : null
                    },
                    _ => throw new InvalidOperationException($"Unknown operation type: {type}")
                });
            }

            return _workspace.ApplyFileOperations(operations);
        }
        catch (JsonException ex)
        {
            return new MCPFileOperationResult
            {
                Success = false,
                ErrorMessage = $"Failed to parse MCP output: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new MCPFileOperationResult
            {
                Success = false,
                ErrorMessage = $"Error applying file operations: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Parse line changes from JSON element.
    /// </summary>
    private static List<MCPLineChange> ParseLineChanges(JsonElement changesElement)
    {
        var changes = new List<MCPLineChange>();

        foreach (var change in changesElement.EnumerateArray())
        {
            changes.Add(new MCPLineChange
            {
                LineNumber = change.GetProperty("line_number").GetInt32(),
                OldContent = change.TryGetProperty("old_content", out var old) ? old.GetString() : null,
                NewContent = change.GetProperty("new_content").GetString()!,
                LinesToReplace = change.TryGetProperty("lines_to_replace", out var lines) ? lines.GetInt32() : 1
            });
        }

        return changes;
    }
}
