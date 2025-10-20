# Claude CLI Authentication & Usage

This document clarifies how DevPilot uses Claude CLI and how authentication works.

## 🎯 **CRITICAL: Development Philosophy**

**"Do things the right way, EVERY SINGLE TIME. Be patient and thorough - never rush or take shortcuts."**

This principle overrides all other considerations when developing DevPilot:

1. **Thoroughness > Speed**: Take the time to understand root causes, plan properly, and implement complete solutions
2. **Proper Process Always**: Follow the full workflow (plan → implement → test → validate) without skipping steps
3. **No Shortcuts**: Resist the temptation to "just make it work" - always implement the correct, maintainable solution
4. **Quality Over Convenience**: If something takes 2 hours to do right vs 20 minutes to hack together, invest the 2 hours
5. **Document Everything**: Future sessions benefit from thorough documentation of decisions, principles, and lessons learned

**Examples of "doing it right"**:
- ✅ Fixed evaluator JSON parsing by correcting the system prompt (root cause)
- ❌ Would have been wrong: Adding regex to extract JSON from prose (symptom workaround)

- ✅ Extended existing MCP server for evaluator tools (reused infrastructure)
- ❌ Would have been wrong: Creating duplicate JSON-RPC server (unnecessary duplication)

- ✅ Validated PR #49 build warnings fixed before proceeding to PR #50
- ❌ Would have been wrong: Stacking PRs without validating previous changes

**This philosophy ensures DevPilot remains maintainable, extensible, and high-quality as it evolves.**

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

## MASAI Pipeline Architecture

**DevPilot is a reusable MASAI pipeline orchestrator** that operates on ANY C# repository without requiring code changes. Install it globally once, then use it across unlimited projects - each with their own domain knowledge, custom agents, and conventions.

**MASAI** (Modular Architecture for Software-engineering AI agents) divides complex software problems into sub-problems solved by specialized LLM-powered agents. DevPilot implements five agents (Planner, Coder, Reviewer, Tester, Evaluator) with focused responsibilities.

**Key Architectural Principles**:
- **Framework/Engine Model**: DevPilot is the engine (like Django or .NET), target repos provide domain context
- **Two Distinct Contexts**: DevPilot repository (the engine) vs. Target repository (domain-specific)
- **Target Repo Controls Everything**: CLAUDE.md, .agents/, .runsettings, custom workflows
- **Isolated Workspaces**: Each execution gets its own workspace, no cross-contamination
- **Respect Target Conventions**: Use target repo's test framework, config files, directory structure

**For complete architectural documentation**, see [docs/ARCHITECTURE.md](./docs/ARCHITECTURE.md) which covers:
- What is MASAI and how DevPilot implements it
- Framework/Engine model (DevPilot as reusable orchestrator)
- Two distinct contexts (DevPilot repo vs. Target repo)
- Execution flow step-by-step
- Reusability vision (one DevPilot, unlimited projects)
- Implementation status and future enhancements
- Component overview (Pipeline, Agents, Core Models)

## Repository Structure Awareness

**Status**: ✅ Implemented in PR #42

DevPilot now automatically detects and understands target repository structure, enabling agents to generate correct file paths for any project layout.

### The Problem This Solves

Previously, agents assumed standard directory names like `src/` and `tests/`, causing failures in repositories using different conventions:

```
❌ Before: Agent generates "src/Calculator.cs"
   But repo uses "MyApp/Calculator.cs"
   → Patch application fails: "File does not exist"

✅ After: Agent receives structure context and generates "MyApp/Calculator.cs"
   → Patch applies successfully
```

### How It Works

1. **WorkspaceManager.AnalyzeProjectStructure()** detects:
   - Main project directory (first non-test .csproj found)
   - Test project directories (projects with test framework references or ".Test" in name)
   - All project directories in the repository
   - Presence of docs/, .agents/, CLAUDE.md

