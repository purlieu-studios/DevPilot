# Security Guidelines

This document defines security standards and best practices for the DevPilot project. **All developers and AI assistants (including Claude Code) must follow these rules.**

## Secret Management

### Never Commit Secrets

**Prohibited in commits:**
- API keys and tokens
- Passwords and credentials
- Database connection strings
- Private keys (SSH, SSL/TLS, PGP)
- OAuth client secrets
- Encryption keys
- Service account credentials
- Personal access tokens (GitHub, Azure, AWS, etc.)

### Automated Detection

The `pre-commit` Git hook scans for common secret patterns:

**Detected patterns:**
- `api_key`, `apikey` followed by values
- `secret`, `password`, `passwd`, `pwd` with credentials
- `connectionstring` entries
- Private key headers (`-----BEGIN PRIVATE KEY-----`)
- AWS credentials (`aws_access_key_id`, `aws_secret_access_key`)

**Example violations:**
```csharp
❌ var apiKey = "sk-1234567890abcdef";
❌ var password = "MySecretPass123!";
❌ var connectionString = "Server=prod.db;User=admin;Password=secret";
❌ const string SECRET_KEY = "abc123xyz";
```

### Proper Secret Storage

**Use environment variables:**
```csharp
✅ var apiKey = Environment.GetEnvironmentVariable("API_KEY");
✅ var connString = Configuration.GetConnectionString("DefaultConnection");
```

**Use user secrets (development):**
```bash
# Store secrets locally (not committed)
dotnet user-secrets set "ApiKey" "your-key-here"
```

```csharp
// Access in code
var apiKey = Configuration["ApiKey"];
```

**Use Azure Key Vault (production):**
```csharp
// Production secret management
var keyVaultUrl = new Uri(Environment.GetEnvironmentVariable("KEY_VAULT_URL"));
var client = new SecretClient(keyVaultUrl, new DefaultAzureCredential());
var secret = await client.GetSecretAsync("ApiKey");
```

### Configuration Files

**Safe configuration:**
```json
// ✅ appsettings.json (safe to commit)
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ApiEndpoint": "https://api.example.com",
  "CacheTimeout": 300
}
```

**Unsafe configuration:**
```json
// ❌ Never commit this!
{
  "ConnectionStrings": {
    "Database": "Server=prod;User=admin;Password=secret123"
  },
  "ApiKey": "sk-1234567890abcdef"
}
```

**Use environment-specific secrets:**
```json
// appsettings.Development.json (in .gitignore)
{
  "ConnectionStrings": {
    "Database": "Server=localhost;Integrated Security=true;"
  }
}
```

### .gitignore for Secrets

Ensure these patterns are in `.gitignore`:

```gitignore
# User-specific files with secrets
*.user
appsettings.Development.json
appsettings.Local.json
secrets.json

# Environment files
.env
.env.local
.env.*.local

# Credentials
credentials.json
*.pfx
*.p12
*.key
*.pem
id_rsa
id_dsa
```

## Dependency Security

### Package Management

**Keep dependencies updated:**
```bash
# Check for vulnerable packages
dotnet list package --vulnerable

# Update packages
dotnet add package PackageName --version x.y.z
```

**Review package sources:**
- Only use trusted NuGet sources
- Review package contents before installing
- Check package downloads and ratings
- Verify package maintainer reputation

### Vulnerability Scanning

**Enable automated scanning:**
- GitHub Dependabot alerts
- Azure DevOps vulnerability scanning
- NuGet audit warnings

**Act on vulnerabilities promptly:**
1. Review severity (Critical > High > Medium > Low)
2. Update to patched version if available
3. If no patch, consider alternatives
4. Document accepted risks if mitigation unavailable

## Input Validation

### Always Validate User Input

**Never trust user input:**
```csharp
❌ // Dangerous - SQL injection risk
var query = $"SELECT * FROM Users WHERE Username = '{username}'";

✅ // Safe - parameterized query
var query = "SELECT * FROM Users WHERE Username = @username";
command.Parameters.AddWithValue("@username", username);
```

### Validation Rules

**Required validations:**
- Length limits (prevent buffer overflows)
- Format validation (regex, type checking)
- Range validation (min/max values)
- Allowed character sets (whitelist approach)
- SQL injection prevention (parameterized queries)
- XSS prevention (encode output)
- Path traversal prevention (validate file paths)

