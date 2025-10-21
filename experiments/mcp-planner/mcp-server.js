#!/usr/bin/env node

/**
 * MCP Server for DevPilot Pipeline Tools
 *
 * This server exposes tools that Claude can use to build structured outputs
 * for both planning and evaluation, ensuring consistent schemas instead of
 * free-form JSON or conversational text.
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

// In-memory evaluation state
let currentEvaluation = null;

// Initialize evaluation state
function initEvaluation() {
  currentEvaluation = {
    task_id: '',
    status: 'success',
    evaluation: {
      overall_score: 0.0,
      scores: {
        plan_quality: 0,
        code_quality: 0,
        test_coverage: 0,
        documentation: 0,
        maintainability: 0
      },
      strengths: [],
      weaknesses: [],
      recommendations: [],
      final_verdict: 'REJECT',
      justification: ''
    }
  };
}

// In-memory file operations state
let currentFileOps = null;

// Initialize file operations state
function initFileOps() {
  currentFileOps = {
    operations: []
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
  },
  // Evaluation tools
  {
    name: 'evaluation_init',
    description: 'Initialize a new evaluation with task_id and status',
    inputSchema: {
      type: 'object',
      properties: {
        task_id: { type: 'string', description: 'Unique task identifier' },
        status: { type: 'string', enum: ['success', 'failure'], description: 'Evaluation status' }
      },
      required: ['task_id', 'status']
    }
  },
  {
    name: 'set_scores',
    description: 'Set all 5 dimension scores at once',
    inputSchema: {
      type: 'object',
      properties: {
        plan_quality: { type: 'number', minimum: 0, maximum: 10, description: 'Plan quality score' },
        code_quality: { type: 'number', minimum: 0, maximum: 10, description: 'Code quality score' },
        test_coverage: { type: 'number', minimum: 0, maximum: 10, description: 'Test coverage score' },
        documentation: { type: 'number', minimum: 0, maximum: 10, description: 'Documentation score' },
        maintainability: { type: 'number', minimum: 0, maximum: 10, description: 'Maintainability score' }
      },
      required: ['plan_quality', 'code_quality', 'test_coverage', 'documentation', 'maintainability']
    }
  },
  {
    name: 'add_strength',
    description: 'Add a strength observation (max 5 total)',
    inputSchema: {
      type: 'object',
      properties: {
        strength: { type: 'string', description: 'What was done well' }
      },
      required: ['strength']
    }
  },
  {
    name: 'add_weakness',
    description: 'Add a weakness observation (max 5 total)',
    inputSchema: {
      type: 'object',
      properties: {
        weakness: { type: 'string', description: 'What needs improvement' }
      },
      required: ['weakness']
    }
  },
  {
    name: 'add_recommendation',
    description: 'Add an actionable recommendation (max 5 total)',
    inputSchema: {
      type: 'object',
      properties: {
        recommendation: { type: 'string', description: 'Specific improvement suggestion' }
      },
      required: ['recommendation']
    }
  },
  {
    name: 'set_verdict',
    description: 'Set final verdict and justification (calculates overall_score from dimension scores)',
    inputSchema: {
      type: 'object',
      properties: {
        final_verdict: { type: 'string', enum: ['ACCEPT', 'REJECT'], description: 'Final verdict' },
        justification: { type: 'string', description: 'Clear justification (1-3 sentences)' }
      },
      required: ['final_verdict', 'justification']
    }
  },
  {
    name: 'finalize_evaluation',
    description: 'Finalize and return the complete evaluation as JSON',
    inputSchema: {
      type: 'object',
      properties: {},
      required: []
    }
  },
  // File operation tools for Coder agent
  {
    name: 'file_exists',
    description: 'Check if a file exists in the workspace. Use BEFORE create_file or modify_file to determine which operation to use.',
    inputSchema: {
      type: 'object',
      properties: {
        path: { type: 'string', description: 'Relative path from workspace root (e.g., "src/Calculator.cs")' }
      },
      required: ['path']
    }
  },
  {
    name: 'create_file',
    description: 'Create a brand new file. ONLY use if file_exists returns false. For existing files, use modify_file instead.',
    inputSchema: {
      type: 'object',
      properties: {
        path: { type: 'string', description: 'Relative path for new file' },
        content: { type: 'string', description: 'Complete file content' },
        reason: { type: 'string', description: 'Why this file is needed (1 sentence)' }
      },
      required: ['path', 'content', 'reason']
    }
  },
  {
    name: 'modify_file',
    description: 'Modify an existing file with line-based changes. Use this for files that already exist.',
    inputSchema: {
      type: 'object',
      properties: {
        path: { type: 'string', description: 'Path to existing file' },
        changes: {
          type: 'array',
          description: 'List of line changes to apply sequentially',
          items: {
            type: 'object',
            properties: {
              line_number: { type: 'number', description: 'Line to modify (1-indexed)' },
              old_content: { type: 'string', description: 'Expected current line content (optional, for validation)' },
              new_content: { type: 'string', description: 'New line content (empty string to delete line)' }
            },
            required: ['line_number', 'new_content']
          }
        }
      },
      required: ['path', 'changes']
    }
  },
  {
    name: 'delete_file',
    description: 'Delete a file from the workspace.',
    inputSchema: {
      type: 'object',
      properties: {
        path: { type: 'string', description: 'Path to file to delete' },
        reason: { type: 'string', description: 'Why this file is being deleted' }
      },
      required: ['path', 'reason']
    }
  },
  {
    name: 'rename_file',
    description: 'Rename or move a file to a new location.',
    inputSchema: {
      type: 'object',
      properties: {
        old_path: { type: 'string', description: 'Current file path' },
        new_path: { type: 'string', description: 'New file path' },
        reason: { type: 'string', description: 'Why this file is being renamed/moved' }
      },
      required: ['old_path', 'new_path', 'reason']
    }
  },
  {
    name: 'finalize_file_operations',
    description: 'Finalize and return all queued file operations as structured JSON. Call this after all file operations are queued.',
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

    // Evaluation tool handlers
    case 'evaluation_init':
      if (!currentEvaluation) {
        initEvaluation();
      }
      currentEvaluation.task_id = args.task_id;
      currentEvaluation.status = args.status;
      return { success: true, message: 'Evaluation initialized' };

    case 'set_scores':
      if (!currentEvaluation) {
        initEvaluation();
      }
      currentEvaluation.evaluation.scores = {
        plan_quality: args.plan_quality,
        code_quality: args.code_quality,
        test_coverage: args.test_coverage,
        documentation: args.documentation,
        maintainability: args.maintainability
      };
      return { success: true, message: 'Scores set' };

    case 'add_strength':
      if (!currentEvaluation) {
        initEvaluation();
      }
      if (currentEvaluation.evaluation.strengths.length < 5) {
        currentEvaluation.evaluation.strengths.push(args.strength);
        return { success: true, message: 'Strength added' };
      }
      return { success: false, error: 'Maximum 5 strengths allowed' };

    case 'add_weakness':
      if (!currentEvaluation) {
        initEvaluation();
      }
      if (currentEvaluation.evaluation.weaknesses.length < 5) {
        currentEvaluation.evaluation.weaknesses.push(args.weakness);
        return { success: true, message: 'Weakness added' };
      }
      return { success: false, error: 'Maximum 5 weaknesses allowed' };

    case 'add_recommendation':
      if (!currentEvaluation) {
        initEvaluation();
      }
      if (currentEvaluation.evaluation.recommendations.length < 5) {
        currentEvaluation.evaluation.recommendations.push(args.recommendation);
        return { success: true, message: 'Recommendation added' };
      }
      return { success: false, error: 'Maximum 5 recommendations allowed' };

    case 'set_verdict':
      if (!currentEvaluation) {
        initEvaluation();
      }
      // Calculate overall_score using weighted average
      const scores = currentEvaluation.evaluation.scores;
      const overall = (scores.plan_quality * 1.0 +
                       scores.code_quality * 1.5 +
                       scores.test_coverage * 1.5 +
                       scores.documentation * 1.0 +
                       scores.maintainability * 1.0) / 6.0;

      currentEvaluation.evaluation.overall_score = Math.round(overall * 10) / 10; // Round to 1 decimal
      currentEvaluation.evaluation.final_verdict = args.final_verdict;
      currentEvaluation.evaluation.justification = args.justification;
      return { success: true, message: 'Verdict set', overall_score: currentEvaluation.evaluation.overall_score };

    case 'finalize_evaluation':
      const evaluation = currentEvaluation;
      currentEvaluation = null; // Reset for next request
      return { success: true, evaluation: evaluation };

    // File operation tool handlers
    case 'file_exists':
      // Note: Actual file existence check will be done by C# orchestrator
      // This just acknowledges the query and returns a placeholder response
      return {
        success: true,
        exists: false,
        message: 'File existence will be checked by orchestrator during patch application'
      };

    case 'create_file':
      if (!currentFileOps) {
        initFileOps();
      }
      currentFileOps.operations.push({
        type: 'create',
        path: args.path,
        content: args.content,
        reason: args.reason
      });
      return { success: true, message: `Queued file creation: ${args.path}` };

    case 'modify_file':
      if (!currentFileOps) {
        initFileOps();
      }
      currentFileOps.operations.push({
        type: 'modify',
        path: args.path,
        changes: args.changes
      });
      return { success: true, message: `Queued ${args.changes.length} changes to ${args.path}` };

    case 'delete_file':
      if (!currentFileOps) {
        initFileOps();
      }
      currentFileOps.operations.push({
        type: 'delete',
        path: args.path,
        reason: args.reason
      });
      return { success: true, message: `Queued file deletion: ${args.path}` };

    case 'rename_file':
      if (!currentFileOps) {
        initFileOps();
      }
      currentFileOps.operations.push({
        type: 'rename',
        old_path: args.old_path,
        new_path: args.new_path,
        reason: args.reason
      });
      return { success: true, message: `Queued file rename: ${args.old_path} → ${args.new_path}` };

    case 'finalize_file_operations':
      const fileOps = currentFileOps;
      currentFileOps = null; // Reset for next request
      return { success: true, file_operations: fileOps };

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
            name: 'pipeline-tools',
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
console.error('MCP Pipeline Tools Server started');
