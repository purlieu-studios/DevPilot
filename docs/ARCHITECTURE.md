# DevPilot Architecture: MASAI Pipeline as Reusable Engine

This document explains DevPilot's core architectural vision and how it implements the MASAI framework as a reusable pipeline orchestrator.

## Core Vision

**DevPilot is a reusable MASAI pipeline orchestrator** that operates on ANY C# repository without requiring code changes. You maintain ONE DevPilot repository, install it globally, and use it across hundreds of different projects - each with their own domain knowledge, agents, and conventions.

## What is MASAI?

**MASAI** stands for **Modular Architecture for Software-engineering AI** agents, a framework proposed by Arora et al. (2024) that divides complex software engineering problems into multiple sub-problems solved by specialized LLM-powered sub-agents.

**Reference**: Arora, D., Sonwane, A., Wadhwa, N., Mehrotra, A., Utpala, S., Bairi, R., Kanade, A., & Natarajan, N. (2024). *MASAI: Modular Architecture for Software-engineering AI Agents*. arXiv:2406.11638 [cs.AI]. https://arxiv.org/abs/2406.11638

### MASAI in DevPilot

DevPilot implements MASAI with **five specialized agents** (Planner, Coder, Reviewer, Tester, Evaluator), each with well-defined objectives and tuned strategies.

**This modular approach enables**:
1. Different problem-solving strategies across sub-agents
2. Information gathering from different repository sources
3. Shorter, focused trajectories that reduce costs and context noise

**Agent Pipeline**:
```
User Request
    ↓
Planner → Coder → Reviewer → Tester → Evaluator
    ↓         ↓          ↓         ↓          ↓
 Task       Code     Quality    Test      Quality
 Plan       Patch    Review    Results    Scores
```

Each agent is stateless and focuses on a single concern, making the system easier to test, debug, and extend.

## The Framework/Engine Model

DevPilot follows the proven framework architecture pattern:

| Framework | Engine (Reusable) | Domain (Project-Specific) |
|-----------|-------------------|---------------------------|
| **Django** | Web framework | Your models/views |
| **.NET** | Runtime/SDK | Your application logic |
| **DevPilot** | MASAI orchestrator | Your CLAUDE.md/agents |

**Key Principle**: DevPilot's code, documentation, and configuration are **IRRELEVANT** when executing in target repositories. Only the target repository's context matters.

## Two Distinct Contexts

DevPilot operates in TWO completely separate contexts that must NOT be confused:

### 1. DevPilot Repository (The Engine)

**Location**: `C:\DevPilot\DevPilot` (or wherever you cloned it)

**Purpose**: Provides reusable MASAI pipeline infrastructure

**Contents**:
```
C:\DevPilot\DevPilot/
├── src/DevPilot.Orchestrator/     ← Pipeline orchestration (Planning → Coding → Review → Test → Eval)
├── src/DevPilot.Agents/           ← Agent execution infrastructure (ClaudeCliClient, MCP integration)
├── src/DevPilot.Console/          ← CLI entry point (global tool)
├── .agents/                       ← DEFAULT agents (fallback if target repo has none)
│   ├── planner/
│   ├── coder/
│   ├── reviewer/
│   ├── tester/
│   └── evaluator/
├── CLAUDE.md                      ← How to develop DevPilot ITSELF (NOT read by target repos)
├── .runsettings                   ← For testing DevPilot's own code (NOT used in workspaces)
└── tests/                         ← DevPilot's unit tests

** This repository's context is IRRELEVANT when running in EcommerceApp **
```

**Installation**:
```bash
# One-time setup (or after updates)
dotnet tool install --global DevPilot

# Now available in ANY directory
cd C:\Projects\EcommerceApp
devpilot "Add email validation to User model"
```

### 2. Target Repository (Domain-Specific Context)

**Location**: ANY C# repository (e.g., `C:\Projects\EcommerceApp`)

**Purpose**: Contains domain knowledge, custom agents, and project-specific conventions

**Contents**:
```
C:\Projects\EcommerceApp/
├── CLAUDE.md                      ← "This is an e-commerce app. User has email/password. Payments via Stripe..."
├── .agents/                       ← CUSTOM agents (override DevPilot defaults)
│   ├── planner/
│   │   └── system-prompt.md       ← "You are planning features for an e-commerce platform..."
│   ├── security-reviewer/         ← CUSTOM agent (doesn't exist in DevPilot)
│   │   └── system-prompt.md       ← "Check for PCI compliance, SQL injection, OWASP Top 10..."
│   └── stripe-integration-tester/ ← Domain-specific testing agent
├── .commands/                     ← Custom slash commands
│   ├── deploy-to-prod.md
│   └── run-load-tests.md
├── .runsettings                   ← Test configuration for THIS app
├── docs/
│   ├── api-design.md
│   └── payment-flows.md
├── src/
│   ├── Models/User.cs
│   ├── Services/PaymentService.cs
│   └── ...
└── tests/EcommerceApp.Tests/

** This is what gets indexed by RAG **
** This is what agents read **
** This is the ONLY context that matters during execution **
```

