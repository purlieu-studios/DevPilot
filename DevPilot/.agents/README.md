# MASAI Agent Definitions

This directory contains declarative agent definitions for the DevPilot MASAI (Modular Autonomous Software AI) architecture.

## Overview

MASAI agents are defined declaratively using three files per agent:

1. **system-prompt.md** - Core instructions, behavior, and examples
2. **tools.json** - Available tools/functions the agent can use
3. **config.json** - Model settings, capabilities, and configuration

## Agent Architecture

```
.agents/
├── orchestrator/          # Main coordinator agent
│   ├── system-prompt.md
│   ├── tools.json
│   └── config.json
├── code-generator/        # C# code generation agent
│   ├── system-prompt.md
│   ├── tools.json
│   └── config.json
└── validator/             # Code validation agent
    ├── system-prompt.md
    ├── tools.json
    └── config.json
```

## Available Agents

### 1. Orchestrator Agent
- **Purpose**: Coordinates tasks between specialized agents
- **Capabilities**:
  - Task decomposition
  - Agent routing
  - Workflow management
  - Context management
  - Result aggregation
- **Model**: Claude Sonnet 4.5 (temperature: 0.3)

### 2. Code Generator Agent
- **Purpose**: Generates high-quality C# code
- **Capabilities**:
  - Class/interface generation
  - Method/property generation
  - Code documentation
  - Pattern application
  - Code formatting
- **Model**: Claude Sonnet 4.5 (temperature: 0.2)

### 3. Validator Agent
- **Purpose**: Validates code quality and compliance
- **Capabilities**:
  - Syntax validation
  - Style compliance checking
  - Naming convention validation
  - Documentation validation
  - Code quality analysis
  - Improvement suggestions
- **Model**: Claude Sonnet 4.5 (temperature: 0.1)

## Agent Communication Flow

```
User Request
    ↓
Orchestrator Agent (decomposes task)
    ↓
    ├─→ Code Generator Agent (generates code)
    │       ↓
    └─→ Validator Agent (validates code)
            ↓
            ├─ Pass → Return to user
            └─ Fail → Feedback loop to Code Generator
```

## File Format Specifications

### system-prompt.md
Contains the agent's core instructions:
- Role and responsibilities
- Input/output formats
- Examples
- Best practices
- Decision frameworks

### tools.json
Defines available tools:
```json
{
  "tools": [
    {
      "name": "tool_name",
      "description": "What the tool does",
      "parameters": { /* parameter schema */ },
      "required": ["param1", "param2"]
    }
  ]
}
```

### config.json
Configuration settings:
```json
{
  "agent_name": "agent-name",
  "version": "1.0.0",
  "model": {
    "provider": "anthropic",
    "model_name": "claude-sonnet-4-5-20250929",
    "temperature": 0.2,
    "max_tokens": 4096
  },
  "capabilities": ["capability1", "capability2"]
}
```

## Usage

These agent definitions are loaded by the C# runtime (`DevPilot.Agents`) which:
1. Reads agent definitions from this folder
2. Constructs prompts using system-prompt.md
3. Configures LLM calls using config.json
4. Provides tool implementations from tools.json
5. Executes agent tasks via API calls

## Modifying Agents

To modify an agent's behavior:
1. Edit the `system-prompt.md` for instruction changes
2. Edit `tools.json` to add/remove capabilities
3. Edit `config.json` for model or configuration changes

**No C# code changes required** - agents are declarative!

## Adding New Agents

To add a new agent:
1. Create a new folder: `.agents/agent-name/`
2. Add the three required files: `system-prompt.md`, `tools.json`, `config.json`
3. Update the orchestrator's `system-prompt.md` to include the new agent in routing decisions
4. The runtime will automatically discover and load the new agent

## Best Practices

- Keep system prompts clear and concise
- Provide detailed examples in system-prompt.md
- Set temperature based on task determinism:
  - 0.1-0.2 for deterministic tasks (validation, analysis)
  - 0.2-0.4 for creative tasks (code generation)
  - 0.3-0.5 for decision-making (orchestration)
- Test prompt changes incrementally
- Version control all agent definitions
- Document any breaking changes

## Version History

- **v1.0.0** (2025-10-13): Initial MASAI agent definitions
  - Orchestrator agent
  - Code Generator agent
  - Validator agent
