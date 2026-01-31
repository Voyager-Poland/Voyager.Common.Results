# ADR-0007: Zachowanie Szczegółów Wyjątków w Error

**Status:** Zaakceptowano
**Data:** 2026-01-31
**Kontekst:** Voyager.Common.Results

## Problem

Obecna implementacja `Error.FromException()` traci krytyczne informacje diagnostyczne:

```csharp
public static Error FromException(Exception exception) =>
    new(ErrorType.Unexpected, "Exception", exception.Message);
```

**Co tracimy:**
- `StackTrace` - gdzie dokładnie wystąpił błąd
- `InnerException` - łańcuch przyczyn
- Typ wyjątku - `SqlException` vs `HttpRequestException` vs `TimeoutException`
- `Exception.Data` - dodatkowe metadane
- `Source` - assembly/moduł źródłowy

**Problem w programowaniu funkcyjnym:**

```csharp
// Funkcja przekazana bez kontekstu - gdzie dokładnie wystąpił błąd?
var result = await Result<Config>.TryAsync(
    async () => await LoadConfigAsync());  // Jeśli rzuci, tracimy stack trace

// W logach widzimy tylko:
// [Unexpected] Exception: "Object reference not set to an instance of an object"
// Gdzie? W której linii? Jaki był łańcuch wywołań?
```

## Decyzja

### 1. Nowe właściwości w Error (tylko wartości proste)

**Ważne:** NIE przechowujemy referencji do `Exception` - tylko wyciągamy wartości jako stringi. Dzięki temu:
- GC może zwolnić oryginalny Exception natychmiast po utworzeniu Error
- Brak memory leaks przy długotrwałym przechowywaniu Error (cache, kolekcje)
- Bezpieczna serializacja bez `[JsonIgnore]` hacków

```csharp
public sealed record Error(ErrorType Type, string Code, string Message)
{
    // ... istniejące właściwości ...

    /// <summary>
    /// Stack trace from the original exception.
    /// Copied as string - original Exception can be garbage collected.
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// Full type name of the original exception (e.g., "System.Data.SqlClient.SqlException").
    /// </summary>
    public string? ExceptionType { get; init; }

    /// <summary>
    /// Source of the exception (assembly/module name).
    /// </summary>
    public string? Source { get; init; }
}
```

### Dlaczego NIE przechowujemy `Exception?`

```csharp
// ❌ ŹLE - Exception nie może być zwolniony przez GC
public Exception? Exception { get; init; }

var errors = new List<Error>();
foreach (var item in items)
{
    try { Process(item); }
    catch (Exception ex)
    {
        errors.Add(Error.FromException(ex));
        // ex żyje dopóki errors istnieje!
        // ex może trzymać referencje do innych obiektów (captured variables)
        // = memory leak
    }
}

// ✅ DOBRZE - tylko stringi, Exception zwalniany natychmiast
public string? StackTrace { get; init; }
public string? ExceptionType { get; init; }
```

### 2. Rozbudowany `FromException`

```csharp
/// <summary>
/// Creates an error from an exception, preserving diagnostic information as strings.
/// The original Exception is NOT stored - only string copies are kept,
/// allowing GC to collect the Exception immediately.
/// </summary>
public static Error FromException(Exception exception)
{
    var errorType = MapExceptionToErrorType(exception);
    var code = $"Exception.{exception.GetType().Name}";

    return new Error(errorType, code, exception.Message)
    {
        // Tylko stringi - kopiujemy wartości, nie trzymamy referencji
        StackTrace = exception.StackTrace,
        ExceptionType = exception.GetType().FullName,
        Source = exception.Source,
        InnerError = exception.InnerException is not null
            ? FromException(exception.InnerException)
            : null
    };
    // Po wyjściu z metody, exception może być zwolniony przez GC
}

/// <summary>
/// Creates an error from an exception with custom error type.
/// </summary>
public static Error FromException(Exception exception, ErrorType errorType)
{
    var code = $"Exception.{exception.GetType().Name}";

    return new Error(errorType, code, exception.Message)
    {
        StackTrace = exception.StackTrace,
        ExceptionType = exception.GetType().FullName,
        Source = exception.Source,
        InnerError = exception.InnerException is not null
            ? FromException(exception.InnerException)
            : null
    };
}

/// <summary>
/// Maps common exception types to appropriate ErrorType.
/// </summary>
private static ErrorType MapExceptionToErrorType(Exception exception)
{
    return exception switch
    {
        OperationCanceledException => ErrorType.Cancelled,
        TimeoutException => ErrorType.Timeout,
        HttpRequestException => ErrorType.Unavailable,
        UnauthorizedAccessException => ErrorType.Permission,
        ArgumentException => ErrorType.Validation,
        InvalidOperationException => ErrorType.Business,
        KeyNotFoundException => ErrorType.NotFound,
        // Database exceptions (check by name to avoid hard dependency)
        _ when exception.GetType().Name.Contains("Sql") => ErrorType.Database,
        _ when exception.GetType().Name.Contains("Db") => ErrorType.Database,
        _ => ErrorType.Unexpected
    };
}
```

