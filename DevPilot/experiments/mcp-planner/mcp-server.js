#!/usr/bin/env node

/**
 * MCP Server for DevPilot Planning Tools
 *
 * This server exposes tools that Claude can use to build a structured plan
 * instead of returning free-form JSON that varies in schema.
 */

const readline = require('readline');

// In-memory plan state
let currentPlan = null;

// Initialize plan state
function initPlan() {
  currentPlan = {
    plan: { summary: '', steps: [] },
    file_list: [],
    risk: { level: 'low', factors: [], mitigation: '' },
    verify: { acceptance_criteria: [], test_commands: [], manual_checks: [] },
    rollback: { strategy: '', commands: [], notes: '' },
    needs_approval: false,
    approval_reason: null
  };
}

// Tool definitions matching our PlannerOutput schema
const tools = [
  {
    name: 'plan_init',
    description: 'Initialize a new execution plan with a summary',
    inputSchema: {
      type: 'object',
      properties: {
        summary: {
          type: 'string',
          description: 'High-level summary of what will be accomplished'
        }
      },
      required: ['summary']
    }
  },
  {
    name: 'add_step',
    description: 'Add an execution step to the plan',
    inputSchema: {
      type: 'object',
      properties: {
        step_number: { type: 'number', description: 'Step number (1-based)' },
        description: { type: 'string', description: 'What this step does' },
        file_target: { type: 'string', description: 'Path to file or null' },
        agent: { type: 'string', description: 'Agent to execute: coder, tester, reviewer' },
        estimated_loc: { type: 'number', description: 'Estimated lines of code' }
      },
      required: ['step_number', 'description', 'file_target', 'agent', 'estimated_loc']
    }
  },
  {
    name: 'add_file',
    description: 'Add a file to the file list',
    inputSchema: {
      type: 'object',
      properties: {
        path: { type: 'string', description: 'File path' },
        operation: { type: 'string', enum: ['create', 'modify', 'delete'], description: 'Operation type' },
        reason: { type: 'string', description: 'Why this file is needed' }
      },
      required: ['path', 'operation', 'reason']
    }
  },
  {
    name: 'set_risk',
    description: 'Set risk assessment for the plan',
    inputSchema: {
      type: 'object',
      properties: {
        level: { type: 'string', enum: ['low', 'medium', 'high'], description: 'Risk level' },
        factors: { type: 'array', items: { type: 'string' }, description: 'Risk factors' },
        mitigation: { type: 'string', description: 'How risks are addressed' }
      },
      required: ['level', 'factors', 'mitigation']
    }
  },
  {
    name: 'set_verify',
    description: 'Set verification criteria',
    inputSchema: {
      type: 'object',
      properties: {
        acceptance_criteria: { type: 'array', items: { type: 'string' } },
        test_commands: { type: 'array', items: { type: 'string' } },
        manual_checks: { type: 'array', items: { type: 'string' } }
      },
      required: ['acceptance_criteria', 'test_commands', 'manual_checks']
    }
  },
  {
    name: 'set_rollback',
    description: 'Set rollback strategy',
    inputSchema: {
      type: 'object',
      properties: {
        strategy: { type: 'string', description: 'How to undo changes' },
        commands: { type: 'array', items: { type: 'string' }, description: 'Rollback commands' },
        notes: { type: 'string', description: 'Additional guidance' }
      },
      required: ['strategy', 'commands', 'notes']
    }
  },
  {
    name: 'set_approval',
    description: 'Mark whether plan needs approval',
    inputSchema: {
      type: 'object',
      properties: {
        needs_approval: { type: 'boolean' },
        approval_reason: { type: 'string', description: 'Reason for approval requirement or null' }
      },
      required: ['needs_approval']
    }
  },
  {
    name: 'finalize_plan',
    description: 'Finalize and return the complete plan as JSON',
    inputSchema: {
      type: 'object',
      properties: {},
      required: []
    }
  }
];

// Handle tool calls
function handleToolCall(name, args) {
  if (!currentPlan) {
    initPlan();
  }

  switch (name) {
    case 'plan_init':
      currentPlan.plan.summary = args.summary;
      return { success: true, message: 'Plan initialized' };

    case 'add_step':
      currentPlan.plan.steps.push({
        step_number: args.step_number,
        description: args.description,
        file_target: args.file_target,
        agent: args.agent,
        estimated_loc: args.estimated_loc
      });
      return { success: true, message: `Step ${args.step_number} added` };

    case 'add_file':
      currentPlan.file_list.push({
        path: args.path,
        operation: args.operation,
        reason: args.reason
      });
      return { success: true, message: `File ${args.path} added` };

    case 'set_risk':
      currentPlan.risk = {
        level: args.level,
        factors: args.factors,
        mitigation: args.mitigation
      };
      return { success: true, message: 'Risk assessment set' };

    case 'set_verify':
      currentPlan.verify = {
        acceptance_criteria: args.acceptance_criteria,
        test_commands: args.test_commands,
        manual_checks: args.manual_checks
      };
      return { success: true, message: 'Verification criteria set' };

    case 'set_rollback':
      currentPlan.rollback = {
        strategy: args.strategy,
        commands: args.commands,
        notes: args.notes
      };
      return { success: true, message: 'Rollback strategy set' };

    case 'set_approval':
      currentPlan.needs_approval = args.needs_approval;
      currentPlan.approval_reason = args.approval_reason || null;
      return { success: true, message: 'Approval status set' };

    case 'finalize_plan':
      const plan = currentPlan;
      currentPlan = null; // Reset for next request
      return { success: true, plan: plan };

    default:
      return { success: false, error: `Unknown tool: ${name}` };
  }
}

// MCP JSON-RPC message handling
const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout,
  terminal: false
});

rl.on('line', (line) => {
  try {
    const request = JSON.parse(line);

    if (request.method === 'initialize') {
      // MCP initialization handshake
      const response = {
        jsonrpc: '2.0',
        id: request.id,
        result: {
          protocolVersion: '2024-11-05',
          capabilities: {
            tools: {}
          },
          serverInfo: {
            name: 'planning-tools',
            version: '1.0.0'
          }
        }
      };
      console.log(JSON.stringify(response));
    } else if (request.method === 'tools/list') {
      // Return list of available tools
      const response = {
        jsonrpc: '2.0',
        id: request.id,
        result: { tools: tools }
      };
      console.log(JSON.stringify(response));
    } else if (request.method === 'tools/call') {
      // Execute tool
      const { name, arguments: args } = request.params;
      const result = handleToolCall(name, args);

      const response = {
        jsonrpc: '2.0',
        id: request.id,
        result: { content: [{ type: 'text', text: JSON.stringify(result, null, 2) }] }
      };
      console.log(JSON.stringify(response));
    } else {
      // Unknown method
      const response = {
        jsonrpc: '2.0',
        id: request.id,
        error: { code: -32601, message: 'Method not found' }
      };
      console.log(JSON.stringify(response));
    }
  } catch (error) {
    const response = {
      jsonrpc: '2.0',
      id: null,
      error: { code: -32700, message: 'Parse error', data: error.message }
    };
    console.log(JSON.stringify(response));
  }
});

// Initialize on startup
console.error('MCP Planning Tools Server started');
