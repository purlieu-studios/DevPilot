# Contributing to DevPilot

Thank you for your interest in contributing to DevPilot! This document provides guidelines for developing and improving the DevPilot MASAI pipeline.

## Table of Contents

- [Development Workflow](#development-workflow)
- [Meta-Loop vs Direct Development](#meta-loop-vs-direct-development)
- [Pre-Merge Checklist](#pre-merge-checklist)
- [Testing Guidelines](#testing-guidelines)
- [Code Standards](#code-standards)
  - [Package Management](#package-management)
- [Pull Request Process](#pull-request-process)

## Development Workflow

DevPilot uses a **hybrid development approach**: direct development with Claude Code for complex changes, combined with meta-loop validation for regression testing.

### Recommended Process

```
┌─────────────────────────────────────────────────────────────┐
│ 1. DIRECT DEVELOPMENT (Claude Code)                         │
│    ├─ Architecture changes                                  │
│    ├─ Bug fixes requiring debugging                         │
│    ├─ Complex refactoring (multi-file)                      │
│    ├─ Agent prompt engineering                              │
│    └─ Infrastructure improvements                           │
│                                                              │
│ 2. COMMIT TO BRANCH                                          │
│    └─ git commit + push to PR branch                        │
│                                                              │
│ 3. META-LOOP VALIDATION (Recommended)                       │
│    ├─ Run meta-loop on Testing repo (baseline check)        │
│    ├─ Run meta-loop on DevPilot repo (dogfooding)           │
│    └─ Verify pipeline completes 5 stages                    │
│                                                              │
│ 4. MERGE IF:                                                 │
│    ✅ Direct tests pass (dotnet test)                       │
│    ✅ Meta-loop completes all stages                        │
│    ✅ No regression on simple repos                         │
└─────────────────────────────────────────────────────────────┘
```

## Session Diff Tracking

DevPilot uses a `diffs/` folder to track changes made during development sessions. This provides a historical record of what changed and why.

### Creating Session Diffs

At the end of each development session, create two files:

```bash
# 1. Generate full diff
git diff origin/main..HEAD > diffs/<feature-name>-$(date +%Y%m%d-%H%M%S).diff

# 2. Create summary
cat > diffs/<feature-name>-$(date +%Y%m%d-%H%M%S)-summary.md << 'EOF'
# <Feature Name> - Session Summary

**Date**: YYYY-MM-DD
**Branch**: <branch-name>
**PR**: #<number>

## Files Modified

1. **path/to/file.cs**
   - Description of changes

## Commits

1. <commit-hash> - <commit message>

## Test Results

- Test stats
- Coverage changes

## Next Steps

- What's remaining
- Follow-up tasks
EOF
```

### Diff Folder Structure

```
diffs/
├── ca-analyzer-integration-20251021-193045.diff
├── ca-analyzer-integration-20251021-193045-summary.md
├── epic-breakdown-service-20251022-104530.diff
└── epic-breakdown-service-20251022-104530-summary.md
```

### Benefits

- **Session Continuity**: Helps Claude Code remember what happened between sessions (Claude doesn't have memory across conversations)
- **Historical Record**: See what changed in each session
- **Context Preservation**: Summaries explain why changes were made
- **Review Aid**: Easy to review session work before committing
- **Learning**: Track evolution of features over multiple sessions

### Gitignore

The `diffs/` folder is gitignored (local tracking only), so these files won't be committed to the repository.

## Meta-Loop vs Direct Development

### When to Use Meta-Loop ✅

**1. Validation/Testing of Changes**
- After fixing Planner bugs → run meta-loop to verify it works
- After changing WorkspaceManager → test file copying
- After updating agent prompts → validate quality scores

**2. Dogfooding New Features**
- Added `--yes` flag? → Use it in meta-loop tests
- Built diff preview? → Watch it work on real changes
- New CodeValidator? → See if it catches real issues

**3. Regression Testing**
- Before merging PRs → meta-loop on Testing repo (simple baseline)
- After merging PRs → meta-loop on DevPilot repo (complex scenario)

**4. Documentation/Examples**
- Generate real-world examples for README
- Create case studies of DevPilot improving itself
- Showcase pipeline quality scores

### When NOT to Use Meta-Loop ❌

**1. Initial Development**
- Designing new architecture
- Exploring solutions to complex bugs
- Prototyping features

**2. Deep Debugging**
- Stepping through code with debugger
- Reading stack traces and logs
- Understanding root causes

**3. Multi-Step Refactoring**
- Renaming across 20 files
- Changing interfaces with many implementations
- Large architectural changes

**4. Agent/Infrastructure Changes**
- Modifying system prompts
- Changing MCP tools
- Updating pipeline orchestration

### Meta-Loop as CI/CD

Think of meta-loop like **integration tests** in traditional CI/CD:

| Traditional CI/CD | DevPilot Meta-Loop |
|-------------------|-------------------|
| Unit tests | `dotnet test` |
| Integration tests | **Meta-loop on Testing repo** |
| Smoke tests | **Meta-loop on DevPilot repo** |
| Deploy to prod | Merge to main |

**Value**: Catches issues that unit tests can't:
- "Planner fails on large CLAUDE.md" ← Wouldn't catch with unit tests
- "Workspace copying hangs" ← Only visible in real execution
- "Agents generate low-quality code" ← Needs full pipeline

## Pre-Merge Checklist

Before merging any PR, ensure:

### Unit Tests
```bash
cd C:\DevPilot\DevPilot
dotnet test
```

**Expected**: All tests pass, 0 failures

### Meta-Loop Validation (Testing Repository)

Simple baseline test to ensure no regression on basic repositories:

```bash
cd C:\TestDevPilot\Testing
devpilot --yes "Add a Multiply method to Calculator class"
```

**Expected**:
- ✅ All 5 stages complete (Planning → Coding → Reviewing → Testing → Evaluating)
- ✅ Duration: ~3-4 minutes
- ✅ Quality score: ≥7.0/10
- ✅ All tests pass

### Meta-Loop Validation (DevPilot Repository)

Complex dogfooding test to ensure DevPilot can improve itself:

```bash
cd C:\DevPilot\DevPilot
devpilot --yes "Add XML documentation to <method> in <class>"
```

**Expected**:
- ✅ Planner generates valid plan (8-10 tool calls, not empty `{}`)
- ✅ All 5 stages complete
- ✅ Duration: ~4-5 minutes
- ⚠️ Quality score may vary (documentation tasks often score lower on test_coverage)

### Build Quality
```bash
dotnet build --configuration Release
```

**Expected**: 0 errors, 0 warnings

## Testing Guidelines

### Unit Tests

DevPilot's unit tests live in:
- `tests/DevPilot.Orchestrator.Tests/`
- Uses xUnit v3
- Mock-based for agent isolation

**Running tests**:
```bash
dotnet test --verbosity normal
```

**Coverage focus**:
- Pipeline orchestration logic
- WorkspaceManager file operations
- CodeValidator test detection
- PipelineContext state management

### Meta-Loop Tests

**Testing Repository** (`C:\TestDevPilot\Testing`):
- Simple Calculator class
- No CLAUDE.md
- Minimal project structure
- **Use for**: Baseline regression testing

**DevPilot Repository** (`C:\DevPilot\DevPilot`):
- Complex codebase (multiple projects)
- Large CLAUDE.md (35KB)
- Custom agents (.agents/)
- **Use for**: Dogfooding and stress testing

**Test Commands**:
```bash
# Simple addition (Testing repo)
devpilot --yes "Add <operation> method to Calculator"

# Documentation (DevPilot repo)
devpilot --yes "Add XML docs to <method> in <class>"

# Refactoring (DevPilot repo)
devpilot --yes "Refactor <method> to use <pattern>"
```

## Code Standards

### C# Style
- Follow .editorconfig rules
- Use nullable reference types (`<Nullable>enable</Nullable>`)
- Prefer `ArgumentException.ThrowIfNull` over manual checks
- Document public APIs with XML comments

### Agent Prompts
- Keep system prompts under 300 lines
- Use concrete examples (not just theory)
- Include "CRITICAL" sections for must-follow rules
- Test prompt changes via meta-loop

### Git Commits
- Use conventional commits: `feat:`, `fix:`, `docs:`, `refactor:`, etc.
- Include Co-Authored-By: Claude <noreply@anthropic.com>
- Reference issue numbers when applicable
- Keep commits focused (one logical change per commit)

### Documentation
- Update CLAUDE.md for architecture changes
- Update CONTRIBUTING.md for workflow changes
- Keep README.md user-focused (not developer-focused)
- Add inline comments for complex logic

### Package Management

DevPilot has **strict package version constraints** to ensure Roslyn and MSBuild compatibility.

#### Critical Package Versions

Current versions (as of 2025-10-21):
- `Microsoft.Build.Locator`: **1.7.8** (do NOT upgrade to 1.10.2)
- `Microsoft.CodeAnalysis.CSharp.Workspaces`: **4.11.0**
- `Microsoft.CodeAnalysis.Workspaces.MSBuild`: **4.11.0**
- `Microsoft.CodeAnalysis.NetAnalyzers`: **9.0.0** (latest)

#### Package Upgrade Guidelines

**ALWAYS run regression tests** after upgrading Roslyn/MSBuild packages:

1. **Before upgrading:**
   ```bash
   # Run existing regression tests
   dotnet test --filter "FullyQualifiedName~RoslynSmokeTests"
   dotnet test --filter "FullyQualifiedName~CodeAnalyzerIntegrationTests"
   ```

2. **Upgrade packages** in `*.csproj` files

3. **After upgrading:**
   ```bash
   # Verify smoke tests still pass
   dotnet test --filter "FullyQualifiedName~RoslynSmokeTests"

   # Verify integration tests still pass
   dotnet test --filter "FullyQualifiedName~CodeAnalyzerIntegrationTests"

   # Run full test suite
   dotnet test
   ```

4. **If any regression test fails**, the upgrade broke compatibility - revert and investigate

#### Known Package Constraints

- **Microsoft.Build.Locator 1.10.2**: Requires `ExcludeAssets="runtime"` on `Microsoft.Build.*` packages, which breaks `MSBuildWorkspace`. Stay on **1.7.8**.

- **Microsoft.CodeAnalysis.* versions**: Must match (e.g., all 4.11.0) to avoid `CompilationWithAnalyzers` API incompatibilities.

- **Microsoft.Build.Tasks.Core**: Has security vulnerability [GHSA-h4j7-5rxr-p4wc](https://github.com/advisories/GHSA-h4j7-5rxr-p4wc) but cannot be upgraded due to MSBuildWorkspace compatibility.

#### Why These Tests Exist

The regression tests prevent **exactly this scenario**:
- PR #102 added CodeAnalyzer tests
- PR #103 upgraded NetAnalyzers 8.0.0 → 9.0.0
- PR #103's CI ran BEFORE PR #102 merged
- PR #103 broke the newly added CodeAnalyzer tests
- **Regression tests now catch this before merge**

## Pull Request Process

### Creating a PR

1. **Branch from main**
   ```bash
   git checkout main
   git pull origin main
   git checkout -b <type>/<description>
   ```

2. **Make changes**
   - Use Claude Code for direct development
   - Commit incrementally with descriptive messages

3. **Run pre-merge checklist**
   - Unit tests (`dotnet test`)
   - Meta-loop on Testing repo
   - Meta-loop on DevPilot repo (optional but recommended)

4. **Push and create PR**
   ```bash
   git push -u origin <branch-name>
   gh pr create --title "<title>" --body "<description>"
   ```

### PR Description Template

```markdown
## Problem

<What issue does this PR address?>

## Solution

<How does this PR solve the problem?>

## Changes

<List files modified and why>

## Testing

- [ ] Unit tests pass
- [ ] Meta-loop on Testing repo: ✅/❌
- [ ] Meta-loop on DevPilot repo: ✅/❌

## Impact

<What does this enable? Any breaking changes?>
```

### Review Process

PRs are reviewed for:
- **Correctness**: Does it solve the stated problem?
- **Quality**: Follows code standards and best practices?
- **Testing**: Adequate test coverage?
- **Documentation**: Updated CLAUDE.md/CONTRIBUTING.md if needed?
- **Regression**: Meta-loop validation passed?

### Merging

Once approved:
```bash
gh pr merge <PR-number> --squash
```

Use squash merges to keep main branch history clean.

## Common Development Scenarios

### Scenario 1: Fixing a Planner Bug

**Workflow**:
1. Reproduce bug via meta-loop on DevPilot repo
2. Debug using Claude Code (direct development)
3. Fix the issue in `src/DevPilot.Orchestrator/Pipeline.cs` or agent prompt
4. Run unit tests
5. Run meta-loop on DevPilot repo to verify fix
6. Create PR

**Example**: [PR #47 - Planner context overload fix](https://github.com/purlieu-studios/DevPilot/pull/47)

### Scenario 2: Adding a New Feature

**Workflow**:
1. Design feature using Claude Code (direct development)
2. Implement in appropriate project (Orchestrator, Agents, Core)
3. Write unit tests
4. Update CLAUDE.md with architecture changes
5. Run meta-loop to see feature in action
6. Create PR

**Example**: [PR #46 - Add --yes flag for non-interactive mode](https://github.com/purlieu-studios/DevPilot/pull/46)

### Scenario 3: Updating Agent Prompts

**Workflow**:
1. Identify quality issue (e.g., low test coverage scores)
2. Update agent system prompt (`.agents/<agent>/system-prompt.md`)
3. Run meta-loop on Testing repo to validate improvement
4. Compare before/after quality scores
5. Create PR

**Example**: Agent quality improvements (commits 0e752e9, 96f31d9)

## Questions or Issues?

- **Documentation**: See [CLAUDE.md](./CLAUDE.md) for DevPilot architecture
- **Bug Reports**: [GitHub Issues](https://github.com/purlieu-studios/DevPilot/issues)
- **Discussions**: [GitHub Discussions](https://github.com/purlieu-studios/DevPilot/discussions)

## License

By contributing to DevPilot, you agree that your contributions will be licensed under the same license as the project.
