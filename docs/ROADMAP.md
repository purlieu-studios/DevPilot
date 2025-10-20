# DevPilot Roadmap

**Last Updated**: 2025-10-20
**Vision**: Transform DevPilot from a CLI tool into a comprehensive WPF Project Management system with AI-powered Epic breakdown and task implementation.

---

## Long-Term Vision: Project Management UI

See `project_architecture.drawio.pdf` for detailed workflow diagram.

### The Big Picture

DevPilot will evolve into a visual project management application where users can:
1. **Manage Projects** - Link to target repositories, CLAUDE.md, docs
2. **Create Epics** - Large features (e.g., "Implement Cooking System")
3. **Auto-Generate Tasks** - Use MASAI/RAG to break Epics into implementable tasks
4. **Execute Tasks** - Run DevPilot pipeline on each task with real-time monitoring
5. **Track Quality** - Dashboard showing quality scores, trends, and metrics

### Two-Level MASAI Integration

**Level 1: Epic → Tasks** (Breakdown)
- Analyzes WPF app's project metadata
- Uses CLAUDE.md, docs, RAG from target repository
- Planner agent generates structured task list
- User reviews/edits before committing to database

**Level 2: Task → Implementation** (Execution)
- Executes full DevPilot pipeline on target repository
- Uses repository-specific RAG/context
- Real-time progress monitoring in WPF UI
- Quality scores and file changes tracked

---

## GitHub Issues Overview

### Core Infrastructure (Foundations)

#### #71: Real Test Execution in Tester Agent
- **Priority**: Medium
- **Effort**: 4-6 hours
- **Status**: Open
- **Description**: Execute `dotnet test`, parse results, collect coverage
- **Why**: Provides actual quality metrics for implemented tasks

#### #72: Real Code Review with Roslyn
- **Priority**: Medium
- **Effort**: 8-12 hours
- **Status**: Open
- **Description**: Integrate Roslyn analyzers for static analysis
- **Why**: Improves review quality, catches issues early

#### #73: Approval Workflow & State Persistence
- **Priority**: High
- **Effort**: 6-8 hours
- **Status**: Open
- **Description**: Save pipeline state, enable resume/approve/reject commands
- **Why**: Critical for human-in-the-loop workflow in PM UI

#### #74: MCP Tool Expansion to Coder/Reviewer
- **Priority**: Medium
- **Effort**: 6-8 hours
- **Status**: Open
- **Description**: Extend structured output to all agents
- **Why**: Improves reliability, enables better UI integration

---

### WPF Project Management System (Ultimate Vision)

#### #76: Full WPF Project Management UI ⭐
- **Priority**: HIGH (Ultimate Goal)
- **Effort**: 6-8 weeks
- **Status**: Open
- **Description**: Complete PM system with Projects, Epics, Tasks, and MASAI integration
- **Dependencies**: #77 (library API), #78 (Epic breakdown), #79 (database)
- **Key Features**:
  - Home screen with project list
  - Project view with Epic/Task grid
  - Epic breakdown using MASAI/RAG
  - Task implementation with real-time monitoring
  - Quality trend dashboards