2. **ProjectStructureInfo.ToAgentContext()** formats structure as human-readable text:
   ```
   Repository Structure:
   - Main Project: Testing/
   - Test Projects: Testing.Tests/
   - Documentation: docs/
   - Project Instructions: CLAUDE.md (read this for context)

   IMPORTANT: Use the ACTUAL project directories listed above.
   Do NOT assume standard names like 'src/' or 'tests/' unless they are listed above.
   ```

3. **Pipeline.BuildStageInput()** prepends structure context to Planning and Coding stages
   - These stages generate file paths, so they need structure awareness
   - Other stages work with existing outputs that already have correct paths

### Test Project Detection

WorkspaceManager identifies test projects using two methods:

**1. Name Heuristics** (fast):
- Contains `.Tests`, `.Test`, `Tests.`, or `Test.` in project name

**2. Framework References** (accurate):
```csharp
// Checks .csproj content for:
- xunit, nunit, mstest
- Microsoft.NET.Test.Sdk
- coverlet.collector
```

### Verified Working

Tested successfully with non-standard repository structure:
- Repository uses `Testing/` instead of `src/`
- Repository uses `Testing.Tests/` instead of `tests/`
- Planner correctly generated `Testing/Calculator.cs`
- Patch applied successfully
- Overall quality score: 9.2/10

### Implementation Details

**Files Modified**:
- `src/DevPilot.Core/ProjectStructureInfo.cs` (NEW) - Structure metadata
- `src/DevPilot.Core/PipelineContext.cs` - Added ProjectStructure property
- `src/DevPilot.Orchestrator/WorkspaceManager.cs` - Added AnalyzeProjectStructure()
- `src/DevPilot.Orchestrator/Pipeline.cs` - Structure context injection

**Critical Bug Fixed**:
WorkspaceManager was recursively copying `.devpilot/workspaces/` directories, creating infinite nesting:
```
❌ Before: .devpilot\workspaces\id1\.devpilot\workspaces\id2\.devpilot\...
✅ After: Added exclusion filter to prevent recursive .devpilot copying
```

## devpilot.json Configuration

Target repositories can customize file copying behavior with an optional `devpilot.json` file in the repository root.

### Schema

```json
{
  "folders": ["custom-lib", "shared"],
  "copyAllFiles": false
}
```

**Properties**:
- `folders` (string[]): Additional directories to copy to workspace (beyond auto-detected .csproj directories)
- `copyAllFiles` (boolean): Override selective copying (default: false)

### Auto-Detection Behavior

WorkspaceManager automatically copies:

1. **Default directories** (if they exist):
   - `.agents/` - Custom agent definitions
   - `docs/` - Documentation
   - `src/` - Common main project directory (backward compatibility)
   - `tests/` - Common test directory (backward compatibility)

2. **Auto-detected project directories**:
   - Any directory containing `.csproj` files (recursive search)
   - Excludes: `bin/`, `obj/`, `.git/`, `.vs/`, `node_modules/`, `.devpilot/`, `packages/`

3. **Configured additional folders** (from devpilot.json):
   - Custom libraries, shared code, or domain-specific directories

### Example: Monorepo Configuration

```json
{
  "folders": [
    "shared-models",
    "common-utilities",
    "third-party-integrations"
  ]
}
```

This ensures agents have access to shared code when generating patches.

### Default Behavior (No devpilot.json)

If no `devpilot.json` exists, WorkspaceManager uses:
```csharp
DevPilotConfig.Default
{
    Folders = null,
    CopyAllFiles = false
}
```

Auto-detection handles most repositories correctly without configuration.

## Development Roadmap

**Current Status**: Core MASAI pipeline features implemented. Focus shifting to advanced capabilities and enterprise readiness.

### Recent Completed Work

