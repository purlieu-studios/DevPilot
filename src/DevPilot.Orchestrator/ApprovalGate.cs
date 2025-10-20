using DevPilot.Core;
using System.Text.Json;

namespace DevPilot.Orchestrator;

/// <summary>
/// Evaluates whether a pipeline requires human approval based on plan analysis.
/// </summary>
public static class ApprovalGate
{
    /// <summary>
    /// Evaluates the planner's output to determine if approval is required.
    /// </summary>
    /// <param name="plannerJsonOutput">The JSON output from the planner agent.</param>
    /// <returns>An approval decision with triggers and reason.</returns>
    public static ApprovalDecision Evaluate(string plannerJsonOutput)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plannerJsonOutput);

        try
        {
            var plan = JsonSerializer.Deserialize<PlannerOutput>(plannerJsonOutput);
            if (plan == null)
            {
                return ApprovalDecision.CreateRequired("Failed to parse planner output");
            }

            var triggers = new List<string>();

            // Check 1: Planner explicitly flagged needs_approval
            if (plan.NeedsApproval)
            {
                triggers.Add($"Planner flagged needs_approval: {plan.ApprovalReason ?? "No reason provided"}");
            }

            // Check 2: High-risk operation
            if (plan.Risk.Level.Equals("high", StringComparison.OrdinalIgnoreCase))
            {
                var factors = string.Join(", ", plan.Risk.Factors);
                triggers.Add($"High-risk operation detected (factors: {factors})");
            }

            // Check 3: LOC breach (any step > 300)
            var locBreaches = plan.Plan.Steps
                .Where(s => s.EstimatedLoc > 300)
                .ToList();

            if (locBreaches.Any())
            {
                var maxLoc = locBreaches.Max(s => s.EstimatedLoc);
                triggers.Add($"LOC limit exceeded: Step {locBreaches[0].StepNumber} has {maxLoc} LOC (max 300)");
            }

            // Check 4: Step limit breach (>7 steps)
            if (plan.Plan.Steps.Count > 7)
            {
                triggers.Add($"Step limit exceeded: {plan.Plan.Steps.Count} steps (max 7)");
            }

            // Check 5: File deletions
            var deletions = plan.FileList
                .Where(f => f.Operation.Equals("delete", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (deletions.Any())
            {
                var files = string.Join(", ", deletions.Select(d => d.Path));
                triggers.Add($"File deletion detected: {files}");
            }

            return triggers.Any()
                ? ApprovalDecision.CreateRequired(triggers)
                : ApprovalDecision.CreateNotRequired();
        }
        catch (JsonException ex)
        {
            return ApprovalDecision.CreateRequired($"Invalid planner JSON: {ex.Message}");
        }
    }
}

/// <summary>
/// Represents the decision of whether approval is required for a plan.
/// </summary>
public sealed class ApprovalDecision
{
    /// <summary>
    /// Gets whether approval is required.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Gets the list of triggers that caused the approval requirement.
    /// </summary>
    public IReadOnlyList<string> Triggers { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the combined reason for requiring approval.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Creates an approval decision that requires approval.
    /// </summary>
    /// <param name="triggers">The list of triggers.</param>
    /// <returns>An approval decision requiring approval.</returns>
    public static ApprovalDecision CreateRequired(List<string> triggers)
    {
        return new ApprovalDecision
        {
            Required = true,
            Triggers = triggers.AsReadOnly(),
            Reason = string.Join("; ", triggers)
        };
    }

    /// <summary>
    /// Creates an approval decision that requires approval with a single reason.
    /// </summary>
    /// <param name="reason">The reason for requiring approval.</param>
    /// <returns>An approval decision requiring approval.</returns>
    public static ApprovalDecision CreateRequired(string reason)
    {
        return new ApprovalDecision
        {
            Required = true,
            Triggers = new List<string> { reason }.AsReadOnly(),
            Reason = reason
        };
    }

    /// <summary>
    /// Creates an approval decision that does not require approval.
    /// </summary>
    /// <returns>An approval decision not requiring approval.</returns>
    public static ApprovalDecision CreateNotRequired()
    {
        return new ApprovalDecision
        {
            Required = false,
            Triggers = Array.Empty<string>(),
            Reason = string.Empty
        };
    }
}
