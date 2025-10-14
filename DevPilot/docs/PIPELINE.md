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
- Patch (unified diff from Coding stage)
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
- Apply patch to temporary workspace
- Run `dotnet test` and other test commands
- Parse test output
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

| Directory Name     | Status | Needs Action |
|--------------------|--------|--------------|
| `planner`          | ✅ Good | None |
| `code-generator`   | 🔄 Rename | → `coder` |
| `validator`        | 🔄 Rename | → `reviewer` |
| `orchestrator`     | ❌ Delete | Not needed in linear pipeline |

### Missing Agent Definitions

| Agent Name  | Stage      | Status | Description |
|-------------|------------|--------|-------------|
| `tester`    | Testing    | ❌ Missing | Executes tests, reports results |
| `evaluator` | Evaluating | ❌ Missing | Scores quality, final verdict |

### Final Agent Structure (Target)

```
.agents/
├── planner/           ✅ Already exists
├── coder/             🔄 Rename from code-generator
├── reviewer/          🔄 Rename from validator
├── tester/            ❌ Create new
└── evaluator/         ❌ Create new
```

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

**Total**: 149 tests passing (143 unit + 6 integration)

### 🚧 In Progress

None currently.

### ❌ Missing Components

| Component | Description | Priority | Estimated PR |
|-----------|-------------|----------|--------------|
| ClaudeCliAgent | IAgent implementation using ClaudeCliClient | High | #14 |
| Agent Renaming | Rename code-generator → coder, validator → reviewer | High | #15 |
| Tester Agent Definition | system-prompt.md, tools.json, config.json | High | #16 |
| Evaluator Agent Definition | system-prompt.md, tools.json, config.json | High | #17 |
| Program.cs Wiring | Full pipeline with approval prompts | High | #18 |

---

## Next Steps (Roadmap)

### PR #14: ClaudeCliAgent

**Goal**: Create concrete IAgent implementation that uses ClaudeCliClient

**Components**:
- `ClaudeCliAgent.cs` - Implements IAgent interface
- Reads agent definition from AgentLoader
- Builds system prompt from system-prompt.md
- Calls ClaudeCliClient.ExecuteAsync()
- Parses response and returns AgentResult
- Comprehensive tests

**Estimated LOC**: 200-250

---

### PR #15: Agent Renaming

**Goal**: Align agent names with pipeline stages

**Changes**:
- Rename `.agents/code-generator/` → `.agents/coder/`
- Rename `.agents/validator/` → `.agents/reviewer/`
- Delete `.agents/orchestrator/` (not needed)
- Update agent names in config.json files
- Update references in system-prompt.md files

**Estimated LOC**: 50-100 (mostly renames)

---

### PR #16: Tester Agent Definition

**Goal**: Create declarative agent for Testing stage

**Files**:
- `.agents/tester/system-prompt.md` - Instructions for test execution
- `.agents/tester/tools.json` - Tools: execute_command, parse_test_output
- `.agents/tester/config.json` - Model: Claude Sonnet 4.5, temperature 0.2

**Responsibilities**:
- Apply patch to temporary workspace
- Execute test commands (dotnet test)
- Parse xUnit/NUnit output
- Calculate coverage metrics
- Return JSON test report

**Estimated LOC**: 200-250

---

### PR #17: Evaluator Agent Definition

**Goal**: Create declarative agent for Evaluating stage

**Files**:
- `.agents/evaluator/system-prompt.md` - Scoring criteria and examples
- `.agents/evaluator/tools.json` - Tools: calculate_metrics
- `.agents/evaluator/config.json` - Model: Claude Sonnet 4.5, temperature 0.3

**Responsibilities**:
- Review all stage outputs
- Score 5 quality dimensions (0-10 scale)
- Identify strengths and weaknesses
- Provide actionable recommendations
- Return JSON evaluation with final verdict

**Estimated LOC**: 200-250

---

### PR #18: CLI Application Wiring

**Goal**: Wire up Program.cs with full pipeline execution

**Components**:
- Load agent definitions using AgentLoader
- Create ClaudeCliAgent instances for each stage
- Build Pipeline with agent dictionary
- Parse command-line arguments
- Execute pipeline with user request
- Handle approval prompts (interactive CLI)
- Display results to user
- Error handling and logging

**Features**:
- `devpilot "Create Calculator class"` - Execute request
- `devpilot --approve <pipeline-id>` - Resume after approval
- `devpilot --status <pipeline-id>` - Check pipeline status
- `devpilot --list` - Show recent pipelines

**Estimated LOC**: 250-300

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