**Target Repository Controls**:
- ✅ Project domain knowledge (CLAUDE.md)
- ✅ Custom agents and tools (.agents/)
- ✅ Testing framework and configuration (.runsettings, xUnit/NUnit/MSTest)
- ✅ Documentation structure (docs/)
- ✅ Code style and conventions (.editorconfig)
- ✅ Custom workflows (.commands/)

## Execution Flow: DevPilot in Target Repository

```bash
cd C:\Projects\EcommerceApp
devpilot "Add email validation to User model"
```

**Step-by-Step Execution**:

```
1. WorkspaceManager creates isolated workspace:
   .devpilot/workspaces/abc123-def4-5678/

2. WorkspaceManager copies files FROM EcommerceApp:
   .devpilot/workspaces/abc123/
   ├── CLAUDE.md                  ← EcommerceApp's domain knowledge
   ├── .agents/                   ← EcommerceApp's custom agents (if exist)
   ├── .runsettings               ← EcommerceApp's test config (if exists)
   ├── src/Models/User.cs
   ├── src/Services/PaymentService.cs
   └── tests/EcommerceApp.Tests/

3. RAG indexing (if --enable-rag flag used):
   RAG.Index(".devpilot/workspaces/abc123/")
   ↓
   Vector embeddings of:
   - EcommerceApp/CLAUDE.md          ← E-commerce domain
   - EcommerceApp/src/**/*.cs        ← Existing code
   - EcommerceApp/docs/**/*.md       ← API design, payment flows

   NOT DevPilot's files!
   NOT DevPilot's CLAUDE.md!

   Uses Ollama (mxbai-embed-large) for local embeddings
   Stores in SQLite vector database: .devpilot/rag/abc123.db

4. Planner Agent:
   System Prompt: .devpilot/workspaces/abc123/.agents/planner/system-prompt.md
                  (if exists, else DevPilot's default)

   Context: RAG query results from EcommerceApp

   Reads: .devpilot/workspaces/abc123/CLAUDE.md
          ↓
          "This is an e-commerce app. User model has email/password.
           Payments processed via Stripe. Follow PCI compliance..."

5. Coder Agent:
   Generates unified diff patch:
   --- a/src/Models/User.cs
   +++ b/src/Models/User.cs
   @@ -5,6 +5,12 @@ public class User
       public string Email { get; set; }
   +
   +   public bool IsValidEmail()
   +   {
   +       return Email.Contains("@");
   +   }

6. WorkspaceManager applies patch:
   .devpilot/workspaces/abc123/src/Models/User.cs

7. Tester Agent:
   cd .devpilot/workspaces/abc123
   dotnet test

   ↓ VSTest looks for .runsettings:
   .devpilot/workspaces/abc123/.runsettings
   ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
   EcommerceApp's config! (if exists)

   If not found: Uses VSTest defaults
   DevPilot's .runsettings is NEVER used

8. Custom Agents (if defined):
   .devpilot/workspaces/abc123/.agents/security-reviewer/
   ↓
   DevPilot discovers custom agent
   ↓
   Runs as part of pipeline (after standard Reviewer)
   ↓
   Checks PCI compliance, SQL injection, etc.

9. Evaluator Agent:
   Provides quality scores and recommendations
```

## The Reusability Vision

**You maintain ONE DevPilot repository**, but use it across unlimited projects:

```
DevPilot (installed globally via dotnet tool)
    ↓
    ├─→ C:\Projects\EcommerceApp
    │   ├── CLAUDE.md: "E-commerce platform, Stripe payments, PCI compliance..."
    │   └── .agents/security-reviewer/ (custom agent)
    │
    ├─→ C:\Projects\HealthcareApp
    │   ├── CLAUDE.md: "HIPAA-compliant patient records, HL7 integration..."
    │   └── .agents/hipaa-compliance-checker/ (custom agent)
    │
    ├─→ C:\Projects\GameEngine
    │   ├── CLAUDE.md: "Unity 3D game engine, physics simulation, rendering..."
    │   └── .agents/performance-profiler/ (custom agent)
    │
    └─→ C:\Projects\FinanceApp
        ├── CLAUDE.md: "Banking app, ACH transfers, SOX compliance..."
        └── .agents/financial-audit-reviewer/ (custom agent)
```