- ✅ **Repository Restructuring** (2025-10-19) - Flattened structure (moved DevPilot/* to root), created docs/{RAG,LESSONS_LEARNED,ARCHITECTURE}.md, reduced CLAUDE.md from 54k → 19k tokens (65% reduction)
- ✅ **PR #57**: Zero-warning build (2025-10-19) - 0 warnings, 453/453 tests passing
- ✅ **PR #56**: Continuous regression testing (2025-10-19) - 100% test pass rate, no timeouts
- ✅ **Agent Audit**: All 5 MASAI agents production-ready (2025-10-19) - See AGENT_AUDIT.md
- ✅ **Validation Framework**: RUN_VALIDATION.md, examples/, validation-results/ (2025-10-19)
- ✅ **PR #52**: Windows CLI length fix (2025-10-18) - CLAUDE.md compression for large repos
- ✅ **PR #51**: RAG integration with Ollama (2025-10-18) - See [docs/RAG.md](./docs/RAG.md)
- ✅ **PR #42**: Repository structure awareness (2025-10-17) - Works with any directory layout
- ✅ **PR #50**: Enhanced Coder prompt (2025-10-18) - +4.0 code quality improvement

### Immediate Priority (Next Session)

1. **🔬 Execute Validation Tests** (Ready to Run)
   - Infrastructure complete: RUN_VALIDATION.md, test repos, templates
   - Phase 1: RAG validation (baseline vs --enable-rag)
   - Phase 2: Diverse repository testing (multi-project, monorepos, non-standard naming, no CLAUDE.md, dogfooding)
   - Phase 3: Record results in validation-results/ templates
   - **Estimated Time**: 2-3 hours for full validation suite

2. **📝 GitHub Issues Created** (Based on Agent Audit) ✅
   - Issue #65: Expand MCP Tools to Coder/Reviewer Agents (Priority: Medium)
   - Issue #66: Add Roslyn Analyzer Integration to Reviewer (Priority: Low)
   - Issue #67: Implement State Persistence for Pipeline Resume (Priority: Medium)

### Short-Term (1-2 Weeks)

3. **🔧 Fix RAG Build Issues** (In Progress)
   - Issue: SqliteVectorStoreTests had CancellationToken parameter positioning errors
   - Status: Fixed in current session (3 CS1503 errors resolved)
   - Impact: DevPilot now builds cleanly with RAG tests passing

4. **🧪 Improve Agent Test Quality** (Monitoring)
   - Issue: Agents occasionally generate overly strict floating-point comparisons
   - Example: `Assert.Equal(expected, actual, precision: 10)` → should be `precision: 5`
   - Status: Added floating-point best practices to Coder prompt in PR #50
   - Next: Monitor validation test results for remaining test quality issues

### Medium-Term (1-2 Months)

6. **🔍 Real Code Review (Reviewer Agent)**
   - Currently: Reviewer returns placeholder responses
   - Goal: Implement actual static code analysis
   - Approach: Use Roslyn analyzers or integrate with existing tools
   - Output: Detailed review comments with line numbers and suggestions

7. **⏸️ Approval Workflow & State Persistence**
   - Currently: "Awaiting approval" stops pipeline, but no resume mechanism
   - Goal: Save pipeline state to JSON, allow resume after human review
   - Design: `.devpilot/state/<pipeline-id>.json` with full context
   - CLI commands: `devpilot resume <pipeline-id>` and `devpilot approve <pipeline-id>`

8. **🛠️ MCP Tool Expansion**
   - Currently: Only Planner and Evaluator use MCP tools
   - Goal: Add MCP tools for Coder, Reviewer, Tester
   - Benefits: Structured outputs, schema validation, better reliability
   - Example tools: `create_code_change`, `report_code_issue`, `record_test_failure`

9. **🖥️ WPF UI for Pipeline Monitoring**
   - Currently: CLI-only interface with spinner
   - Goal: Real-time pipeline visualization
   - Features: Stage progress, agent outputs, file diffs, approval buttons
   - Design: Similar to Azure DevOps pipeline UI

### Long-Term Vision (6+ Months)

11. **🌐 Multi-Language Support**
    - Extend beyond C# to Python, JavaScript/TypeScript, Java
    - Requires language-specific test runners, project detectors
    - DevPilot becomes universal MASAI orchestrator

12. **🔌 Plugin System for Custom Pipeline Stages**
    - Allow target repos to define additional stages beyond 5 default
    - Example: Security scanning, performance profiling, deployment
    - `.agents/custom-stages.json` configuration

13. **☁️ Cloud-Based Execution (DevPilot as a Service)**
    - Run pipelines in cloud instead of locally
    - Benefits: Faster execution, persistent state, team collaboration
    - Challenges: Workspace isolation, security, cost

### Success Metrics

How we'll know we're on the right track:

- **Developer Adoption**: DevPilot used successfully in 10+ different repositories
- **Quality Scores**: Evaluator reports >8.0/10 average across diverse projects
- **Custom Agents**: 3+ repositories define custom agents (proves .agents/ extensibility)
- **Test Pass Rates**: >95% of agent-generated tests pass on first run
- **Time Savings**: Developers report 50%+ time reduction on routine feature tasks

## Production Testing & Validation Results

DevPilot has been extensively tested on real repositories to validate features and identify bugs early. These real-world tests have uncovered critical issues and proven the effectiveness of enhancements.

**Key Findings**:
- Repository structure awareness works excellently for file modifications, but required prompt engineering for new file creation
- Enhanced C# best practices in Coder prompt delivered **+4.0 code quality improvement** (4.5 → 8.5/10)
- Windows command-line length limits (32KB) blocked dogfooding until PR #52 implemented compression
- Agent-generated tests achieve **98.4% pass rate** with comprehensive edge case coverage

**For complete testing insights**, see [docs/LESSONS_LEARNED.md](./docs/LESSONS_LEARNED.md) which covers:
- Repository Structure Awareness Testing (PR #42) - Bug fixes, metrics, lessons learned
- Enhanced Coder Prompt Validation (PR #50) - Quality improvements, Windows CLI bug discovery
- Critical bugs found and resolutions
- General testing best practices for future development

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

## RAG (Retrieval Augmented Generation)

**Status**: ✅ Implemented (Feature flag: `--enable-rag`)

DevPilot includes an optional RAG system that enhances agents with semantic search over workspace files using Ollama embeddings. When enabled, agents receive relevant code snippets and documentation as context.

**Quick Start**:
```bash
# Install Ollama and pull the embedding model
winget install Ollama.Ollama
ollama pull mxbai-embed-large

# Use RAG in your pipeline
devpilot --enable-rag "Add authentication to User class"
```

**For complete documentation**, see [docs/RAG.md](./docs/RAG.md) which covers:
- Architecture (DocumentChunker, OllamaEmbeddingService, SqliteVectorStore)
- Setup and configuration
- Usage examples
- Troubleshooting common issues
- Implementation status and future enhancements

## Development Workflow

**For detailed contributor guidelines, see [CONTRIBUTING.md](./CONTRIBUTING.md).**

### Quick Reference: When to Use Meta-Loop vs Direct Development

**Meta-Loop** (DevPilot improving DevPilot):
- ✅ Validation/regression testing after changes
- ✅ Dogfooding new features
- ✅ Generating documentation examples
- ❌ Initial development or architecture design
- ❌ Deep debugging or multi-step refactoring

**Direct Development** (Claude Code):
- ✅ Complex refactoring and architecture changes
- ✅ Bug fixes requiring debugging
- ✅ Agent prompt engineering
- ✅ Infrastructure improvements

**Recommended Workflow**:
1. Develop changes directly using Claude Code
2. Commit to PR branch
3. Run meta-loop validation on Testing repo (simple baseline)
4. Run meta-loop validation on DevPilot repo (dogfooding)
5. Merge if all tests pass

See [CONTRIBUTING.md](./CONTRIBUTING.md) for detailed pre-merge checklist, testing guidelines, and development scenarios.

---

If problems persist, see the [Claude CLI documentation](https://docs.anthropic.com/claude/docs) or file an issue in the DevPilot repository.
