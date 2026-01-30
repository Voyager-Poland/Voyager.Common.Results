# ADR-0004: Circuit Breaker Pattern for Resilience

## Status

**Proposed** - January 2026

## Context

While the core `Voyager.Common.Results` library provides lightweight retry functionality for transient failures, distributed systems also need protection against cascading failures. A circuit breaker pattern detects when a dependency is unhealthy and fails fast without overloading it.

### Failure Modes

**Scenario 1: Without Circuit Breaker**
```
Request 1 → Service down → Retry 3x → Delay 1s + 2s + 4s = 7s total
Request 2 → Service down → Retry 3x → Delay 7s
Request 3 → Service down → Retry 3x → Delay 7s
... (all waiting, exhausting thread pool)
→ Cascading failure across entire system
```

**Scenario 2: With Circuit Breaker**
```
Request 1 → Service down → Retry 3x → Failure
Request 2 → Service down → Retry 3x → Failure
Request 3 → Service down → Failure immediately (circuit OPEN)
Request 4 → Service down → Failure immediately
... (fail fast, recover thread pool, allow service recovery)
Request N → Service recovered → Try again (circuit HALF-OPEN)
→ System degrades gracefully
```

### Design Questions

1. **Separate package or core?**
   - Core library is pure functional (Result<T>)
   - Circuit breaker requires **shared mutable state** (failure counters, timers)
   - Different scope and maintenance burden
   
2. **Thread safety?**
   - Retry doesn't need shared state (stateless policy per call)
   - Circuit breaker **requires** thread-safe access (concurrent requests evaluate same circuit)
   - Must handle multiple threads accessing circuit state simultaneously

3. **State Management?**
   - Simple retry: function local (in-memory, per operation)
   - Circuit breaker: application-level (tracks failures across all requests)

## Decision

**Create separate package `Voyager.Common.Resilience`** with:

### 1. Architecture
```
src/
├── Voyager.Common.Results/          # Pure functional core
│   └── Extensions/
│       └── ResultRetryExtensions.cs
│
└── Voyager.Common.Resilience/       # Stateful infrastructure ← NEW
    ├── CircuitBreakerPolicy.cs
    ├── CircuitBreakerState.cs
    └── Extensions/
        └── ResultCircuitBreakerExtensions.cs
```

### 2. Implementation Strategy

**CircuitBreakerState:** Three-state model

```csharp
public enum CircuitBreakerState
{
    Closed,      // Normal operation, requests flow through
    Open,        // Too many failures, reject requests immediately
    HalfOpen     // Testing if service recovered, allow limited requests
}

public class CircuitBreakerPolicy
{
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private int _failureCount = 0;
    private DateTime _lastFailureTime;
    private readonly SemaphoreSlim _stateLock = new SemaphoreSlim(1, 1); // Thread safety
    
    // Configuration
    private readonly int _failureThreshold;          // Failures before Open
    private readonly TimeSpan _timeout;              // Duration in Open state
    private readonly int _halfOpenMaxAttempts = 1;   // Requests to allow in HalfOpen
}
```

**State Transitions:**

```
CLOSED
  ↓ (failure_count >= threshold)
OPEN (reject all requests immediately)
  ↓ (timeout expires)
HALF_OPEN (allow 1 request to test recovery)
  ↓ (request succeeds)
CLOSED (recovered!)
  ↓ (request fails)
OPEN (service still unhealthy)
```

### 3. Thread Safety

Use `SemaphoreSlim` for async-safe locking:

```csharp
public async Task<Result<int>> EvaluateAsync(Error error)
{
    await _stateLock.WaitAsync(); // Acquire lock
    try
    {
        // Update state atomically
        _failureCount++;
        if (_failureCount >= _failureThreshold)
            _state = CircuitBreakerState.Open;
    }
    finally
    {
        _stateLock.Release();
    }
}
```

**Why not `lock` statement?**
- `lock` is sync-only (blocks thread)
- `SemaphoreSlim` works with async (integrates with Task scheduler)
- Follows ConfigureAwait(false) pattern from core library

### 4. API Design

