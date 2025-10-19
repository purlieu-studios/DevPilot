# DevPilot Example Repositories

This directory contains test repositories for validating DevPilot across diverse scenarios.

## Available Examples

### 1. simple-calculator
**Purpose**: Baseline RAG validation
**Structure**: Standard `Calculator/` and `Calculator.Tests/`
**Features**: CLAUDE.md with detailed coding standards

**Test Command**:
```bash
cd simple-calculator
dotnet run --project ../../src/DevPilot.Console -- "Add Multiply method"
```

---

### 2. multi-project
**Purpose**: Test multi-project solution handling
**Structure**:
- `Web/` - ASP.NET Core MVC
- `API/` - Web API project
- `Worker/` - Background worker
- `Shared/` - Common library

**Test Command**:
```bash
cd multi-project
dotnet run --project ../../src/DevPilot.Console -- "Add logging to UserService in API project"
```

**Success Criteria**: DevPilot correctly identifies API project and doesn't modify Web/Worker/Shared

---

### 3. monorepo
**Purpose**: Test monorepo structure with shared libraries
**Structure**:
- `shared/common-lib/` - Shared utilities
- `apps/web-app/` - Web application
- `apps/mobile-app/` - Mobile backend

**Test Command**:
```bash
cd monorepo
dotnet run --project ../../src/DevPilot.Console -- "Add string validation to common-lib"
```

**Success Criteria**: Changes only affect `shared/common-lib/`, not apps

---

### 4. non-standard
**Purpose**: Test non-standard directory naming
**Structure**:
- `source/` instead of `src/`
- `unit-tests/` instead of `tests/`
- `documentation/` instead of `docs/`

**Test Command**:
```bash
cd non-standard
dotnet run --project ../../src/DevPilot.Console -- "Add unit test for Calculator"
```

**Success Criteria**: Repository structure awareness detects non-standard paths

---

### 5. no-docs
**Purpose**: Test graceful degradation without CLAUDE.md
**Structure**: Minimal - just `Calculator/` and `Calculator.Tests/`
**Features**: **NO** CLAUDE.md, **NO** documentation

**Test Command**:
```bash
cd no-docs
dotnet run --project ../../src/DevPilot.Console -- "Add Multiply method"
```

**Success Criteria**: Pipeline doesn't crash, uses reasonable defaults

---

## Validation Results

After running tests, results are saved to:
- `validation-results/baseline-no-rag/` - Baseline without RAG
- `validation-results/rag-enabled/` - RAG-enabled results

See `../RUN_VALIDATION.md` for detailed testing instructions.

---

## Quick Setup

To build all examples:
```bash
cd C:\DevPilot\DevPilot\examples

# Build each solution
dotnet build simple-calculator/Calculator.sln
dotnet build multi-project/MultiProject.sln
dotnet build monorepo/Monorepo.sln
dotnet build non-standard/NonStandard.sln
dotnet build no-docs/NoDocsRepo.sln
```

---

## Notes

- All examples use .NET 8.0
- xUnit is the test framework
- Nullable reference types enabled
- Each example is self-contained and buildable
