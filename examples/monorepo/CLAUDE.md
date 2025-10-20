# Monorepo Example

This repository demonstrates a monorepo structure with multiple applications sharing common libraries.

## Structure

```
monorepo/
├── apps/
│   ├── app1/       # Console application 1
│   └── app2/       # Console application 2
└── shared/
    └── core/       # Shared utilities used by all apps
```

## Architecture

- **apps/app1**: First console application that uses shared logging
- **apps/app2**: Second console application that uses shared logging
- **shared/core**: Common utilities (Logger) shared across all apps

## Guidelines

**When adding features**:
- App-specific logic → Respective app directory (apps/app1 or apps/app2)
- Shared utilities → shared/core

**Coding Standards**:
- Use XML documentation for public APIs
- Follow Microsoft C# naming conventions
- Use async/await for I/O operations

## Important

DevPilot should identify whether changes belong in app-specific code or shared libraries based on the request context.
