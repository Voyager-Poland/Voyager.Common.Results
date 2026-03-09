# ADR-0012: Wzorzec repozytorium — `NullToResult` jako most między `T?` a `Result<T>`

**Status:** Zaakceptowane
**Data:** 2026-03-09
**Kontekst:** Voyager.Common.Results — wzorzec repozytorium i wyszukiwanie encji

## Problem

W typowych aplikacjach .NET repozytoria zwracają `null` gdy encja nie istnieje:

```csharp
User? user = _repo.GetById(id);         // null = nie znaleziono
Order? order = _repo.FindByNumber(num);  // null = brak zamówienia
```

Programiści adoptujący `Result<T>` stają przed problemem: biblioteka ma tylko dwa stany — `Success(T)` i `Failure(Error)` — a brak wartości nie pasuje czysto do żadnego z nich.

### Nierozróżnialne stany

ADR-0011 odrzucił `Optional<T>` i zaproponował mitygacje (`Ensure`, ternary, `OrElse`). Wszystkie zmuszają do potraktowania braku wartości jako `Failure(Error.NotFoundError(...))`:

```csharp
// Sytuacja A: repozytorium poprawnie sprawdziło — usera nie ma
Result<User> a = Error.NotFoundError("User 5 not found");

// Sytuacja B: prawdziwy błąd systemowy zamapowany na NotFound
Result<User> b = Error.NotFoundError("User 5 not found");

// Caller dostaje identyczny Result — nie wie czy:
// - user naprawdę nie istnieje (normalny stan)
// - zapytanie się nie wykonało (prawdziwy błąd)
```

### Kiedy "brak wartości" jest błędem?

**Ta sama operacja repozytorium** może być błędem lub normalnym stanem, w zależności od kontekstu:

```csharp
// Kontekst 1: Rejestracja — null jest OCZEKIWANY (email wolny = sukces!)
User? existing = _repo.FindByEmail(email);
if (existing is null)
    return CreateUser(email);

// Kontekst 2: Fakturowanie — null jest BŁĘDEM (zamówienie musi istnieć)
Order? order = _repo.FindByNumber(orderNumber);
if (order is null)
    return Error.NotFoundError($"Order {orderNumber} not found");

// Kontekst 3: Cache — null jest NEUTRALNY (sprawdź następne źródło)
User? cached = _cache.Find(id);
```

**Wniosek:** Repozytorium nie wie czy brak wartości to błąd. To **caller** decyduje. Obecne API nie daje callerowi ergonomicznego sposobu wyrażenia tej decyzji — wymusza boilerplate przy każdym wywołaniu.

## Rozważone opcje

### Opcja A: Status quo — `ErrorType.NotFound` + ręczna konwersja

Obecne podejście z ADR-0011 (Ensure, ternary, OrElse).

**Odrzucona** — boilerplate przy każdym wywołaniu repo. `NotFound` jest semantycznie przeciążony.

### Opcja B: Minimalny `Maybe<T>` — rewizja ADR-0011

Nowy typ `Maybe<T>` jako bridge type z `Some`/`None`/`Map`/`Bind`/`Match`/`ToResult`.

**Odrzucona** — nowy typ to dodatkowa koncepcja do nauki. Użytkownicy muszą zrozumieć kiedy `Maybe<T>` vs `Result<T>` vs `T?` — trzy sposoby wyrażenia "wartość opcjonalna" to za dużo. Repozytoria w .NET konwencjonalnie zwracają `T?`, wymuszanie `Maybe<T>` na granicy repozytorium to walka z ekosystemem.

### Opcja C: Nowy `ErrorType.Empty`

Dedykowany typ błędu oddzielony od `NotFound`.

**Odrzucona** — modeluje nie-błąd jako `Failure`, co jest fundamentalną sprzecznością. Jaki `ToHttpStatusCode()`? Propaguje jak błąd w łańcuchu Railway.

### Opcja D: `Result<T?>` z `Success(null)`

**Odrzucona** — łamie kontrakt `Value` (VCR0020), łamie `result.Value!` w collection extensions, narusza monad laws. Już odrzucone w ADR-0011.

### Opcja E: `NullToResult` extension method z uproszczonym API ✅

Pragmatyczne rozwiązanie "80/20": extension method z parametrem `string` zamiast `Error`, bo 90% konwersji `null → Result` to `NotFoundError`:

```csharp
// Zamiast (verbose):
_repo.Find(id).ToResult(Error.NotFoundError($"User {id} not found"))

// Proste API (tylko string):
_repo.Find(id).NullToResult($"User {id} not found")
```

## Decyzja

**Dodać `NullToResult<T>()` extension methods + `IsNotFound()` helper.**

### API

```csharp
public static class NullableResultExtensions
{
    // === NullToResult: T? → Result<T> ===

    /// <summary>
    /// Converts a nullable reference to Result — Success if non-null,
    /// Failure with NotFoundError if null.
    /// </summary>
    public static Result<T> NullToResult<T>(this T? value, string message) where T : class
        => value is not null ? value : Error.NotFoundError(message);

    /// <summary>
    /// Converts a nullable value type to Result — Success if HasValue,
    /// Failure with NotFoundError if null.
    /// </summary>
    public static Result<T> NullToResult<T>(this T? value, string message) where T : struct
        => value.HasValue ? value.Value : Error.NotFoundError(message);

    // === Overloady z pełnym Error — gdy potrzebujesz innego ErrorType ===

    /// <summary>
    /// Converts a nullable reference to Result with a custom error.
    /// Use when NotFoundError is not the right error type.
    /// </summary>
    public static Result<T> NullToResult<T>(this T? value, Error error) where T : class
        => value is not null ? value : error;

    /// <summary>
    /// Converts a nullable value type to Result with a custom error.
    /// </summary>
    public static Result<T> NullToResult<T>(this T? value, Error error) where T : struct
        => value.HasValue ? value.Value : error;
}
```

