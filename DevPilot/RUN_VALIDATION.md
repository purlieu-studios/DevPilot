# DevPilot Validation Execution Guide

**Purpose**: Step-by-step instructions to validate DevPilot across diverse scenarios
**Estimated Time**: 2-3 hours (includes pipeline execution + analysis)
**Prerequisites**: Ollama running, DevPilot built

---

## Pre-Flight Check

Before starting validation, verify:

```bash
# 1. Ollama is running
curl http://localhost:11434
# Expected: "Ollama is running"

# 2. mxbai-embed-large model is available
ollama list | grep mxbai
# Expected: mxbai-embed-large:latest

# 3. DevPilot builds
cd C:\DevPilot\DevPilot
dotnet build --no-restore
# Expected: Build succeeded, 0 Warning(s), 0 Error(s)

# 4. Verify simple-calculator repository
cd examples/simple-calculator
dotnet build && dotnet test
# Expected: 4/4 tests passed
```

---

## Phase 1: RAG Validation (60-90 min)

### Test 1.1: Baseline (No RAG)

**Objective**: Establish baseline quality scores without RAG

**Steps**:
```bash
cd C:\DevPilot\DevPilot\examples\simple-calculator

# Run DevPilot WITHOUT RAG
dotnet run --project ../../src/DevPilot.Console --no-build -- "Add a Multiply method to the Calculator class with comprehensive tests"
```

**Duration**: ~5 minutes

**Metrics to Record**:
1. Open the final evaluation output (JSON)
2. Record these scores in `RAG_VALIDATION.md`:
   - Plan Quality: ____ / 10
   - Code Quality: ____ / 10
   - Test Coverage: ____ / 10
   - Documentation: ____ / 10
   - Overall Score: ____ / 10

3. Check workspace:
   - File: `.devpilot/workspaces/{pipeline-id}/Calculator/Calculator.cs`
   - Verify Multiply method added: YES / NO
   - Test pass rate: ____ / ____ tests passed

4. Code quality observations:
   - Follows CLAUDE.md patterns? YES / NO
   - Uses `double` type? YES / NO
   - XML documentation complete? YES / NO
   - Expression-bodied member? YES / NO
   - Tests use precision: 5? YES / NO

**Save Artifacts**:
```bash
# Copy workspace for later comparison
cp -r .devpilot/workspaces/{pipeline-id} ../validation-results/baseline-no-rag/
```

---

### Test 1.2: RAG-Enabled

**Objective**: Measure RAG impact on code quality

**Steps**:
```bash
cd C:\DevPilot\DevPilot\examples\simple-calculator

# Run DevPilot WITH RAG
dotnet run --project ../../src/DevPilot.Console --no-build -- --enable-rag "Add a Divide method to the Calculator class with comprehensive tests"
```

**Duration**: ~5-7 minutes (includes RAG indexing)

**Metrics to Record**:
Same as Test 1.1, plus:
- RAG indexing time: ____ seconds
- Number of chunks indexed: ____
- RAG chunks retrieved: ____ (check logs)
- Top chunk relevance score: ____ (check logs)
- CLAUDE.md included in context? YES / NO

**Save Artifacts**:
```bash
cp -r .devpilot/workspaces/{pipeline-id} ../validation-results/rag-enabled/
```

---

### Test 1.3: Analysis

**Compare Results**:
```bash
cd C:\DevPilot\DevPilot\examples\validation-results

# Compare generated code
code --diff baseline-no-rag/Calculator/Calculator.cs rag-enabled/Calculator/Calculator.cs

# Compare tests
code --diff baseline-no-rag/Calculator.Tests/CalculatorTests.cs rag-enabled/Calculator.Tests/CalculatorTests.cs
```

**Questions to Answer** (document in `RAG_VALIDATION.md`):
1. **Quality Delta**: Did RAG improve overall score? By how much?
2. **Pattern Adherence**: Did RAG version follow CLAUDE.md patterns better?
3. **Test Quality**: Did RAG version generate more comprehensive tests?
4. **Documentation**: Did RAG version produce better XML docs?
5. **Evidence**: Specific examples showing RAG influence (e.g., similar phrasing to CLAUDE.md)

**Verdict**:
- [ ] RAG significantly improves quality (+1.0 or more points)
- [ ] RAG slightly improves quality (+0.3 to +0.9 points)
- [ ] RAG has no measurable impact (< ±0.3 points)
- [ ] RAG decreases quality (negative delta)

**Recommendation**:
Based on results, when should developers use `--enable-rag`?

---

## Phase 2: Diverse Repository Testing (120-150 min)

### Test 2.1: Multi-Project Solution

**Repository**: `examples/multi-project/`

**Test Command**:
```bash
cd C:\DevPilot\DevPilot\examples\multi-project
dotnet run --project ../../src/DevPilot.Console --no-build -- "Add logging to UserService in the API project"
```

**Success Criteria**:
- [ ] Planner identifies API project correctly
- [ ] File path uses `API/Services/UserService.cs` (not Web/ or Worker/)
- [ ] No changes to other projects (Web/, Worker/, Shared/)
- [ ] Overall quality ≥ 8.0/10

**Record**:
- Pass/Fail: ____
- Quality Score: ____ / 10
- Issues: ____

---

### Test 2.2: Monorepo Structure

**Repository**: `examples/monorepo/`

**Test Command**:
```bash
cd C:\DevPilot\DevPilot\examples\monorepo
dotnet run --project ../../src/DevPilot.Console --no-build -- "Add string validation helper to common-lib"
```

