# Test runner for MCP Planning Tools with Claude CLI (PowerShell version)
#
# Usage:
#   .\test-runner.ps1 "Your request here"
#
# Example:
#   .\test-runner.ps1 "Create a Calculator class with Add method"

param(
    [Parameter(Mandatory=$true)]
    [string]$Request
)

$ErrorActionPreference = "Stop"

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputFile = "examples\output-$timestamp.json"

# System prompt instructing Claude to use the tools
$systemPrompt = @"
You are the Planner Agent in a MASAI pipeline.

When creating an execution plan, you MUST use the provided planning tools in this order:

1. Call plan_init with a summary
2. Call add_step for each execution step (up to 7 steps)
3. Call add_file for each file to be created/modified/deleted
4. Call set_risk with risk assessment
5. Call set_verify with verification criteria
6. Call set_rollback with rollback strategy
7. Call set_approval to indicate if approval is needed
8. Call finalize_plan to complete the plan

IMPORTANT: Use the tools, do NOT return JSON directly. The tools will build the structured plan for you.

Example flow for "Create Calculator class":
- plan_init(summary: "Create Calculator class with Add method")
- add_step(step_number: 1, description: "Create Calculator.cs", file_target: "src/Calculator.cs", agent: "coder", estimated_loc: 30)
- add_file(path: "src/Calculator.cs", operation: "create", reason: "Calculator implementation")
- set_risk(level: "low", factors: [...], mitigation: "...")
- set_verify(acceptance_criteria: [...], test_commands: [...], manual_checks: [])
- set_rollback(strategy: "...", commands: [...], notes: "...")
- set_approval(needs_approval: false)
- finalize_plan()

Now create a plan for the user's request.
"@

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Testing MCP Planning Tools" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Request: $Request"
Write-Host "Output: $outputFile"
Write-Host ""

# Run Claude CLI with MCP config
$escapedSystemPrompt = $systemPrompt.Replace('"', '\"')
$Request | claude `
    --print `
    --verbose `
    --system-prompt "$escapedSystemPrompt" `
    --model sonnet `
    --output-format stream-json `
    --mcp-config plan-tools.json `
    --permission-mode bypassPermissions `
    | Tee-Object -FilePath $outputFile

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Output saved to: $outputFile" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
