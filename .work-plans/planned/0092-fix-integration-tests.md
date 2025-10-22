# Work Plan: Fix Failing TestRunner Integration Tests

**Status:** Planned
**Issue:** #92
**PR:** TBD
**Branch:** fix/integration-test-failures
**Started:** TBD
**Completed:** TBD

---

## Problem Statement

Three integration tests in `TestRunnerIntegrationTests` are failing on the main branch:

1. `TestRunner_MultipleSolutionFiles_ChoosesCorrectOne`
2. `TestRunner_SingleSolutionFile_CollectsCoverage`
3. `TestRunner_WithDirectoryBuildProps_AppliesCoverletCollector`

All three tests fail after approximately 15 minutes with `result.Pass = False`, indicating they hit a timeout or the test runner failed to complete successfully.

**User Impact:**
- ❌ CI fails on every commit to main
- ⚠️ Unable to verify CI passes for new PRs
- ⚠️ Blocks confidence in merging new work

## Evidence

**CI Runs Showing Failures:**
- PR #91: https://github.com/purlieu-studios/DevPilot/actions/runs/18696609563
- Main branch (latest): https://github.com/purlieu-studios/DevPilot/actions/runs/18694905341
- Main branch: https://github.com/purlieu-studios/DevPilot/actions/runs/18693815885
- Main branch: https://github.com/purlieu-studios/DevPilot/actions/runs/18691569353

**Error Message from CI:**
```
Code analysis failed: The solution file has two projects named "Calculator".
```

**Failure Pattern:**
```
Error Message:
  Expected result.Pass to be True because all tests should pass, but found False.

Stack Trace:
  at DevPilot.Orchestrator.Tests.TestRunnerIntegrationTests.TestRunner_SingleSolutionFile_CollectsCoverage()
  in D:\a\DevPilot\DevPilot\tests\DevPilot.Orchestrator.Tests\TestRunnerIntegrationTests.cs:line 45
```

All three tests consistently fail after ~15 minutes, suggesting:
- Test timeout (default xUnit timeout is 15 minutes)
- Test runner hangs or enters infinite loop
- Duplicate project names causing MSBuild to fail

## Proposed Approach

### Investigation Phase

1. **Examine test fixtures** in `tests/DevPilot.Orchestrator.Tests/TestFixtures/` for duplicate "Calculator" projects
2. **Run tests locally** to reproduce the failure and see full error output
3. **Review recent changes** to TestRunner or test fixtures that might have introduced the issue

### Likely Root Cause

The error message suggests duplicate projects named "Calculator" in solution files:
```
Code analysis failed: The solution file has two projects named "Calculator".
```

Possible causes:
- Test fixtures accidentally contain multiple projects with same name
- Workspace setup copies Calculator examples incorrectly
- .sln file generation creates duplicate entries

### Fix Strategy

Once root cause is identified:
- If duplicate projects in fixtures: Rename or remove duplicates
- If workspace setup issue: Fix copying logic in test setup
- If .sln generation issue: Fix solution file creation/modification logic

Increase test verbosity to capture full MSBuild output if needed.

## Files to Modify

**Likely candidates** (TBD based on investigation):
- Test fixture files under `tests/DevPilot.Orchestrator.Tests/TestFixtures/`
- `tests/DevPilot.Orchestrator.Tests/TestRunnerIntegrationTests.cs` (test setup/teardown)
- Possibly `src/DevPilot.Orchestrator/TestRunner.cs` if issue is in runner logic

## Files NOT to Modify

**Recently modified in PR #91** (avoid conflicts):
- `.agents/coder/system-prompt.md`
- `experiments/mcp-planner/mcp-server.js`
- `src/DevPilot.Orchestrator/WorkspaceManager.cs` (ApplyLineChanges method)

**Recently modified in PR #88** (check for related changes):
- Files related to state persistence and Roslyn integration

## Success Criteria

- [ ] All 3 TestRunner integration tests pass locally
- [ ] Tests complete in < 5 minutes (not hitting timeout)
- [ ] CI build passes with all tests green
- [ ] No duplicate project errors in MSBuild output
- [ ] Test fixtures correctly represent intended scenarios

## Testing Strategy

### Local Reproduction

```bash
# Run just the failing tests with verbose output
dotnet test tests/DevPilot.Orchestrator.Tests \
  --filter "FullyQualifiedName~TestRunner" \
  --verbosity detailed \
  --logger "console;verbosity=detailed"
```

### Verification

After fix, verify:
1. All 3 tests pass locally
2. Tests complete quickly (< 5 min total)
3. Run full test suite to ensure no regressions
4. Push to PR branch and verify CI passes

### Edge Cases

- Multiple solution files in workspace
- Directory.Build.props affecting project resolution
- Coverlet collector configuration

## Handoff Prompt

```
Fix the 3 failing integration tests documented in issue #92.

CONTEXT:
- Issue: https://github.com/purlieu-studios/DevPilot/issues/92
- These tests are failing on main branch (pre-existing, not from recent PRs)
- Build and unit tests pass; only these 3 integration tests fail

FAILING TESTS (in tests/DevPilot.Orchestrator.Tests/TestRunnerIntegrationTests.cs):
1. TestRunner_MultipleSolutionFiles_ChoosesCorrectOne
2. TestRunner_SingleSolutionFile_CollectsCoverage
3. TestRunner_WithDirectoryBuildProps_AppliesCoverletCollector

All fail after ~15 minutes with: "Expected result.Pass to be True, but found False"

ERROR SEEN IN CI LOGS:
"Code analysis failed: The solution file has two projects named 'Calculator'."

This suggests duplicate Calculator projects in test fixtures.

SCOPE:
- Work ONLY on fixing these 3 tests
- Create branch: fix/integration-test-failures
- DO NOT modify:
  - .agents/coder/system-prompt.md (recently updated in PR #91)
  - experiments/mcp-planner/mcp-server.js (recently updated in PR #91)
  - src/DevPilot.Orchestrator/WorkspaceManager.cs ApplyLineChanges method (recently updated in PR #91)

WORKFLOW:
1. Create branch: git checkout -b fix/integration-test-failures
2. Investigate test fixtures for duplicate "Calculator" projects
3. Run tests locally to verify fix: dotnet test tests/DevPilot.Orchestrator.Tests --filter "FullyQualifiedName~TestRunner"
4. Ensure all 3 tests pass
5. Create PR when done

DEFINITION OF DONE:
- All 3 TestRunner integration tests pass locally
- CI passes (build + all tests)
- PR created referencing "Fixes #92"
```

---

## Notes

[To be filled in during implementation]

## Related Work

- Issue: #92
- Related to: TestRunner functionality, test fixtures
- May be related to recent changes in PR #88 (state persistence) or earlier
