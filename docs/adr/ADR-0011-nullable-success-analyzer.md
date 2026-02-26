# ADR-0011: Analyzer wykrywający `Result<T>.Success(null)` — VCR0070

**Status:** Zaakceptowane
**Data:** 2026-02-25
**Kontekst:** Voyager.Common.Results.Analyzers

## Problem

Wzorzec Result ma zastępować `null` jako mechanizm sygnalizacji "brak wartości". Tymczasem nic nie blokuje użytkownika przed stworzeniem sukcesu z wartością `null`:

```csharp
// ❌ Realny przypadek z produkcji — żaden analizator tego nie złapał
return Result<RequiredFee?>.Success(null);
```

**Dlaczego to jest problem:**

1. **Podwaja dwuznaczność** — konsument musi sprawdzić `IsSuccess` ORAZ `Value != null`, co niweluje sens Result pattern
2. **Psuje łańcuch Railway** — operatory (`Map`, `Bind`, `Tap`, `Ensure`) przekazują `Value!` do delegatów, co prowadzi do `NullReferenceException` w runtime:
   ```csharp
   Result<RequiredFee?>.Success(null)
       .Map(fee => fee.Amount)    // 💥 NullReferenceException — fee is null
       .Bind(amount => Validate(amount));
   ```
3. **Ukrywa brak decyzji projektowej** — programista nie zdecydował czy "brak fee" to sukces czy porażka, więc zwraca `Success(null)` jako "path of least resistance"
4. **Zaraża typy** — `Result<T?>` wymusza nullable propagation w dalszym kodzie:
   ```csharp
   // Teraz każdy konsument musi obsługiwać null
   var fee = result.Value;      // RequiredFee? — nullable
   var amount = fee?.Amount;    // decimal? — nullable propagation
   var text = amount?.ToString(); // string? — i tak dalej...
   ```

### Warianty problemu

```csharp
// Wariant 1: Jawne Success(null)
Result<Order?>.Success(null);                    // ❌ najbardziej oczywisty

// Wariant 2: Implicit conversion z null
Result<Order?> GetOrder() => (Order?)null;       // ❌ implicit operator → Success(null)

// Wariant 3: Literał default
Result<Order?>.Success(default);                 // ❌ default(Order?) == null

// Wariant 4: Przekazanie zmiennej, która jest null (runtime)
Order? order = null;
Result<Order?>.Success(order);                   // ⚠ trudne do wykrycia statycznie

// Wariant 5: Null-forgiving
Result<Order>.Success(null!);                    // ❌ wymuszenie null mimo non-nullable T
```

## Decyzja

Stworzyć Roslyn Analyzer **VCR0070** wykrywający literał `null` / `default` jako argument `Success()` oraz podejrzane wzorce z `Result<T?>`.

### Diagnostyki

| ID | Severity | Komunikat | Wykrywa |
|---|---|---|---|
| `VCR0070` | Warning | `Result<T>.Success()` should not receive null. A successful result must carry a value. Use `Result<T>.Failure()` or remove nullable from the type parameter. | Literał `null`/`default` w `Success()` |
| `VCR0071` | Info | `Result<T?>` uses a nullable type parameter. Consider `Result<T>` with `Failure` for absent values, or introduce a dedicated "none" value. | `Result<T?>` jako typ (opcjonalny, domyślnie wyłączony) |

### Faza 1 — VCR0070: `Success(null)` / `Success(default)` (Warning)

Wykrywanie literału `null` lub `default` jako argumentu metody `Success`:

```csharp
// ⚠ VCR0070 — wszystkie poniższe warianty
Result<Order?>.Success(null);
Result<Order>.Success(null!);
Result<Order?>.Success(default);
Result<string?>.Success(null);

// ⚠ VCR0070 — implicit conversion z literału null
Result<Order?> result = (Order?)null;

// ✅ OK — brak diagnostyki
Result<Order>.Success(new Order());
Result<int?>.Success(42);                // nullable T, ale wartość nie jest null
Result<string?>.Success("hello");        // nullable T, ale wartość nie jest null
```

