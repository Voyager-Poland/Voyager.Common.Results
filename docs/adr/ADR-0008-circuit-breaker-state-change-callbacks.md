# ADR-0008: Callbacki zmiany stanu Circuit Breaker

**Status:** Propozycja
**Data:** 2026-01-31
**Kontekst:** Voyager.Common.Proxy (ADR-008 Diagnostics Strategy)

## Problem

`CircuitBreakerPolicy` zmienia stan wewnętrznie, ale **nie powiadamia nikogo** o zmianie:

```csharp
// Obecnie - stan się zmienia, ale nikt o tym nie wie
await _circuitBreaker.RecordFailureAsync(error);
// Czy stan się zmienił? Closed → Open? Trzeba sprawdzić ręcznie.
```

**Konsumenci potrzebują wiedzieć o zmianach stanu, aby:**
1. Logować zdarzenia (`Circuit breaker OPEN for UserService`)
2. Wysyłać alerty (Slack, PagerDuty)
3. Aktualizować metryki (Prometheus, Application Insights)
4. Reagować na awarie w czasie rzeczywistym

**Obecne obejście w Voyager.Common.Proxy:**

```csharp
// Wrapper porównujący stan przed/po - brzydkie i podatne na race conditions
var oldState = _policy.State;
await _policy.RecordFailureAsync(error);
if (oldState != _policy.State)
    OnStateChanged(oldState, _policy.State);  // Może przegapić szybkie zmiany
```

## Decyzja

Dodać mechanizm callbacków do `CircuitBreakerPolicy`.

### API

```csharp
public sealed class CircuitBreakerPolicy
{
    /// <summary>
    /// Callback invoked when circuit breaker state changes.
    /// Called synchronously within the lock - keep handler fast and non-blocking.
    /// </summary>
    /// <remarks>
    /// Parameters: (oldState, newState, failureCount, lastError)
    /// </remarks>
    public Action<CircuitState, CircuitState, int, Error?>? OnStateChanged { get; set; }

    // Alternatywnie - event (ale Action jest prostsze dla DI)
    // public event EventHandler<CircuitBreakerStateChangedEventArgs>? StateChanged;
}
```

### Użycie

```csharp
var circuitBreaker = new CircuitBreakerPolicy(
    failureThreshold: 5,
    openTimeout: TimeSpan.FromSeconds(30));

circuitBreaker.OnStateChanged = (oldState, newState, failures, lastError) =>
{
    _logger.LogWarning(
        "Circuit breaker state changed: {OldState} → {NewState}, failures: {Failures}, error: {Error}",
        oldState, newState, failures, lastError?.Message);

    if (newState == CircuitState.Open)
    {
        _alertService.SendAlert($"Circuit breaker OPEN: {lastError?.Message}");
    }
};
```

### Implementacja

```csharp
public sealed class CircuitBreakerPolicy
{
    // ... existing fields ...

    /// <summary>
    /// Callback invoked when circuit breaker state changes.
    /// </summary>
    public Action<CircuitState, CircuitState, int, Error?>? OnStateChanged { get; set; }

    public async Task RecordFailureAsync(Error error)
    {
        if (!ShouldCountError(error))
            return;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var oldState = _state;
            _failureCount++;
            _lastError = error;

            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
                _halfOpenAttempts = 0;
            }
            else if (_state == CircuitState.Closed && _failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
            }

            // Notify if state changed
            if (oldState != _state)
            {
                OnStateChanged?.Invoke(oldState, _state, _failureCount, _lastError);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RecordSuccessAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var oldState = _state;
            _failureCount = 0;
            _lastError = null;

            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Closed;
                _halfOpenAttempts = 0;
            }

            // Notify if state changed
            if (oldState != _state)
            {
                OnStateChanged?.Invoke(oldState, _state, _failureCount, null);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result<bool>> ShouldAllowRequestAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var oldState = _state;

            switch (_state)
            {
                case CircuitState.Closed:
                    return Result<bool>.Success(true);

                case CircuitState.Open:
                    if (DateTime.UtcNow - _openedAt >= _openTimeout)
                    {
                        _state = CircuitState.HalfOpen;
                        _halfOpenAttempts = 0;

                        // Notify state change
                        OnStateChanged?.Invoke(oldState, _state, _failureCount, _lastError);

                        return Result<bool>.Success(true);
                    }
                    return Result<bool>.Failure(Error.CircuitBreakerOpenError(_lastError));

                case CircuitState.HalfOpen:
                    if (_halfOpenAttempts < _halfOpenMaxAttempts)
                    {
                        _halfOpenAttempts++;
                        return Result<bool>.Success(true);
                    }
                    _state = CircuitState.Open;
                    _openedAt = DateTime.UtcNow;

                    // Notify state change
                    OnStateChanged?.Invoke(oldState, _state, _failureCount, _lastError);

                    return Result<bool>.Failure(Error.CircuitBreakerOpenError(_lastError));

                default:
                    return Result<bool>.Failure(Error.UnexpectedError($"Unknown state: {_state}"));
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}
```

## Alternatywy rozważane

### Alternatywa 1: Event zamiast Action

```csharp
public event EventHandler<CircuitBreakerStateChangedEventArgs>? StateChanged;
```