```csharp
public static class ResultQueryExtensions
{
    /// <summary>
    /// Returns true if the result is a failure with ErrorType.NotFound.
    /// </summary>
    public static bool IsNotFound(this Result result)
        => result.IsFailure && result.Error.Type == ErrorType.NotFound;

    /// <summary>
    /// Returns true if the result is a failure with ErrorType.NotFound.
    /// </summary>
    public static bool IsNotFound<T>(this Result<T> result)
        => result.IsFailure && result.Error.Type == ErrorType.NotFound;
}
```

### Uzasadnienie

1. **Nazwa `NullToResult`** — jawnie mówi co robi: konwertuje nullable na Result. Lepsza niż `ToResult`, bo `ToResult` jest zbyt ogólna (co konwertujemy? skąd?).

2. **Parametr `string` jako domyślny** — 90% użyć to `NotFoundError` z komunikatem. Overload z `Error` istnieje dla rzadkich przypadków (np. `ValidationError`).

3. **`IsNotFound()`** — prosty helper na `ErrorType.NotFound`. Nie próbujemy rozróżniać "null z repo" vs "prawdziwy 404" — w praktyce caller zazwyczaj nie potrzebuje tej informacji. Jeśli potrzebuje, może użyć `Error.Code` do rozróżnienia.

4. **Rezygnacja z `Maybe<T>`** — trzy typy dla "wartość opcjonalna" (`T?`, `Maybe<T>`, `Result<T>`) to za dużo koncepcji. Repozytoria w .NET zwracają `T?` — nie walczymy z konwencją, tylko dajemy ergonomiczny most do Railway.

### Wzorce użycia

```csharp
// === Punkt wejścia do Railway (główny use case) ===

Result<Invoice> CreateInvoice(string orderNumber) =>
    _repo.FindByNumber(orderNumber)
        .NullToResult($"Order {orderNumber} not found")
        .Bind(order => _invoiceService.Generate(order))
        .Tap(invoice => _repo.Save(invoice));

// === Cache → Database fallback ===

Result<User> GetUser(int id) =>
    _cache.Find(id)
        .NullToResult($"User {id} not in cache")
        .OrElse(() => _db.Find(id)
            .NullToResult($"User {id} not found"));

// === Nullable value type ===

Result<DateTime> GetLastLogin(int userId) =>
    _repo.GetLastLoginDate(userId)   // DateTime?
        .NullToResult($"No login record for user {userId}");

// === Walidacja obecności wielu encji ===

Result<(User buyer, Product product)> ValidatePurchase(int userId, int productId) =>
    _userRepo.Find(userId)
        .NullToResult($"User {userId} not found")
        .Bind(user => _productRepo.Find(productId)
            .NullToResult($"Product {productId} not found")
            .Map(product => (user, product)));

// === Rzadki przypadek: inny ErrorType niż NotFound ===

Result<Config> GetRequiredConfig(string key) =>
    _configProvider.Get(key)
        .NullToResult(Error.ValidationError($"Required config '{key}' is missing"));

// === Sprawdzenie wyniku ===

var result = _repo.Find(id).NullToResult($"User {id}");
if (result.IsNotFound())
    // obsłuż brak — np. zwróć 404, zaloguj, użyj domyślnej wartości
```

### Kiedy NIE używać `NullToResult`

```csharp
// ❌ Nie opakowuj w Result jeśli null jest normalnym stanem
bool IsEmailAvailable(string email)
{
    User? existing = _repo.FindByEmail(email);
    return existing is null;  // T? wystarczy — nie potrzebujesz Result
}

// ❌ Nie używaj NullToResult w środku łańcucha Result — użyj Bind
result.Map(x => _repo.Find(x.Id))              // ❌ Result<User?>
result.Bind(x => _repo.Find(x.Id)              // ✅
    .NullToResult($"User {x.Id} not found"))

// ❌ Nie używaj gdy masz już Result z repozytorium
// Jeśli repo rzuca wyjątek — użyj Result<T>.Try()
Result<User>.Try(() => _repo.GetOrThrow(id))    // wyjątek → Failure
```

## Konsekwencje

### Pozytywne
- Eliminuje boilerplate przy konwersji `T?` → `Result<T>`
- Ergonomiczny punkt wejścia do Railway z repozytoriów
- Zero nowych typów — `T?` pozostaje konwencją repozytoriów
- Spójne z ADR-0011 (rozszerzenie "Alternatywy 3")
- `IsNotFound()` daje prosty sposób na sprawdzenie wyniku

### Negatywne
- `ErrorType.NotFound` pozostaje semantycznie przeciążony ("null z repo" vs "prawdziwy 404")
- Caller nadal musi zdecydować: zostać w `T?` czy przejść do `Result<T>`

### Mitigacja
- Akceptujemy przeciążenie `NotFound` — w praktyce rozróżnienie rzadko potrzebne. Gdy jest potrzebne, `Error.Code` pozwala na konwencyjne rozróżnienie
- Dokumentacja wzorców (kiedy `T?`, kiedy `NullToResult`)

---

**Powiązane:**
- [ADR-0011: Rezygnacja z typu Optional\<T\>](./ADR-0011-no-optional-type.md) — decyzja o braku Optional, Alternatywa 3 (ToResult)
- [ADR-0005: Error Classification for Resilience](./ADR-0005-error-classification-for-resilience.md) — klasyfikacja ErrorType.NotFound
