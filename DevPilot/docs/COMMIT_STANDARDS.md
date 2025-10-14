# Commit Standards

This document defines commit message standards and Git workflow conventions for the DevPilot project. **All developers and AI assistants (including Claude Code) must follow these rules.**

## Conventional Commits

### Required Format

All commit messages **must** follow the Conventional Commits specification:

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

### Commit Types

| Type | Description | When to Use | Examples |
|------|-------------|-------------|----------|
| `feat` | New feature | Adding new functionality | `feat: add user authentication` |
| `fix` | Bug fix | Fixing a bug | `fix: resolve null reference exception` |
| `docs` | Documentation | Documentation only changes | `docs: update README with setup steps` |
| `style` | Code style | Formatting, whitespace, etc. | `style: apply consistent indentation` |
| `refactor` | Code refactoring | Code restructuring without behavior change | `refactor: simplify data access layer` |
| `perf` | Performance | Performance improvements | `perf: optimize database queries` |
| `test` | Tests | Adding or updating tests | `test: add unit tests for user service` |
| `build` | Build system | Build configuration changes | `build: update NuGet packages` |
| `ci` | CI/CD | CI/CD pipeline changes | `ci: add automated deployment` |
| `chore` | Maintenance | Other changes (dependencies, configs) | `chore: update .gitignore` |
| `revert` | Revert | Reverting previous commit | `revert: revert commit abc123` |

### Subject Line Rules

**Maximum length**: 50 characters (warning threshold)

**Format requirements:**
- Start with lowercase letter (after type and scope)
- Use imperative mood: "add" not "added" or "adds"
- No period at the end
- Be concise but descriptive

**Valid examples:**
```
✅ feat: add email validation
✅ fix(api): handle timeout errors
✅ docs: update installation guide
✅ refactor(service): simplify error handling

❌ feat: Added email validation          # Not imperative
❌ fix: Fix bug.                          # Has period, not descriptive
❌ Fixed the null reference bug           # Missing type
❌ feat: Add email validation feature     # Redundant "feature"
```

### Scope (Optional)

Scopes indicate the part of the codebase affected:

**Common scopes:**
- `api` - API layer changes
- `ui` - User interface changes
- `db` - Database changes
- `auth` - Authentication/authorization
- `config` - Configuration changes
- `test` - Test-related changes

**Examples:**
```bash
feat(auth): add JWT token validation
fix(api): resolve CORS configuration
docs(readme): add deployment instructions
```

### Body (Optional)

The body provides additional context:

**When to include body:**
- Complex changes requiring explanation
- Breaking changes (required)
- Multiple related changes
- Reasoning behind the change

**Format:**
- Blank line after subject
- Wrap at 72 characters
- Explain "what" and "why", not "how"
- Use bullet points for multiple items

**Example:**
```
feat: add user profile caching

Implement Redis caching for user profiles to reduce database load.
This improves response times from ~200ms to ~20ms for profile requests.

- Cache expires after 15 minutes
- Invalidate on profile updates
- Fallback to database if cache unavailable
```

### Footer (Optional)

Footers reference issues, breaking changes, or co-authors:

**Breaking changes:**
```
feat: change authentication API

BREAKING CHANGE: Auth endpoint moved from /auth to /api/v2/auth
Clients must update their API calls to use the new endpoint.
```

**Issue references:**
```
fix: resolve memory leak in background service

Fixes #123
Closes #456
```

**Co-authors:**
```
feat: implement payment processing

Co-Authored-By: John Doe <john@example.com>
Co-Authored-By: Jane Smith <jane@example.com>
```

## Commit Message Validation

### Automated Checks

The `commit-msg` Git hook validates:
- ✅ Conventional Commits format
- ✅ Valid commit type
- ✅ Subject line length (≤50 chars)
- ✅ Imperative mood
- ✅ No blocked words (WIP, TODO, FIXME, temp)

### Blocked Words

These words are **prohibited** in commit messages:

| Blocked Word | Why | Alternative |
|--------------|-----|-------------|
| `WIP` | Work-in-progress commits shouldn't be pushed | Complete the work or use a draft PR |
| `TODO` | Indicates incomplete work | Finish the work before committing |
| `FIXME` | Code needs fixing | Fix it before committing |
| `temp` / `tmp` | Temporary changes shouldn't be committed | Make permanent changes |

**If you need to commit incomplete work:**
- Use a draft pull request
- Create a feature branch
- Use GitHub issues to track remaining work
- Don't commit placeholder code

## Commit Best Practices

### Atomic Commits

**One commit = One logical change**

✅ **Good (atomic commits):**
```bash
git commit -m "feat: add user validation"
git commit -m "test: add user validation tests"
git commit -m "docs: document user validation API"
```

❌ **Bad (mixed changes):**
```bash
git commit -m "feat: add user validation, fix bug, update docs"
```

### Commit Frequency

**Commit often, push when stable**

- Commit after each logical change
- Don't wait until end of day
- Push when tests pass
- Push before switching branches

