# MASAI Pipeline Architecture

This document defines the complete MASAI (Modular Autonomous Software AI) pipeline architecture for DevPilot.

## Overview

DevPilot implements a **linear 5-agent pipeline** where each agent performs a specialized task in sequence:

```
User Request
    ↓
[1] Planner  → JSON plan with steps, files, risk assessment
    ↓
[2] Coder    → Unified diff patch implementing the plan
    ↓
[3] Reviewer → JSON verdict (APPROVE/REJECT/REVISE)
    ↓
[4] Tester   → JSON test report (pass/fail with details)
    ↓
[5] Evaluator → JSON quality scores (0-10 metrics)
    ↓
Result (Success/Failure/AwaitingApproval)
```

### Key Design Principles

1. **Sequential Execution**: Each agent runs in strict order, no parallelism
2. **Hard Stops**: Pipeline pauses for human approval when risk thresholds exceeded
3. **Immutable Context**: Each stage receives immutable input, produces immutable output
4. **Audit Trail**: Every stage transition recorded with timestamp
5. **Fail-Fast**: Any agent failure stops the entire pipeline immediately

## Pipeline Stages

### Stage 1: Planning

**Purpose**: Analyze user request and create detailed execution plan

**Agent**: `planner`

**Input**:
- User request (raw string)

**Output** (JSON):
```json
{
  "plan": {
    "summary": "Brief description",
    "steps": [
      {
        "step_number": 1,
        "description": "What this step does",
        "file_target": "path/to/file.cs",
        "agent": "coder",
        "estimated_loc": 150
      }
    ]
  },
  "file_list": [
    {"path": "src/File.cs", "operation": "create|modify|delete", "reason": "Why needed"}
  ],
  "risk": {
    "level": "low|medium|high",
    "factors": ["Risk factor 1", "Risk factor 2"],
    "mitigation": "How risks will be addressed"
  },
  "verify": {
    "acceptance_criteria": ["Criterion 1"],
    "test_commands": ["dotnet test"],
    "manual_checks": ["Manual check 1"]
  },
  "rollback": {
    "strategy": "How to undo changes",
    "commands": ["git restore file.cs"],
    "notes": "Additional guidance"
  },
  "needs_approval": false,
  "approval_reason": "Only present if needs_approval is true"
}
```

**Constraints**:
- 3-7 steps maximum
- 1 file per step maximum
- 300 LOC per step maximum

**Approval Gate**: After this stage, 5 triggers checked (see Approval Gates section)

---

### Stage 2: Coding

**Purpose**: Generate unified diff patch implementing the plan

**Agent**: `coder`

**Input**:
- Plan JSON from Planning stage

**Output** (Unified Diff):
```diff
diff --git a/src/Calculator.cs b/src/Calculator.cs
new file mode 100644
index 0000000..abc1234
--- /dev/null
+++ b/src/Calculator.cs
@@ -0,0 +1,15 @@
+namespace DevPilot;
+
+public class Calculator
+{
+    public int Add(int a, int b) => a + b;
+    public int Subtract(int a, int b) => a - b;
+}
```

**Responsibilities**:
- Follow plan steps exactly
- Generate idiomatic C# code
- Follow .editorconfig rules
- Include XML documentation
- Stay within LOC limits per file

---

### Stage 3: Reviewing

**Purpose**: Validate code quality, style, and correctness

**Agent**: `reviewer`

**Input**:
- Patch (unified diff from Coding stage)

**Output** (JSON):
```json
{
  "verdict": "APPROVE|REJECT|REVISE",
  "issues": [
    {
      "severity": "error|warning|info",
      "file": "src/Calculator.cs",
      "line": 15,
      "message": "Missing XML documentation",
      "suggestion": "Add /// <summary> comment"
    }
  ],
  "summary": "Brief assessment of code quality",
  "metrics": {
    "complexity": 3,
    "maintainability": 8,
    "test_coverage_estimate": 0
  }
}
```

**Validation Checks**:
- Syntax correctness
- Naming conventions (PascalCase, camelCase, _camelCase)
- XML documentation presence
- Code complexity
- .editorconfig compliance
- SOLID principles

**Verdicts**:
- **APPROVE**: Code meets all standards, proceed to Testing
- **REJECT**: Critical issues, pipeline fails
- **REVISE**: Minor issues, feedback loop to Coder (not yet implemented)

---

