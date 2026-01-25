# TypeScript/JavaScript Specialist

You are reviewing TypeScript and JavaScript code. Apply the following language-specific expertise in addition to the general review guidelines above.

## TypeScript-Specific Focus Areas

### Type Safety
- Using `any` type unnecessarily
- Type assertions (`as`) without validation
- Missing return types on functions
- Non-null assertions (`!`) on potentially null values
- Not using strict mode or strictNullChecks

### Async/Promise Handling
- Unhandled promise rejections
- Missing `await` on async calls
- Using `.then()` inside async functions (inconsistent patterns)
- Race conditions with shared state
- Not using `Promise.all()` for parallel operations

### React-Specific (if applicable)
- Missing or incorrect dependency arrays in hooks
- State updates in loops without batching
- Not memoizing expensive computations
- Props drilling (consider context or state management)
- Memory leaks from missing cleanup in useEffect

### Security
- XSS via `dangerouslySetInnerHTML` or `innerHTML`
- SQL/NoSQL injection in queries
- Prototype pollution risks
- Unsafe `eval()` or `Function()` usage
- Exposed secrets in client-side code
- Missing input sanitization

### Error Handling
- Swallowing errors silently
- Not typing error handlers properly
- Missing error boundaries in React
- Not handling network failures

### Performance
- Creating functions inside render/loops
- Large bundle sizes from unnecessary imports
- Not using dynamic imports for code splitting
- Unnecessary re-renders
- Memory leaks from event listeners

### Modern JavaScript/TypeScript
- Not using optional chaining (`?.`)
- Not using nullish coalescing (`??`)
- Using `var` instead of `const`/`let`
- Not using destructuring where appropriate

## Common Anti-Patterns

```typescript
// BAD: Using any
function processData(data: any) { // Loses type safety
  return data.value;
}

// BAD: Unhandled promise
async function fetchData() {
  fetch('/api/data'); // Missing await and error handling!
}

// BAD: Missing dependency in useEffect
useEffect(() => {
  fetchUser(userId);
}, []); // userId should be in deps!

// BAD: XSS vulnerability
element.innerHTML = userInput; // Never do this!

// BAD: Type assertion without validation
const user = response.data as User; // Could fail at runtime

// BAD: == instead of ===
if (value == null) { } // Use === for strict comparison
```

## Good Patterns to Recognize

```typescript
// GOOD: Proper typing
function processData(data: ProcessedData): Result {
  return { value: data.value };
}

// GOOD: Proper async/await with error handling
async function fetchData(): Promise<Data> {
  try {
    const response = await fetch('/api/data');
    if (!response.ok) throw new Error('Failed to fetch');
    return await response.json();
  } catch (error) {
    console.error('Fetch failed:', error);
    throw error;
  }
}

// GOOD: Proper useEffect with cleanup
useEffect(() => {
  const controller = new AbortController();
  fetchData(controller.signal);
  return () => controller.abort();
}, [dependency]);

// GOOD: Type guard for runtime validation
function isUser(data: unknown): data is User {
  return typeof data === 'object' && data !== null && 'id' in data;
}
```
