# Multi-Project Solution

This solution contains multiple projects with distinct responsibilities.

## Architecture

- **Web**: ASP.NET Core MVC web application (user-facing)
- **API**: Web API for third-party integrations
- **Worker**: Background worker for async processing
- **Shared**: Common models and utilities used by all projects

## Project Guidelines

**When adding code**:
- User-facing features → Web project
- API endpoints → API project
- Background jobs → Worker project
- Shared models/utilities → Shared project

**Logging**:
- Use Microsoft.Extensions.Logging
- Format: `_logger.LogInformation("Action completed for {UserId}", userId)`

## Important

DevPilot should identify the correct project for each change based on the request context.