#### Implementacja

Analyzer rejestruje dwa callbacki:
- `OperationKind.Invocation` — wykrywa `Success(null)`, `Success(default)`, `Success(null!)`
- `OperationKind.Conversion` — wykrywa implicit conversion `T → Result<T>` z null (np. `return (Order?)null;`)

Kluczowe decyzje implementacyjne:
- `IsNullOrDefault()` — rekurencyjnie unwrapuje `IConversionOperation` (obsługuje cast, null-forgiving, implicit conversions) i sprawdza `ConstantValue`
- `AnalyzeConversion()` — filtruje po `conversion.OperatorMethod` aby uniknąć false positives na konwersjach referencyjnych (`Result<T> r = null`) i konwersjach `Error → Result<T>`
- Nie wykrywa zmiennych, które są `null` w runtime (Wariant 4) — analiza flow jest poza scope

**Pliki:** `NullableSuccessAnalyzer.cs`, `NullableSuccessCodeFixProvider.cs`

#### Code Fix Provider

Zamienia `Success(null)` na `Failure(Error.NotFoundError("TODO: provide meaningful error"))`. Obsługuje tylko `InvocationExpressionSyntax` (jawne wywołanie `Success()`). Nie obsługuje implicit conversion — transformacja implicit conversion w jawne `Failure()` wymaga zmiany struktury kodu i jest zbyt kontekstowa dla automatycznego fix-a.

### Faza 2 — VCR0071: `Result<T?>` jako typ (Info, domyślnie wyłączony)

Bardziej agresywna reguła — flaguje sam typ `Result<T?>` niezależnie od tego czy `Success(null)` jest wywoływane:

```csharp
// ℹ VCR0071 — nullable T w Result sugeruje brak modelowania
Result<Order?> GetOrder(int id);           // ℹ rozważ Result<Order> + Failure(NotFound)
Task<Result<Fee?>> CalculateFee();         // ℹ rozważ Result<Fee> + Failure / dedykowany typ

// ✅ OK — non-nullable T
Result<Order> GetOrder(int id);
Task<Result<Fee>> CalculateFee();

// ✅ OK — nullable value types z semantycznym sensem (dyskusyjne)
Result<int?> ParseOptionalField(string s); // int? ma sens jako "pole opcjonalne"
```

**Severity:** `Info`, `isEnabledByDefault: false` — to bardziej wskazówka projektowa niż bug. Zespoły mogą włączyć w `.editorconfig`:

```ini
dotnet_diagnostic.VCR0071.severity = warning
```

## Alternatywy rozważone

### Alternatywa 1: Runtime check w `Success()` — `ArgumentNullException`

```csharp
public static Result<TValue> Success(TValue value)
{
    if (value is null)
        throw new ArgumentNullException(nameof(value),
            "Use Result<T>.Failure() for absent values, not Success(null)");
    return new(value);
}
```

**Odrzucona:**
- **Breaking change** — istniejący kod, który (niestety) używa `Success(null)`, zacznie rzucać wyjątki w runtime
- Wyjątek w `Success()` jest nieoczekiwany — metoda fabryczna nie powinna rzucać
- Nie wykrywa problemu w compile-time — programista dowie się dopiero po uruchomieniu
- **Nie działa z value types** — `Result<int?>.Success(null)` to `Success(default(int?))`, compiler boxing sprawia że `is null` check jest mniej oczywisty
- Konflikt z generics — `where TValue : notnull` constraint nie działa z nullable value types

### Alternatywa 2: Generic constraint `where TValue : notnull`

```csharp
public class Result<TValue> where TValue : notnull { ... }
```

