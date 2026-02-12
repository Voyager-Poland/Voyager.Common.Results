# ADR-0005: Klasyfikacja Błędów dla Wzorców Resilience

**Status:** Zaakceptowano
**Data:** 2026-01-31
**Kontekst:** Voyager.Common.Proxy (ADR-007, ADR-008)

## Problem

Biblioteka `Voyager.Common.Proxy` potrzebuje klasyfikować błędy, aby:

1. **Retry logic** - wiedzieć, które błędy są przejściowe (warto ponawiać)
2. **Circuit Breaker** - wiedzieć, które błędy liczyć do progu otwarcia
3. **HTTP mapping** - mapować `ErrorType` na kody HTTP po stronie serwera

Obecnie brak jest metod klasyfikacji na poziomie `ErrorType`, co wymusza duplikację logiki.

## Aktualny stan ErrorType

Istniejące wartości w `ErrorType`:
- `None`, `Validation`, `Permission`, `Unauthorized`, `Database`, `Business`
- `NotFound`, `Conflict`, `Unavailable`, `Timeout`, `Cancelled`, `Unexpected`
- `CircuitBreakerOpen`

## Decyzja

### 1. Nowa wartość ErrorType

```csharp
public enum ErrorType
{
    // ... istniejące wartości ...

    /// <summary>
    /// Rate limiting - too many requests (HTTP 429)
    /// </summary>
    TooManyRequests
}
```

> **Decyzja:** Nie dodajemy `NotImplemented` - brak konkretnego use case w projekcie.

### 2. Extension methods - ErrorTypeExtensions.cs

```csharp
namespace Voyager.Common.Results
{
    /// <summary>
    /// Extension methods for ErrorType classification.
    /// </summary>
    public static class ErrorTypeExtensions
    {
        /// <summary>
        /// Transient errors - client MAY retry with exponential backoff.
        /// Circuit breaker SHOULD count these toward failure threshold.
        /// </summary>
        public static bool IsTransient(this ErrorType errorType)
        {
            return errorType is
                ErrorType.Timeout or
                ErrorType.Unavailable or
                ErrorType.CircuitBreakerOpen or
                ErrorType.TooManyRequests;
        }

        /// <summary>
        /// Business errors - client should NOT retry.
        /// Circuit breaker should IGNORE these.
        /// </summary>
        public static bool IsBusinessError(this ErrorType errorType)
        {
            return errorType is
                ErrorType.Validation or
                ErrorType.Business or
                ErrorType.NotFound or
                ErrorType.Unauthorized or
                ErrorType.Permission or
                ErrorType.Conflict or
                ErrorType.Cancelled;
        }

        /// <summary>
        /// Infrastructure errors - client should NOT retry.
        /// Circuit breaker SHOULD count these toward failure threshold.
        /// </summary>
        public static bool IsInfrastructureError(this ErrorType errorType)
        {
            return errorType is
                ErrorType.Database or
                ErrorType.Unexpected;
        }

        /// <summary>
        /// Should circuit breaker count this error toward failure threshold?
        /// CircuitBreakerOpen is excluded - it's a protection mechanism, not a real failure.
        /// </summary>
        public static bool ShouldCountForCircuitBreaker(this ErrorType errorType)
        {
            // CircuitBreakerOpen is transient (can retry later) but should NOT
            // count toward circuit breaker threshold - it's already a protection mechanism
            if (errorType == ErrorType.CircuitBreakerOpen)
                return false;

            return IsTransient(errorType) || IsInfrastructureError(errorType);
        }

        /// <summary>
        /// Should the operation be retried?
        /// </summary>
        public static bool ShouldRetry(this ErrorType errorType)
        {
            return IsTransient(errorType);
        }

        /// <summary>
        /// Maps ErrorType to HTTP status code.
        /// </summary>
        public static int ToHttpStatusCode(this ErrorType errorType)
        {
            return errorType switch
            {
                ErrorType.None => 200,
                ErrorType.Validation => 400,
                ErrorType.Business => 400,
                ErrorType.Unauthorized => 401,
                ErrorType.Permission => 403,
                ErrorType.NotFound => 404,
                ErrorType.Conflict => 409,
                ErrorType.Cancelled => 499,
                ErrorType.TooManyRequests => 429,
                ErrorType.Timeout => 504,
                ErrorType.Unavailable => 503,
                ErrorType.CircuitBreakerOpen => 503,
                ErrorType.Database => 500,
                ErrorType.Unexpected => 500,
                _ => 500
            };
        }
    }
}
```

