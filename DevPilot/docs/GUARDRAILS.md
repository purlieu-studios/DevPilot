# Development Guardrails

This document defines code quality standards and development guardrails for the DevPilot project. **All developers and AI assistants (including Claude Code) must follow these rules.**

## Lines of Code (LOC) Limits

### Per-Commit Limits
- **Maximum**: 300 lines changed per commit (hard limit - **BLOCKS commit**)
- **Warning threshold**: 200 lines changed per commit
- **Recommended**: Keep commits under 150 lines for optimal reviewability

### Why LOC Limits?
1. **Easier code reviews** - Reviewers can thoroughly examine smaller changes
2. **Faster feedback cycles** - Small PRs get reviewed and merged quicker
3. **Cleaner git history** - Each commit has a single, clear purpose
4. **Easier debugging** - `git bisect` becomes more effective
5. **Reduced merge conflicts** - Smaller changesets = fewer conflicts

### What Counts Toward LOC Limit
- Lines **added** in staged files (deletions don't count)
- All code files (.cs, .js, .ts, .ps1, etc.)
- Configuration changes
- Test files

### What's Excluded from LOC Limit
- Generated files: `*.designer.cs`, `*.g.cs`, `*.g.i.cs`, `*.generated.cs`
- Assembly info files: `*AssemblyInfo.cs`, `*AssemblyAttributes.cs`
- Lock files: `package-lock.json`, `yarn.lock`, `*.lock.json`
- Documentation: `*.md` files
- Configuration: `.editorconfig`, `.gitattributes`, `.gitignore`

### Handling LOC Limit Violations

**If a commit is blocked due to LOC limit:**

1. **Split into multiple commits** (preferred)
   ```bash
   # Stage and commit related files separately
   git add FileGroup1.cs
   git commit -m "feat: implement user validation"

   git add FileGroup2.cs
   git commit -m "feat: add user data models"
   ```

2. **Use `--no-verify` only if absolutely necessary**
   - Large refactoring that can't be split
   - Database migrations
   - Initial repository setup
   - Emergency hotfixes
   ```bash
   git commit --no-verify -m "refactor: large architectural change"
   ```

3. **Request configuration adjustment** if limits are consistently too restrictive

## Code Quality Standards

### .editorconfig Compliance
- **All C# code must comply** with `.editorconfig` rules
- Formatting violations are **build errors**
- Run `dotnet format` before committing
- Configure IDE to format on save

### Naming Conventions (Error Severity)
- **Interfaces**: Must start with `I` (e.g., `IUserService`)
- **Classes/Methods/Properties**: PascalCase
- **Private fields**: `_camelCase` prefix (e.g., `_userName`)
- **Local variables/parameters**: camelCase
- **Async methods**: Must end with `Async` (e.g., `GetUserAsync`)
- **Type parameters**: Must start with `T` (e.g., `TResult`)
- **Constants**: PascalCase (not UPPER_CASE)

### Code Organization
- **Accessibility modifiers required** on all members
- **Braces required** for all control blocks (if, for, while, etc.)
- **Using statements** must be outside namespaces
- **File-scoped namespaces** preferred (C# 10+)
- **One class per file** (with matching filename)

### Modern C# Features
- **Prefer `var`** for local variables when type is obvious
- **Use pattern matching** over type checks and casts
- **Use null-coalescing** operators (`??`, `?.`)
- **Use expression-bodied members** when appropriate
- **Use collection/object initializers** when appropriate

### Code Formatting
- **Max line length**: 120 characters
- **Indentation**: 4 spaces for C#, 2 spaces for JSON/YAML/HTML/JS/TS
- **Line endings**: CRLF for Windows files, LF for shell scripts
- **Final newline**: Required in all text files

## File Management

### File Size Limits
- **Warning threshold**: 5MB per file
- **Large files**: Use Git LFS for files >5MB
- **Binary files**: Must be explicitly marked in `.gitattributes`

### Prohibited Files
- **Secrets/credentials**: No API keys, passwords, or connection strings
- **Build artifacts**: No bin/, obj/, or compiled outputs
- **IDE-specific files**: No .suo, .user files (use .gitignore)
- **Large binaries**: No unnecessary DLLs or executables

## Branch and Workflow Rules

### Branch Naming
- **Required format**: `type/description`
- **Valid types**: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`
- **Description**: Lowercase, hyphen-separated

**Examples:**
```
✅ feat/user-authentication
✅ fix/null-reference-bug
✅ docs/update-readme
✅ refactor/simplify-data-access

❌ feature-branch              # Wrong format
❌ FIX/BUG                     # Uppercase not allowed
❌ feat_user_auth              # Use hyphens, not underscores
```

### Protected Branches
- **Direct pushes blocked** to `main` and `master`
- **All changes via pull requests** - No exceptions
- **PR template required** - Use `.github/PULL_REQUEST_TEMPLATE.md`

### Pull Request Requirements
- **Descriptive title** - Follows Conventional Commits format
- **Summary section** - What and why (not how)
- **Test plan** - How changes were verified
- **Breaking changes** - Explicitly documented if any
- **Related issues** - Link to issues/tickets

## Testing Requirements

### When Tests are Required
- **New features** - Must include unit tests
- **Bug fixes** - Must include regression tests
- **Refactoring** - Existing tests must pass
- **Breaking changes** - Integration tests required

### Test Quality Standards
- **Meaningful names** - Tests describe what they verify
- **Single responsibility** - One assertion per test (generally)
- **Fast execution** - Unit tests complete in milliseconds
- **Deterministic** - Tests produce same result every run
- **Isolated** - No dependencies between tests

## Documentation Requirements

### Code Documentation
- **Public APIs** - XML documentation required (warning severity)
- **Complex logic** - Inline comments explaining "why" (not "what")
- **TODO comments** - Not allowed in committed code
- **Magic numbers** - Replace with named constants

### Project Documentation
- **README.md** - Keep up to date with setup instructions
- **CLAUDE.md** - Update when workflows change
- **Changelogs** - Auto-generated from Conventional Commits

## Performance Considerations

### Build Performance
- **Incremental builds** - Avoid unnecessary rebuilds
- **Parallel compilation** - Enabled by default
- **Analyzer caching** - Don't disable without reason

### Runtime Performance
- **LINQ queries** - Profile before optimizing
- **Async/await** - Use for I/O-bound operations
- **Memory allocations** - Minimize in hot paths
- **Database queries** - Use appropriate indexes

## Exceptions and Overrides

### When to Bypass Guardrails
1. **Emergency hotfixes** - Production down situations
2. **Large migrations** - Database schema changes
3. **Initial setup** - Repository initialization
4. **Third-party code** - Vendored dependencies

### How to Request Exceptions
1. **Document reason** - Explain why exception needed
2. **Get approval** - From tech lead or senior developer
3. **Use `--no-verify`** - Only after approval
4. **Add to commit message** - Note that hooks were bypassed

## Compliance and Enforcement

### Automated Enforcement
- **Git hooks** - Pre-commit, commit-msg, pre-push checks
- **CI/CD pipelines** - Build and test validation
- **Code review** - Manual verification of standards

### Manual Review Checklist
- [ ] Code follows naming conventions
- [ ] No hardcoded secrets or credentials
- [ ] Tests included and passing
- [ ] Documentation updated
- [ ] Commit messages follow Conventional Commits
- [ ] LOC limits respected
- [ ] Breaking changes documented

## References

- **Git Hooks Details**: See `.husky/README.md`
- **Commit Standards**: See `docs/COMMIT_STANDARDS.md`
- **Security Guidelines**: See `docs/SECURITY.md`
- **Editor Configuration**: See `.editorconfig`

## Questions or Issues?

If you have questions about these guardrails or believe they need adjustment:
1. Open a discussion in the repository
2. Propose changes via pull request
3. Tag tech lead or senior developers for review

These guardrails exist to maintain quality and consistency. They should help, not hinder. If they're blocking legitimate work, let's discuss improvements.