**Odrzucona:**
- **Masywny breaking change** — wymusi zmianę na każdym `Result<string?>`, `Result<int?>` w dowolnym kodzie konsumenckim
- Blokuje legalne scenariusze — `Result<int?>` gdzie `null` oznacza "pole opcjonalne ale operacja się powiodła"
- Generuje CS8714 warning zamiast naszej dedykowanej diagnostyki z konkretnym rozwiązaniem
- Nie kontrolujemy treści komunikatu

### Alternatywa 3: Oddzielny typ `Result<T>.None` / `Maybe<T>`

```csharp
// Zamiast Result<T?>.Success(null) →
Result<Maybe<Fee>> CalculateFee();   // Maybe<Fee>.None vs Maybe<Fee>.Some(fee)
```

**Odrzucona na etapie analyzera:**
- Wymaga stworzenia nowego typu (`Maybe<T>` / `Option<T>`)
- Zwiększa złożoność API — użytkownik musi poznać dodatkową abstrakcję
- Nie rozwiązuje istniejącego problemu — ludzie nadal mogą napisać `Success(null)`
- **Może być rozważona jako osobna funkcjonalność** w przyszłości, ale VCR0070 jest potrzebne niezależnie

### Alternatywa 4: Brak akcji (status quo)

**Odrzucona:**
- Problem jest realny — znaleziony w produkcyjnym kodzie (`Result<RequiredFee?>.Success(null)`)
- Prowadzi do `NullReferenceException` wewnątrz łańcucha Railway (Map, Bind, Tap)
- Każdy `Success(null)` to potencjalna bomba zegarowa — może działać przez miesiące, aż ktoś doda `.Map(fee => fee.Amount)`

## Wzorce do rozpoznania

### Pełna lista wykrywanych wzorców (VCR0070)

```csharp
// 1. Jawne wywołanie Success z null
Result<Order?>.Success(null);                    // ⚠ VCR0070

// 2. Jawne wywołanie Success z default
Result<Order?>.Success(default);                 // ⚠ VCR0070
Result<string?>.Success(default);                // ⚠ VCR0070

// 3. Null-forgiving operator (obejście nullability)
Result<Order>.Success(null!);                    // ⚠ VCR0070

// 4. Cast do nullable + null
Result<Order?>.Success((Order?)null);            // ⚠ VCR0070

// 5. Implicit conversion z null (return null w metodzie → Result<T?>)
Result<Order?> GetOrder() => (Order?)null;       // ⚠ VCR0070

// 6. default literal w return (gdy return type to Result<T?>)
Result<Order?> GetOrder() => default;            // ⚠ VCR0070 (default Result<T?> == default struct)
```

### Fałszywe pozytywy do uniknięcia

```csharp
// ✅ Non-null wartość w nullable Result — OK
Result<Order?>.Success(new Order());

// ✅ Non-nullable T — Success nie może przyjąć null bez null!
Result<Order>.Success(new Order());

// ✅ Nullable value type z wartością — OK
Result<int?>.Success(42);

// ✅ Failure z Error — to nie Success
Result<Order?>.Failure(Error.NotFoundError("Order"));

// ✅ Zmienna, która MOŻE być null — nie analizujemy flow (Wariant 4, poza scope Fazy 1)
Order? order = GetOrderOrNull();
Result<Order?>.Success(order);  // ✅ nie raportujemy — wartość nieznana compile-time
```

## Rekomendowane naprawy

| Oryginalny kod | Intencja | Sugerowana naprawa |
|---|---|---|
| `Result<Fee?>.Success(null)` | Brak fee = OK | `Result.Success()` (non-generic Result, void success) |
| `Result<Fee?>.Success(null)` | Brak fee = błąd | `Result<Fee>.Failure(Error.NotFoundError("Fee"))` |
| `Result<Fee?>.Success(null)` | Fee opcjonalne | Zmień model: `Result<FeeResult>` gdzie `FeeResult` ma `bool HasFee` |
| `Result<Order>.Success(null!)` | Hack / workaround | Naprawić logikę — Success musi mieć wartość |