**Odrzucona:**
- Więcej boilerplate (EventArgs class)
- Trudniejsze w DI (trzeba subskrybować po utworzeniu)
- `Action` jest prostsze i wystarczające

### Alternatywa 2: IObservable<T> (Reactive Extensions)

```csharp
public IObservable<CircuitBreakerStateChange> StateChanges { get; }
```

**Odrzucona:**
- Wymaga zależności od System.Reactive
- Overengineering dla prostego use case
- Większość użytkowników nie potrzebuje Rx

### Alternatywa 3: Osobny interfejs ICircuitBreakerObserver

```csharp
public interface ICircuitBreakerObserver
{
    void OnStateChanged(CircuitState oldState, CircuitState newState, int failures, Error? lastError);
}

public CircuitBreakerPolicy(ICircuitBreakerObserver? observer = null)
```

**Odrzucona:**
- Nowy interfejs do implementacji
- Trudniejsze w prostych scenariuszach (lambda)
- `Action` pokrywa oba use cases

## Wpływ na Voyager.Common.Proxy

Po implementacji, `Voyager.Common.Proxy` może uprościć kod:

```csharp
// Przed - wrapper porównujący stan
internal class ObservableCircuitBreaker
{
    private CircuitState _lastKnownState;

    public async Task RecordFailureAsync(Error error)
    {
        var oldState = _policy.State;
        await _policy.RecordFailureAsync(error);
        if (oldState != _policy.State)
            EmitEvent(...);  // Race condition possible!
    }
}

// Po - bezpośredni callback
_circuitBreaker.OnStateChanged = (oldState, newState, failures, lastError) =>
{
    foreach (var handler in _diagnostics)
        handler.OnCircuitBreakerStateChanged(new CircuitBreakerStateChangedEvent
        {
            ServiceName = _serviceName,
            OldState = oldState,
            NewState = newState,
            FailureCount = failures,
            LastErrorType = lastError?.Type.ToString(),
            LastErrorMessage = lastError?.Message
        });
};
```

## Testy jednostkowe

```csharp
public class CircuitBreakerCallbackTests
{
    [Fact]
    public async Task OnStateChanged_CalledWhenCircuitOpens()
    {
        // Arrange
        CircuitState? capturedOldState = null;
        CircuitState? capturedNewState = null;

        var cb = new CircuitBreakerPolicy(failureThreshold: 2);
        cb.OnStateChanged = (old, @new, _, _) =>
        {
            capturedOldState = old;
            capturedNewState = @new;
        };

        // Act - trigger 2 failures to open circuit
        await cb.RecordFailureAsync(Error.UnavailableError("fail 1"));
        await cb.RecordFailureAsync(Error.UnavailableError("fail 2"));

        // Assert
        capturedOldState.Should().Be(CircuitState.Closed);
        capturedNewState.Should().Be(CircuitState.Open);
    }

    [Fact]
    public async Task OnStateChanged_CalledWhenCircuitCloses()
    {
        // Arrange
        var cb = new CircuitBreakerPolicy(failureThreshold: 1, openTimeout: TimeSpan.Zero);
        await cb.RecordFailureAsync(Error.UnavailableError("fail"));
        cb.State.Should().Be(CircuitState.Open);

        CircuitState? capturedNewState = null;
        cb.OnStateChanged = (_, @new, _, _) => capturedNewState = @new;

        // Act - wait for timeout, allow request (goes to HalfOpen), then success (goes to Closed)
        await cb.ShouldAllowRequestAsync();  // Open → HalfOpen
        await cb.RecordSuccessAsync();        // HalfOpen → Closed

        // Assert
        capturedNewState.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task OnStateChanged_NotCalledForBusinessErrors()
    {
        // Arrange
        var callCount = 0;
        var cb = new CircuitBreakerPolicy(failureThreshold: 1);
        cb.OnStateChanged = (_, _, _, _) => callCount++;

        // Act - business errors should not affect state
        await cb.RecordFailureAsync(Error.ValidationError("invalid"));
        await cb.RecordFailureAsync(Error.NotFoundError("not found"));

        // Assert
        callCount.Should().Be(0);
        cb.State.Should().Be(CircuitState.Closed);
    }
}
```

## Implementacja

- [ ] Dodać `OnStateChanged` property do `CircuitBreakerPolicy`
- [ ] Wywołać callback w `RecordFailureAsync()` gdy stan się zmieni
- [ ] Wywołać callback w `RecordSuccessAsync()` gdy stan się zmieni
- [ ] Wywołać callback w `ShouldAllowRequestAsync()` gdy Open → HalfOpen
- [ ] Testy jednostkowe
- [ ] Dokumentacja
- [ ] Wydać jako wersję 1.8.0

## Kompatybilność wsteczna

- `OnStateChanged` jest nullable - domyślnie `null`
- Istniejący kod działa bez zmian
- Brak breaking changes

---

**Powiązane:**
- [ADR-0004: Circuit Breaker Pattern](./ADR-0004-circuit-breaker-pattern-for-resilience.md)
- [Voyager.Common.Proxy ADR-008: Diagnostics Strategy](file:///C:/zrodla/voyager.common.proxy/docs/adr/ADR-008-Diagnostics-Strategy.md)