**Success Criteria**:
- [ ] File path uses `shared/common-lib/StringHelpers.cs`
- [ ] Tests added to `shared/common-lib.Tests/`
- [ ] No modifications to `apps/web-app/` or `apps/mobile-app/`
- [ ] Overall quality ≥ 8.0/10

**Record**:
- Pass/Fail: ____
- Quality Score: ____ / 10
- Issues: ____

---

### Test 2.3: Non-Standard Naming

**Repository**: `examples/non-standard/`

**Test Command**:
```bash
cd C:\DevPilot\DevPilot\examples\non-standard
dotnet run --project ../../src/DevPilot.Console --no-build -- "Add unit test for Calculator.Add method"
```

**Success Criteria**:
- [ ] Repository structure detected correctly
- [ ] Test path uses `unit-tests/CalculatorTests.cs` (not `tests/`)
- [ ] Source path uses `source/Calculator.cs` (not `src/`)
- [ ] Pipeline completes without errors
- [ ] Overall quality ≥ 8.0/10

**Record**:
- Pass/Fail: ____
- Quality Score: ____ / 10
- Issues: ____

---

### Test 2.4: No CLAUDE.md (Minimal Documentation)

**Repository**: `examples/no-docs/`

**Test Command**:
```bash
cd C:\DevPilot\DevPilot\examples\no-docs
dotnet run --project ../../src/DevPilot.Console --no-build -- "Add Multiply method to Calculator class"
```

**Success Criteria**:
- [ ] Pipeline doesn't crash
- [ ] Reasonable defaults used (double type, expression-bodied members)
- [ ] Quality score ≥ 7.0/10 (acceptable without docs)
- [ ] Tests follow standard xUnit patterns

**Record**:
- Pass/Fail: ____
- Quality Score: ____ / 10
- Issues: ____
- Notes on what defaults were assumed: ____

---

### Test 2.5: Dogfooding (DevPilot on Itself)

**Repository**: `C:\DevPilot\DevPilot` (DevPilot itself)

**Test Command**:
```bash
cd C:\DevPilot\DevPilot
dotnet run --project src/DevPilot.Console --no-build -- --enable-rag "Add input validation to ensure MCP tool names don't contain invalid characters"
```

**Success Criteria**:
- [ ] RAG retrieves existing MCP code examples
- [ ] Generated code follows DevPilot's C# style
- [ ] Respects DevPilot's architecture (e.g., uses proper namespace)
- [ ] Overall quality ≥ 8.5/10

**Record**:
- Pass/Fail: ____
- Quality Score: ____ / 10
- RAG Context Quality (did it find relevant code?): ____
- Issues: ____

---

## Phase 3: Results Documentation

### Create TEST_MATRIX.md

After all tests complete, document results:

```markdown
# DevPilot Test Matrix - Validation Results

**Date**: [DATE]
**DevPilot Version**: [COMMIT HASH]
**Ollama Model**: mxbai-embed-large
**Test Duration**: [TOTAL TIME]

## Summary

| Test Scenario | Repository | Pass/Fail | Quality Score | Key Issues |
|---------------|------------|-----------|---------------|------------|
| Baseline (No RAG) | simple-calculator | ✅/❌ | X.X/10 | ... |
| RAG Enabled | simple-calculator | ✅/❌ | X.X/10 | ... |
| Multi-Project | multi-project | ✅/❌ | X.X/10 | ... |
| Monorepo | monorepo | ✅/❌ | X.X/10 | ... |
| Non-Standard | non-standard | ✅/❌ | X.X/10 | ... |
| No CLAUDE.md | no-docs | ✅/❌ | X.X/10 | ... |
| Dogfooding | DevPilot itself | ✅/❌ | X.X/10 | ... |

**Overall Success Rate**: X/7 (XX%)

## RAG Effectiveness

| Metric | Baseline | RAG-Enabled | Delta |
|--------|----------|-------------|-------|
| Overall Score | X.X/10 | X.X/10 | ±X.X |
| Code Quality | X.X/10 | X.X/10 | ±X.X |
| Documentation | X.X/10 | X.X/10 | ±X.X |
| Test Coverage | X.X/10 | X.X/10 | ±X.X |

**Verdict**: [RAG significantly improves/slightly improves/has no impact on/decreases quality]

## Edge Cases Discovered

1. **[Issue Title]**: [Description]
   - Reproduction: [Steps]
   - Impact: [Severity]
   - Workaround: [If available]

## Recommendations

1. **When to use RAG**: [Based on findings]
2. **Repository structures validated**: [List]
3. **Known limitations**: [List]

## Next Steps

- [ ] Create GitHub issues for bugs found
- [ ] Update CLAUDE.md with validation results
- [ ] Document known limitations in README
```

---

## Tips for Efficient Validation

1. **Run tests in parallel terminals** - Start multiple tests simultaneously if you have resources
2. **Monitor logs** - Keep an eye on `.devpilot/logs/` for RAG context and errors
3. **Save workspaces** - Always copy workspaces before next test for comparison
4. **Document as you go** - Don't wait until end to write up findings
5. **Take breaks** - Each pipeline run is 5 minutes, perfect for coffee

---

## Troubleshooting

**"Pipeline timed out"**:
- Increase timeout in ClaudeCliClient.cs (default: 5 minutes)

**"RAG indexing failed"**:
- Check Ollama is running: `curl http://localhost:11434`
- Verify model: `ollama list | grep mxbai`

**"Build failed in workspace"**:
- Check if .csproj files were copied correctly
- Verify project structure was analyzed properly

**"Claude CLI not found"**:
- Verify authentication: `claude login`
- Check PATH: `where claude` (Windows) or `which claude` (Unix)

---

*Estimated completion time: 2-3 hours*
*This guide ensures reproducible, systematic validation of all DevPilot features.*
