# GitHub Templates & Configuration

This directory contains GitHub-related templates and configuration files for the DevPilot repository.

## Files

### PULL_REQUEST_TEMPLATE.md

Template for all pull requests in this repository. Automatically appears when creating a new PR on GitHub.

**Features:**
- Structured summary section (what changed, why)
- Type of change checkboxes
- Reviewer guidance section
- Screenshots/demo section for visual changes
- Breaking changes documentation
- Testing instructions and checklist

**Usage:** Automatically loaded by GitHub when creating a PR. Fill in all relevant sections and delete any that don't apply.

### .gitmessage

Commit message template following [Conventional Commits](https://www.conventionalcommits.org/) specification.

**Format:** `<type>[optional scope]: <description>`

**Common Types:**
- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation changes
- `style:` - Code style changes
- `refactor:` - Code refactoring
- `perf:` - Performance improvements
- `test:` - Test changes
- `build:` - Build system changes
- `ci:` - CI configuration changes
- `chore:` - Other changes

**Setup (local):**
```bash
git config commit.template .github/.gitmessage
```

After setup, the template will appear in your editor when you run `git commit` (without `-m`).

## Guidelines

All commits and pull requests should follow the conventions outlined in these templates to maintain consistency and clarity across the project.
