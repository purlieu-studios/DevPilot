# Git Hooks Configuration

This directory contains Git hooks managed by [Husky.NET](https://alirezanet.github.io/Husky.Net/) that automatically enforce code quality standards and development workflows.

## Overview

Git hooks are scripts that run automatically at specific points in the Git workflow. These hooks help maintain code quality, enforce commit conventions, and prevent common mistakes.

## Active Hooks

### 1. pre-commit
**When it runs:** Before creating a commit
**What it checks:**
- **LOC Limit (200-300 lines)** - Enforces small, reviewable commits
- **Secrets Detection** - Prevents committing API keys, passwords
- **File Size** - Warns about large files (>5MB)
- **Code Formatting** - Validates .editorconfig rules

**Example output:**
```
🔍 Running pre-commit checks...

📊 Checking lines of code...
  Total lines changed: 150
✓ LOC limit check passed

🔐 Scanning for secrets...
✓ No secrets detected

📦 Checking file sizes...
✓ File size check passed

✅ All pre-commit checks passed!
```

### 2. commit-msg
**When it runs:** After entering commit message
**What it checks:**
- **Conventional Commits format** - `type(scope): description`
- **Valid types** - feat, fix, docs, style, refactor, perf, test, build, ci, chore
- **Subject line** - Max 50 chars, lowercase, imperative mood
- **Blocked words** - WIP, TODO, FIXME, temp

**Valid commit messages:**
```bash
✅ feat: add user authentication
✅ fix(api): resolve null reference exception
✅ docs: update README with setup instructions
✅ refactor(service): simplify data processing logic

❌ Added new feature              # Missing type
❌ feat: Added feature            # Not imperative mood
❌ WIP: working on feature        # Blocked word
```

### 3. pre-push
**When it runs:** Before pushing to remote
**What it checks:**
- **Protected branches** - Blocks direct push to main/master
- **Branch naming** - Enforces `type/description` pattern
- **Build verification** - Runs `dotnet build` (optional)
- **Tests** - Runs `dotnet test` (optional)

**Example output:**
```
🚀 Running pre-push checks...

🌿 Current branch: feat/user-auth
✓ Branch name follows convention

📦 Found solution: DevPilot.sln
ℹ️  Build check disabled in configuration
ℹ️  Test check disabled in configuration

✅ All pre-push checks passed!
```

### 4. post-checkout
**When it runs:** After switching branches
**What it does:**
- Restores NuGet dependencies (`dotnet restore`)
- Displays recent commits on the new branch
- Warns about stale build artifacts

### 5. post-merge
**When it runs:** After merging branches
**What it does:**
- Restores dependencies
- Rebuilds solution to detect merge issues
- Lists any merge conflicts

## Configuration

Edit `.husky/hooks-config.json` to customize hook behavior:

```json
{
  "loc_limits": {
    "max_lines": 300,              // Hard limit for commit size
    "warn_lines": 200,             // Show warning at this threshold
    "excludePatterns": [...]       // Files to exclude from LOC count
  },
  "commit_message": {
    "max_subject_length": 50,
    "types": [...],                // Valid commit types
    "blocked_words": [...]         // Words not allowed in commits
  },
  "pre_push": {
    "require_tests": false,        // Set true to require tests
    "require_build": false,        // Set true to require builds
    "branch_name_pattern": "..."   // Regex for branch names
  },
  "secrets_detection": {
    "patterns": [...]              // Regex patterns for secrets
  }
}
```

## Bypassing Hooks

Sometimes you need to bypass hooks (use sparingly!):

```bash
# Skip all commit hooks
git commit --no-verify -m "emergency fix"

# Skip pre-push hook
git push --no-verify
```

**When to bypass:**
- Emergency hotfixes
- Large refactoring (exceeds LOC limit)
- Database migrations (generated files)
- Initial repository setup

**When NOT to bypass:**
- "It's faster" - Hooks exist for a reason
- "I'll fix it later" - Fix it now
- "It's blocking me" - The hook found a real issue

## Troubleshooting

### Hooks not running
```bash
# Reinstall hooks
dotnet tool restore
dotnet husky install
```

### PowerShell execution policy error
```bash
# Run as Administrator
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### LOC limit blocking legitimate commit
```bash
# Split into smaller commits
git add file1.cs
git commit -m "feat: implement part 1"

git add file2.cs
git commit -m "feat: implement part 2"

# OR bypass if truly necessary
git commit --no-verify -m "feat: large refactoring"
```

### Commit message validation failing
```bash
# Check the format
git commit -m "feat: add user authentication"  # ✓ Correct

# Not this:
git commit -m "Added user authentication"      # ✗ Wrong
git commit -m "feat: Added authentication"     # ✗ Not imperative
```

## Modifying Hooks

All hook scripts are PowerShell files in this directory:

- `pre-commit.ps1` - Pre-commit validation
- `commit-msg.ps1` - Commit message checking
- `pre-push.ps1` - Pre-push verification
- `post-checkout.ps1` - Post-checkout automation
- `post-merge.ps1` - Post-merge cleanup

To modify a hook:
1. Edit the corresponding `.ps1` file
2. Test your changes: `pwsh .husky/pre-commit.ps1`
3. Commit the changes
4. Hooks update automatically for all developers

## Benefits

### Why LOC Limits?
- **Easier code reviews** - Reviewers can focus on small changes
- **Faster feedback** - Smaller PRs get reviewed quicker
- **Cleaner history** - Each commit has a single, clear purpose
- **Easier debugging** - Simpler to identify which commit broke something

### Why Conventional Commits?
- **Automated changelogs** - Generate release notes automatically
- **Semantic versioning** - Determine version bumps from commit types
- **Better history** - Understand what changed at a glance
- **Consistent team communication** - Everyone follows same format

### Why Protected Branch Checks?
- **Enforces code review** - All changes go through PRs
- **Maintains quality** - No accidental pushes to main
- **Audit trail** - All changes are tracked and reviewed

## Learn More

- [Conventional Commits](https://www.conventionalcommits.org/)
- [Husky.NET Documentation](https://alirezanet.github.io/Husky.Net/)
- [Git Hooks Documentation](https://git-scm.com/docs/githooks)
- Project documentation: `DevPilot/CLAUDE.md`
