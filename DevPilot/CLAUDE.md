# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DevPilot is a new project repository. The codebase is currently in early setup phase.

## Environment

- **Platform**: Windows
- **IDE Setup**: Visual Studio (based on .gitignore configuration)

## Repository Structure

```
DevPilot/
├── .github/
│   ├── .gitmessage                # Commit message template (Conventional Commits)
│   ├── PULL_REQUEST_TEMPLATE.md   # PR template for standardized submissions
│   └── README.md                  # Documentation for GitHub templates
├── .gitignore                     # Visual Studio ignore patterns
└── CLAUDE.md                      # This file
```

## Development Guidelines

### Git Workflow

- **All changes must be made on a separate branch** - Create a feature branch for your work
- **All changes must go through pull requests** - Do not push directly to `main`
- Pull requests should use the provided PR template in `.github/PULL_REQUEST_TEMPLATE.md`
- The main branch is `main`

### Commit Messages

- **Follow Conventional Commits format**: `<type>[optional scope]: <description>`
- A commit message template is available in `.github/.gitmessage`
- To use the template locally, run: `git config commit.template .github/.gitmessage`
- Common types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`
- Use imperative mood ("Add feature" not "Added feature")
- Subject line: max 50 characters, capitalized, no period
- Add body for context (what and why, not how)

## Git Hooks

This repository uses [Husky.NET](https://alirezanet.github.io/Husky.Net/) to manage Git hooks that enforce code quality and workflow standards.

### Automated Checks

The following hooks run automatically:

#### pre-commit (Before Creating a Commit)
- **LOC Limit Enforcement** - Maximum 300 lines per commit (warn at 200)
  - Excludes generated files, lock files, and documentation
  - Provides detailed breakdown of changes per file
  - Encourages small, focused commits for easier review
- **Secrets Detection** - Scans for API keys, passwords, and connection strings
- **File Size Check** - Warns about files larger than 5MB
- **Code Formatting** - Validates .editorconfig compliance (when dotnet-format is installed)

#### commit-msg (Validating Commit Messages)
- **Conventional Commits** - Enforces format: `type(scope): description`
  - Valid types: feat, fix, docs, style, refactor, perf, test, build, ci, chore, revert
- **Subject Line Validation** - Max 50 characters, lowercase start, no period
- **Imperative Mood** - Ensures commands ("add" not "added")
- **Blocked Words** - Prevents WIP, TODO, FIXME in commit messages

#### pre-push (Before Pushing to Remote)
- **Protected Branch Check** - Blocks direct pushes to main/master
- **Branch Name Validation** - Enforces `type/description` format
- **Build Verification** - Runs `dotnet build` (configurable)
- **Test Execution** - Runs `dotnet test` (configurable)
- **Uncommitted Changes Warning** - Alerts about unpushed changes

#### post-checkout (After Switching Branches)
- **Dependency Restoration** - Runs `dotnet restore` automatically
- **Build Artifact Notice** - Suggests cleaning stale bin/obj folders
- **Recent Commits** - Displays last 5 commits on the new branch

#### post-merge (After Merging)
- **Dependency Updates** - Runs `dotnet restore`
- **Rebuild Solution** - Ensures merge didn't break build
- **Conflict Detection** - Lists unresolved merge conflicts

### Configuration

Hook behavior is configured in `.husky/hooks-config.json`:

```json
{
  "loc_limits": {
    "max_lines": 300,        // Maximum lines per commit
    "warn_lines": 200        // Warning threshold
  },
  "pre_push": {
    "require_tests": false,  // Enable to require tests before push
    "require_build": false   // Enable to require build before push
  }
}
```

### Bypassing Hooks

In emergency situations, you can bypass hooks:

```bash
# Skip pre-commit and commit-msg hooks
git commit --no-verify

# Skip pre-push hook
git push --no-verify
```

**Warning:** Use `--no-verify` sparingly. Hooks exist to maintain code quality and prevent issues.

### Setup for New Developers

Hooks are automatically installed when cloning the repository. If hooks aren't working:

```bash
# Reinstall hooks
dotnet tool restore
dotnet husky install
```

### Hook Files

All hook scripts are in the `.husky/` directory:
- `pre-commit.ps1` - Pre-commit checks
- `commit-msg.ps1` - Commit message validation
- `pre-push.ps1` - Pre-push verification
- `post-checkout.ps1` - Post-checkout setup
- `post-merge.ps1` - Post-merge updates
- `hooks-config.json` - Configuration file
- `task-runner.json` - Husky task definitions

## Notes for Future Development

This CLAUDE.md should be updated as the project grows to include:
- Build and test commands
- Architecture overview
- Key design patterns and conventions
- Development workflow specifics
