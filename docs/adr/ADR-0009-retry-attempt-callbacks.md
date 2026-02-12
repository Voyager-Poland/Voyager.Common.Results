# ADR-0009: Callbacki prób retry

**Status:** Zaakceptowano
**Data:** 2026-01-31
**Kontekst:** Voyager.Common.Proxy (ADR-008 Diagnostics Strategy)

## Problem

`ResultRetryExtensions.BindWithRetryAsync()` wykonuje retry wewnętrznie, ale **nie powiadamia nikogo** o próbach:

```csharp
// Obecnie - próba retry jest niewidoczna dla obserwatorów
var result = await operation.BindWithRetryAsync(
    async value => await HttpCallAsync(value),
    RetryPolicies.TransientErrors(maxAttempts: 3));
// Ile było prób? Jakie błędy? Ile trwały? Nieznane.
```

**Konsumenci potrzebują wiedzieć o próbach retry, aby:**
1. Logować zdarzenia (`Retry attempt 2/3 for UserService after 408 Timeout`)
2. Monitorować metryki (liczba retries, success rate po N próbach)
3. Debugować problemy (dlaczego operacja trwała tak długo?)
4. Alertować przy nadmiernych retries (symptom degradacji serwisu)

**Obecne obejście w Voyager.Common.Proxy:**

```csharp
// Wrapper wokół całej operacji - nie widzi pojedynczych prób
var stopwatch = Stopwatch.StartNew();
var result = await operation.BindWithRetryAsync(func, policy);
_logger.LogInformation("Operation completed in {Elapsed}", stopwatch.Elapsed);
// Nie wiemy: ile prób, jakie błędy, ile delay między próbami
```

## Decyzja

Dodać przeciążenie `BindWithRetryAsync` z callbackiem `onRetryAttempt`.

### API

```csharp
public static class ResultRetryExtensions
{
    /// <summary>
    /// Callback invoked before each retry attempt (after failure, before delay).
    /// </summary>
    /// <remarks>
    /// Parameters: (attemptNumber, error, delayMs)
    /// - attemptNumber: 1-based number of the attempt that just failed
    /// - error: The error from the failed attempt
    /// - delayMs: Delay before next attempt (0 if no more retries)
    /// </remarks>
    public static async Task<Result<TOut>> BindWithRetryAsync<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Task<Result<TOut>>> func,
        RetryPolicy policy,
        Action<int, Error, int>? onRetryAttempt = null)
}
```

### Użycie

```csharp
var result = await operation.BindWithRetryAsync(
    async value => await _httpClient.GetAsync(value),
    RetryPolicies.TransientErrors(maxAttempts: 3),
    onRetryAttempt: (attempt, error, delayMs) =>
    {
        _logger.LogWarning(
            "Retry attempt {Attempt} failed with {ErrorType}: {Message}. Waiting {Delay}ms before next attempt.",
            attempt, error.Type, error.Message, delayMs);

        _metrics.IncrementRetryCounter(error.Type.ToString());
    });
```

### Implementacja

```csharp
public static async Task<Result<TOut>> BindWithRetryAsync<TIn, TOut>(
    this Result<TIn> result,
    Func<TIn, Task<Result<TOut>>> func,
    RetryPolicy policy,
    Action<int, Error, int>? onRetryAttempt = null)
{
    if (result.IsFailure) return Result<TOut>.Failure(result.Error);

    int attempt = 1;
    Result<TOut> lastOutcome = default!;

    while (true)
    {
        lastOutcome = await func(result.Value).ConfigureAwait(false);

        if (lastOutcome.IsSuccess) return lastOutcome;

        var retryDecision = policy(attempt, lastOutcome.Error);

        if (retryDecision.IsFailure)
        {
            // No more retries - notify with delayMs=0
            onRetryAttempt?.Invoke(attempt, lastOutcome.Error, 0);
            return lastOutcome; // Preserve original error
        }

        var delayMs = retryDecision.Value;

        // Notify about retry before delay
        onRetryAttempt?.Invoke(attempt, lastOutcome.Error, delayMs);

        await Task.Delay(delayMs).ConfigureAwait(false);
        attempt++;
    }
}
```

## Alternatywy rozważone

### Alternatywa 1: Osobny interfejs IRetryObserver

```csharp
public interface IRetryObserver
{
    void OnAttemptFailed(int attempt, Error error, int delayMs);
    void OnRetryExhausted(int totalAttempts, Error finalError);
}
```

**Odrzucona:**
- Wymaga nowego interfejsu
- Trudniejsze w prostych scenariuszach (lambda)
- `Action` jest wystarczające

### Alternatywa 2: Callback w RetryPolicy delegate

```csharp
public delegate Result<int> RetryPolicy(int attemptNumber, Error error, Action<int, Error, int>? notify);
```

**Odrzucona:**
- Zmienia sygnaturę istniejącego delegate (breaking change)
- Miesza odpowiedzialności (decyzja vs notyfikacja)
- Mniej intuicyjne API

### Alternatywa 3: Event w statycznej klasie

```csharp
public static class RetryEvents
{
    public static event EventHandler<RetryAttemptEventArgs>? OnRetryAttempt;
}
```

**Odrzucona:**
- Statyczne eventy są anty-wzorcem (memory leaks, testability)
- Trudne do śledzenia który retry do której operacji
- Nie można mieć różnych handlerów dla różnych operacji