### 3. Factory methods dla TooManyRequests

```csharp
// W klasie Error - dodać:

public static Error TooManyRequestsError(string code, string message) =>
    new(ErrorType.TooManyRequests, code, message);

public static Error TooManyRequestsError(string message) =>
    new(ErrorType.TooManyRequests, "RateLimit.Exceeded", message);
```

## Klasyfikacja błędów

| ErrorType | HTTP | Klasyfikacja | Retry? | CB liczy? |
|-----------|------|--------------|--------|-----------|
| `Validation` | 400 | Business | ❌ | ❌ |
| `Business` | 400 | Business | ❌ | ❌ |
| `Unauthorized` | 401 | Business | ❌ | ❌ |
| `Permission` | 403 | Business | ❌ | ❌ |
| `NotFound` | 404 | Business | ❌ | ❌ |
| `Conflict` | 409 | Business | ❌ | ❌ |
| `Cancelled` | 499 | Business | ❌ | ❌ |
| `TooManyRequests` | 429 | Transient | ✅ | ✅ |
| `Timeout` | 504 | Transient | ✅ | ✅ |
| `Unavailable` | 503 | Transient | ✅ | ✅ |
| `CircuitBreakerOpen` | 503 | Transient | ✅ | ❌ * |
| `Database` | 500 | Infrastructure | ❌ | ✅ |
| `Unexpected` | 500 | Infrastructure | ❌ | ✅ |

> \* `CircuitBreakerOpen` jest transient (można retry), ale NIE liczy się do progu CB - to mechanizm ochronny, nie prawdziwy błąd.

## Testy jednostkowe

```csharp
public class ErrorTypeExtensionsTests
{
    [Theory]
    [InlineData(ErrorType.Timeout, true)]
    [InlineData(ErrorType.Unavailable, true)]
    [InlineData(ErrorType.CircuitBreakerOpen, true)]
    [InlineData(ErrorType.TooManyRequests, true)]
    [InlineData(ErrorType.Validation, false)]
    [InlineData(ErrorType.Database, false)]
    public void IsTransient_ReturnsCorrectValue(ErrorType type, bool expected)
    {
        type.IsTransient().Should().Be(expected);
    }

    [Theory]
    [InlineData(ErrorType.Validation, true)]
    [InlineData(ErrorType.NotFound, true)]
    [InlineData(ErrorType.Timeout, false)]
    [InlineData(ErrorType.Database, false)]
    public void IsBusinessError_ReturnsCorrectValue(ErrorType type, bool expected)
    {
        type.IsBusinessError().Should().Be(expected);
    }

    [Theory]
    [InlineData(ErrorType.Timeout, true)]
    [InlineData(ErrorType.Database, true)]
    [InlineData(ErrorType.Validation, false)]
    [InlineData(ErrorType.CircuitBreakerOpen, false)] // Protection mechanism, not a real failure
    public void ShouldCountForCircuitBreaker_ReturnsCorrectValue(ErrorType type, bool expected)
    {
        type.ShouldCountForCircuitBreaker().Should().Be(expected);
    }

    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Timeout, 504)]
    [InlineData(ErrorType.Unavailable, 503)]
    [InlineData(ErrorType.TooManyRequests, 429)]
    public void ToHttpStatusCode_ReturnsCorrectCode(ErrorType type, int expected)
    {
        type.ToHttpStatusCode().Should().Be(expected);
    }
}
```

## Implementacja

- [x] Dodać `TooManyRequests` do `ErrorType`
- [x] Utworzyć `ErrorTypeExtensions.cs`
- [x] Dodać factory methods `TooManyRequestsError` do `Error`
- [x] Testy jednostkowe
- [ ] Wydać jako wersję 1.7.0

## Wpływ na Voyager.Common.Proxy

```csharp
// Przed - duplikacja logiki
if (error.Type == ErrorType.Timeout || error.Type == ErrorType.Unavailable || ...)

// Po - czytelne i DRY
if (error.Type.IsTransient())
if (error.Type.ShouldCountForCircuitBreaker())
var statusCode = error.Type.ToHttpStatusCode();
```

---

**Powiązane:**
- [ADR-0003: Retry Extensions](./ADR-0003-retry-extensions-for-transient-failures.md)
- [ADR-0004: Circuit Breaker Pattern](./ADR-0004-circuit-breaker-pattern-for-resilience.md)
