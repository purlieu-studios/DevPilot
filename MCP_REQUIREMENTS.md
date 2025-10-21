# MCP Requirements for DevPilot

## What Is Required

For MCP (Model Context Protocol) file operations to work in DevPilot, you need:

1. **Node.js Installed** - MCP server runs on Node.js
   - Install: https://nodejs.org/
   - Verify: `node --version`

2. **MCP Server Running** - The MCP server must be available
   - Location: `experiments/mcp-planner/mcp-server.js`
   - Auto-started by Claude CLI when config specifies `mcp_config_path`

3. **Agent Configuration** - Agent config must specify MCP tools
   - File: `.agents/coder/config.json`
   - Must include: `"mcp_config_path": "../../experiments/mcp-planner/plan-tools.json"`

4. **MCP Tools Config** - Tools must be defined
   - File: `experiments/mcp-planner/plan-tools.json`
   - Specifies MCP server path and tools

## Verification

Check if MCP is working:

```bash
# 1. Verify Node.js
node --version

# 2. Verify MCP server can start
node experiments/mcp-planner/mcp-server.js
# (Should wait for input - press Ctrl+C to exit)

# 3. Check Coder config has MCP enabled
cat .agents/coder/config.json | grep mcp_config_path
# Should show: "mcp_config_path": "../../experiments/mcp-planner/plan-tools.json"

# 4. Run a simple pipeline test
dotnet run --project src/DevPilot.Console "Add a Calculator class" --yes
# Should complete without "Failed to apply patch" errors
```

## How It Works

1. **Claude CLI** reads agent config and sees `mcp_config_path`
2. **MCP Server** is launched as subprocess (mcp-server.js)
3. **Coder Agent** uses MCP tools (create_file, modify_file, etc.) instead of outputting unified diffs
4. **Pipeline.cs** parses MCP tool responses and applies file operations

## Troubleshooting

### Error: "Failed to apply patch: No valid file patches found"

**Cause:** MCP tool response format mismatch (FIXED in commit)

**Solution:** Update to latest main branch which includes MCP response parsing fix

### Error: MCP server not starting

**Cause:** Node.js not installed or not in PATH

**Solution:**
```bash
# Install Node.js
winget install OpenJS.NodeJS

# Verify
node --version
npm --version
```

### Coder agent not using MCP tools

**Cause:** Coder config missing mcp_config_path

**Solution:** Check `.agents/coder/config.json` contains:
```json
{
  "agent_name": "coder",
  "mcp_config_path": "../../experiments/mcp-planner/plan-tools.json"
}
```

## No Special Setup Required

DevPilot automatically handles:
- ✅ MCP server process management
- ✅ Tool registration and discovery
- ✅ JSON-RPC communication
- ✅ File operation parsing and application

You just need Node.js installed. Everything else is handled by the pipeline.
