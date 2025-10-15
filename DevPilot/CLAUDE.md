# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DevPilot is a MASAI (Modular Autonomous Software AI) pipeline for automated code generation and review. It implements a linear 5-agent pipeline (Planner → Coder → Reviewer → Tester → Evaluator) that transforms user requests into production-ready code with comprehensive testing and quality scoring.

## Environment

- **Platform**: Windows
- **IDE Setup**: Visual Studio (based on .gitignore configuration)

## Required Reading for Claude Code

**IMPORTANT:** Before performing any code changes, commits, or pushes, Claude Code MUST read and follow all rules in these documents:

- **Code Quality Standards**: `docs/GUARDRAILS.md`
  - LOC limits (200-300 lines per commit)
  - Naming conventions and code style
  - File management and organization
  - Branch naming and workflow rules

- **Commit Message Standards**: `docs/COMMIT_STANDARDS.md`
  - Conventional Commits format
  - Valid commit types and scopes
  - Subject line rules and examples
  - Blocked words and validation rules

- **Security Guidelines**: `docs/SECURITY.md`
  - Secret management (never commit credentials)
  - Input validation requirements
  - Authentication/authorization standards
  - Data protection and encryption

- **Git Hooks Documentation**: `.husky/README.md`
  - Automated enforcement details
  - Hook-specific requirements
  - Troubleshooting and bypass procedures

- **Pipeline Architecture**: `docs/PIPELINE.md`
  - Complete MASAI pipeline specification
  - Agent definitions and responsibilities
  - Data flow and approval gates
  - Implementation status and roadmap

These documents contain detailed rules and examples. Claude Code should proactively reference them when:
- Creating commits (check COMMIT_STANDARDS.md and GUARDRAILS.md)
- Handling secrets or credentials (check SECURITY.md)
- Encountering hook failures (check .husky/README.md)
- Planning large changes (check GUARDRAILS.md for LOC limits)
- Understanding pipeline architecture (check PIPELINE.md)

## Repository Structure

```
DevPilot/
├── .agents/                        # Declarative agent definitions
│   ├── planner/                   # Stage 1: Planning agent
│   ├── code-generator/            # Stage 2: Coding agent (to be renamed to 'coder')
│   ├── validator/                 # Stage 3: Reviewing agent (to be renamed to 'reviewer')
│   └── orchestrator/              # (to be deleted - not used in linear pipeline)
├── .github/
│   ├── .gitmessage                # Commit message template
│   ├── PULL_REQUEST_TEMPLATE.md   # PR template
│   └── README.md                  # GitHub templates documentation
├── .husky/                        # Git hooks (Husky.NET)
│   ├── pre-commit.ps1             # LOC limits, secrets detection
│   ├── commit-msg.ps1             # Conventional Commits validation
│   ├── pre-push.ps1               # Protected branch checks, build/test
│   ├── post-checkout.ps1          # Dependency restoration
│   ├── post-merge.ps1             # Post-merge updates
│   ├── hooks-config.json          # Hook configuration
│   └── task-runner.json           # Husky task definitions
├── docs/
│   ├── PIPELINE.md                # Complete MASAI pipeline architecture
│   ├── GUARDRAILS.md              # Code quality standards and LOC limits
│   ├── COMMIT_STANDARDS.md        # Commit message conventions
│   └── SECURITY.md                # Security guidelines
├── src/
│   ├── DevPilot.Core/             # Core domain models (IAgent, AgentContext, PipelineStage)
│   ├── DevPilot.Agents/           # Agent implementations (ClaudeCliAgent, AgentLoader)
│   ├── DevPilot.Orchestrator/     # Pipeline orchestration (Pipeline, ApprovalGate)
│   └── DevPilot.Console/          # CLI application (not yet implemented)
├── tests/
│   ├── DevPilot.Agents.Tests/     # Unit tests (143 tests)
│   └── DevPilot.Agents.IntegrationTests/  # Integration tests (6 tests)
├── .editorconfig                  # Code style rules (strict C# enforcement)
├── .gitattributes                 # Git file handling (cross-platform)
├── .gitignore                     # Visual Studio ignore patterns
└── CLAUDE.md                      # This file - Claude Code instructions
```

## Development Guidelines

### Git Workflow

- **All changes must be made on a separate branch** - Create a feature branch for your work
- **All changes must go through pull requests** - Do not push directly to `main`
- Pull requests should use the provided PR template in `.github/PULL_REQUEST_TEMPLATE.md`
- The main branch is `main`

### Commit Messages