### Stage 4: Testing

**Purpose**: Execute tests and verify functionality

**Agent**: `tester`

**Input**:
- Workspace path (isolated directory with applied patches)
- List of applied files
- Verify section from Plan (test commands, acceptance criteria)

**Output** (JSON):
```json
{
  "pass": true,
  "summary": "All 15 tests passed",
  "test_results": [
    {
      "test_name": "Calculator_Add_ReturnsSum",
      "status": "passed|failed|skipped",
      "duration_ms": 23,
      "message": "Optional failure message"
    }
  ],
  "coverage": {
    "line_coverage_percent": 95,
    "branch_coverage_percent": 87
  },
  "performance": {
    "total_duration_ms": 1234,
    "slowest_test": "LongRunningTest"
  }
}
```

**Responsibilities**:
- Navigate to isolated workspace (patch already applied by Pipeline)
- Build solution with `dotnet build`
- Run `dotnet test` and other test commands
- Parse test output (TRX format)
- Calculate coverage metrics
- Detect flaky tests
- Report performance issues

---

### Stage 5: Evaluating

**Purpose**: Score overall quality and provide final assessment

**Agent**: `evaluator`

**Input** (all previous outputs):
- User request
- Plan JSON
- Patch (unified diff)
- Review JSON
- Test report JSON

**Output** (JSON):
```json
{
  "overall_score": 8.5,
  "scores": {
    "plan_quality": 9.0,
    "code_quality": 8.5,
    "test_coverage": 9.5,
    "documentation": 7.0,
    "maintainability": 8.0
  },
  "strengths": [
    "Comprehensive test coverage",
    "Clean separation of concerns"
  ],
  "weaknesses": [
    "Missing XML docs on some methods",
    "High cyclomatic complexity in one method"
  ],
  "recommendations": [
    "Add more inline comments for complex logic",
    "Consider extracting validation logic to separate class"
  ],
  "final_verdict": "ACCEPT|REJECT",
  "justification": "Code meets quality standards with minor documentation gaps"
}
```

**Scoring Criteria** (0-10 scale):
- **Plan Quality**: Completeness, clarity, feasibility
- **Code Quality**: Readability, patterns, idioms
- **Test Coverage**: Percentage, edge cases, assertions
- **Documentation**: XML docs, inline comments, README updates
- **Maintainability**: Complexity, cohesion, coupling

**Final Verdict**:
- **ACCEPT**: Overall score ≥ 7.0, present results to user
- **REJECT**: Overall score < 7.0, pipeline fails with detailed feedback

---

## Approval Gates

After the **Planning** stage, the `ApprovalGate` evaluates the plan against 5 triggers. If **any** trigger fires, the pipeline enters `AwaitingApproval` stage (hard stop).

### Trigger 1: Planner Explicitly Flags Approval

**Condition**: `needs_approval: true` in plan JSON

**Reasons**:
- Task exceeds capability limits
- Files outside allowlist
- Ambiguous requirements requiring clarification

**Example**:
```json
{
  "needs_approval": true,
  "approval_reason": "Task requires database migration which is high-risk"
}
```

---

### Trigger 2: High-Risk Operation

**Condition**: `risk.level == "high"`

**High-Risk Factors**:
- File deletions
- Authentication/authorization changes
- Database migrations
- Breaking API changes
- Dependency upgrades (major versions)

**Example**:
```json
{
  "risk": {
    "level": "high",
    "factors": ["File deletion", "Auth changes"],
    "mitigation": "Backup before deletion"
  }
}
```

---

### Trigger 3: LOC Limit Breach

**Condition**: Any step has `estimated_loc > 300`

**Reason**: Commits exceeding 300 LOC are harder to review and more error-prone

**Example**:
```json
{
  "plan": {
    "steps": [
      {"step_number": 1, "estimated_loc": 450, ...}
    ]
  }
}
```

**Approval Message**: "LOC limit exceeded: Step 1 has 450 LOC (max 300)"

---

### Trigger 4: Step Limit Breach

**Condition**: Plan has more than 7 steps

**Reason**: Tasks with >7 steps should be broken into multiple smaller tasks

**Example**:
```json
{
  "plan": {
    "steps": [
      {"step_number": 1, ...},
      {"step_number": 2, ...},
      ...
      {"step_number": 8, ...}
    ]
  }
}
```

**Approval Message**: "Step limit exceeded: 8 steps (max 7)"

