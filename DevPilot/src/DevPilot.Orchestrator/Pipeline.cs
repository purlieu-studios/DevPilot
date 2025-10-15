using DevPilot.Core;
using System.Diagnostics;
using System.Text.Json;

namespace DevPilot.Orchestrator;

/// <summary>
/// Orchestrates execution of the 5-agent MASAI pipeline: Planner → Coder → Reviewer → Tester → Evaluator.
/// </summary>
public sealed class Pipeline
{
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
            // Clean up workspace in all exit paths
            workspace?.Dispose();
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
}