- **Follow Conventional Commits format**: `<type>[optional scope]: <description>`
- A commit message template is available in `.github/.gitmessage`
- To use the template locally, run: `git config commit.template .github/.gitmessage`
- Common types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`
- Use imperative mood ("Add feature" not "Added feature")
- Subject line: max 50 characters, capitalized, no period
- Add body for context (what and why, not how)

## Git Hooks

This repository uses [Husky.NET](https://alirezanet.github.io/Husky.Net/) to manage Git hooks that enforce code quality and workflow standards.

### Automated Checks

The following hooks run automatically:

#### pre-commit (Before Creating a Commit)
- **LOC Limit Enforcement** - Maximum 300 lines per commit (warn at 200)
  - Excludes generated files, lock files, and documentation
  - Provides detailed breakdown of changes per file
  - Encourages small, focused commits for easier review
- **Secrets Detection** - Scans for API keys, passwords, and connection strings
- **File Size Check** - Warns about files larger than 5MB
- **Code Formatting** - Validates .editorconfig compliance (when dotnet-format is installed)

#### commit-msg (Validating Commit Messages)
- **Conventional Commits** - Enforces format: `type(scope): description`
  - Valid types: feat, fix, docs, style, refactor, perf, test, build, ci, chore, revert
- **Subject Line Validation** - Max 50 characters, lowercase start, no period
- **Imperative Mood** - Ensures commands ("add" not "added")
- **Blocked Words** - Prevents WIP, TODO, FIXME in commit messages

#### pre-push (Before Pushing to Remote)
- **Protected Branch Check** - Blocks direct pushes to main/master
- **Branch Name Validation** - Enforces `type/description` format
- **Build Verification** - Runs `dotnet build` (configurable)
- **Test Execution** - Runs `dotnet test` (configurable)
- **Uncommitted Changes Warning** - Alerts about unpushed changes

#### post-checkout (After Switching Branches)
- **Dependency Restoration** - Runs `dotnet restore` automatically
- **Build Artifact Notice** - Suggests cleaning stale bin/obj folders
- **Recent Commits** - Displays last 5 commits on the new branch

#### post-merge (After Merging)
- **Dependency Updates** - Runs `dotnet restore`
- **Rebuild Solution** - Ensures merge didn't break build
- **Conflict Detection** - Lists unresolved merge conflicts

### Configuration

Hook behavior is configured in `.husky/hooks-config.json`:

```json
{
  "loc_limits": {
    "max_lines": 300,        // Maximum lines per commit
    "warn_lines": 200        // Warning threshold
  },
  "pre_push": {
    "require_tests": false,  // Enable to require tests before push
    "require_build": false   // Enable to require build before push
  }
}
```

### Bypassing Hooks

In emergency situations, you can bypass hooks:

```bash
# Skip pre-commit and commit-msg hooks
git commit --no-verify

