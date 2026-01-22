# Architectural Principles (A.8-A.20)

You MUST detect violations of these architectural principles. When flagging issues, use category `Architecture`.

## A.8: Independent Services

Services should operate independently without direct coupling to other services.

**Detect:**
- Direct HTTP/REST calls to internal microservices (e.g., `HttpClient.GetAsync("http://internal-service/api/...")`)
- Service-to-service calls without abstraction layers
- Hardcoded internal service URLs

**Severity:** Major
**Impact Example:** "If ServiceA directly calls ServiceB, a failure in ServiceB causes cascading failures. Changes to ServiceB's API break ServiceA without warning."

```csharp
// BAD: Direct service coupling
var client = new HttpClient();
var result = await client.GetAsync("http://order-service/api/orders");

// GOOD: Use message broker or abstraction
await _messageBroker.PublishAsync(new GetOrdersQuery { CustomerId = id });
```

## A.9: Interface Versioning

All API interfaces MUST be versioned.

**Detect:**
- API routes without version prefix (e.g., `/api/users` instead of `/api/v1/users`)
- Controllers without `[ApiVersion]` attribute
- Missing version in route templates

**Severity:** Critical
**Impact Example:** "Without versioning, you cannot evolve the API without breaking all existing clients simultaneously."

```csharp
// BAD: Unversioned API
[Route("api/users")]
public class UsersController { }

// GOOD: Versioned API
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
public class UsersController { }
```

## A.15: Prefer Async Over Sync

Synchronous operations should not block async contexts.

**Detect:**
- `.Result` or `.GetAwaiter().GetResult()` on tasks
- `.Wait()` calls on tasks
- `Task.Run(() => AsyncMethod().Result)` patterns
- Mixing sync and async code inappropriately

**Severity:** Major
**Impact Example:** "Calling .Result in an async context causes thread pool starvation under load, leading to request timeouts and service degradation."

```csharp
// BAD: Blocking async context
var data = GetDataAsync().Result;
await Task.Run(() => SomeAsyncMethod().Wait());

// GOOD: Proper async/await
var data = await GetDataAsync();
await SomeAsyncMethod();
```

## A.16: No Direct Dependencies Between Services

Services should communicate through well-defined interfaces, not direct dependencies.

**Detect:**
- Direct instantiation of other service classes
- Tight coupling through shared types (not interfaces)
- Services directly referencing other service's internal types

**Severity:** Major
**Impact Example:** "Direct dependencies make services impossible to deploy independently and create a distributed monolith."

```csharp
// BAD: Direct dependency
var orderService = new OrderService();  // Tight coupling

// GOOD: Interface dependency with DI
public class CheckoutService(IOrderService orderService) { }
```

## A.18: No Shared Data Stores

Each service should own its data store. No direct database access across service boundaries.

**Detect:**
- Connection strings referencing databases owned by other services
- Direct SQL queries to tables owned by other services
- Shared DbContext across service boundaries

**Severity:** Critical
**Impact Example:** "Shared databases create hidden coupling. Changes to the schema by one service break others without warning, and you cannot scale services independently."

```csharp
// BAD: Accessing another service's database
var conn = "Server=shared-db;Database=OrdersDb;...";  // Not owned by this service

// GOOD: Each service owns its data
var conn = "Server=my-db;Database=CustomerDb;...";  // Owned by this service
// For cross-service data: use APIs or events
```

## A.12: Single Responsibility

Classes and modules should have a single, well-defined responsibility.

**Detect:**
- Classes with multiple unrelated public methods
- God classes with too many dependencies (>5-7 constructor parameters)
- Classes mixing data access, business logic, and presentation
- Files with multiple unrelated classes

**Severity:** Minor
**Impact Example:** "Classes with multiple responsibilities are hard to test, maintain, and modify without unexpected side effects."

## A.19: Event-Driven Communication

Prefer asynchronous event-driven communication over synchronous request/response.

**Detect:**
- Synchronous HTTP calls for operations that could be eventual
- Missing event publishing for state changes
- Request/response patterns for non-query operations

**Severity:** Minor (suggestion)
**Impact Example:** "Synchronous calls create temporal coupling. If the downstream service is slow or unavailable, the entire operation fails."

## Summary: What to Flag

| Violation | Pattern to Detect | Severity |
|-----------|-------------------|----------|
| Direct service calls | `HttpClient` to internal services, hardcoded internal URLs | Major |
| Unversioned APIs | Routes without `/v1/`, `/v2/` etc. | Critical |
| Sync over async | `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` | Major |
| Shared databases | Connection strings to other services' DBs | Critical |
| God classes | >7 constructor dependencies | Minor |
