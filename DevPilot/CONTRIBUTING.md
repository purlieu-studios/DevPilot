# Contributing to DevPilot

Thank you for your interest in contributing to DevPilot! This document provides guidelines for developing and improving the DevPilot MASAI pipeline.

## Table of Contents

- [Development Workflow](#development-workflow)
- [Meta-Loop vs Direct Development](#meta-loop-vs-direct-development)
- [Pre-Merge Checklist](#pre-merge-checklist)
- [Testing Guidelines](#testing-guidelines)
- [Code Standards](#code-standards)
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
