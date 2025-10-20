# Non-Standard Directory Structure Example

This repository uses non-conventional directory names to test DevPilot's structure awareness.

## Structure

```
non-standard/
├── source/                # Main application code (instead of "src/")
├── unit-tests/            # Test projects (instead of "tests/")
└── documentation/         # Documentation files (instead of "docs/")
```

## Architecture

- **source/**: Main application library (MyApplication)
- **unit-tests/**: xUnit test project for MyApplication
- **documentation/**: Project documentation

## Guidelines

**Directory Conventions** (NON-STANDARD):
- Main code → source/
- Tests → unit-tests/
- Documentation → documentation/

**Coding Standards**:
- Use XML documentation for public APIs
- Follow Microsoft C# naming conventions
- Test coverage > 80%

## Important

This repository intentionally uses non-standard directory names to validate DevPilot's repository structure detection. DevPilot should correctly identify "source/" as the main project and "unit-tests/" as the test project.
