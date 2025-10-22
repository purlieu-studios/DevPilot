# DevPilot Test Suite

This directory contains all tests for the DevPilot project, organized by component.

## Test Categories

Tests are categorized using xUnit `[Trait]` attributes to control which tests run in different environments:

### Unit Tests
**Default behavior** - Tests without special traits are considered unit tests.
- ✅ Run in CI (default)
- ✅ Run locally
- Fast (<1s per test)
- No external dependencies
- No file system operations beyond test fixtures

**Example**:
```csharp
[Fact]
public void Calculator_Add_ReturnsSum()
{
    // Pure unit test
}
```

### Integration Tests
**Trait**: `[Trait("Category", "Integration")]`
- ✅ Run in CI
- ✅ Run locally
- May spawn subprocesses (`dotnet build`, `dotnet test`)
- Can take several seconds to minutes
- Isolated workspace creation/cleanup

**Example**:
```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task TestRunner_ExecutesTests_ReturnsResults()
{
    // Creates temp workspace, runs dotnet test
}
```

**CI Filter**: `Category=Integration&Category!=RequiresClaudeAuth&Category!=LocalOnly`

### Local-Only Integration Tests
**Trait**: `[Trait("Category", "LocalOnly")]`
- ❌ Skip in CI
- ✅ Run locally
- Tests that hang or fail in CI environment but work locally
- Useful for debugging CI-specific issues

**Example**:
```csharp
[Fact]
[Trait("Category", "Integration")]
[Trait("Category", "LocalOnly")]
public async Task TestRunner_MultipleSolutionFiles_ChoosesCorrectOne()
{
    // Hangs in CI (Windows Server 2025), passes locally in 3s
}
```

### Claude API Tests
**Trait**: `[Trait("Category", "RequiresClaudeAuth")]`
- ❌ Skip in CI
- ✅ Run locally (requires `claude login`)
- Long-running (10-15 minutes)
- Requires Claude CLI authentication

**Example**:
```csharp
[Fact]
[Trait("Category", "Integration")]
[Trait("Category", "RequiresClaudeAuth")]
public async Task Pipeline_RealAgents_CompletesSuccessfully()
{
    // Calls real Claude API via claude CLI
}
```

## Running Tests

### Run All Tests (Local)
```bash
dotnet test
```

### Run Only Unit Tests
```bash
dotnet test --filter "Category!=Integration&Category!=EndToEnd"
```

### Run CI-Friendly Integration Tests
```bash
dotnet test --filter "Category=Integration&Category!=RequiresClaudeAuth&Category!=LocalOnly"
```

### Run All Integration Tests (Including Local-Only)
```bash
dotnet test --filter "Category=Integration&Category!=RequiresClaudeAuth"
```

### Run Claude API Tests
```bash
# Requires: claude login
dotnet test --filter "Category=RequiresClaudeAuth"
```

### Run Specific Test Suite
```bash
dotnet test tests/DevPilot.Orchestrator.Tests
dotnet test tests/DevPilot.Agents.Tests
dotnet test tests/DevPilot.Core.Tests
```

## CI Pipeline Behavior

The GitHub Actions CI pipeline runs three jobs:

1. **Unit Tests** - Fast, no external dependencies
   - Filter: `Category!=Integration&Category!=EndToEnd`
   - ~30 seconds

2. **Integration Tests** - Subprocesses, workspace isolation
   - Filter: `Category=Integration&Category!=RequiresClaudeAuth&Category!=LocalOnly`
   - ~5-10 minutes

3. **Build Validation** - Ensures Release build succeeds
   - No tests, just `dotnet build --configuration Release`

## When to Use Each Category

| Scenario | Category | CI | Duration |
|----------|----------|-----|----------|
| Pure logic, no I/O | (none) | ✅ | <1s |
| File operations, temp directories | `Integration` | ✅ | <10s |
| Subprocess spawning (`dotnet test`) | `Integration` | ✅ | 1-5min |
| **Hangs in CI, works locally** | `Integration` + `LocalOnly` | ❌ | Varies |
| Real Claude API calls | `Integration` + `RequiresClaudeAuth` | ❌ | 10-15min |

## Adding a New Test Category

1. Add trait to test:
   ```csharp
   [Trait("Category", "YourCategory")]
   ```

2. Update CI filter in `.github/workflows/ci-test-suite.yml`:
   ```yaml
   --filter "Category=Integration&Category!=YourCategory"
   ```

3. Document it in this README

## Troubleshooting

### Test Hangs in CI But Passes Locally
→ Mark it `[Trait("Category", "LocalOnly")]` and investigate CI environment differences

### Test Needs Claude Authentication
→ Mark it `[Trait("Category", "RequiresClaudeAuth")]` and run manually after `claude login`

### Test Fails Only in Release Configuration
→ Check for Debug-only dependencies or conditional compilation (`#if DEBUG`)

### Test Times Out in CI
→ Consider if it's truly an integration test or if it can be refactored to unit test with mocks