**Example validation:**
```csharp
public class UserValidator : AbstractValidator<User>
{
    public UserValidator()
    {
        // Length validation
        RuleFor(x => x.Username)
            .NotEmpty()
            .Length(3, 50)
            .Matches("^[a-zA-Z0-9_]+$"); // Alphanumeric only

        // Email validation
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        // Password strength
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]") // Uppercase
            .Matches("[a-z]") // Lowercase
            .Matches("[0-9]") // Number
            .Matches("[^a-zA-Z0-9]"); // Special char
    }
}
```

## Authentication & Authorization

### Authentication Best Practices

**Password handling:**
```csharp
❌ // Never store plain text passwords
user.Password = password;

✅ // Use password hashing (bcrypt, Argon2, PBKDF2)
user.PasswordHash = _passwordHasher.HashPassword(user, password);
```

**Session management:**
- Use secure session cookies (HttpOnly, Secure, SameSite)
- Implement session timeout
- Invalidate sessions on logout
- Use CSRF tokens for state-changing operations

**Multi-factor authentication:**
- Require MFA for sensitive operations
- Support TOTP (Time-based One-Time Password)
- Provide backup codes
- Rate limit MFA attempts

### Authorization Best Practices

**Principle of least privilege:**
```csharp
[Authorize(Roles = "User")]
public IActionResult ViewProfile()
{
    // Users can view their own profile
}

[Authorize(Roles = "Admin")]
public IActionResult DeleteUser(int userId)
{
    // Only admins can delete users
}
```

**Resource-based authorization:**
```csharp
// Check ownership before allowing operation
var document = await _context.Documents.FindAsync(id);
if (document.OwnerId != User.GetUserId())
{
    return Forbid();
}
```

## Data Protection

### Encryption at Rest

**Encrypt sensitive data:**
```csharp
// Use Data Protection API
var protectedData = _protector.Protect(sensitiveData);

// Store encrypted data
user.EncryptedSsn = protectedData;
```

**Database encryption:**
- Enable Transparent Data Encryption (TDE)
- Encrypt sensitive columns
- Use encrypted backups

### Encryption in Transit

**Always use HTTPS:**
```csharp
// Enforce HTTPS in production
app.UseHttpsRedirection();
app.UseHsts(); // HTTP Strict Transport Security
```

**TLS configuration:**
- Use TLS 1.2 or higher
- Disable weak cipher suites
- Use strong key exchange algorithms
- Keep certificates up to date

### Personal Identifiable Information (PII)

**Handle PII carefully:**
- Encrypt at rest and in transit
- Minimize PII collection
- Implement data retention policies
- Support data deletion (GDPR compliance)
- Log PII access for audit

**PII examples:**
- Full name
- Email address
- Phone number
- Physical address
- Social Security Number
- Credit card information
- IP addresses (in some jurisdictions)

## Logging and Monitoring

### Safe Logging Practices

**Never log sensitive data:**
```csharp
❌ // Dangerous - logs password
_logger.LogInformation("User {Username} logged in with password {Password}",
    username, password);

✅ // Safe - no sensitive data
_logger.LogInformation("User {Username} logged in successfully", username);
```

**Log security events:**
- Authentication attempts (success and failure)
- Authorization failures
- Input validation failures
- Configuration changes
- Admin actions
- Suspicious activity

### Monitoring and Alerts

**Set up alerts for:**
- Multiple failed login attempts
- Unauthorized access attempts
- Unusual API usage patterns
- Database access anomalies
- Configuration changes
- Security scan failures

## Error Handling

### Secure Error Messages

**Don't leak implementation details:**
```csharp
❌ // Exposes database structure
return BadRequest($"Column 'Username' does not exist in table 'Users'");

✅ // Generic error message
return BadRequest("Invalid request. Please check your input.");
```

**Log detailed errors, show generic messages:**
```csharp
try
{
    // Operation
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error processing user request");
    return StatusCode(500, "An error occurred. Please try again later.");
}
```

### Exception Management

**Don't expose stack traces:**
- Disable detailed errors in production
- Log full exception details server-side
- Return generic errors to clients
- Implement global exception handler

## Code Security

### Avoid Common Vulnerabilities