```csharp
// Factory
var breaker = new CircuitBreakerPolicy(
    failureThreshold: 5,           // Open circuit after 5 failures
    timeout: TimeSpan.FromSeconds(30) // Reset after 30s
);

// Usage
var result = await GetUser(id)
    .BindWithCircuitBreakerAsync(
        user => FetchDetails(user),
        breaker
    );

// Real-world: Combine Retry + Circuit Breaker
var result = await GetUser(id)
    .BindWithRetryAsync(
        user => FetchDetails(user),
        RetryPolicies.TransientErrors(maxAttempts: 3)
    )
    .BindWithCircuitBreakerAsync(
        user => SaveToCache(user),
        breaker
    );
```

### 5. Error Preservation

**CRITICAL**: Circuit breaker must distinguish why request failed:

```csharp
// ❌ BAD - loses context
if (_state == CircuitBreakerState.Open)
    return Result<T>.Failure(Error.Unavailable("Circuit breaker open"));

// ✅ GOOD - preserves original error
if (_state == CircuitBreakerState.Open && lastError != null)
    return Result<T>.Failure(lastError);
else if (lastError == null)
    return Result<T>.Failure(Error.Unavailable("Circuit breaker open - no prior error"));
```

## Rationale

### Why Separate Package?

1. **Scope Clarity**: Core = functional, Resilience = stateful infrastructure
2. **Dependency Control**: Users opt-in to resilience patterns
3. **Independent Versioning**: Circuit breaker changes don't version-bump core
4. **Reduced Complexity**: Core library remains lightweight

### Why NOT Polly?

While Polly is powerful, it doesn't align with Result<T> philosophy:
- Polly is exception-centric; we're functional
- Our API is more ergonomic: `.BindWithCircuitBreakerAsync(op, policy)`
- Polly's HandleResult feels awkward with Result<T>
- We control the exact behavior

### Why SemaphoreSlim?

- Thread-safe for concurrent requests
- Async-aware (integrates with async/await)
- Prevents context switches (vs `lock` statement)
- Matches library's async-first philosophy

## Consequences

### Positive

- ✅ Separation of concerns: Core library remains pure, focused
- ✅ Explicit shared state management (SemaphoreSlim)
- ✅ Zero external dependencies in core library
- ✅ Thread-safe by design for distributed systems
- ✅ Clear state model (Closed/Open/HalfOpen)
- ✅ Complements retry for comprehensive resilience
- ✅ Error context preserved for debugging

### Negative

- ❌ Circuit breaker state is global (per instance)
- ❌ More moving parts for users to understand
- ❌ Requires testing with concurrent scenarios
- ❌ Shared state can hide bugs (testing challenges)

### Mitigated

- Thread safety through SemaphoreSlim
- Comprehensive test coverage (multi-threaded scenarios)
- Clear documentation with examples
- State transitions well-defined and testable

## Implementation Plan

1. **New Project** `Voyager.Common.Resilience`
   - Same multi-framework targets (net48, net6.0, net8.0)
   - Dependency: `Voyager.Common.Results` (NuGet reference)

2. **Types**
   - `CircuitBreakerState` enum
   - `CircuitBreakerPolicy` class
   - `ICircuitBreakerObserver` interface (optional, for metrics)

3. **Extension Methods**
   - `BindWithCircuitBreakerAsync<T>(result, func, policy)`
   - `BindWithCircuitBreakerAsync<T>(resultTask, func, policy)`
   - Overloads with default configuration

4. **Tests** (xUnit, same structure)
   - State transition tests
   - Multi-threaded scenarios
   - Timeout behavior
   - Error preservation

5. **Documentation**
   - README with examples
   - Architecture diagram
   - When to use (patterns)
   - Real-world scenarios

## Future Considerations

- **Metrics/Observability**: ICircuitBreakerObserver for monitoring
- **Reset Strategy**: Customizable half-open behavior
- **Bulkhead Isolation**: Separate circuits per dependency
- **Polly Bridge**: Optional integration layer for Polly users
- **Timeout Policies**: Combine circuit breaker with timeout

## References

- [Circuit Breaker Pattern - Chris Richardson](https://microservices.io/patterns/reliability/circuit-breaker.html)
- [SemaphoreSlim for Async Locks - Stephen Cleary](https://blog.stephencleary.com/2015/02/async-oop-1-constructors.html)
- [Release It! - Michael Nygard](https://pragprog.com/titles/mnee2/release-it-second-edition/)
- ADR-0003: Retry Extensions
- ADR-0001: ConfigureAwait Convention
