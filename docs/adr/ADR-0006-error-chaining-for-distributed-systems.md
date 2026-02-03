# ADR-0006: Error Chaining dla Systemów Rozproszonych

**Status:** Zaakceptowano
**Data:** 2026-01-31
**Kontekst:** Voyager.Common.Results, Voyager.Common.Proxy

## Problem

W łańcuchu wywołań serwisów (A -> B -> C) tracimy informację o źródle błędu:

```
Serwis A          Serwis B          Serwis C
    |                 |                 |
    | --- Request --> | --- Request --> |
    |                 |                 |
    |                 | <-- NotFound -- |
    |                 |                 |
    | <-- 500 ------- |  (mapuje na 500)
    |
    v
  Klient widzi 500, nie wie że C zwrócił NotFound
```

**Problemy:**
1. Utrata oryginalnego typu błędu (NotFound -> 500)
2. Brak informacji który serwis zawiódł
3. Trudności w debugowaniu i monitoringu
4. Logi nie pokazują pełnego kontekstu

## Decyzja

Dodajemy opcjonalną właściwość `InnerError` do klasy `Error`, wzorowaną na `Exception.InnerException`.

### 1. Rozszerzenie klasy Error

```csharp
public sealed record Error(ErrorType Type, string Code, string Message)
{
    /// <summary>
    /// Inner error that caused this error (similar to Exception.InnerException).
    /// Used for error chaining in distributed systems.
    /// </summary>
    public Error? InnerError { get; init; }

    /// <summary>
    /// Creates a copy of this error with an inner error attached.
    /// </summary>
    public Error WithInner(Error inner) => this with { InnerError = inner };

    /// <summary>
    /// Gets the root cause error by traversing the InnerError chain.
    /// </summary>
    public Error GetRootCause()
    {
        var current = this;
        while (current.InnerError is not null)
        {
            current = current.InnerError;
        }
        return current;
    }

    /// <summary>
    /// Returns true if any error in the chain matches the predicate.
    /// </summary>
    public bool HasInChain(Func<Error, bool> predicate)
    {
        var current = this;
        while (current is not null)
        {
            if (predicate(current))
                return true;
            current = current.InnerError;
        }
        return false;
    }
}
```

### 2. Extension method dla Result

```csharp
public static class ResultErrorChainExtensions
{
    /// <summary>
    /// Wraps the error with a new error, preserving the original as InnerError.
    /// </summary>
    public static Result<T> WrapError<T>(
        this Result<T> result,
        Func<Error, Error> wrapperFactory)
    {
        if (result.IsSuccess)
            return result;

        var wrapper = wrapperFactory(result.Error);
        return Result<T>.Failure(wrapper.WithInner(result.Error));
    }

    /// <summary>
    /// Wraps the error with context about the calling service.
    /// </summary>
    public static Result<T> AddErrorContext<T>(
        this Result<T> result,
        string serviceName,
        string operation)
    {
        if (result.IsSuccess)
            return result;

        var contextError = new Error(
            result.Error.Type,  // Preserve original type
            $"{serviceName}.{operation}.Failed",
            $"{serviceName}.{operation} failed: {result.Error.Message}"
        ) { InnerError = result.Error };

        return Result<T>.Failure(contextError);
    }
}
```

### 3. Użycie w praktyce

```csharp
// Serwis B wywołuje Serwis C
public async Task<Result<Order>> GetOrderWithProductAsync(int orderId)
{
    var order = await _orderRepository.GetAsync(orderId);
    if (order is null)
        return Error.NotFoundError("Order.NotFound", $"Order {orderId} not found");

    // Wywołanie zewnętrznego serwisu
    var productResult = await _productServiceClient.GetProductAsync(order.ProductId)
        .AddErrorContext("ProductService", "GetProduct");

    if (productResult.IsFailure)
    {
        // Zwracamy błąd z pełnym kontekstem
        return Error.UnavailableError(
            "Order.ProductUnavailable",
            $"Cannot get product for order {orderId}")
            .WithInner(productResult.Error);
    }

    return order.WithProduct(productResult.Value);
}
```

### 4. Logowanie z pełnym kontekstem