---

### Trigger 5: File Deletion Detected

**Condition**: Any file operation is `"delete"`

**Reason**: Deletions are irreversible and high-risk

**Example**:
```json
{
  "file_list": [
    {"path": "old/Legacy.cs", "operation": "delete", "reason": "Cleanup"}
  ]
}
```

**Approval Message**: "File deletion detected: old/Legacy.cs"

---

### Multiple Triggers

If multiple triggers fire, all are combined into a single approval reason:

**Example**:
```
Approval Required:
- Planner flagged needs_approval: Database migration required
- High-risk operation detected (factors: Migration, Breaking changes)
- LOC limit exceeded: Step 2 has 420 LOC (max 300)
```

---

## Data Flow

### PipelineContext State

The `PipelineContext` object carries state through all stages:

```csharp
public sealed class PipelineContext
{
    // Identity
    public string PipelineId { get; }                    // Unique GUID
    public string UserRequest { get; }                   // Original request

    // Stage outputs
    public string? Plan { get; private set; }            // Planning JSON
    public string? Patch { get; private set; }           // Coding diff
    public string? Review { get; private set; }          // Reviewing JSON
    public string? TestReport { get; private set; }      // Testing JSON
    public string? Scores { get; private set; }          // Evaluating JSON

    // Workspace tracking
    public string? WorkspaceRoot { get; private set; }   // Isolated workspace path
    public IReadOnlyList<string>? AppliedFiles { get; private set; }  // Files modified by patch

    // Current state
    public PipelineStage CurrentStage { get; }           // Current stage
    public bool ApprovalRequired { get; }                // Hard stop flag
    public string? ApprovalReason { get; }               // Why stopped

    // Audit trail
    public IReadOnlyList<PipelineStageEntry> StageHistory { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? CompletedAt { get; }
}
```

### Stage Transitions

Each stage transition is recorded in `StageHistory`:

```csharp
public sealed class PipelineStageEntry
{
    public PipelineStage Stage { get; }           // Stage entered
    public PipelineStage PreviousStage { get; }   // Previous stage
    public DateTimeOffset EnteredAt { get; }      // Timestamp
}
```

**Example History**:
```
1. NotStarted → Planning     @ 2025-10-14T12:00:00Z
2. Planning → Coding         @ 2025-10-14T12:00:15Z
3. Coding → Reviewing        @ 2025-10-14T12:01:30Z
4. Reviewing → Testing       @ 2025-10-14T12:01:45Z
5. Testing → Evaluating      @ 2025-10-14T12:03:00Z
6. Evaluating → Completed    @ 2025-10-14T12:03:10Z
```

---

## Agent Mapping

### Current Agent Definitions (`.agents/`)

All agents are now implemented and correctly mapped to pipeline stages:

| Agent Directory | Pipeline Stage | Status | Description |
|-----------------|----------------|--------|-------------|
| `planner`       | Planning       | ✅ Complete | Creates execution plan with MCP tools |
| `coder`         | Coding         | ✅ Complete | Generates unified diff patches |
| `reviewer`      | Reviewing      | ✅ Complete | Semantic validation with verdicts |
| `tester`        | Testing        | ✅ Complete | Receives workspace for test execution |
| `evaluator`     | Evaluating     | ✅ Complete | Quality scoring with MCP tools |

### Agent Structure (Current)

```
.agents/
├── planner/           ✅ MCP tools for structured JSON output
├── coder/             ✅ Outputs unified diff patches
├── reviewer/          ✅ Semantic validation (quality, intent, patterns)
├── tester/            ✅ Real test execution with TestRunner utility
└── evaluator/         ✅ MCP tools for structured scoring
```

