# Work Plan: Fix Duplicate Method Bug

**Status:** Completed ✅
**Issue:** #89
**PR:** #91
**Branch:** fix/mcp-multiline-operations
**Started:** 2025-10-21
**Completed:** 2025-10-21 (merged at 22:51:31Z)

---

## Problem Statement

The Coder agent was generating duplicate methods when modifying existing methods. When asked to add validation to an existing method, it would insert the new method definition while leaving the old one in place, resulting in CS0111 compilation errors:

```
error CS0111: Type 'Calculator' already defines a member called 'Add' with the same parameter types
```

**User Impact:** Pipeline would fail at the test stage due to compilation errors, preventing any code changes from being applied.

## Evidence

- Initial discovery: NaN validation test on simple-calculator example
- Error: "Code analysis failed with 2 errors" showing CS0111 duplicate member errors
- Root cause: MCP `modify_file` tool only inserted new content but didn't delete old method lines

## Proposed Approach

Add a `lines_to_replace` parameter to the MCP `modify_file` tool that:
1. Allows specifying how many consecutive lines to delete before inserting new content
2. Defaults to 1 (preserving existing behavior for single-line changes)
3. For method replacements, deletes all old method lines before inserting the new method

**Why this approach:**
- Minimal API change (one optional parameter)
- Backward compatible (defaults to current behavior)
- Gives agent explicit control over deletion scope
- Prevents accidental data loss (requires explicit count)

## Files Modified

- `experiments/mcp-planner/mcp-server.js` - Added `lines_to_replace` parameter to modify_file schema
- `src/DevPilot.Orchestrator/WorkspaceManager.cs` - Implemented deletion logic in ApplyLineChanges method
- `.agents/coder/system-prompt.md` - Added Example 3 showing proper usage for method modifications
- `tests/DevPilot.Agents.IntegrationTests/CoderPromptValidationTests.cs` - Updated test expectations for new example

## Files NOT Modified

- No conflicts with concurrent work

## Success Criteria

- [x] LinesToReplace parameter added to MCP modify_file schema
- [x] WorkspaceManager deletes N-1 lines before current line when LinesToReplace > 1
- [x] Coder system prompt documents usage with comprehensive examples
- [x] All unit tests pass (425/425)
- [x] Build passes with 0 errors
- [x] Integration test failures confirmed as pre-existing (not caused by this PR)

## Testing Results

**Local Testing:**
- ✅ Full build: 0 errors, 28 warnings (CA1031 - expected)
- ✅ All unit tests: 425/425 passing
- ✅ CoderPrompt validation tests: All 8 assertions passing

**CI Results:**
- ✅ Build Validation: PASSED
- ✅ Unit Tests: PASSED (425 tests)
- ❌ Integration Tests: FAILED (3 tests) - **Pre-existing on main, not caused by this PR**

The 3 failing integration tests (`TestRunner_MultipleSolutionFiles_ChoosesCorrectOne`, `TestRunner_SingleSolutionFile_CollectsCoverage`, `TestRunner_WithDirectoryBuildProps_AppliesCoverletCollector`) were already failing on main branch before this PR. Tracked separately in issue #92.

## Implementation Notes

### Key Design Decision: Why `lines_to_replace` instead of auto-detection?

We considered having the tool automatically detect method boundaries, but chose explicit count because:
1. **Simplicity**: Agent already has the context (can count lines in its prompt)
2. **Reliability**: No complex parsing or heuristics needed
3. **Flexibility**: Works for any multiline replacement, not just methods
4. **Clarity**: Explicit is better than implicit

### Example Usage

When modifying a 13-line method:

```json
{
  "path": "Calculator/Calculator.cs",
  "changes": [{
    "line_number": 8,
    "old_content": "    /// <summary>",
    "new_content": "    /// <summary>\n    /// Adds two numbers...\n    public double Add(double a, double b)\n    {\n        if (double.IsNaN(a)) throw new ArgumentException(...);\n        return a + b;\n    }",
    "lines_to_replace": 13
  }]
}
```

This deletes lines 8-20 (13 lines total), then inserts the new method at line 8.

## Follow-up Work

- None required - feature is complete and working

## Related Work

- Issue: #89 (duplicate method errors)
- PR: #91 (this work)
- Related Issue: #92 (pre-existing integration test failures)