**Phase Breakdown**:
1. **Phase 1**: Core UI framework (2-3 weeks)
   - MVVM structure
   - Database setup (#79)
   - Basic CRUD for Projects/Epics/Tasks

2. **Phase 2**: MASAI Integration - Epic Breakdown (1-2 weeks)
   - Project metadata discovery (#78)
   - Context gathering (CLAUDE.md, docs, RAG)
   - Task generation from Planner agent

3. **Phase 3**: MASAI Integration - Task Implementation (1-2 weeks)
   - DevPilot pipeline execution (#77)
   - Real-time monitoring (#75)
   - Quality metrics tracking

4. **Phase 4**: Advanced Features (2-3 weeks)
   - Task dependencies
   - Sprint planning
   - Quality trends
   - Bulk operations

#### #77: Library-Friendly Refactoring (Prerequisite for #76)
- **Priority**: HIGH (Blocker for #76)
- **Effort**: 3-4 days
- **Status**: Open
- **Description**: Extract Pipeline logic from CLI, add IProgress callbacks
- **Why**: Makes DevPilot embeddable in WPF (and other UIs)

**Key Changes**:
```csharp
// Before: CLI-only
var pipeline = new Pipeline(workingDir);
await pipeline.RunAsync(request); // Writes to console

// After: Library-friendly
var pipeline = new Pipeline(workingDir);
var progress = new Progress<PipelineProgress>(p => UpdateWpfUI(p));
await pipeline.RunAsync(request, progress: progress);
```

#### #78: Epic Breakdown Service (Core Feature for #76)
- **Priority**: HIGH (Critical for #76)
- **Effort**: 1-2 weeks
- **Status**: Open
- **Description**: MASAI-powered service that decomposes Epics into Tasks
- **Dependencies**: #77 (need reusable Planner agent)
- **Example**:
  - Input: "Implement Cooking System" (Epic description)
  - Output: 8 structured tasks with dependencies, LOC estimates, target files

#### #79: WPF Database Layer (Foundation for #76)
- **Priority**: HIGH (Blocker for #76)
- **Effort**: 3-4 days
- **Status**: Open
- **Description**: SQLite + EF Core for Projects/Epics/Tasks/Executions
- **Tech Stack**: Entity Framework Core 8.0, SQLite, Repository pattern

**Schema**:
- Projects → Epics → Tasks → PipelineExecutions → FileChanges

#### #75: Real-Time Pipeline Monitoring (Subset of #76)
- **Priority**: Medium
- **Effort**: 16-20 hours
- **Status**: Open
- **Description**: WPF control for live pipeline progress
- **Note**: Originally standalone, now integrated into #76

---

## Implementation Roadmap

### Phase 1: Foundation (Week 1-2) 🏗️

**Goal**: Make DevPilot library-friendly and set up WPF infrastructure

1. ✅ **Complete validation testing** (2/7 scenarios done)
   - Results: 9.6-9.7/10 quality scores
   - RAG provides +0.5 plan quality improvement

2. **#77: Library-friendly refactoring** (3-4 days)
   - Extract IProgress callbacks
   - Remove Spectre.Console from Orchestrator
   - Create ConsolePipelineRenderer

3. **#79: Database layer** (3-4 days)
   - Set up SQLite + EF Core
   - Create entity models
   - Implement repository pattern

**Deliverable**: DevPilot can be embedded in WPF, database ready

---

### Phase 2: Core PM UI (Week 3-5) 📊

**Goal**: Build basic Project/Epic/Task management with manual breakdown

1. **#76 Phase 1: Core UI** (2-3 weeks)
   - Home screen (project list)
   - Project view (Epic/Task grid)
   - Manual CRUD for Epics and Tasks
   - Basic navigation

**Deliverable**: Functional PM UI with manual task creation

---

### Phase 3: AI-Powered Breakdown (Week 6-7) 🤖

**Goal**: Integrate MASAI for automatic Epic → Task decomposition

1. **#78: Epic breakdown service** (1-2 weeks)
   - Project context analysis
   - RAG integration
   - Planner agent execution
   - Task generation from plan

2. **#76 Phase 2: MASAI Level 1** (integrated with #78)
   - "Generate Tasks" button on Epics
   - Progress indicator during generation
   - Review/edit generated tasks before saving

**Deliverable**: Auto-generate tasks from Epic descriptions

---

### Phase 4: Task Implementation (Week 8-9) ⚙️

**Goal**: Execute DevPilot pipeline on tasks with live monitoring

1. **#76 Phase 3: MASAI Level 2** (1-2 weeks)
   - "Implement Task" button
   - Real-time pipeline progress (#75 embedded)
   - Quality score tracking
   - File change visualization

**Deliverable**: End-to-end workflow (Epic → Tasks → Implementation)

---

### Phase 5: Quality & Polish (Week 10-12) ✨

**Goal**: Advanced features and production readiness

1. **#76 Phase 4: Advanced features** (2-3 weeks)
   - Task dependencies
   - Sprint planning
   - Quality trend dashboards
   - Bulk task operations

2. **#71: Real test execution** (4-6 hours)
   - Accurate quality metrics

3. **#72: Real code review** (8-12 hours)
   - Roslyn analyzer integration

4. **#73: Approval workflow** (6-8 hours)
   - State persistence
   - Resume/approve/reject commands

**Deliverable**: Production-ready PM system

---

## Success Metrics

### Development Velocity
- Target: Complete Phase 1 in 2 weeks
- Track: Days spent per issue vs. estimate

### Quality Scores
- Target: Maintain 9.0+ overall quality across all tasks
- Track: Average quality score per implemented task

### User Adoption (Post-Launch)
- Target: 10+ repositories using DevPilot PM UI
- Target: 50+ Epics successfully decomposed
- Target: 200+ Tasks implemented via pipeline

### RAG Effectiveness
- Baseline: 9.6/10 without RAG
- With RAG: 9.7/10 (+0.5 plan quality)
- Target: Maintain or improve with more data

---

## Technical Architecture

### Repository Structure
```
DevPilot/
├── src/
│   ├── DevPilot.Console/          # CLI entry point
│   ├── DevPilot.Orchestrator/     # Core pipeline (library)
│   ├── DevPilot.Agents/           # Agent execution
│   ├── DevPilot.Core/             # Shared models
│   ├── DevPilot.Diagnostics/      # Quality tools
│   ├── DevPilot.UI.Wpf/           # WPF Project Management UI (NEW)
│   │   ├── ViewModels/            # MVVM view models
│   │   ├── Views/                 # WPF windows/controls
│   │   ├── Services/              # Epic breakdown, etc.
│   │   ├── Data/                  # EF Core DbContext
│   │   └── Models/                # Entity models
│   └── DevPilot.UI.Console/       # Spectre.Console rendering (NEW)
├── tests/
│   └── DevPilot.UI.Wpf.Tests/     # WPF UI tests (NEW)
├── docs/
│   ├── ROADMAP.md                 # This file
│   ├── ARCHITECTURE.md            # System design
│   └── project_architecture.drawio.pdf  # Workflow diagram
└── examples/
    └── validation-results/
        └── 2025-10-20-initial-validation.md
```

### Technology Stack

**Core Pipeline**:
- .NET 8.0
- Claude CLI (Anthropic)
- Ollama (RAG embeddings)

**WPF UI** (NEW):
- WPF (.NET 8.0)
- CommunityToolkit.Mvvm
- Entity Framework Core 8.0
- SQLite
- ModernWpfUI or MaterialDesignInXaml
- LiveCharts2 (quality trends)

---

## Risk Assessment

### Technical Risks

1. **Prompt Engineering Complexity** (Epic Breakdown)
   - Risk: Generated tasks may be too vague or miss dependencies
   - Mitigation: User review/edit step, iterative prompt refinement
   - Likelihood: Medium | Impact: High

2. **Performance** (RAG Indexing)
   - Risk: Indexing large codebases may be slow
   - Mitigation: Background indexing, caching, incremental updates
   - Likelihood: Medium | Impact: Medium

3. **Integration Complexity** (Library Refactoring)
   - Risk: Extracting CLI logic may introduce bugs
   - Mitigation: Comprehensive unit tests, maintain CLI parity
   - Likelihood: Low | Impact: Medium

### Scope Risks

1. **Feature Creep**
   - Risk: WPF UI scope expands beyond initial plan
   - Mitigation: Stick to phased approach, defer non-critical features
   - Likelihood: High | Impact: High

2. **Timeline Slip**
   - Risk: 12-week estimate may be optimistic
   - Mitigation: Focus on MVP first (Phases 1-3), Phase 4 optional
   - Likelihood: Medium | Impact: Medium

---

## Decision Log

### 2025-10-20: WPF Over Web UI
**Decision**: Build WPF desktop app instead of Blazor/Web UI
**Rationale**:
- Tight integration with local DevPilot CLI
- No deployment complexity (runs locally)
- Better for developer workflows
- Rich desktop UX (file system access, etc.)

**Trade-offs**:
- Windows-only initially (could add Avalonia later for cross-platform)
- No web-based collaboration features

---

## Future Enhancements (Post-MVP)

### Near-Term (3-6 months)
- **Multi-Language Support**: Extend beyond C# (Python, TypeScript, Java)
- **GitHub Integration**: Create PRs directly from implemented tasks
- **Team Collaboration**: Shared project database (SQL Server, PostgreSQL)
- **CI/CD Integration**: Trigger pipelines from CI systems

### Long-Term (6-12 months)
- **Cloud Hosting**: DevPilot as a Service (centralized execution)
- **VS Code Extension**: Lightweight UI in VS Code
- **Custom Agent Marketplace**: Share/download custom agents
- **Analytics Dashboard**: Team velocity, quality trends, cost tracking

---

## Related Documents

- [ARCHITECTURE.md](./ARCHITECTURE.md) - System design and MASAI principles
- [RAG.md](./RAG.md) - RAG implementation details
- [LESSONS_LEARNED.md](./LESSONS_LEARNED.md) - Testing insights
- [CONTRIBUTING.md](../CONTRIBUTING.md) - Development workflow
- [project_architecture.drawio.pdf](../../Users/purli/Documents/project_architecture.drawio.pdf) - WPF workflow diagram

---

## Get Involved

**Immediate Priorities**:
1. Complete example repository implementations (multi-project, monorepo, non-standard, no-docs)
2. Finish validation testing (5 scenarios remaining)
3. Start Phase 1: Library refactoring (#77) and database setup (#79)

**How to Contribute**: See [CONTRIBUTING.md](../CONTRIBUTING.md)

**Questions/Feedback**: Open a GitHub issue or discussion