### 3. Metoda `ToDetailedString()` do logowania

```csharp
/// <summary>
/// Returns detailed error information including stack trace and inner errors.
/// Useful for logging and debugging.
/// </summary>
public string ToDetailedString()
{
    var sb = new StringBuilder();
    AppendErrorDetails(sb, this, depth: 0);
    return sb.ToString();
}

private static void AppendErrorDetails(StringBuilder sb, Error error, int depth)
{
    var indent = new string(' ', depth * 2);

    sb.AppendLine($"{indent}[{error.Type}] {error.Code}: {error.Message}");

    if (error.ExceptionType is not null)
        sb.AppendLine($"{indent}  Exception: {error.ExceptionType}");

    if (error.StackTrace is not null)
    {
        sb.AppendLine($"{indent}  Stack Trace:");
        foreach (var line in error.StackTrace.Split('\n').Take(10))
        {
            sb.AppendLine($"{indent}    {line.Trim()}");
        }
        if (error.StackTrace.Split('\n').Length > 10)
            sb.AppendLine($"{indent}    ... (truncated)");
    }

    if (error.InnerError is not null)
    {
        sb.AppendLine($"{indent}  Caused by:");
        AppendErrorDetails(sb, error.InnerError, depth + 1);
    }
}
```

### 4. Extension method dla logowania

```csharp
public static class ErrorLoggingExtensions
{
    /// <summary>
    /// Logs error with full details including stack trace.
    /// </summary>
    public static void LogDetailed(this Error error, ILogger logger, LogLevel level = LogLevel.Error)
    {
        logger.Log(level, "Error occurred: {ErrorDetails}", error.ToDetailedString());
    }

    /// <summary>
    /// Logs error chain with context.
    /// </summary>
    public static void LogWithContext(
        this Error error,
        ILogger logger,
        string operation,
        LogLevel level = LogLevel.Error)
    {
        logger.Log(level,
            "Operation {Operation} failed:\n{ErrorDetails}",
            operation,
            error.ToDetailedString());
    }
}
```

## Przykład użycia

```csharp
// Przed - tracimy informacje
var result = Result<Data>.Try(() => riskyOperation());
if (result.IsFailure)
    _logger.LogError("Failed: {Message}", result.Error.Message);
// Output: "Failed: Object reference not set..."

// Po - pełne informacje diagnostyczne
var result = Result<Data>.Try(() => riskyOperation());
if (result.IsFailure)
    _logger.LogError("Failed:\n{Details}", result.Error.ToDetailedString());
// Output:
// Failed:
// [Unexpected] Exception.NullReferenceException: Object reference not set...
//   Exception: System.NullReferenceException
//   Stack Trace:
//     at MyService.RiskyOperation() in MyService.cs:line 42
//     at MyService.Process() in MyService.cs:line 28
//     ...
```

## Automatyczne mapowanie typów wyjątków

| Exception Type | ErrorType | Code |
|----------------|-----------|------|
| `OperationCanceledException` | Cancelled | Exception.OperationCanceledException |
| `TimeoutException` | Timeout | Exception.TimeoutException |
| `HttpRequestException` | Unavailable | Exception.HttpRequestException |
| `UnauthorizedAccessException` | Permission | Exception.UnauthorizedAccessException |
| `ArgumentException` | Validation | Exception.ArgumentException |
| `InvalidOperationException` | Business | Exception.InvalidOperationException |
| `KeyNotFoundException` | NotFound | Exception.KeyNotFoundException |
| `*Sql*Exception` | Database | Exception.SqlException |
| Other | Unexpected | Exception.{TypeName} |

## Testy jednostkowe

