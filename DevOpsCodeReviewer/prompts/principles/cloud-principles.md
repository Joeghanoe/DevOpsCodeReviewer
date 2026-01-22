# Cloud & DevOps Principles

These principles ensure cloud-ready, maintainable code. When flagging issues, use category `CloudCompliance`.

## No PII in Logs

Personal Identifiable Information must never appear in logs.

**Detect:**
- Logging email addresses, phone numbers, SSNs, credit card numbers
- Logging user passwords, tokens, or secrets
- Logging full request/response bodies without sanitization
- Logging IP addresses combined with user identifiers

**Severity:** Critical
**Impact Example:** "Logging email addresses violates GDPR. If logs are compromised, user privacy is breached. Log aggregation services may store this data indefinitely."

```csharp
// BAD: PII in logs
_logger.LogInformation("User {Email} logged in from {IP}", user.Email, request.IpAddress);
_logger.LogDebug("Request body: {Body}", JsonSerializer.Serialize(request));

// GOOD: Log identifiers, not PII
_logger.LogInformation("User {UserId} logged in", user.Id);
_logger.LogDebug("Request processed for correlation {CorrelationId}", correlationId);
```

## Stateless Services

Cloud services should be stateless to enable horizontal scaling.

**Detect:**
- `static` fields with mutable state
- In-memory caches without distributed backing (except short-lived/read-only)
- Session state stored in-process (`Session["key"]`)
- Thread-local storage or `AsyncLocal` for request state across calls
- Singleton services with mutable instance fields

**Severity:** Major
**Impact Example:** "Static mutable state is not shared across instances. In a load-balanced environment, users get inconsistent results depending on which instance handles their request."

```csharp
// BAD: Static mutable state
public class OrderService
{
    private static List<Order> _orderCache = new();  // Not shared across instances
    private static int _orderCounter = 0;  // Race conditions + inconsistent across instances
}

// GOOD: Use distributed cache or database
public class OrderService(IDistributedCache cache, IDatabase db)
{
    public async Task<Order> GetOrderAsync(int id)
    {
        var cached = await cache.GetAsync<Order>($"order:{id}");
        return cached ?? await db.Orders.FindAsync(id);
    }
}
```

## DRY (Don't Repeat Yourself)

Avoid duplicating code. Extract common logic into reusable components.

**Detect:**
- Duplicate code blocks (>10 lines of similar logic)
- Copy-pasted error handling or validation patterns
- Repeated configuration or setup code
- Similar methods that could be generalized

**Severity:** Minor
**Impact Example:** "Duplicated validation logic must be updated in multiple places. One copy gets fixed, others remain buggy, creating inconsistent behavior."

```csharp
// BAD: Duplicated validation
public void CreateUser(UserDto dto)
{
    if (string.IsNullOrEmpty(dto.Email)) throw new ValidationException("Email required");
    if (!dto.Email.Contains("@")) throw new ValidationException("Invalid email");
    // ... create user
}

public void UpdateUser(UserDto dto)
{
    if (string.IsNullOrEmpty(dto.Email)) throw new ValidationException("Email required");
    if (!dto.Email.Contains("@")) throw new ValidationException("Invalid email");
    // ... update user
}

// GOOD: Extracted validation
public void CreateUser(UserDto dto)
{
    _validator.ValidateEmail(dto.Email);
    // ... create user
}
```

## n-1 Frameworks

Use current or previous major version of frameworks. Avoid deprecated APIs.

**Detect:**
- Usage of deprecated/obsolete APIs (methods marked `[Obsolete]`)
- References to old framework patterns (e.g., `WebClient` instead of `HttpClient`)
- Legacy configuration patterns

**Severity:** Major
**Impact Example:** "Deprecated APIs may be removed in future versions, causing upgrade blockers. They often have known issues that won't be fixed."

```csharp
// BAD: Deprecated APIs
var client = new WebClient();  // Obsolete
var config = ConfigurationManager.AppSettings["key"];  // Legacy pattern

// GOOD: Current patterns
var client = _httpClientFactory.CreateClient();
var config = _configuration["key"];
```

## All Code Must Be Testable

Code should be designed for testability through dependency injection and abstraction.

**Detect:**
- Direct instantiation of dependencies (`new Service()`) instead of DI
- Static method calls for business logic
- Hard-coded configuration values
- Tight coupling to infrastructure (file system, network, time)
- Missing interfaces for services

**Severity:** Major
**Impact Example:** "Code that directly instantiates dependencies cannot be unit tested. You can only do integration tests, which are slower and flakier."

```csharp
// BAD: Untestable code
public class OrderProcessor
{
    public void Process(Order order)
    {
        var emailService = new EmailService();  // Can't mock
        var now = DateTime.Now;  // Non-deterministic
        File.WriteAllText($"C:\\logs\\{now}.log", "processed");  // Infrastructure coupling
    }
}

// GOOD: Testable code
public class OrderProcessor(IEmailService emailService, ITimeProvider time, IFileSystem fs)
{
    public void Process(Order order)
    {
        var now = time.UtcNow;
        fs.WriteAllText($"logs/{now}.log", "processed");
        emailService.Send(order.CustomerEmail, "Order processed");
    }
}
```

## POLA (Principle of Least Astonishment)

Code should behave as users and developers expect. Avoid surprises.

**Detect:**
- Methods with side effects not indicated by their name (e.g., `GetUser()` that modifies data)
- Misleading method/variable names
- Unexpected mutations of input parameters
- Non-obvious control flow

**Severity:** Minor
**Impact Example:** "A method named GetUserProfile() that also updates the last-login timestamp confuses developers and causes unexpected behavior in tests."

```csharp
// BAD: Surprising behavior
public User GetUser(int id)
{
    var user = _db.Users.Find(id);
    user.LastAccessed = DateTime.UtcNow;  // Unexpected side effect
    _db.SaveChanges();  // Surprising!
    return user;
}

// GOOD: Clear intent
public User GetUser(int id) => _db.Users.Find(id);

public User GetAndTrackUser(int id)
{
    var user = _db.Users.Find(id);
    user.LastAccessed = DateTime.UtcNow;
    _db.SaveChanges();
    return user;
}
```

## Immutability Where Possible

Prefer immutable data structures to reduce bugs from unexpected mutations.

**Detect:**
- Public mutable collections (`public List<T> Items { get; set; }`)
- Modifying input parameters
- Returning internal mutable collections directly

**Severity:** Minor
**Impact Example:** "Returning a mutable internal collection allows callers to modify your internal state, causing bugs that are hard to trace."

```csharp
// BAD: Mutable exposure
public class Order
{
    public List<OrderItem> Items { get; set; } = new();
}

// GOOD: Immutable exposure
public class Order
{
    private readonly List<OrderItem> _items = new();
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
}
```

## Summary: Cloud Patterns to Flag

| Violation | Pattern to Detect | Severity |
|-----------|-------------------|----------|
| PII in logs | Logging email, phone, SSN, tokens | Critical |
| Mutable static state | `static` fields with mutable types | Major |
| Untestable code | `new Service()`, static business logic | Major |
| Deprecated APIs | `[Obsolete]` usage, `WebClient`, legacy patterns | Major |
| Duplicate code | >10 lines of similar code | Minor |
| Surprising behavior | Side effects in getters, misleading names | Minor |
| Mutable exposure | Public `List<T>`, returning internal collections | Minor |
