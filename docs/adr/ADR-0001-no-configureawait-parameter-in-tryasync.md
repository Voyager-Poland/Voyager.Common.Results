# ADR-0001: No ConfigureAwait Parameter in TryAsync

## Status

**Accepted** - December 2024

## Context

When implementing `TryAsync` methods with `CancellationToken` support, we considered adding an optional `continueOnCapturedContext` parameter to allow callers to control `ConfigureAwait` behavior:

```csharp
// Considered but rejected:
public static async Task<Result<TValue>> TryAsync<TValue>(
    Func<CancellationToken, Task<TValue>> func,
    CancellationToken cancellationToken,
    bool continueOnCapturedContext = false)  // ❌ Not added
```

## Decision

**We decided NOT to add `continueOnCapturedContext` parameter to `TryAsync` methods.**

All `TryAsync` implementations use hardcoded `ConfigureAwait(false)`.

## Rationale

### 1. Library Best Practice
Microsoft guidelines state that library code should **always** use `ConfigureAwait(false)` to:
- Avoid deadlocks in synchronization contexts (WinForms, WPF, ASP.NET)
- Improve performance by avoiding unnecessary context switches

### 2. Consumer Controls Their Own Context
Users who need to return to a specific synchronization context can do so **after** calling `TryAsync`:

```csharp
// Library internally uses ConfigureAwait(false) - correct
var result = await Result<string>.TryAsync(
    async ct => await httpClient.GetStringAsync(url, ct),
    cancellationToken);

// Consumer controls their own context AFTER the call
await UpdateUIAsync(result).ConfigureAwait(true);
```

### 3. API Complexity
Adding the parameter would create 8 additional overloads (4 existing × 2 boolean options), significantly complicating the API:

| Without parameter | With parameter |
|-------------------|----------------|
| 4 overloads | 8+ overloads |
| Clear intent | Confusing options |

### 4. Principle of Least Surprise
Developers expect library async methods to use `ConfigureAwait(false)`. Adding a parameter that defaults to `false` but can be `true` introduces unexpected behavior.

## Consequences

### Positive
- ✅ Simpler API surface
- ✅ Consistent behavior across all `TryAsync` overloads
- ✅ Follows .NET library conventions
- ✅ No deadlock risk from library code

### Negative
- ❌ Users cannot force context capture from within `TryAsync`
- ❌ Requires additional `ConfigureAwait(true)` call if context needed

### Mitigated
The negative consequences are mitigated because:
1. Context capture is rarely needed in library consumers
2. When needed, it's explicit and visible in consumer code
3. This matches behavior of all .NET BCL async methods

## References

- [ConfigureAwait FAQ - Stephen Toub](https://devblogs.microsoft.com/dotnet/configureawait-faq/)
- [CA2007: Do not directly await a Task](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2007)
- [Async/Await Best Practices](https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)