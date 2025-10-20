# DevPilot

<!-- CI Workflow Validation Test -->

**An autonomous AI coding agent implementing the MASAI architecture for C# projects**

DevPilot is a reusable MASAI (Modular Architecture for Software-engineering AI) pipeline that orchestrates five specialized agents to plan, code, review, test, and evaluate software changes—all automatically.

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()
[![Tests](https://img.shields.io/badge/tests-453%2F453-brightgreen)]()
[![Warnings](https://img.shields.io/badge/warnings-0-brightgreen)]()

---

## Quick Start

### Prerequisites

1. **.NET 8.0 SDK** or later
2. **Claude CLI** (Anthropic's command-line tool)
3. **Git** (for cloning)

### Installation

```bash
# 1. Install Claude CLI
npm install -g @anthropic-ai/claude-code

# 2. Authenticate with Anthropic
claude login

# 3. Clone DevPilot
git clone https://github.com/purlieu-studios/DevPilot.git
cd DevPilot

# 4. Build DevPilot
dotnet build

# 5. Verify installation
dotnet test
# Expected: 453/453 tests passing
```

### Your First DevPilot Run

```bash
# Navigate to your C# project
cd /path/to/your-csharp-project

# Run DevPilot
dotnet run --project /path/to/DevPilot/src/DevPilot.Console -- "Add email validation to User class"

# Watch the MASAI pipeline execute:
# ✓ Planning (2-3 min) - Analyzes request, creates execution plan
# ✓ Coding (30-60 sec) - Generates unified diff patch
# ✓ Reviewing (30-60 sec) - Validates code quality
# ✓ Testing (30-60 sec) - Runs dotnet test
# ✓ Evaluating (30-60 sec) - Provides quality scores

# Total: ~5 minutes
```

**Result**: DevPilot creates an isolated workspace, generates code, runs tests, and provides quality metrics (1-10 scale).

---

## What is MASAI?

**MASAI** (Modular Architecture for Software-engineering AI) is a framework that divides complex software engineering tasks into specialized sub-agents, each with focused objectives and strategies.

**Reference**: [Arora et al. (2024)](https://arxiv.org/abs/2406.11638)

### DevPilot's 5-Agent Pipeline

| Agent | Role | Implementation | Output |
|-------|------|----------------|--------|
| **Planner** | Analyzes request, creates execution plan | Claude CLI + MCP (8 tools) | JSON plan with file operations, risk assessment |
| **Coder** | Generates code changes | Claude CLI (C# expert) | Unified diff patch |
| **Reviewer** | Validates code quality semantically | Claude CLI | JSON with verdict (APPROVE/REJECT/REVISE) |
| **Tester** | Executes tests, reports results | Native C# (TestRunner) | JSON with test pass/fail + coverage |
| **Evaluator** | Scores overall quality | Claude CLI + MCP (7 tools) | JSON with scores (1-10) + recommendations |

---

## Features

### ✅ Production-Ready Features

- **5-Agent MASAI Pipeline** - Planning → Coding → Reviewing → Testing → Evaluating
- **RAG Integration (Optional)** - Retrieval Augmented Generation using Ollama + mxbai-embed-large
- **Repository Structure Awareness** - Works with any C# project layout (not just `src/` and `tests/`)
- **MCP Tool Integration** - Structured outputs for Planner and Evaluator agents
- **Real Test Execution** - Native C# TestRunner executes `dotnet test` and parses results
- **Workspace Isolation** - Changes made in `.devpilot/workspaces/{id}/` (never touches your source)
- **Zero-Warning Build** - Maintained at 453/453 tests passing, 0 build warnings

### 🚧 Advanced Features

- **Custom Agents** - Override `.agents/` in your repository
- **Project-Specific Config** - `CLAUDE.md` for domain knowledge, `devpilot.json` for settings
- **Patch Application** - Automated unified diff patch application
- **Quality Metrics** - 5 dimensions (plan, code, test, docs, maintainability)

---

## Optional: Enable RAG

RAG (Retrieval Augmented Generation) enhances agents with semantic search over your codebase.

### Setup

```bash
# 1. Install Ollama
# Windows:
winget install Ollama.Ollama

# macOS:
brew install ollama

# Linux:
curl -fsSL https://ollama.com/install.sh | sh

# 2. Pull embedding model
ollama pull mxbai-embed-large

# 3. Start Ollama service
ollama serve
# Keep this running in a terminal

# 4. Run DevPilot with RAG
dotnet run --project /path/to/DevPilot/src/DevPilot.Console -- --enable-rag "your request"
```

**What RAG Does**:
- Indexes your workspace files (`.cs`, `.csproj`, `.md`, etc.)
- Retrieves top-5 most relevant code chunks for each agent
- Provides semantic context (e.g., existing patterns, CLAUDE.md conventions)

**When to Use RAG**:
- Large codebases (>50 files)
- Complex domain-specific projects
- When you want agents to follow existing patterns

---

## Architecture

### The Reusability Model

DevPilot is **framework-like** - you install it once and use it across unlimited C# projects:

```
DevPilot (engine)
    ├─→ Your E-Commerce App (uses DevPilot)
    ├─→ Your Healthcare App (uses DevPilot)
    ├─→ Your Game Engine (uses DevPilot)
    └─→ Your Finance App (uses DevPilot)
```

**Each project gets**:
- Same MASAI pipeline quality
- Its own `CLAUDE.md` for domain knowledge
- Custom `.agents/` if needed
- Isolated workspaces

### How It Works

```
1. User runs: devpilot "Add feature X"
2. WorkspaceManager creates isolated workspace in .devpilot/workspaces/{id}/
3. WorkspaceManager copies your project files to workspace
4. [Optional] RagService indexes workspace, retrieves relevant context
5. Pipeline executes 5 agents sequentially:
   - Planner: Creates JSON execution plan
   - Coder: Generates unified diff patch
   - Reviewer: Validates code quality (semantic)
   - Tester: Runs `dotnet test` in workspace
   - Evaluator: Provides quality scores (1-10)
6. Results displayed to user with quality metrics
7. Workspace preserved for inspection at .devpilot/workspaces/{id}/
```

---

## Configuration

### Project-Level Configuration

#### CLAUDE.md (Domain Knowledge)

Create `CLAUDE.md` in your repository root to provide domain-specific context:

```markdown
# My E-Commerce Platform

## Architecture
- User authentication via JWT tokens
- Payments processed through Stripe
- Product catalog uses PostgreSQL

## Coding Standards
- Use decimal for money (not double)
- All API endpoints must validate input
- Follow PCI DSS compliance for payment data

## Common Patterns
[Include code examples]
```

DevPilot reads this and provides it to all agents as context.

---

#### devpilot.json (Build Configuration)

Optional file for customizing file copying:

```json
{
  "folders": ["custom-lib", "shared-utilities"],
  "copyAllFiles": false
}
```

**Properties**:
- `folders`: Additional directories to copy to workspace
- `copyAllFiles`: Override selective copying (default: false)

**Auto-Detection**:
DevPilot automatically copies:
- `.agents/` (custom agent definitions)
- `docs/` (documentation)
- `src/`, `tests/` (common directories)
- All directories containing `.csproj` files

---

### Custom Agents

Override default agents by creating `.agents/` in your repository:

```
your-repo/
├── .agents/
│   ├── planner/
│   │   └── system-prompt.md
│   ├── custom-security-reviewer/
│   │   └── system-prompt.md
│   └── config.json
├── CLAUDE.md
└── src/
```

**Example**: Custom security reviewer for HIPAA compliance:

`.agents/custom-security-reviewer/system-prompt.md`:
```markdown
# Security Reviewer Agent

You are a security-focused code reviewer specializing in HIPAA compliance.

Review all code changes for:
- PHI (Protected Health Information) exposure
- Encryption of data at rest and in transit
- Access control enforcement
- Audit logging completeness

Return JSON with security verdict...
```

---

## Troubleshooting

### "Claude CLI not found"

**Solution**:
```bash
# Install Claude CLI
npm install -g @anthropic-ai/claude-code

# Verify
claude --version
```

---

### "Authentication failed"

**Solution**:
```bash
# Re-authenticate
claude login

# Test
echo "test" | claude --print --model sonnet
```

---

### "RAG disabled: Failed to connect to Ollama"

**Solution**:
```bash
# Check if Ollama is running
curl http://localhost:11434

# If not running
ollama serve

# Verify model
ollama list | grep mxbai-embed-large

# If model not found
ollama pull mxbai-embed-large
```

---

### "Build failed in workspace"

**Possible Causes**:
- Missing `.csproj` files in workspace
- Incorrect project structure detection

**Debug**:
```bash
# Check what was copied to workspace
ls -R .devpilot/workspaces/{pipeline-id}/

# Verify devpilot.json (if exists)
cat devpilot.json
```

---

## Examples

See `examples/` directory for test repositories:

- `simple-calculator/` - Baseline testing
- `multi-project/` - Multiple main projects (Web/API/Worker)
- `monorepo/` - Shared libraries + multiple apps
- `non-standard/` - Non-standard directory names
- `no-docs/` - Minimal repository without CLAUDE.md

Run validation:
```bash
cd examples
cat README.md
```

---

## Contributing

We welcome contributions! See [CONTRIBUTING.md](./CONTRIBUTING.md) for:
- Development setup
- Testing guidelines
- PR checklist
- Code style guide

---

## Validation Status

DevPilot has been validated through:

- ✅ **453 passing tests** (100% pass rate)
- ✅ **Zero build warnings**
- ✅ **Agent implementation audit** - All 5 agents production-ready
- ✅ **Repository structure awareness** - Tested with non-standard layouts
- ✅ **C# best practices** - Code quality scores 8.5+/10

See `AGENT_AUDIT.md` and `RUN_VALIDATION.md` for detailed validation results.

---

## Roadmap

### Completed Recent Work

- ✅ PR #57: Zero-warning build (eliminated 65+ warnings)
- ✅ PR #56: Continuous regression testing with telemetry
- ✅ PR #52: Windows command-line length fix (CLAUDE.md compression)
- ✅ PR #51: RAG integration with Ollama
- ✅ PR #50: Enhanced C# best practices in Coder prompt
- ✅ PR #42: Repository structure awareness

### Immediate Priorities

1. RAG performance validation (measure quality improvements)
2. Diverse repository testing (multi-project, monorepos)
3. Documentation sprint (external contributor guides)

### Medium-Term (1-2 Months)

- Real code review (Reviewer agent with Roslyn analyzers)
- Approval workflow & state persistence
- MCP tool expansion (Coder, Reviewer agents)

See [CLAUDE.md](./CLAUDE.md) for full development roadmap.

---

## License

[Your License Here]

---

## Citation

If you use DevPilot in research, please cite the MASAI framework:

```bibtex
@article{arora2024masai,
  title={MASAI: Modular Architecture for Software-engineering AI Agents},
  author={Arora, Daman and Sonwane, Aditya and Wadhwa, Nalin and Mehrotra, Aayush and Utpala, Saiteja and Bairi, Ramakrishna and Kanade, Aditya and Natarajan, Nagarajan},
  journal={arXiv preprint arXiv:2406.11638},
  year={2024}
}
```

---

**Built with ❤️ using Claude Sonnet 4.5**
