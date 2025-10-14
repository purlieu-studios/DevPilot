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
│   └── PULL_REQUEST_TEMPLATE.md  # PR template for standardized submissions
├── .gitmessage                    # Commit message template (Conventional Commits)
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
- A commit message template is available in `.gitmessage`
- To use the template locally, run: `git config commit.template .gitmessage`
- Common types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`
- Use imperative mood ("Add feature" not "Added feature")
- Subject line: max 50 characters, capitalized, no period
- Add body for context (what and why, not how)

## Notes for Future Development

This CLAUDE.md should be updated as the project grows to include:
- Build and test commands
- Architecture overview
- Key design patterns and conventions
- Development workflow specifics
