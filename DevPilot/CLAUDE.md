# Claude CLI Authentication & Usage

This document clarifies how DevPilot uses Claude CLI and how authentication works.

## ⚠️ Important: Claude CLI vs Anthropic API

**DevPilot uses Claude CLI, NOT the Anthropic API directly.**

This means:
- ❌ **NO** `ANTHROPIC_API_KEY` environment variable needed
- ✅ **YES** Claude CLI authentication via `claude login`
- ✅ **YES** Claude CLI must be installed and in PATH

## Prerequisites

### 1. Install Claude CLI

Claude CLI is the official Anthropic command-line tool. Install it via npm:

```bash
npm install -g @anthropic-ai/claude-code
```

Verify installation:

```bash
claude --version
```

Expected output: Version number (e.g., `1.0.0` or similar)

### 2. Authenticate with Claude CLI

Claude CLI has its own authentication system. You do NOT need an API key.

```bash
claude login
```

This will:
1. Open your browser for authentication
2. Prompt you to log in with your Anthropic account
3. Store credentials locally for Claude CLI to use

To verify you're authenticated:

```bash
claude --help
```

If you see the help menu without errors, you're authenticated correctly.

### 3. Verify Claude CLI Works

Test with a simple prompt:

```bash
echo "What is 2 + 2?" | claude --print --model sonnet
```

Expected output: Claude's response (should say "4" or similar)

If this fails, re-run `claude login`.

## How DevPilot Uses Claude CLI

DevPilot's `ClaudeCliClient` class (in `src/DevPilot.Agents/ClaudeCliClient.cs`) executes Claude as a subprocess:

```csharp
// Simplified example of what happens internally:
Process.Start("claude", "--print --system-prompt <prompt> --model sonnet");
```

This means:
- DevPilot spawns `claude` as a subprocess
- Claude CLI handles authentication automatically (uses stored credentials)
- No API key needed in environment variables
- Claude CLI must be in your system PATH

## Running the DevPilot Pipeline

Once Claude CLI is authenticated, you can run the pipeline:

```bash
cd C:\DevPilot\DevPilot
dotnet run --project src/DevPilot.Console "Create a Calculator class with Add and Subtract methods"
```

### Expected Behavior

1. **Pipeline starts** - You'll see the DevPilot ASCII banner
2. **Spinner appears** - "Executing pipeline..."
3. **5 stages execute sequentially**:
   - Planning (2-3 min) - Uses MCP tools for structured planning
   - Coding (~30-60 sec) - Generates unified diff patch
   - Reviewing (~30-60 sec) - Validates code quality
   - Testing (~30-60 sec) - Checks test coverage
   - Evaluating (~30-60 sec) - Provides quality scores
4. **Results displayed** - Success message with quality scores

### Total Duration

~5 minutes for full pipeline execution (validated in PR #24)

## Troubleshooting

### "Claude CLI not found at path"

**Problem**: DevPilot can't find the `claude` executable.

**Solution**:
```bash
# Check if claude is in PATH
where claude

# If not found, reinstall Claude CLI:
npm install -g @anthropic-ai/claude-code

# Verify it's in PATH after install:
where claude
```

### "Authentication failed" or "Unauthorized"

**Problem**: Claude CLI credentials are missing or expired.

**Solution**:
```bash
# Re-authenticate
claude login

# Test authentication works
echo "test" | claude --print --model sonnet
```

### "Claude CLI timed out after 300 seconds"

**Problem**: A stage took longer than 5 minutes (default timeout).

**Solution**: This is rare but can happen with complex requests. The timeout is configurable in `ClaudeCliClient.cs` (default: 5 minutes per agent execution).

### Pipeline completes but shows placeholder responses

**Expected behavior** - Currently, only the Planner agent generates real structured output via MCP tools. The other agents (Coder, Reviewer, Tester, Evaluator) return placeholder responses.

**Why?**: Full implementation is pending (see PIPELINE.md "Future Work" section):
- Patch application (Coder output → workspace files)
- Real test execution (Tester → `dotnet test`)
- Real code review (Reviewer → analyze generated code)

## Architecture Notes

### Why Claude CLI instead of Anthropic API SDK?

1. **MCP Support**: Claude CLI has built-in support for Model Context Protocol (MCP) servers
2. **Simplicity**: No need to manage API keys, request formatting, or response parsing
3. **Tool Integration**: MCP allows structured tool calling with schema validation
4. **Subprocess Isolation**: Each agent runs in isolated subprocess for reliability

### ClaudeCliClient Implementation

See `src/DevPilot.Agents/ClaudeCliClient.cs` for details:

- **Process Spawning**: Uses `System.Diagnostics.Process` to spawn `claude` subprocess
- **Stdin Prompt**: Passes user prompt via stdin (not command-line args)
- **Timeout Handling**: 5-minute default timeout per agent
- **Stream-JSON Parsing**: When using MCP, parses stream-json format for tool results
- **.cmd Resolution**: On Windows, resolves `claude.cmd` wrapper to underlying `cli.js` for proper argument handling

### MCP Configuration

Only the Planner agent uses MCP tools (see `.agents/planner/config.json`):

```json
{
  "mcpConfigPath": "../../experiments/mcp-planner/plan-tools.json"
}
```

This enables structured planning with 8 tools:
- `create_section`, `create_file_operation`, `create_validation_rule`, etc.

Other agents use plain text output (no MCP tools).

## MASAI Pipeline Architecture: Target Repository vs DevPilot Repository

**CRITICAL DISTINCTION**: DevPilot operates in TWO separate contexts that must NOT be confused:

### 1. Target Repository (External Projects)

When you run `devpilot "Create a Calculator class"` in a directory like `C:\MyProject`, DevPilot:

- **Creates isolated workspace** in `.devpilot/workspaces/<pipeline-id>/`
- **Copies files** from `C:\MyProject` to the workspace (using WorkspaceManager)
- **Executes MASAI pipeline** agents (Planning → Coding → Reviewing → Testing → Evaluating)
- **Reads target repo's CLAUDE.md** for project-specific instructions
- **Uses target repo's testing framework** (xUnit, NUnit, MSTest, etc.)
- **Uses target repo's .runsettings** (if present) for test configuration
- **Respects target repo's agents** (if custom agents defined)
- **Applies patches** to workspace files (not original repo files)

**Key Point**: The target repository controls:
- ✅ Project instructions (CLAUDE.md)
- ✅ Testing library and configuration (.runsettings, test framework)
- ✅ Documentation structure
- ✅ Custom agents and tools
- ✅ Code style and conventions

### 2. DevPilot Repository (This Codebase)

DevPilot's own repository (`C:\DevPilot\DevPilot`) contains:

- **MASAI pipeline implementation** (src/DevPilot.Orchestrator)
- **Core agents** (Planning, Coding, Reviewing, Testing, Evaluating)
- **DevPilot's own tests** (tests/DevPilot.Agents.Tests, tests/DevPilot.Orchestrator.Tests)
- **DevPilot's .runsettings** (for DevPilot's own test execution, NOT target repos)
- **DevPilot's CLAUDE.md** (this file - development instructions for DevPilot itself)

