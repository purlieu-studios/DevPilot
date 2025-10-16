using DevPilot.Core;
using DevPilot.Orchestrator.Validation;
using System.Diagnostics;
using System.Text.Json;

namespace DevPilot.Orchestrator;

/// <summary>
/// Orchestrates execution of the 5-agent MASAI pipeline: Planner → Coder → Reviewer → Tester → Evaluator.
/// </summary>
public sealed class Pipeline
{
    private const int MaxRevisionIterations = 2;
    private readonly IReadOnlyDictionary<PipelineStage, IAgent> _agents;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pipeline"/> class.
    /// </summary>
    /// <param name="agents">Dictionary mapping pipeline stages to agent implementations.</param>
    public Pipeline(IReadOnlyDictionary<PipelineStage, IAgent> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        ValidateAgents(agents);
        _agents = agents;
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
        var context = new PipelineContext { UserRequest = userRequest };
        WorkspaceManager? workspace = null;

        try
        {
            // Create isolated workspace for this pipeline execution
            workspace = WorkspaceManager.CreateWorkspace(context.PipelineId);
            context.SetWorkspaceRoot(workspace.WorkspaceRoot);
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
                }

                context.AdvanceToStage(stage, agentResult.Output);

                // Apply patch to workspace after Coding stage
                if (stage == PipelineStage.Coding && workspace != null)
                {
                    try
                    {
                        var patchResult = workspace.ApplyPatch(agentResult.Output);
                        if (!patchResult.Success)
                        {
                            var errorMsg = $"Failed to apply patch: {patchResult.ErrorMessage}";
                            context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                            stopwatch.Stop();
                            return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                        }

                        context.SetAppliedFiles(workspace.AppliedFiles);

                        // Copy project files (.csproj and .sln) to workspace for compilation
                        workspace.CopyProjectFiles(Directory.GetCurrentDirectory());

                        // Validate workspace before building to catch errors early
                        var validator = new CodeValidator();
                        var validationResult = validator.ValidateWorkspace(workspace.WorkspaceRoot);
                        if (!validationResult.Success)
                        {
                            var errorMsg = $"Pre-build validation failed: {validationResult.Summary}\n\n{validationResult.Details}";
                            context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                            stopwatch.Stop();
                            return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                        }
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
                            if (workspace != null)
                            {
                                try
                                {
                                    workspace.Rollback();
                                    var patchResult = workspace.ApplyPatch(coderResult.Output);
                                    if (!patchResult.Success)
                                    {
                                        var errorMsg = $"Failed to apply revised patch: {patchResult.ErrorMessage}";
                                        context.AdvanceToStage(PipelineStage.Failed, errorMsg);
                                        stopwatch.Stop();
                                        return PipelineResult.CreateFailure(context, stopwatch.Elapsed, errorMsg);
                                    }
                                    context.SetAppliedFiles(workspace.AppliedFiles);

                                    // Copy project files (.csproj and .sln) to workspace for compilation
                                    workspace.CopyProjectFiles(Directory.GetCurrentDirectory());

                                    // Validate revised code before building
                                    var validator = new CodeValidator();
                                    var validationResult = validator.ValidateWorkspace(workspace.WorkspaceRoot);
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
                        return PipelineResult.CreateAwaitingApproval(context, stopwatch.Elapsed);
                    }

                    return PipelineResult.CreateFailure(context, stopwatch.Elapsed, "Pipeline stopped unexpectedly");
                }
            }

            // All stages completed successfully
            context.AdvanceToStage(PipelineStage.Completed, "Pipeline completed successfully");
            context.CompletedAt = DateTimeOffset.UtcNow;
            stopwatch.Stop();

            return PipelineResult.CreateSuccess(context, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            context.AdvanceToStage(PipelineStage.Failed, "Pipeline execution was cancelled");
            return PipelineResult.CreateFailure(context, stopwatch.Elapsed, "Pipeline execution was cancelled");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            context.AdvanceToStage(PipelineStage.Failed, ex.Message);
            return PipelineResult.CreateFailure(context, stopwatch.Elapsed, $"Pipeline execution failed: {ex.Message}");
        }
        finally
        {
            // Always preserve workspace for inspection (both success and failure)
            // User can manually clean up workspaces when no longer needed
            // This allows investigation of why evaluator rejected the code
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
        var agentContext = new AgentContext();
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
    private static string BuildStageInput(PipelineStage stage, PipelineContext context)
    {
        return stage switch
        {
            PipelineStage.Planning => context.UserRequest,
            PipelineStage.Coding => context.Plan ?? string.Empty,
            PipelineStage.Reviewing => context.Patch ?? string.Empty,
            PipelineStage.Testing => BuildTesterInput(context),
            PipelineStage.Evaluating => BuildEvaluatorInput(context),
            _ => string.Empty
        };
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

        var agentContext = new AgentContext();
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
}