```csharp
public static class ErrorLoggingExtensions
{
    public static void LogErrorChain(this ILogger logger, Error error)
    {
        var sb = new StringBuilder();
        var current = error;
        var depth = 0;

        while (current is not null)
        {
            var indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}[{current.Type}] {current.Code}: {current.Message}");
            current = current.InnerError;
            depth++;
        }

        logger.LogError("Error chain:\n{ErrorChain}", sb.ToString());
    }
}

// Output:
// Error chain:
// [Unavailable] Order.ProductUnavailable: Cannot get product for order 123
//   [Unavailable] ProductService.GetProduct.Failed: ProductService.GetProduct failed: Product 456 not found
//     [NotFound] Product.NotFound: Product 456 not found
```

### 5. HTTP Response z kontekstem (opcjonalnie)

```csharp
public static class ErrorHttpExtensions
{
    public static ProblemDetails ToProblemDetails(this Error error, bool includeChain = false)
    {
        var problem = new ProblemDetails
        {
            Status = error.Type.ToHttpStatusCode(),
            Title = error.Code,
            Detail = error.Message
        };

        if (includeChain && error.InnerError is not null)
        {
            problem.Extensions["rootCause"] = new
            {
                type = error.GetRootCause().Type.ToString(),
                code = error.GetRootCause().Code,
                message = error.GetRootCause().Message
            };
        }

        return problem;
    }
}
```

## Klasyfikacja z uwzględnieniem łańcucha

```csharp
public static class ErrorTypeExtensions
{
    /// <summary>
    /// Checks if the root cause is transient (retryable).
    /// </summary>
    public static bool IsRootCauseTransient(this Error error)
    {
        return error.GetRootCause().Type.IsTransient();
    }

    /// <summary>
    /// Gets the most specific HTTP status code from the chain.
    /// Business errors take precedence over infrastructure errors.
    /// </summary>
    public static int GetMostSpecificHttpCode(this Error error)
    {
        var rootCause = error.GetRootCause();

        // If root cause is a business error (4xx), use it
        if (rootCause.Type.IsBusinessError())
            return rootCause.Type.ToHttpStatusCode();

        // Otherwise use the top-level error's code
        return error.Type.ToHttpStatusCode();
    }
}
```

## Testy jednostkowe

```csharp
public class ErrorChainTests
{
    [Fact]
    public void WithInner_CreatesChain()
    {
        var inner = Error.NotFoundError("Product.NotFound", "Product not found");
        var outer = Error.UnavailableError("Order.Failed", "Order processing failed")
            .WithInner(inner);

        outer.InnerError.Should().Be(inner);
        outer.Type.Should().Be(ErrorType.Unavailable);
        outer.InnerError!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void GetRootCause_ReturnsDeepestError()
    {
        var root = Error.NotFoundError("Deep.Error", "Root cause");
        var middle = Error.DatabaseError("Middle.Error", "Middle").WithInner(root);
        var top = Error.UnavailableError("Top.Error", "Top").WithInner(middle);

        top.GetRootCause().Should().Be(root);
    }

    [Fact]
    public void HasInChain_FindsMatchingError()
    {
        var inner = Error.NotFoundError("Product.NotFound", "Not found");
        var outer = Error.UnavailableError("Service.Failed", "Failed")
            .WithInner(inner);

        outer.HasInChain(e => e.Type == ErrorType.NotFound).Should().BeTrue();
        outer.HasInChain(e => e.Type == ErrorType.Validation).Should().BeFalse();
    }

    [Fact]
    public void GetMostSpecificHttpCode_PrefersBusinessErrors()
    {
        var notFound = Error.NotFoundError("Product.NotFound", "Not found");
        var unavailable = Error.UnavailableError("Service.Failed", "Failed")
            .WithInner(notFound);

        // Root cause is NotFound (404), should return 404 not 503
        unavailable.GetMostSpecificHttpCode().Should().Be(404);
    }
}
```

## Implementacja

- [x] Dodać `InnerError` property do `Error`
- [x] Dodać `WithInner()` method
- [x] Dodać `GetRootCause()` method
- [x] Dodać `HasInChain()` method
- [x] Dodać `WrapError` i `AddErrorContext` extension methods
- [x] Testy jednostkowe
- [ ] Dokumentacja

## Kompatybilność wsteczna

- `InnerError` jest opcjonalne (`init` property) - istniejący kod działa bez zmian
- Nowe metody są addytywne
- Brak breaking changes

## Alternatywy rozważane

1. **Metadata dictionary** - elastyczne, ale brak typowania
2. **Osobna klasa ErrorChain** - zbyt inwazyjne, zmienia API
3. **Tylko w logach** - nie rozwiązuje problemu propagacji typów błędów

---

**Powiązane:**
- [ADR-0005: Error Classification](./ADR-0005-error-classification-for-resilience.md)