# Skip pre-push hook
git push --no-verify
```

**Warning:** Use `--no-verify` sparingly. Hooks exist to maintain code quality and prevent issues.

### Claude Code Hook Failure Handling

When Claude Code encounters a Git hook failure (commit blocked, push blocked, etc.), it MUST:

1. **Immediately report the block** using this format:
   ```
   [BLOCKED] - <reason for the block>
   ```

2. **Wait for user guidance** - Do not automatically retry, bypass, or attempt workarounds

3. **Examples:**
   ```
   [BLOCKED] - LOC limit exceeded: 450 lines changed (max 300)
   [BLOCKED] - Commit message does not follow Conventional Commits format
   [BLOCKED] - Direct push to main branch is not allowed
   [BLOCKED] - Secrets detected in staged files
   ```

4. **After reporting the block**, present options to the user:
   - Split the commit into smaller pieces
   - Fix the validation error
   - Use `--no-verify` if appropriate
   - Adjust configuration if needed

**Important:** Never automatically use `--no-verify` without explicit user approval. The user decides when bypassing hooks is appropriate.

### Setup for New Developers

Hooks are automatically installed when cloning the repository. If hooks aren't working:

```bash
# Reinstall hooks
dotnet tool restore
dotnet husky install
```

### Hook Files

All hook scripts are in the `.husky/` directory:
- `pre-commit.ps1` - Pre-commit checks
- `commit-msg.ps1` - Commit message validation
- `pre-push.ps1` - Pre-push verification
- `post-checkout.ps1` - Post-checkout setup
- `post-merge.ps1` - Post-merge updates
- `hooks-config.json` - Configuration file
- `task-runner.json` - Husky task definitions

## Architecture

DevPilot implements a **linear 5-agent MASAI pipeline**. See `docs/PIPELINE.md` for complete specification.

### Pipeline Stages

1. **Planner** - Analyzes user request, creates execution plan with risk assessment
2. **Coder** - Generates unified diff patch implementing the plan
3. **Reviewer** - Validates code quality, style, and correctness
4. **Tester** - Executes tests and reports results
5. **Evaluator** - Scores overall quality and provides final verdict

### Approval Gates

After the Planning stage, the pipeline may pause for human approval if:
- Planner explicitly flags `needs_approval: true`
- High-risk operation detected (`risk.level == "high"`)
- LOC limit breach (>300 LOC per step)
- Step limit breach (>7 steps)
- File deletion detected

### Key Components

- **ClaudeCliAgent** - Executes agents via Claude CLI subprocess
- **AgentLoader** - Loads agent definitions from `.agents/` directory
- **Pipeline** - Orchestrates linear execution of all 5 agents
- **ApprovalGate** - Evaluates plans against 5 approval triggers
- **PipelineContext** - Carries immutable state through all stages

### Testing

Run all tests:
```bash
dotnet test
```

Current status: **149 tests passing** (143 unit + 6 integration)

## MCP Servers for Structured Output

### What is an MCP Server?

**MCP (Model Context Protocol)** is Anthropic's standard for adding custom tools to Claude. An MCP server is a **local program you write** that exposes custom tools beyond Claude's built-in ones (Read, Write, Bash, etc.).

**Key Facts:**
- ✅ **Locally owned** - MCP servers are scripts YOU write and run on your machine
- ✅ **Zero cost** - No API calls, no subscriptions beyond your existing Claude CLI access
- ✅ **JSON-RPC protocol** - Simple stdin/stdout communication
- ✅ **Schema enforcement** - Tool input schemas are validated by the MCP protocol
- ✅ **Trivially portable** - Copy the script anywhere, modify anytime

### When to Use MCP Servers

Use MCP servers when you need **structured output from Claude CLI** with guaranteed schema consistency.

**Problem MCP Solves:**
```
Without MCP: Ask Claude for JSON → varies every run (schema inconsistency)
With MCP: Claude calls structured tools → guaranteed schema enforcement
```

**Example Use Cases:**
- Generating structured plans/reports
- Building configuration files
- Database queries with typed results
- Any scenario where Claude CLI must return consistent JSON structure

### Our Existing MCP Server

**Location:** `experiments/mcp-planner/`

**Purpose:** Provides 8 planning tools that build structured execution plans with guaranteed schema.

**Tools Exposed:**
- `plan_init` - Initialize plan with summary
- `add_step` - Add execution step (step_number, description, file_target, agent, estimated_loc)
- `add_file` - Add file to track (path, operation, reason)
- `set_risk` - Set risk assessment (level, factors, mitigation)
- `set_verify` - Set verification criteria (acceptance_criteria, test_commands, manual_checks)
- `set_rollback` - Set rollback strategy (strategy, commands, notes)
- `set_approval` - Mark approval requirement (needs_approval, approval_reason)
- `finalize_plan` - Return complete plan JSON

**How It Works:**
1. Claude CLI spawns `mcp-server.js` as subprocess
2. Server exposes tools via JSON-RPC 2.0 over stdin/stdout
3. Claude calls tools (e.g., `plan_init`, `add_step`) with validated arguments
4. Server builds plan in memory as tools are called
5. `finalize_plan` returns complete, consistently-structured JSON

**Testing:**
```bash
cd experiments/mcp-planner
.\test-runner.ps1 "Your request here"
```

**Documentation:** See `experiments/mcp-planner/FINDINGS.md` for detailed findings and results.

### How MCP Enforces Schema Consistency

**Tool definitions have strict input schemas:**
```javascript
{
  name: 'add_step',
  inputSchema: {
    type: 'object',
    properties: {
      step_number: { type: 'number' },
      description: { type: 'string' },
      // ... more fields
    },
    required: ['step_number', 'description', ...]
  }
}
```

**Benefits:**
- Claude MUST provide all required fields
- Claude MUST use correct types (string, number, array)
- If arguments don't match schema, tool call fails
- No "creative restructuring" possible - schema is enforced

**Result:** Identical JSON structure on every run, no deserialization failures.

### When NOT to Use MCP Servers

- Simple, one-off requests where schema variation doesn't matter
- When Claude's built-in tools (Read, Write, Bash) are sufficient
- When you don't need structured output from Claude CLI

### Integration Guidance

If you need structured output from Claude CLI:

1. **Check if existing MCP server fits** - The `experiments/mcp-planner/` server may already have the tools you need
2. **Extend existing server** - Add new tools to `mcp-server.js` (just add to `tools` array and `handleToolCall` switch)
3. **Create new MCP server** - For unrelated domains, create a new server following the same pattern

**DO NOT:**
- Ask Claude CLI to return free-form JSON (schema varies)
- Implement complex flexible deserialization (MCP is cleaner)
- Think MCP is a cloud service (it's just a local script you own)

## Development Workflow

### Typical PR Flow

1. Create feature branch: `git checkout -b <type>/<description>`
2. Make changes (respecting 200-300 LOC limit per commit)
3. Run tests: `dotnet test`
4. Commit with Conventional Commits format
5. Push and create PR using `.github/PULL_REQUEST_TEMPLATE.md`
6. Wait for review and merge

### Current Roadmap (see PIPELINE.md for details)

**Completed (PR #16-20):**
- ✅ Agent Renaming (code-generator → coder, validator → reviewer)
- ✅ Tester agent definition
- ✅ Evaluator agent definition
- ✅ CLI application wiring (Program.cs)
- ✅ MCP server experiment for structured planning

**Next Steps:**
- **First End-to-End Test**: Run complete pipeline with real request
- **Integrate MCP**: Wire MCP server into planner agent execution
- **Patch Application**: Implement workspace manager and diff application
- **Test Runner**: Implement actual test execution in tester agent
