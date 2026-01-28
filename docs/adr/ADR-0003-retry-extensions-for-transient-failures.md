# ADR-0003: Retry Extensions for Transient Failures

## Status

**Proposed** - January 2026

## Context

Users of the library frequently need to handle transient failures (network timeouts, temporary service unavailability) in operations that return `Result<T>`. The question arose whether to add retry functionality to the core library.

### Initial Proposal

```csharp
// Initial idea - hardcoded retry count
public static async Task<Result<TOut>> BindWithRetry<TIn, TOut>(
    this Result<TIn> result,
    Func<TIn, Task<Result<TOut>>> func,
    int maxAttempts = 3)
```

### Considerations

1. **Polly Integration**: Polly is the de-facto standard for resilience in .NET, but:
   - Polly is exception-centric (`Policy.Handle<Exception>()`)
   - `Result<T>` eliminates exceptions by design (functional error handling)
   - Polly does support `HandleResult<T>`, but adds heavy dependency for simple scenarios

2. **Scope Question**: Is retry an "operator" (pure transformation) or "infrastructure concern"?
   - Operators: `Map`, `Bind`, `Tap` - pure transformations on Result
   - Retry: Controls execution flow, introduces delays, manages state

3. **Error Context**: Critical requirement - retry logic MUST preserve original error context, not replace it with generic "max retries exceeded" messages.

## Decision

**Add minimal retry functionality to the library** with the following constraints:

### 1. Separate Extension File
Create `ResultRetryExtensions.cs` - clearly separated from pure operators in `TaskResultExtensions.cs`.

### 2. Policy Delegate Pattern
Use a delegate-based policy instead of hardcoded parameters:

```csharp
public delegate Result<int> RetryPolicy(int attemptNumber, Error error);
```

**Why delegate over interface:**
- Zero allocation (no object creation)
- Functional style (matches library philosophy)
- Simple composition with factory methods
- No dependencies

### 3. Preserve Original Errors
**CRITICAL**: Retry logic MUST return the original error from the last attempt, never replace it:

```csharp
// ✅ CORRECT - preserves error context
if (retryDecision.IsFailure) return lastOutcome; // Original error

// ❌ WRONG - destroys context
return Result<T>.Failure(Error.UnexpectedError("Max retries exceeded"));
```

**Rationale**: Users need to know the actual failure cause (e.g., "Database connection timeout"), not cryptic infrastructure messages.

### 4. Default to Transient Errors Only
The default policy retries only:
- `ErrorType.Unavailable` - Service temporarily down, network issues, deadlocks
- `ErrorType.Timeout` - Operation exceeded time limit

**Not retried by default:**
- `Validation`, `NotFound`, `Permission` - Permanent failures
- `Unexpected` - Needs circuit breaker, not retry

### 5. Exponential Backoff Default
Default strategy uses exponential backoff to prevent overwhelming failing services:
- Attempt 1: baseDelay (1000ms)
- Attempt 2: 2 × baseDelay (2000ms)
- Attempt 3: 4 × baseDelay (4000ms)

## Implementation

```csharp
public static class ResultRetryExtensions
{
    /// <summary>
    /// Executes operation with retry logic for transient failures.
    /// Returns the ORIGINAL error from the last attempt (preserves error context).
    /// </summary>
    /// <remarks>
    /// For circuit breaker patterns or advanced resilience, consider Polly or a separate library.
    /// This is a lightweight solution for simple transient failure scenarios.
    /// </remarks>
    public static async Task<Result<TOut>> BindWithRetryAsync<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Task<Result<TOut>>> func,
        RetryPolicy policy)
    {
        if (result.IsFailure) return Result<TOut>.Failure(result.Error);

        int attempt = 1;
        Result<TOut> lastOutcome = default!;
        
        while (true)
        {
            lastOutcome = await func(result.Value).ConfigureAwait(false);
            
            if (lastOutcome.IsSuccess) return lastOutcome;
            
            var retryDecision = policy(attempt, lastOutcome.Error);
            
            if (retryDecision.IsFailure) return lastOutcome; // Preserve original error
            
            await Task.Delay(retryDecision.Value).ConfigureAwait(false);
            attempt++;
        }
    }
}

public static class RetryPolicies
{
    /// <summary>
    /// Retry only transient errors (Unavailable, Timeout) with exponential backoff.
    /// This is the recommended default for most scenarios.
    /// </summary>
    public static RetryPolicy TransientErrors(int maxAttempts = 3, int baseDelayMs = 1000)
    {
        return (attempt, error) =>
        {
            bool isTransient = error.Type == ErrorType.Unavailable 
                            || error.Type == ErrorType.Timeout;
            
            if (attempt >= maxAttempts || !isTransient)
                return Result<int>.Failure(error); // Signal to stop
            
            int delayMs = baseDelayMs * (int)Math.Pow(2, attempt - 1);
            return Result<int>.Success(delayMs);
        };
    }
    
    public static RetryPolicy Default() => TransientErrors();
}
```