```csharp
public class ErrorFromExceptionTests
{
    [Fact]
    public void FromException_PreservesStackTrace()
    {
        // Arrange
        Exception caught;
        try { throw new InvalidOperationException("Test"); }
        catch (Exception ex) { caught = ex; }

        // Act
        var error = Error.FromException(caught);

        // Assert
        Assert.NotNull(error.StackTrace);
        Assert.Contains("FromException_PreservesStackTrace", error.StackTrace);
    }

    [Fact]
    public void FromException_PreservesExceptionType()
    {
        // Arrange
        var exception = new ArgumentNullException("param");

        // Act
        var error = Error.FromException(exception);

        // Assert
        Assert.Equal("System.ArgumentNullException", error.ExceptionType);
        Assert.Equal("Exception.ArgumentNullException", error.Code);
    }

    [Fact]
    public void FromException_PreservesSource()
    {
        // Arrange
        var exception = new InvalidOperationException("Test");

        // Act
        var error = Error.FromException(exception);

        // Assert - Source may be null for manually created exceptions
        // but should be preserved when present
        Assert.Equal(exception.Source, error.Source);
    }

    [Fact]
    public void FromException_MapsToCorrectErrorType()
    {
        Assert.Equal(ErrorType.Cancelled, Error.FromException(new OperationCanceledException()).Type);
        Assert.Equal(ErrorType.Timeout, Error.FromException(new TimeoutException()).Type);
        Assert.Equal(ErrorType.Validation, Error.FromException(new ArgumentException()).Type);
        Assert.Equal(ErrorType.NotFound, Error.FromException(new KeyNotFoundException()).Type);
    }

    [Fact]
    public void FromException_ChainsInnerExceptions()
    {
        // Arrange
        var inner = new InvalidOperationException("Inner");
        var outer = new Exception("Outer", inner);

        // Act
        var error = Error.FromException(outer);

        // Assert
        Assert.NotNull(error.InnerError);
        Assert.Equal("Inner", error.InnerError.Message);
        Assert.Equal(ErrorType.Business, error.InnerError.Type);
    }

    [Fact]
    public void FromException_DoesNotHoldReferenceToException()
    {
        // Arrange
        WeakReference<Exception> weakRef;
        Error error;

        // Create exception in separate scope
        void CreateError()
        {
            var exception = new InvalidOperationException("Test");
            weakRef = new WeakReference<Exception>(exception);
            error = Error.FromException(exception);
        }
        CreateError();

        // Act - force GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert - exception should be collected, but error still has data
        Assert.False(weakRef.TryGetTarget(out _), "Exception should be garbage collected");
        Assert.NotNull(error.ExceptionType);
        Assert.Equal("Test", error.Message);
    }

    [Fact]
    public void ToDetailedString_IncludesAllInformation()
    {
        // Arrange
        Exception caught;
        try { throw new InvalidOperationException("Test error"); }
        catch (Exception ex) { caught = ex; }
        var error = Error.FromException(caught);

        // Act
        var detailed = error.ToDetailedString();

        // Assert
        Assert.Contains("[Business]", detailed);
        Assert.Contains("InvalidOperationException", detailed);
        Assert.Contains("Test error", detailed);
        Assert.Contains("Stack Trace:", detailed);
    }
}
```

## Implementacja

- [x] Dodać `StackTrace?` property do `Error`
- [x] Dodać `ExceptionType?` property do `Error`
- [x] Dodać `Source?` property do `Error`
- [x] Rozbudować `FromException()` o zachowanie szczegółów
- [x] Dodać `FromException(exception, errorType)` overload
- [x] Dodać prywatną metodę `MapExceptionToErrorType()`
- [x] Dodać `ToDetailedString()` method
- [x] Testy jednostkowe
- [ ] Dokumentacja

## Kompatybilność wsteczna

- Nowe właściwości są opcjonalne (`init` properties)
- Istniejący kod używający `FromException` automatycznie zyskuje nowe funkcje
- Wszystkie właściwości są stringami - bezpieczna serializacja
- Brak breaking changes

## Alternatywy rozważane

1. **Przechowywanie `Exception?` bezpośrednio**
   - ❌ Odrzucone - blokuje GC, potencjalny memory leak
   - Exception może trzymać referencje do captured variables, kontekstu itp.
   - Przy długotrwałym przechowywaniu Error (cache, kolekcje) = wyciek pamięci

2. **`WeakReference<Exception>`**
   - ❌ Odrzucone - Exception może zostać zwolniony zanim zdążymy go użyć
   - Nieokreślone zachowanie - czasem działa, czasem nie

3. **Tylko `Metadata` dictionary**
   - ❌ Odrzucone - brak typowania i struktury

4. **Kopiowanie jako stringi (wybrane rozwiązanie)**
   - ✅ GC może natychmiast zwolnić Exception
   - ✅ Bezpieczna serializacja
   - ✅ Deterministyczne zachowanie
   - ✅ Brak memory leaks

## Uwagi dotyczące wydajności

- `StackTrace` jako string może być duży - ale to jednorazowa kopia przy tworzeniu Error
- Brak referencji do Exception = GC może działać efektywnie
- `ToDetailedString()` jest lazy - wywoływany tylko przy logowaniu
- Stringi są immutable i dobrze zoptymalizowane w .NET

---

**Powiązane:**
- [ADR-0006: Error Chaining](./ADR-0006-error-chaining-for-distributed-systems.md) - `InnerError` wykorzystywany do łańcucha wyjątków
