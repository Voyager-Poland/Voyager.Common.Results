# Voyager.Common.Resilience

[![NuGet](https://img.shields.io/nuget/v/Voyager.Common.Resilience.svg)](https://www.nuget.org/packages/Voyager.Common.Resilience/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Voyager.Common.Resilience.svg)](https://www.nuget.org/packages/Voyager.Common.Resilience/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Resilience patterns for Railway Oriented Programming** - Advanced failure handling extensions for [Voyager.Common.Results](https://www.nuget.org/packages/Voyager.Common.Results/).

Prevent cascading failures and improve system reliability with the **Circuit Breaker pattern** integrated into the Result monad.

**Supports .NET Framework 4.8, .NET 6.0, and .NET 8.0** 🚀

## ✨ Features

- 🛡️ **Circuit Breaker Pattern** - Prevent cascading failures in distributed systems
- 🧵 **Thread-safe** - Built with SemaphoreSlim for safe async operations
- 🎯 **3-State Model** - Closed, Open, HalfOpen with automatic transitions
- 📝 **Error Context Preservation** - CircuitBreakerOpenError includes last failure details
- ⚡ **Result&lt;T&gt; Integration** - Seamless Railway Oriented Programming
- 🔧 **Configurable Policies** - Failure thresholds, timeouts, recovery attempts
- 📦 **Minimal Dependencies** - Only depends on Voyager.Common.Results
- 🧪 **Fully Tested** - Comprehensive test coverage across all target frameworks

## 📦 Installation

```bash
# Install both packages
dotnet add package Voyager.Common.Results
dotnet add package Voyager.Common.Resilience
```

## 🚀 Quick Start

```csharp
using Voyager.Common.Results;
using Voyager.Common.Resilience;

// Create a circuit breaker policy
var circuitBreaker = new CircuitBreakerPolicy(
    failureThreshold: 5,                    // Open after 5 consecutive failures
    openTimeout: TimeSpan.FromSeconds(30),  // Stay open for 30 seconds
    halfOpenMaxAttempts: 3                  // Allow 3 recovery test attempts
);

// Execute operations through the circuit breaker
var result = await GetUser(userId)
    .BindWithCircuitBreakerAsync(
        user => CallExternalApiAsync(user),
        circuitBreaker
    );

// Handle results including circuit breaker state
var message = result.Match(
    onSuccess: data => $"Success: {data}",
    onFailure: error => error.Type == ErrorType.CircuitBreakerOpen
        ? "Service temporarily unavailable - circuit breaker is open"
        : $"Error: {error.Message}"
);
```

## 🎯 Circuit Breaker States

The circuit breaker implements a 3-state model:

### 🟢 Closed (Normal Operation)
- All requests flow through to the protected operation
- Failures are counted
- When failure threshold is reached → transitions to **Open**

### 🔴 Open (Failing Fast)
- Requests immediately fail with `ErrorType.CircuitBreakerOpen`
- No calls are made to the protected operation (prevents cascading failures)
- After `openTimeout` → transitions to **HalfOpen**

### 🟡 HalfOpen (Testing Recovery)
- Limited number of test requests are allowed (`halfOpenMaxAttempts`)
- If all test requests succeed → transitions to **Closed**
- If any test request fails → transitions back to **Open**

## 🔧 Configuration Options

```csharp
var policy = new CircuitBreakerPolicy(
    failureThreshold: 10,                   // Number of failures before opening
    openTimeout: TimeSpan.FromMinutes(1),   // How long to stay open
    halfOpenMaxAttempts: 5                  // Test attempts in half-open state
);
```

**Best Practices:**
- **failureThreshold**: 3-10 for most scenarios (lower = more sensitive)
- **openTimeout**: 30-60 seconds for typical services (longer for slow recovery)
- **halfOpenMaxAttempts**: 3-5 attempts (enough to verify recovery without overload)

## 📖 Usage Examples

### Basic Circuit Breaker

```csharp
var policy = new CircuitBreakerPolicy(
    failureThreshold: 5,
    openTimeout: TimeSpan.FromSeconds(30),
    halfOpenMaxAttempts: 3
);

// Sync function
var result = await GetUserId()
    .BindWithCircuitBreakerAsync(
        id => _externalService.GetUserData(id),
        policy
    );

// Async function
var result = await GetUserIdAsync()
    .BindWithCircuitBreakerAsync(
        id => _externalService.GetUserDataAsync(id),
        policy
    );
```

### With Async Result Chains

```csharp
var result = await ValidateRequestAsync(request)
    .BindAsync(req => AuthenticateAsync(req))
    .BindWithCircuitBreakerAsync(
        user => _externalApi.FetchDataAsync(user),
        circuitBreaker
    )
    .MapAsync(data => ProcessData(data));
```

### Monitoring Circuit State

```csharp
// Check current state
switch (policy.State)
{
    case CircuitBreakerState.Closed:
        _logger.LogInfo("Circuit healthy");
        break;
    case CircuitBreakerState.Open:
        _logger.LogWarning("Circuit open - service degraded");
        break;
    case CircuitBreakerState.HalfOpen:
        _logger.LogInfo("Circuit testing recovery");
        break;
}

// Manual reset if needed (e.g., after manual intervention)
policy.Reset();
```

### Error Handling

```csharp
var result = await operation.BindWithCircuitBreakerAsync(CallServiceAsync, policy);

result.Switch(
    onSuccess: data => Console.WriteLine($"Success: {data}"),
    onFailure: error =>
    {
        if (error.Type == ErrorType.CircuitBreakerOpen)
        {
            // Circuit breaker is open
            var cbError = error; // Contains last failure in message
            _logger.LogWarning($"Circuit open: {error.Message}");
            
            // Implement fallback behavior
            return GetCachedData();
        }
        else
        {
            // Other error types
            _logger.LogError($"Operation failed: {error.Message}");
        }
    }
);
```

### Combining with Retry

```csharp
using Voyager.Common.Results.Extensions;

// Retry for transient failures + Circuit Breaker for cascading failures
var result = await GetConnectionAsync()
    .BindWithRetryAsync(
        conn => ExecuteQueryAsync(conn),
        RetryPolicies.TransientErrors(maxAttempts: 3)
    )
    .BindWithCircuitBreakerAsync(
        data => CallDownstreamServiceAsync(data),
        circuitBreaker
    );
```

## 🏗️ Architecture

The Resilience library is designed as a separate package to:
- ✅ Keep core Results library dependency-free
- ✅ Allow independent versioning of resilience patterns
- ✅ Enable optional adoption (use only what you need)
- ✅ Future-proof for additional patterns (Bulkhead, RateLimiter, etc.)

See [ADR-0004](../../docs/adr/ADR-0004-circuit-breaker-pattern-for-resilience.md) for architectural rationale.

## 🔗 Related Packages

- **[Voyager.Common.Results](https://www.nuget.org/packages/Voyager.Common.Results/)** - Core Result pattern library (required dependency)
- **[Polly](https://www.nuget.org/packages/Polly/)** - More advanced resilience library (if you need Bulkhead, Rate Limiter, etc.)

## 📚 Documentation

- [Main Documentation](../../README.md) - Voyager.Common.Results overview
- [ADR-0004](../../docs/adr/ADR-0004-circuit-breaker-pattern-for-resilience.md) - Circuit Breaker design decisions
- [CHANGELOG](../../CHANGELOG.md) - Version history

## 🤝 Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](../../CONTRIBUTING.md).

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](../../LICENSE) file for details.

## 🔖 Version History

See [CHANGELOG.md](../../CHANGELOG.md) for version history and release notes.
