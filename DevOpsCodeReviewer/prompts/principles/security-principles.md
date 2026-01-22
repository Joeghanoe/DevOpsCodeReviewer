# Security Principles (S.1-S.10)

These principles ENHANCE the existing Security category. When flagging issues, use category `Security`.

## S.1: Principle of Least Privilege

Grant only the minimum permissions necessary for the task.

**Detect:**
- Wildcard permissions (`*` in policies, `Admin` roles for regular operations)
- Over-permissive CORS: `AllowAnyOrigin()`, `AllowAnyMethod()`, `AllowAnyHeader()` combined
- Overly broad file system access
- Service accounts with excessive permissions

**Severity:** Critical
**Impact Example:** "AllowAnyOrigin() combined with AllowCredentials() enables any website to make authenticated requests as your users."

```csharp
// BAD: Over-permissive CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("Open", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// GOOD: Specific origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("Restricted", policy =>
        policy.WithOrigins("https://trusted-app.com")
              .WithMethods("GET", "POST")
              .WithHeaders("Content-Type", "Authorization"));
});
```

## S.4: Secure Defaults

Default configurations should be secure. Insecure options should require explicit opt-in.

**Detect:**
- `[AllowAnonymous]` on sensitive endpoints without explicit justification
- Disabled security features: `RequireHttpsMetadata = false`, `ValidateIssuer = false`
- Debug/development settings in production code paths
- Disabled CSRF protection

**Severity:** Critical
**Impact Example:** "[AllowAnonymous] on a user data endpoint exposes all user information without authentication."

```csharp
// BAD: Security disabled without justification
[AllowAnonymous]
public IActionResult GetUserProfile(int userId) { }

options.RequireHttpsMetadata = false;  // Never in production
options.TokenValidationParameters.ValidateIssuer = false;

// GOOD: Explicit security with justification
[Authorize(Roles = "User")]
public IActionResult GetUserProfile() { }  // Gets user from claims

// If AllowAnonymous is needed, document why:
/// <summary>
/// Public health check endpoint - no sensitive data exposed.
/// </summary>
[AllowAnonymous]
public IActionResult HealthCheck() { }
```

## S.6: Fail Securely

When errors occur, fail in a way that doesn't expose sensitive information.

**Detect:**
- Returning `ex.ToString()`, `ex.StackTrace`, or `ex.Message` directly to clients
- Detailed error messages containing internal paths, connection strings, or system info
- Exception details in API responses
- Verbose error logging that includes sensitive data

**Severity:** Critical
**Impact Example:** "Returning ex.ToString() exposes internal file paths, database names, and code structure to attackers for reconnaissance."

```csharp
// BAD: Exposing internal details
catch (Exception ex)
{
    return BadRequest(ex.ToString());  // Exposes stack trace
    return Problem(ex.Message);  // May contain sensitive info
}

// GOOD: Generic error to client, detailed logging internally
catch (Exception ex)
{
    _logger.LogError(ex, "Operation failed for request {RequestId}", requestId);
    return Problem("An unexpected error occurred. Please try again.");
}
```

## S.7: Defense in Depth

Don't rely on a single security control. Layer multiple defenses.

**Detect:**
- Single validation point (e.g., only client-side validation)
- Missing server-side validation for data also validated on client
- Relying solely on authentication without authorization checks
- Missing input sanitization after validation

**Severity:** Major
**Impact Example:** "Client-side validation can be bypassed. Without server-side validation, malicious input reaches your business logic."

```csharp
// BAD: Only authentication, no authorization
[Authorize]
public IActionResult DeleteUser(int userId)
{
    _userService.Delete(userId);  // Any authenticated user can delete any user!
}

// GOOD: Defense in depth
[Authorize]
public IActionResult DeleteUser(int userId)
{
    var currentUserId = User.GetUserId();
    if (currentUserId != userId && !User.IsInRole("Admin"))
        return Forbid();

    _userService.Delete(userId);
}
```

## S.8: Avoid Security by Obscurity

Don't rely on hidden URLs, undocumented endpoints, or secret paths for security.

**Detect:**
- Comments like "hidden", "secret", "internal only" on unprotected endpoints
- Security through URL complexity (long random-looking paths without auth)
- Undocumented but unprotected admin endpoints

**Severity:** Critical
**Impact Example:** "Hidden endpoints are discovered through URL fuzzing, source code leaks, or browser history. They offer no real protection."

```csharp
// BAD: "Hidden" endpoint without protection
[Route("api/x7k9m2/admin/users")]  // "Secret" URL
public IActionResult GetAllUsers() { }

// GOOD: Proper authorization
[Authorize(Roles = "Admin")]
[Route("api/admin/users")]
public IActionResult GetAllUsers() { }
```

## S.9: Complete Mediation

Check authorization for every request, not just the initial access.

**Detect:**
- Missing `[Authorize]` on controller actions that access protected resources
- Authorization checked at entry point but not in subsequent service calls
- Cached authorization decisions without expiry

**Severity:** Major
**Impact Example:** "If authorization is only checked on the list endpoint but not on individual item access, users can access items they shouldn't by guessing IDs."

## S.10: Separation of Privilege

Require multiple conditions to be met for sensitive operations.

**Detect:**
- Single factor for sensitive operations (password change, payment, etc.)
- Missing confirmation steps for destructive actions
- Lack of approval workflow for elevated operations

**Severity:** Minor (unless handling payments/PII)
**Impact Example:** "A single compromised session can perform irreversible actions. Multi-factor or approval workflows limit blast radius."

## Summary: Security Patterns to Flag

| Violation | Pattern to Detect | Severity |
|-----------|-------------------|----------|
| Over-permissive CORS | `AllowAnyOrigin()` + `AllowAnyMethod()` | Critical |
| Exposed exceptions | `ex.ToString()`, `ex.StackTrace` in responses | Critical |
| Missing auth | `[AllowAnonymous]` on data endpoints | Critical |
| Disabled validation | `ValidateIssuer = false`, `RequireHttpsMetadata = false` | Critical |
| Single validation layer | Client-only validation, auth without authz | Major |
| Hidden endpoints | Unprotected "secret" URLs | Critical |