## Rationale

### Why Add to Core Library

1. **Common Use Case**: Transient failures are extremely common in distributed systems
2. **Zero Dependencies**: Implementation requires only `Task.Delay` (already used in tests)
3. **Functional Style**: Delegate pattern matches library philosophy
4. **Polly Mismatch**: Polly's exception-centric model conflicts with Result<T> design

### Why NOT Use Polly

- Polly adds significant dependency weight for simple retry scenarios
- `HandleResult<T>` exists but is less ergonomic than exception handling
- Users wanting advanced features (circuit breaker, bulkhead) can still integrate Polly separately

### Why Policy Delegate vs Parameters

Rejected alternatives:
```csharp
// ❌ Inflexible - can't customize delay strategy
BindWithRetry(..., int maxAttempts = 3)

// ❌ Too many parameters - API bloat
BindWithRetry(..., int maxAttempts, Func<Error, bool> shouldRetry, Func<int, int> delayStrategy)

// ✅ Flexible - compose policies with factory methods
BindWithRetry(..., RetryPolicy policy)
```

## Consequences

### Positive
- ✅ Users can handle transient failures without external dependencies
- ✅ Error context is always preserved (critical for debugging)
- ✅ Functional style matches library philosophy
- ✅ Exponential backoff prevents service overload
- ✅ Explicit - retry is visible in code, not hidden
- ✅ Follows ConfigureAwait(false) convention (ADR-0001)

### Negative
- ❌ Adds `Task.Delay` dependency (but already used in async extensions)
- ❌ Scope creep - retry is infrastructure, not pure operator
- ❌ No circuit breaker (but documented as out-of-scope)
- ❌ Potential infinite loop if policy is badly written (mitigated by factory methods)

### Mitigated
- Circuit breaker explicitly documented as separate concern
- Default policies prevent common mistakes
- Documentation guides users to Polly for advanced scenarios
- Policy delegate allows custom strategies without API changes

## Alternatives Considered

### 1. No Retry Support
**Rejected**: Too common a use case, forces every consumer to implement their own.

### 2. Separate NuGet Package
**Rejected**: Overkill for such minimal functionality. Consider if circuit breaker is added.

### 3. Interface-Based Policy
**Rejected**: Delegate is simpler, zero allocation, more functional.

### 4. Error Wrapping
**Rejected**: Destroys error context, violates Railway-Oriented Programming principles.

## Documentation Requirements

1. **README.md**: Add retry example in "Async Operations" section
2. **docs/async-operations.md**: Dedicated retry section with examples
3. **XML Comments**: Emphasize error preservation, reference Polly for advanced scenarios
4. **Tests**: Cover transient/permanent errors, max attempts, exponential backoff, error preservation

## References

- [Transient Fault Handling - Microsoft Docs](https://docs.microsoft.com/en-us/azure/architecture/best-practices/transient-faults)
- [Polly - Resilience Framework](https://github.com/App-vNext/Polly)
- [Railway Oriented Programming - Scott Wlaschin](https://fsharpforfunandprofit.com/rop/)
- ADR-0001: ConfigureAwait Convention
