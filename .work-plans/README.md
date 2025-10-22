# Work Plans

This directory tracks planned, in-progress, and completed work for DevPilot development.

## Purpose

Work plans help us:
- **Scope work clearly** before starting implementation
- **Enable parallel development** by documenting what files will be modified
- **Maintain context** across sessions and between Claude instances
- **Create a history** of decisions and approaches

## Folder Structure

```
.work-plans/
├── planned/        # Work that's scoped but not started
├── in-progress/    # Work currently being implemented
├── completed/      # Finished work (merged PRs)
└── TEMPLATE.md     # Template for new work plans
```

## Workflow

### 1. Creating a New Work Plan

When starting new work:

```bash
# Copy the template
cp .work-plans/TEMPLATE.md .work-plans/planned/NNNN-short-description.md

# Fill in the details
# Edit the file with problem statement, approach, files to modify, etc.
```

**Naming Convention:** `NNNN-short-description.md` where:
- `NNNN` is the GitHub issue number (or `feat`/`fix` prefix if no issue)
- `short-description` is a brief slug (e.g., `fix-duplicate-methods`)

### 2. Starting Work

When beginning implementation:

```bash
# Move to in-progress
git mv .work-plans/planned/0092-fix-integration-tests.md .work-plans/in-progress/

# Create the branch mentioned in the work plan
git checkout -b fix/integration-test-failures
```

### 3. Completing Work

When PR is merged:

```bash
# Move to completed
git mv .work-plans/in-progress/0092-fix-integration-tests.md .work-plans/completed/

# Add completion metadata to the file (PR number, merge date)
```

## Work Plan Contents

Each work plan should include:

- **Problem Statement**: What are we trying to solve?
- **Proposed Approach**: How will we solve it?
- **Branch Name**: Dedicated branch for this work
- **Files to Modify**: What will change?
- **Files NOT to Modify**: Avoid conflicts with concurrent work
- **Success Criteria**: How do we know we're done?
- **Handoff Prompt**: Instructions for another Claude instance to pick up the work

See `TEMPLATE.md` for the full structure.

## Benefits

### For Context Management
- New Claude instances can read `in-progress/*.md` to understand what's happening
- No need to re-explain the approach if a session crashes

### For Parallel Work
- Easily see what files are being modified in other work plans
- Avoid merge conflicts by coordinating file changes

### For Historical Context
- `completed/` folder serves as a record of how we approached problems
- Useful for understanding "why did we do it this way?"

### For Team Collaboration
- Human developers can see what AI instances are working on
- Clear handoff points between sessions

## Example

```markdown
# Work Plan: Fix Duplicate Method Bug

**Status:** Completed
**Issue:** #89
**PR:** #91
**Branch:** fix/mcp-multiline-operations
**Completed:** 2025-10-21

## Problem
Coder agent generates duplicate methods when modifying existing code...

[rest of plan]
```

## Tips

- **Write work plans BEFORE coding**: Helps clarify thinking
- **Keep them concise**: 1-2 pages max
- **Update as you go**: Add notes about challenges or decisions
- **Reference related work**: Link to other work plans or PRs
- **Use checklists**: Makes progress visible

## Integration with GitHub

Work plans complement (not replace) GitHub issues:
- **Issues**: Track user-facing problems and feature requests
- **Work Plans**: Track implementation approach and technical details

A single issue might have multiple work plans (e.g., breaking work into phases).