### Alternatywa 4: IObservable<RetryAttempt>

```csharp
public static IObservable<RetryAttempt> BindWithRetryObservable<TIn, TOut>(...)
```

**Odrzucona:**
- Wymaga System.Reactive
- Overengineering dla prostego use case
- Zmienia sygnaturę zwracanego typu

## Wpływ na Voyager.Common.Proxy

Po implementacji, `HttpMethodInterceptor` może emitować zdarzenia diagnostyczne:

```csharp
// Przed - brak informacji o retries
var result = await operation.BindWithRetryAsync(
    async _ => await ExecuteHttpRequestAsync(...),
    _retryPolicy);

// Po - pełna obserwacja
var result = await operation.BindWithRetryAsync(
    async _ => await ExecuteHttpRequestAsync(...),
    _retryPolicy,
    onRetryAttempt: (attempt, error, delayMs) =>
    {
        foreach (var handler in _diagnosticsHandlers)
        {
            handler.OnRetryAttempt(new RetryAttemptEvent
            {
                ServiceName = _serviceName,
                MethodName = methodName,
                AttemptNumber = attempt,
                ErrorType = error.Type.ToString(),
                ErrorMessage = error.Message,
                DelayBeforeNextAttemptMs = delayMs,
                WillRetry = delayMs > 0
            });
        }
    });
```

## Testy jednostkowe

```csharp
public class RetryCallbackTests
{
    [Fact]
    public async Task OnRetryAttempt_CalledForEachFailedAttempt()
    {
        // Arrange
        var attempts = new List<(int attempt, string errorType, int delay)>();
        var callCount = 0;

        Func<int, Task<Result<string>>> failingOperation = async _ =>
        {
            callCount++;
            if (callCount < 3) return Result<string>.Failure(Error.UnavailableError("Service down"));
            return Result<string>.Success("OK");
        };

        // Act
        var result = await Result<int>.Success(1).BindWithRetryAsync(
            failingOperation,
            RetryPolicies.TransientErrors(maxAttempts: 5, baseDelayMs: 10),
            onRetryAttempt: (attempt, error, delay) =>
            {
                attempts.Add((attempt, error.Type.ToString(), delay));
            });

        // Assert
        result.IsSuccess.Should().BeTrue();
        attempts.Should().HaveCount(2); // 2 failures before success
        attempts[0].attempt.Should().Be(1);
        attempts[0].errorType.Should().Be("Unavailable");
        attempts[0].delay.Should().Be(10);  // First delay
        attempts[1].attempt.Should().Be(2);
        attempts[1].delay.Should().Be(20);  // Exponential backoff
    }

    [Fact]
    public async Task OnRetryAttempt_CalledWithZeroDelay_WhenNoMoreRetries()
    {
        // Arrange
        int? lastDelay = null;

        Func<int, Task<Result<string>>> alwaysFails = async _ =>
            Result<string>.Failure(Error.UnavailableError("Always fails"));

        // Act
        var result = await Result<int>.Success(1).BindWithRetryAsync(
            alwaysFails,
            RetryPolicies.TransientErrors(maxAttempts: 2, baseDelayMs: 10),
            onRetryAttempt: (attempt, error, delay) => lastDelay = delay);

        // Assert
        result.IsFailure.Should().BeTrue();
        lastDelay.Should().Be(0); // No more retries
    }

    [Fact]
    public async Task OnRetryAttempt_NotCalled_WhenFirstAttemptSucceeds()
    {
        // Arrange
        var called = false;

        Func<int, Task<Result<string>>> succeeds = async _ =>
            Result<string>.Success("OK");

        // Act
        var result = await Result<int>.Success(1).BindWithRetryAsync(
            succeeds,
            RetryPolicies.TransientErrors(),
            onRetryAttempt: (_, _, _) => called = true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        called.Should().BeFalse();
    }

    [Fact]
    public async Task OnRetryAttempt_NotCalled_ForNonTransientErrors()
    {
        // Arrange
        var called = false;

        Func<int, Task<Result<string>>> validationError = async _ =>
            Result<string>.Failure(Error.ValidationError("Invalid input"));

        // Act
        var result = await Result<int>.Success(1).BindWithRetryAsync(
            validationError,
            RetryPolicies.TransientErrors(),
            onRetryAttempt: (_, _, _) => called = true);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        called.Should().BeFalse(); // Non-transient = no retry = no callback
    }
}
```

## Implementacja

- [x] Dodać przeciążenie `BindWithRetryAsync` z parametrem `onRetryAttempt`
- [x] Zachować istniejące przeciążenie bez callbacka (backward compatibility)
- [x] Testy jednostkowe (7 nowych testów)
- [x] Dokumentacja XML
- [ ] Wydać jako część wersji 1.8.0

## Kompatybilność wsteczna

- Nowe przeciążenie - istniejący kod działa bez zmian
- Parametr `onRetryAttempt` jest nullable z domyślną wartością `null`
- Brak breaking changes

---

**Powiązane:**
- [ADR-0003: Retry Extensions for Transient Failures](./ADR-0003-retry-extensions-for-transient-failures.md)
- [ADR-0008: Circuit Breaker State Change Callbacks](./ADR-0008-circuit-breaker-state-change-callbacks.md)
- [Voyager.Common.Proxy ADR-008: Diagnostics Strategy](file:///C:/zrodla/voyager.common.proxy/docs/adr/ADR-008-Diagnostics-Strategy.md)
