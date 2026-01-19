# Code Review Prompts

This directory contains prompt templates used by the AI code reviewer to analyze pull request changes.

## Available Prompts

| File | Description |
|------|-------------|
| `generic-code-review.md` | General code review prompt applicable to all languages |
| `csharp-code-review.md` | C# and .NET specific patterns and anti-patterns |
| `typescript-code-review.md` | TypeScript/JavaScript and React specific patterns |

## How Prompts Work

The code reviewer selects prompts based on the file extensions in the PR:

- `.cs` files → Uses `csharp-code-review.md`
- `.ts`, `.tsx`, `.js`, `.jsx` files → Uses `typescript-code-review.md`
- Other files → Uses `generic-code-review.md`

## Customizing Prompts

### Adding a New Language

1. Create a new file: `{language}-code-review.md`
2. Follow the structure of existing prompts:
   - Language-specific focus areas
   - Common anti-patterns with examples
   - Good patterns to recognize
   - Output format specification
3. Update `LlmService.cs` to use the new prompt for relevant file extensions

### Modifying Existing Prompts

You can customize prompts to match your team's coding standards:

1. **Add company-specific rules**: Include internal best practices
2. **Adjust severity levels**: Change what constitutes Critical vs Minor
3. **Add/remove categories**: Focus on areas important to your codebase
4. **Include framework-specific guidance**: Add rules for your tech stack

### Example: Adding Company Rules

```markdown
## Company-Specific Rules

### Logging
- All public methods in services must have trace logging
- Error logs must include correlation ID
- Never log PII (personally identifiable information)

### Naming Conventions
- Repository classes must end with `Repository`
- Service classes must end with `Service`
- DTOs must end with `Dto` or `Request`/`Response`
```

## Prompt Structure

Each prompt should contain:

### 1. Role Definition
Tell the AI what expertise it should apply.

### 2. Focus Areas
List specific things to look for, organized by category.

### 3. Severity Definitions
Define what each severity level means for your team.

### 4. Anti-Pattern Examples
Show concrete code examples of what to flag.

### 5. Good Pattern Examples
Show what correct code looks like.

### 6. Output Format
Specify the exact JSON structure expected.

## Testing Prompts

To test prompt changes:

1. Run the function locally
2. Create a test PR with known issues
3. Verify the AI catches expected problems
4. Adjust prompt wording as needed

## Tips for Effective Prompts

1. **Be Specific**: Vague instructions lead to vague reviews
2. **Show Examples**: The AI learns from examples better than rules
3. **Balance Coverage**: Too many rules can dilute focus
4. **Test Iteratively**: Small changes, test, repeat
5. **Consider Context**: Remember the AI sees code in isolation

## Prompt Token Limits

Keep prompts concise to leave room for code:

- System prompt: ~1,000-2,000 tokens recommended
- User prompt (code): Up to 100,000 tokens with gpt-4o
- Response: Limited by `MaxTokens` setting (default: 4,096)

Large prompts reduce the amount of code that can be reviewed in a single request.