### Meaningful Messages

**Good commit messages:**
```
✅ feat: add email validation with regex pattern
✅ fix: resolve race condition in cache invalidation
✅ refactor: extract duplicate validation logic
✅ perf: optimize user query with indexed lookup
```

**Bad commit messages:**
```
❌ fix: fix bug
❌ update: update code
❌ changes
❌ WIP
```

## Git Workflow

### Feature Branch Workflow

1. **Create feature branch** from `main`
   ```bash
   git checkout main
   git pull
   git checkout -b feat/user-authentication
   ```

2. **Make changes and commit** following standards
   ```bash
   git add .
   git commit -m "feat: add user authentication"
   ```

3. **Push to remote**
   ```bash
   git push -u origin feat/user-authentication
   ```

4. **Create pull request** on GitHub

5. **Address review feedback** with new commits
   ```bash
   git commit -m "fix: address PR feedback on validation"
   ```

6. **Merge after approval** (squash or merge commit)

### Commit History Management

**When to amend:**
```bash
# Fix typo in last commit message
git commit --amend -m "feat: add user authentication"

# Add forgotten file to last commit
git add forgotten-file.cs
git commit --amend --no-edit
```

**⚠️ Never amend after pushing** (unless working alone on branch)

**When to rebase:**
- Cleaning up commit history before PR
- Resolving merge conflicts
- Squashing related commits

```bash
# Interactive rebase last 3 commits
git rebase -i HEAD~3
```

**⚠️ Never rebase public branches** (main, master, shared branches)

### Merge Strategies

**Squash and merge** (recommended for features)
- Combines all commits into one
- Keeps main branch history clean
- Good for feature branches

**Merge commit** (for release branches)
- Preserves all commits
- Shows branch history
- Good for tracking releases

**Rebase and merge** (for simple changes)
- Linear history
- No merge commits
- Good for bug fixes

## Commit Templates

### Using Git Commit Template

Set up the provided commit template:

```bash
git config commit.template .github/.gitmessage
```

This loads the Conventional Commits template when you run `git commit`.

### Template Content

The `.github/.gitmessage` template:

```
# <type>[optional scope]: <description>
# |<----  Max 50 characters  ---->|

# [optional body]
# |<----   Max 72 characters   ---->|

# [optional footer(s)]

# --- COMMIT TYPES ---
# feat:     New feature
# fix:      Bug fix
# docs:     Documentation only
# style:    Code style (formatting, etc.)
# refactor: Code restructuring
# perf:     Performance improvement
# test:     Adding/updating tests
# build:    Build system changes
# ci:       CI/CD changes
# chore:    Maintenance tasks
#
# --- GUIDELINES ---
# - Use imperative mood: "add" not "added"
# - No period at end of subject line
# - Separate subject from body with blank line
# - Limit subject to 50 characters
# - Wrap body at 72 characters
# - Explain what and why, not how
```

## Examples

### Simple Feature

```
feat: add password strength validator

Implement password validation requiring:
- Minimum 8 characters
- At least one uppercase letter
- At least one number
- At least one special character
```

### Bug Fix with Issue Reference

```
fix: resolve null reference in user service

The GetUserById method did not check for null before accessing
user properties, causing crashes when user not found.

Now returns null safely and logs warning message.

Fixes #456
```

### Breaking Change

```
feat: migrate to ASP.NET Core 8

BREAKING CHANGE: Minimum .NET version is now 8.0

This upgrade provides:
- Improved performance
- New minimal API features
- Enhanced JSON serialization

Migration guide: docs/MIGRATION.md
```

### Refactoring

```
refactor: extract validation logic to separate class

Moved user validation logic from UserController to UserValidator
to improve testability and reusability.

No functional changes, existing tests still pass.
```

## Troubleshooting

### Hook Validation Failed

**Problem:** Commit message rejected by `commit-msg` hook

**Solutions:**
1. Fix the commit message format
   ```bash
   git commit --amend -m "feat: correct message format"
   ```

2. Check validation errors in hook output
3. Review this document for correct format
4. Use commit template: `git config commit.template .github/.gitmessage`

### Imperative Mood Confusion

**Imperative mood** = Command form (as if giving orders)

| ✅ Imperative (Correct) | ❌ Past Tense (Wrong) |
|-------------------------|----------------------|
| add feature | added feature |
| fix bug | fixed bug |
| update docs | updated docs |
| refactor code | refactored code |

**Think:** "This commit will ___"
- "This commit will **add** feature" ✅
- "This commit will **added** feature" ❌

## References

- **Conventional Commits Spec**: https://www.conventionalcommits.org/
- **Git Hooks**: See `.husky/README.md`
- **Code Quality**: See `docs/GUARDRAILS.md`
- **Security**: See `docs/SECURITY.md`

## Questions?

If commit standards are unclear or need adjustment:
1. Open a discussion in the repository
2. Propose changes via pull request
3. Tag tech lead for clarification

Good commit messages make history searchable and debugging easier. Take the extra minute to write quality commit messages!
