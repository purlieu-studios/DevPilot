# MCP Planning Tools Experiment

## Overview

This experiment tests whether Claude CLI's MCP (Model Context Protocol) tool calling can solve the schema consistency problem we've been experiencing with direct JSON output.

## The Problem

When asking Claude CLI to return structured JSON, the schema varies between runs even with identical prompts:
- Run 1: Returns `{plan, file_list, risk, verify, rollback}`
- Run 2: Returns `{task, analysis, steps, risk}` (different schema!)
- Run 3: Returns conversational text with no JSON

## The Hypothesis

Instead of asking Claude to return JSON, we provide **tools** that Claude can call to build structured data:
- `plan_init` - Initialize plan
- `add_step` - Add execution step
- `add_file` - Add file to list
- `set_risk` - Set risk assessment
- `set_verify` - Set verification criteria
- `set_rollback` - Set rollback strategy
- `set_approval` - Mark approval requirement
- `finalize_plan` - Return complete plan

**Benefits:**
- Tool schemas are enforced by MCP protocol
- Each tool call has guaranteed structure
- Claude can explain AND build structured data
- Works with Claude's conversational nature

## Files

- `mcp-server.js` - MCP server exposing planning tools
- `plan-tools.json` - MCP configuration for Claude CLI
- `test-runner.sh` - Script to test Claude with tools
- `examples/` - Test outputs
- `FINDINGS.md` - Results and recommendation

## How to Test

1. **Run the test:**
   ```bash
   cd experiments/mcp-planner
   chmod +x test-runner.sh
   ./test-runner.sh "Create a Calculator class with Add method"
   ```

2. **Check the output:**
   - Look at `examples/output-*.json`
   - Verify Claude called the tools
   - Check if `finalize_plan` returned correct schema

3. **Run multiple times:**
   ```bash
   ./test-runner.sh "Create Calculator class with Add method"
   ./test-runner.sh "Create Calculator class with Add method"
   ./test-runner.sh "Create Calculator class with Add method"
   ```
   Verify consistency across runs.

## Success Criteria

✅ **Success**: Claude consistently:
- Calls tools in logical order
- Provides correct arguments to each tool
- Returns complete plan with all required fields
- Schema is identical across multiple runs

❌ **Failure**: Claude:
- Ignores tools and returns JSON directly
- Calls tools with incorrect arguments
- Doesn't call `finalize_plan`
- Tool calling is inconsistent

## Integration Plan

**If successful:**
1. Create `ClaudeMcpClient` class in `src/DevPilot.Agents/`
2. Update `ClaudeCliAgent` to use MCP client for planner
3. Parse tool calls from stream-json output
4. Build `PlannerOutput` from tool results
5. Add integration tests

**If unsuccessful:**
- Fall back to flexible JSON deserialization
- Use custom JsonConverters to handle schema variations

## Notes

- This is an isolated experiment - no impact on main codebase
- Total LOC: ~280 (server + config + tests)
- Can be split into multiple commits if integrated