**SQL Injection:**
```csharp
❌ string.Format("SELECT * FROM Users WHERE Id = {0}", userId)
✅ Parameterized queries or ORM (Entity Framework)
```

**XSS (Cross-Site Scripting):**
```csharp
❌ @Html.Raw(userInput)
✅ @userInput (automatic encoding in Razor)
```

**CSRF (Cross-Site Request Forgery):**
```csharp
✅ [ValidateAntiForgeryToken] on POST/PUT/DELETE actions
```

**Path Traversal:**
```csharp
❌ var path = Path.Combine(baseDir, userInput);
✅ Validate userInput doesn't contain ".." or absolute paths
```

### Secure Coding Practices

**Use security headers:**
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "no-referrer");
    await next();
});
```

**Implement rate limiting:**
```csharp
// Prevent brute force attacks
[RateLimit(Requests = 5, Period = TimeSpan.FromMinutes(15))]
public IActionResult Login(LoginModel model) { ... }
```

**Validate file uploads:**
```csharp
// Check file type, size, content
if (file.Length > maxFileSize)
    return BadRequest("File too large");

var allowedExtensions = new[] { ".jpg", ".png", ".pdf" };
if (!allowedExtensions.Contains(Path.GetExtension(file.FileName)))
    return BadRequest("Invalid file type");

// Scan for malware if possible
```

## Third-Party Integrations

### API Security

**Authenticate API calls:**
- Use API keys or OAuth tokens
- Rotate keys regularly
- Store keys securely (Key Vault)
- Use separate keys for dev/staging/prod

**Validate API responses:**
```csharp
// Don't trust third-party data
var response = await _httpClient.GetAsync(apiUrl);
if (!response.IsSuccessStatusCode)
{
    _logger.LogWarning("API call failed: {StatusCode}", response.StatusCode);
    return null;
}

var data = await response.Content.ReadFromJsonAsync<ApiResponse>();
// Validate data before using
if (data == null || !IsValid(data))
{
    _logger.LogWarning("Invalid API response");
    return null;
}
```

### Webhook Security

**Verify webhook signatures:**
```csharp
// Verify request came from trusted source
var signature = Request.Headers["X-Signature"];
var computedSignature = ComputeSignature(requestBody, webhookSecret);
if (signature != computedSignature)
{
    return Unauthorized();
}
```

## Incident Response

### If a Secret is Exposed

**Immediate actions:**
1. **Revoke the exposed secret** immediately
2. **Generate new credentials**
3. **Update all systems** using the secret
4. **Review access logs** for unauthorized usage
5. **Document the incident**
6. **Notify affected parties** if required

**Prevention:**
- Use Git history rewriting tools (BFG Repo-Cleaner, git-filter-repo)
- Rotate secrets regularly even without exposure
- Monitor for exposed secrets (GitHub secret scanning)

### Security Review Checklist

Before deploying:
- [ ] No secrets in code or configuration
- [ ] All inputs validated
- [ ] Authentication/authorization implemented
- [ ] HTTPS enforced
- [ ] Sensitive data encrypted
- [ ] Security headers configured
- [ ] Error messages don't leak information
- [ ] Logging doesn't expose secrets
- [ ] Dependencies scanned for vulnerabilities
- [ ] Code reviewed for security issues

## Resources

- **OWASP Top 10**: https://owasp.org/www-project-top-ten/
- **OWASP Cheat Sheets**: https://cheatsheetseries.owasp.org/
- **.NET Security**: https://learn.microsoft.com/en-us/aspnet/core/security/
- **Azure Key Vault**: https://learn.microsoft.com/en-us/azure/key-vault/
- **Git Hooks**: See `.husky/README.md` for automated secret detection
- **Code Quality**: See `docs/GUARDRAILS.md`
- **Commit Standards**: See `docs/COMMIT_STANDARDS.md`

## Reporting Security Issues

**If you discover a security vulnerability:**

1. **Do NOT create a public issue**
2. **Email security team** at [security contact - update when available]
3. **Provide details:**
   - Description of vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if any)
4. **Wait for response** before public disclosure

We take security seriously and will respond promptly to all reports.

## Questions?

For security questions or clarification:
1. Consult this document first
2. Review OWASP resources
3. Ask tech lead or security team
4. When in doubt, be more secure

**Remember: Security is everyone's responsibility!**