## Testy

```csharp
public class NullableSuccessAnalyzerTests
{
    // ⚠ Powinien zgłosić VCR0070
    [Fact] Task ReportsWarning_WhenSuccessWithLiteralNull()
    [Fact] Task ReportsWarning_WhenSuccessWithDefault()
    [Fact] Task ReportsWarning_WhenSuccessWithNullForgiving()
    [Fact] Task ReportsWarning_WhenSuccessWithCastNull()
    [Fact] Task ReportsWarning_WhenImplicitConversionFromNull()

    // ✅ Nie powinien zgłosić
    [Fact] Task NoWarning_WhenSuccessWithNonNullValue()
    [Fact] Task NoWarning_WhenSuccessWithNonNullValueInNullableResult()
    [Fact] Task NoWarning_WhenSuccessWithVariable()  // nie analizujemy flow
    [Fact] Task NoWarning_WhenFailureWithError()
    [Fact] Task NoWarning_WhenNonResultType()

    // Code fix
    [Fact] Task CodeFix_ReplacesSuccessNullWithFailure()
}

public class NullableResultTypeAnalyzerTests
{
    // ℹ VCR0071 (domyślnie wyłączony)
    [Fact] Task ReportsInfo_WhenResultWithNullableReferenceType()
    [Fact] Task ReportsInfo_WhenTaskResultWithNullableType()
    [Fact] Task NoInfo_WhenResultWithNonNullableType()
    [Fact] Task NoInfo_WhenDisabledByDefault()
}
```

## Implementacja

- [x] VCR0070: `NullableSuccessAnalyzer` — wykrywanie `Success(null)` / `Success(default)`
- [x] VCR0070: `NullableSuccessCodeFixProvider` — zamiana na `Failure(Error.NotFoundError(...))`
- [x] Testy VCR0070 z `CSharpAnalyzerTest` + inline `ResultStubs`
- [x] VCR0071: `NullableResultTypeAnalyzer` — flagowanie `Result<T?>` (Info, domyślnie wyłączony)
- [x] Testy VCR0071
- [x] Aktualizacja CLAUDE.md — dodanie VCR0070/VCR0071 do tabeli analizatorów
- [ ] Wydanie jako część kolejnej wersji

## Konfiguracja

```ini
# .editorconfig

# VCR0070 — domyślnie Warning, może być Error (zalecane w strict mode)
dotnet_diagnostic.VCR0070.severity = error

# VCR0071 — domyślnie wyłączony, włącz jako Info lub Warning
dotnet_diagnostic.VCR0071.severity = warning
```

## Kompatybilność wsteczna

- **Nie jest breaking change** — VCR0070 emituje Warning, nie Error
- Istniejący kod z `Success(null)` zobaczy nowe warningi
- **UWAGA:** jeśli projekt ma `TreatWarningsAsErrors=true` (jak nasz), nowy Warning stanie się compile error — to jest **pożądane** zachowanie, bo `Success(null)` to prawie zawsze bug
- Użytkownicy mogą wyłączyć via `.editorconfig` lub `#pragma warning disable VCR0070`
- VCR0071 jest domyślnie wyłączony — zero wpływu na istniejące projekty

## Priorytet

**Wysoki** — problem został znaleziony w produkcyjnym kodzie i żaden z istniejących analizatorów (VCR0010–VCR0060) go nie wykrywa. `Success(null)` prowadzi do runtime `NullReferenceException` w łańcuchu Railway, co jest dokładnie tym typem błędu, który Result pattern miał eliminować.

---

**Powiązane:**
- [ADR-0010: Roslyn Analyzer wymuszający konsumpcję Result](./ADR-0010-result-consumption-analyzer.md)
- VCR0020: Value accessed without check — komplementarny, ale wykrywa *odczyt* null, nie *tworzenie* null
- VCR0050: Failure(Error.None) — analogiczny wzorzec "semantyczny nonsens", ale dla Failure