**Each project gets**:
- ✅ Same MASAI pipeline quality (Planning → Coding → Review → Test → Eval)
- ✅ Domain-specific context (their CLAUDE.md, not DevPilot's)
- ✅ Custom agents (their .agents/, merged with DevPilot defaults)
- ✅ Test configurations (their .runsettings, not DevPilot's)
- ✅ Isolated workspaces (no cross-contamination)

## Implementation Status

### ✅ Working Today

- WorkspaceManager creates isolated workspaces
- WorkspaceManager copies files from target repo
- Agents execute in workspace context
- Default agents (planner, coder, reviewer, tester, evaluator)
- Repository structure awareness (auto-detects project directories)
- RAG integration with Ollama (optional via --enable-rag flag)

### ⚠️ Needs Verification

- [ ] Do agents read CLAUDE.md from workspace? (Or DevPilot's CLAUDE.md?)
- [ ] Does DevPilot discover custom agents from `.devpilot/workspaces/abc123/.agents/`?
- [ ] Are custom agents merged with default pipeline?

### 📋 Future Enhancements

- ✅ ~~RAG indexing of workspace files~~ (Implemented in PR #51 - see RAG.md)
- Custom agent discovery and execution
- .commands/ support (domain-specific workflows)
- WPF UI for repository selection and pipeline monitoring

## DevPilot Repository Scope

**What Lives Here**:
- ✅ Pipeline orchestration logic (src/DevPilot.Orchestrator)
- ✅ Agent execution infrastructure (src/DevPilot.Agents)
- ✅ Default agents (fallback if target repo has none)
- ✅ DevPilot's own tests (tests/)
- ✅ DevPilot's .runsettings (for testing DevPilot code only)
- ✅ DevPilot's CLAUDE.md (how to develop DevPilot itself)

**What Does NOT Live Here**:
- ❌ Domain knowledge for target repositories
- ❌ Custom agents for specific projects
- ❌ Test configurations for external repos
- ❌ Project-specific documentation

## Example: Running DevPilot in External Repository

```bash
cd C:\TestProject
devpilot "Add authentication to User class"
```

**What Happens**:
1. DevPilot reads `C:\TestProject\CLAUDE.md` (NOT `C:\DevPilot\DevPilot\CLAUDE.md`)
2. WorkspaceManager copies files from `C:\TestProject` to `.devpilot/workspaces/<id>/`
3. Planner agent reads `C:\TestProject\CLAUDE.md` and generates plan
4. Coder agent generates unified diff patch
5. WorkspaceManager applies patch to workspace files
6. Tester agent runs `dotnet test` in workspace (uses `C:\TestProject\.runsettings` if present)
7. Reviewer and Evaluator agents analyze results
8. Pipeline completes, workspace preserved for inspection

## Testing Configuration Scopes

| Configuration File | Scope | Purpose |
|-------------------|-------|---------|
| `C:\DevPilot\DevPilot\.runsettings` | DevPilot's own tests | Prevents DevPilot unit tests from hanging (10s timeout, Microsoft.CodeCoverage) |
| `C:\TestProject\.runsettings` | Target repo tests | Used by TestRunner when executing tests in workspace (optional) |

**Important**: TestRunner.cs does NOT force .runsettings usage. It respects whatever test configuration exists in the target repository.

## Design Principles

### Wrong vs. Right Thinking

❌ **WRONG Thinking**: "Let me add .runsettings to WorkspaceManager so all tests use it"
- This would force DevPilot's test configuration on external repositories
- Breaks repos using NUnit, MSTest, or custom xUnit configurations
- Violates principle of respecting target repo's conventions

✅ **RIGHT Thinking**: "Target repos control their own test configuration"
- TestRunner uses whatever .runsettings exists in workspace (or none)
- DevPilot's .runsettings only affects DevPilot's own CI/CD tests
- Workspaces inherit target repo's test configuration naturally

### Development Guidelines

When modifying DevPilot:

1. **WorkspaceManager**: Only copies files from target repo. Do NOT inject DevPilot's configuration files.
2. **TestRunner**: Must work with ANY test framework/configuration. No assumptions about .runsettings existence.
3. **Agents**: Read target repo's CLAUDE.md, NOT DevPilot's CLAUDE.md.
4. **Pipeline**: Executes in workspace context, NOT DevPilot repo context.

## Component Overview

### Pipeline Orchestration (src/DevPilot.Orchestrator)

**Key Classes**:
- `Pipeline.cs` - Executes the 5-stage MASAI workflow
- `WorkspaceManager.cs` - Creates isolated workspaces, copies files, applies patches
- `PipelineContext.cs` - Shared state across agents

**Responsibilities**:
- Stage sequencing (Planning → Coding → Review → Test → Eval)
- Input/output passing between stages
- Workspace lifecycle management (create, execute, cleanup)
- Project structure analysis (auto-detect directories)

### Agent Execution (src/DevPilot.Agents)

**Key Classes**:
- `ClaudeCliClient.cs` - Executes Claude CLI as subprocess
- `PlannerAgent.cs`, `CoderAgent.cs`, etc. - Agent-specific logic
- MCP server integration (for Planner & Evaluator)

**Responsibilities**:
- Agent definition loading (system prompts, models, tools)
- Process spawning and timeout management
- MCP tool execution and result parsing
- Error handling and retry logic

### Core Models (src/DevPilot.Core)

**Key Classes**:
- `AgentDefinition.cs` - Agent configuration (name, model, system prompt)
- `AgentResult.cs` - Agent execution output
- `ProjectStructureInfo.cs` - Repository structure metadata
- `PatchApplicationResult.cs` - Patch application status

## Related Documentation

- [CLAUDE.md](../CLAUDE.md) - Main project instructions and practical usage
- [PIPELINE.md](./PIPELINE.md) - Detailed pipeline stage documentation
- [RAG.md](./RAG.md) - Retrieval Augmented Generation architecture
- [LESSONS_LEARNED.md](./LESSONS_LEARNED.md) - Production testing insights