**Note**: All agents renamed and aligned with pipeline stages (PR #16, #17, #18).

---

## Implementation Status

### ✅ Completed

| Component | Description | PR |
|-----------|-------------|-----|
| PipelineContext | State management for pipeline execution | #10 |
| PipelineStage | Enum defining all stages | #10 |
| PipelineResult | Result model for pipeline outcomes | #11 |
| Pipeline | Main orchestrator executing 5 stages | #11 |
| PlannerOutput | Data models for planner JSON | #12 |
| ApprovalGate | Evaluator with 5 triggers | #12 |
| ClaudeCliResponse | Response model for CLI subprocess | #13 |
| ClaudeCliClient | Subprocess executor for Claude CLI | #13 |
| Integration Tests | Separate project for scheduled API testing | #13 |
| Planner Agent | Declarative agent definition | #9 |
| ClaudeCliAgent | IAgent implementation using ClaudeCliClient | #15 |
| AgentDefinition Simplification | Reduced from 137 LOC to 32 LOC (model alias strings) | #15 |
| AgentLoader Simplification | Reduced from 220 LOC to 120 LOC | #15 |
| PATH Resolution Fix | Automatic discovery of Claude CLI in PATH | #15 |
| Coder Agent (renamed) | Renamed from code-generator to match pipeline stage | #16 |
| Reviewer Agent (renamed) | Renamed from validator to match pipeline stage | #16 |
| Tester Agent | Declarative agent definition for Testing stage | #17 |
| Evaluator Agent | Declarative agent definition for Evaluating stage | #18 |
| CLI Application Wiring | Full pipeline execution with approval prompts | #19 |
| MCP Planning Experiment | Structured planning via tool calling | #20 |
| MCP Integration | Integrated MCP server into ClaudeCliClient | #21 |
| MCP Production Fixes | Fixed .cmd wrapper and stream-json parsing | #22 |
| Agent Loading Smoke Tests | Infrastructure verification without API calls | #23 |
| MCP Evaluator Integration | Extended MCP tools for structured evaluation output | #24 |
| Unified Diff Parser | Git-style diff parsing with regex state machine (~320 LOC) | main |
| Workspace Manager | Isolated workspace creation, patch application, rollback (~400 LOC) | main |
| Coder Prompt Fix | Updated to output unified diffs instead of JSON | main |
| Tester Workspace Integration | Tester receives workspace path instead of raw patch | #25 |
| Reviewer Semantic Validation | Semantic-only validation with verdict enforcement | #26 |
| PIPELINE.md Updates | Documentation updates for PRs #24-26 | #27 |
| E2E Workspace Tests | Comprehensive workspace infrastructure validation tests | #28 |
| Reviewer Feedback Loop | REVISE verdict with iterative code improvements (max 2 iterations) | #29 |
| Real Test Execution | TestRunner utility with TRX parsing and native test execution | #30 |

**Total**: 170 unit tests passing + 19 integration tests (189 total)

### 🚧 In Progress

None currently.


---

## Next Steps

### ✅ Recently Completed (PR #27-30)

- **PR #27**: PIPELINE.md Documentation Updates - Updated docs for PRs #24-26
- **PR #28**: E2E Workspace Tests - Comprehensive workspace infrastructure validation without API calls
- **PR #29**: Reviewer Feedback Loop - REVISE verdict with iterative code improvements (max 2 iterations)
- **PR #30**: Real Test Execution - TestRunner utility with TRX parsing and native test execution

### 🎯 Current Focus

**✅ PR #24 - End-to-End Validation & MCP Evaluator Integration:**

Successfully validated the complete pipeline infrastructure and fixed critical evaluator bug with MCP tool integration.

**Infrastructure Validation (All Pass ✅):**
- **Pipeline Orchestration**: All 5 stages executed sequentially without errors
- **Agent Loading**: All agents loaded from `.agents/` directory successfully
- **MCP Integration**: Both Planner and Evaluator use MCP tools for structured output
- **Approval Gates**: Correctly evaluated and didn't trigger for low-risk request
- **State Management**: PipelineContext successfully passed data through all stages
- **Stage History**: All 6 transitions recorded (NotStarted → Planning → Coding → Reviewing → Testing → Evaluating → Completed)
- **Error Handling**: Pipeline completed successfully with proper evaluation scores

**Performance Metrics:**
- **Total Duration**: ~4.5 minutes (faster than initial 10-15 min estimate!)
- **Planner Stage**: ~2-3 minutes with MCP tools
- **Evaluator Stage**: ~1 minute with MCP tools
- **Other Stages**: ~30-60 seconds each (placeholder responses)

**Bugs Fixed:**
1. ❌ **Display Bug**: Program.cs:187 - Spectre.Console markup parsing failed on raw JSON with newlines
   - ✅ **Fixed**: Changed `MarkupLine()` to `WriteLine()` for raw score display

2. ❌ **Critical Bug**: Evaluator returned conversational text instead of JSON
   - ✅ **Fixed**: Extended MCP server with 7 evaluation tools (evaluation_init, set_scores, add_strength, add_weakness, add_recommendation, set_verdict, finalize_evaluation)
   - Now outputs pure structured JSON via schema-validated tool calling
   - Pipeline completes successfully with accurate scores: **9.4/10 ACCEPT** ✅

3. ❌ **Critical Bug**: Pipeline.cs ignored Evaluator verdict
   - ✅ **Fixed**: Added `ParseEvaluatorVerdict()` method and verdict checking logic
   - Now correctly fails pipeline when verdict is "REJECT" or score < 7.0

**MCP Server Architecture:**
- Renamed "planning-tools" → "pipeline-tools" to reflect dual purpose
- Extended existing server instead of duplicating (~80 LOC vs ~270 LOC duplicate)
- Weighted score calculation: `(plan×1.0 + code×1.5 + test×1.5 + doc×1.0 + maint×1.0) / 6.0`
- Documented "Extend vs Duplicate" principle in CLAUDE.md

**✅ PR #25 - Tester Workspace Integration:**

Integrated Tester with workspace-based test execution instead of raw patch processing.

**Changes:**
- Updated `Pipeline.cs` to pass workspace path to Tester instead of raw patch
- Updated Tester system prompt to navigate to workspace and run tests there
- Tester now receives: workspace path + list of applied files

**✅ PR #26 - Reviewer Semantic Validation & Verdict Enforcement:**

Fixed Reviewer to perform semantic validation and enforce REJECT verdicts.

**Changes:**
- Rewrote Reviewer system prompt (~327 LOC) for semantic-only validation
- Clear separation: Reviewer judges quality/intent, Tester verifies mechanics
- Added `ParseReviewerVerdict()` method to `Pipeline.cs`
- Pipeline now fails when Reviewer returns REJECT verdict
- Added 2 unit tests for verdict enforcement (160 total tests passing)

**Direct-to-Main Commits (Unified Diff & Workspace):**
- `f4f50af`: Implemented unified diff parsing (~320 LOC)
- `f4f50af`: Implemented workspace manager (~400 LOC)
- `f4f50af`: Integrated patch application into Pipeline.cs
- `5dc8c68`: Fixed Coder system prompt to output unified diffs (not JSON)

**Infrastructure Now Complete:**
- ✅ Unified diff parsing and validation
- ✅ Workspace creation and management (isolated per pipeline execution)
- ✅ Patch application with rollback capability
- ✅ Workspace cleanup in all exit paths
- ✅ Reviewer verdict enforcement
- ✅ Tester workspace integration

**Current Limitations (Known & Documented):**
1. ✅ ~~Test execution pending~~ → **RESOLVED in PR #30**: Tester now runs real tests with TestRunner utility
2. **Real API calls required**: Integration tests require live Claude API access (not suitable for CI/CD without mocking)
3. ✅ ~~Reviewer feedback loop~~ → **RESOLVED in PR #29**: REVISE verdict implemented with 2-iteration feedback loop

### 📋 Future Work

1. ✅ ~~Implement Real Test Execution~~ → **COMPLETED in PR #30**
2. **Add Mock Execution Tests**: Create integration tests with mocked Claude CLI responses for faster feedback (next priority)
3. ✅ ~~Implement Reviewer Feedback Loop~~ → **COMPLETED in PR #29**
4. **Add Rollback UI**: User interface for triggering rollback on pipeline failure
5. **Performance Optimization**: Reduce Claude CLI subprocess overhead
6. **CI/CD Integration**: Design testing strategy for automated builds without API calls

---

## Example End-to-End Flow

### User Request
```
devpilot "Add Calculator class with Add and Subtract methods"
```

### Stage 1: Planning
```json
{
  "plan": {
    "summary": "Create Calculator class with basic arithmetic",
    "steps": [
      {"step_number": 1, "description": "Create Calculator class", "file_target": "src/Calculator.cs", "agent": "coder", "estimated_loc": 45},
      {"step_number": 2, "description": "Create tests", "file_target": "tests/CalculatorTests.cs", "agent": "coder", "estimated_loc": 120}
    ]
  },
  "file_list": [
    {"path": "src/Calculator.cs", "operation": "create", "reason": "Implementation"},
    {"path": "tests/CalculatorTests.cs", "operation": "create", "reason": "Test coverage"}
  ],
  "risk": {"level": "low", "factors": ["Isolated class"], "mitigation": "Tests"},
  "needs_approval": false
}
```

**ApprovalGate**: ✅ All checks pass, continue

---

### Stage 2: Coding
```diff
diff --git a/src/Calculator.cs b/src/Calculator.cs
new file mode 100644
--- /dev/null
+++ b/src/Calculator.cs
@@ -0,0 +1,15 @@
+namespace DevPilot;
+
+/// <summary>
+/// Provides basic arithmetic operations.
+/// </summary>
+public class Calculator
+{
+    /// <summary>Adds two integers.</summary>
+    public int Add(int a, int b) => a + b;
+
+    /// <summary>Subtracts two integers.</summary>
+    public int Subtract(int a, int b) => a - b;
+}

diff --git a/tests/CalculatorTests.cs b/tests/CalculatorTests.cs
new file mode 100644
--- /dev/null
+++ b/tests/CalculatorTests.cs
@@ -0,0 +1,20 @@
+using Xunit;
+using FluentAssertions;
+
+public class CalculatorTests
+{
+    [Fact]
+    public void Add_ReturnsSumOfTwoNumbers()
+    {
+        var calc = new Calculator();
+        calc.Add(2, 3).Should().Be(5);
+    }
+
+    [Fact]
+    public void Subtract_ReturnsDifference()
+    {
+        var calc = new Calculator();
+        calc.Subtract(5, 3).Should().Be(2);
+    }
+}
```

---

### Stage 3: Reviewing
```json
{
  "verdict": "APPROVE",
  "issues": [],
  "summary": "Code follows all conventions and is well-documented",
  "metrics": {
    "complexity": 1,
    "maintainability": 10,
    "test_coverage_estimate": 100
  }
}
```

---

### Stage 4: Testing
```json
{
  "pass": true,
  "summary": "All 2 tests passed",
  "test_results": [
    {"test_name": "Add_ReturnsSumOfTwoNumbers", "status": "passed", "duration_ms": 12},
    {"test_name": "Subtract_ReturnsDifference", "status": "passed", "duration_ms": 8}
  ],
  "coverage": {
    "line_coverage_percent": 100,
    "branch_coverage_percent": 100
  }
}
```

---

### Stage 5: Evaluating
```json
{
  "overall_score": 9.5,
  "scores": {
    "plan_quality": 10.0,
    "code_quality": 9.5,
    "test_coverage": 10.0,
    "documentation": 9.0,
    "maintainability": 10.0
  },
  "strengths": [
    "Excellent test coverage",
    "Clear documentation",
    "Simple, maintainable design"
  ],
  "weaknesses": [],
  "recommendations": [],
  "final_verdict": "ACCEPT",
  "justification": "Excellent implementation meeting all quality standards"
}
```

---

### Result
```
✅ Pipeline completed successfully in 3.2 seconds

Overall Score: 9.5/10

Changes:
  + src/Calculator.cs (15 lines)
  + tests/CalculatorTests.cs (20 lines)

Tests: 2 passed, 0 failed
Coverage: 100% line, 100% branch

Ready to apply? (y/n):
```

---

## Design Decisions

### Why Linear Pipeline?

**Pros**:
- Simple to understand and debug
- Predictable execution order
- Clear failure points
- Easy to test

**Cons**:
- Can't parallelize independent tasks
- Slower than parallel execution

**Decision**: Simplicity and debuggability outweigh performance for initial version. Parallelism can be added later if needed.

---

### Why Hard Stops?

**Reason**: Human oversight prevents costly mistakes (e.g., accidental deletions, breaking changes).

**Alternative Considered**: Soft warnings that can be ignored
**Rejected Because**: Too risky for production use

---

### Why JSON Output Formats?

**Pros**:
- Structured, machine-parseable
- Schema validation possible
- Easy to extend

**Cons**:
- More verbose than plain text
- Agents must follow strict format

**Decision**: Structure and validation benefits outweigh verbosity cost.

---

## References

- **ApprovalGate Implementation**: `src/DevPilot.Orchestrator/ApprovalGate.cs`
- **Pipeline Orchestrator**: `src/DevPilot.Orchestrator/Pipeline.cs`
- **PipelineContext**: `src/DevPilot.Core/PipelineContext.cs`
- **Agent Definitions**: `.agents/*/`
- **Commit Standards**: `docs/COMMIT_STANDARDS.md`
- **Code Guardrails**: `docs/GUARDRAILS.md`