**Key Point**: DevPilot's .runsettings is ONLY for testing DevPilot's own codebase. It has NO effect on tests run in target repositories.

### Example: Running DevPilot in External Repo

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

### Testing Configuration Scopes

| Configuration File | Scope | Purpose |
|-------------------|-------|---------|
| `C:\DevPilot\DevPilot\.runsettings` | DevPilot's own tests | Prevents DevPilot unit tests from hanging (10s timeout, Microsoft.CodeCoverage) |
| `C:\TestProject\.runsettings` | Target repo tests | Used by TestRunner when executing tests in workspace (optional) |

**Important**: TestRunner.cs does NOT force .runsettings usage. It respects whatever test configuration exists in the target repository.

### Why This Matters

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

## Related Documentation

- **PIPELINE.md**: Full pipeline architecture and stage documentation
- **ARCHITECTURE.md**: System design and component overview
- **README.md**: Project overview and getting started guide

## Questions?

If you encounter issues with Claude CLI authentication or usage, check:

1. Claude CLI is installed: `claude --version`
2. You're authenticated: `claude login`
3. Test auth works: `echo "test" | claude --print --model sonnet`
4. Claude is in PATH: `where claude` (Windows) or `which claude` (Unix)

## Development Principles

### Always the Proper Fix, Never Cut Corners

When fixing bugs or implementing features in DevPilot:

1. **Root Cause First**: Fix the underlying issue, not just the symptom
2. **No Workarounds**: Don't add regex hacks or string parsing when the agent prompt can be fixed
3. **Maintain Consistency**: If other agents output pure JSON, all agents should
4. **Document Why**: Explain the proper approach vs why shortcuts were rejected
5. **Test Properly**: Ensure fixes work end-to-end, not just in isolation

**Example**: When the evaluator returned conversational text instead of JSON:
- ❌ **Wrong**: Add regex to extract JSON from prose
- ✅ **Right**: Fix evaluator system prompt to output only JSON (matches other agents)

This principle ensures long-term maintainability and consistency across the codebase.

### Extend vs Duplicate: When to Reuse Infrastructure

When implementing new features in DevPilot, always ask: "Can I extend existing infrastructure instead of duplicating it?"

**Prefer Extension When**:
- Existing system handles similar concerns (e.g., structured output, schema validation)
- Core architecture is the same (e.g., JSON-RPC protocol, tool definitions)
- Duplication would create maintenance burden (e.g., two nearly identical servers)
- Naming can accommodate broader scope (e.g., "pipeline-tools" instead of "planning-tools")

**Prefer Duplication When**:
- Concerns are truly different (e.g., agent execution vs MCP server)
- Isolation is critical for security/stability
- Combining would create tight coupling or confusion
- Systems have different lifecycles or deployment needs

**Example: MCP Server Extension**

When adding evaluator MCP support, we had two options:

❌ **Wrong (Duplicate)**: Create `experiments/mcp-evaluator/mcp-server.js` with ~270 LOC of duplicated JSON-RPC scaffolding

✅ **Right (Extend)**: Add 7 evaluation tools to existing `experiments/mcp-planner/mcp-server.js` and rename to "pipeline-tools"

**Benefits of Extension**:
- Single server to maintain and debug
- Consistent tool calling pattern for all agents
- Shared JSON-RPC infrastructure (no duplication)
- Natural evolution: "planning-tools" → "pipeline-tools" reflects broader scope

**Result**: Added evaluation support with ~80 LOC instead of ~270 LOC, reduced maintenance burden, and maintained architectural consistency.

**Decision Framework**:
1. Identify the core concern (e.g., "enforcing structured output via schema-validated tools")
2. Find existing systems handling that concern (e.g., MCP server)
3. Evaluate if extension is feasible (e.g., can add tools without breaking existing functionality)
4. If yes, extend and rename if needed; if no, document why duplication is necessary

This principle complements "Always the Proper Fix" - proper fixes often involve extending existing solutions rather than creating parallel systems.

---

If problems persist, see the [Claude CLI documentation](https://docs.anthropic.com/claude/docs) or file an issue in the DevPilot repository.
