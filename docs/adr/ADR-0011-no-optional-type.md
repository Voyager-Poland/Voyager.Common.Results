# ADR-0011: Rezygnacja z typu Optional\<T\>

**Status:** Zaakceptowane
**Data:** 2026-02-23
**Kontekst:** Voyager.Common.Results

## Problem

W ekosystemie programowania funkcyjnego obok typu `Result<T>` (sukces/porażka) często występuje typ `Optional<T>` (aka `Maybe<T>`, `Option<T>`), który modeluje **obecność lub brak wartości** bez kontekstu błędu:

```csharp
// Result<T> — operacja się powiodła lub nie (z informacją o błędzie)
Result<User> GetUser(int id);  // Success(user) | Failure(error)

// Optional<T> — wartość jest lub jej nie ma (bez informacji dlaczego)
Optional<User> FindUser(int id);  // Some(user) | None
```

**Pytanie:** Czy Voyager.Common.Results powinien dostarczać typ `Optional<T>` jako uzupełnienie `Result<T>`?

### Motywacja

Istnieją scenariusze, gdzie brak wartości **nie jest błędem**:

```csharp
// Cache hit/miss — brak w cache to normalny stan, nie error
Optional<User> GetFromCache(int id);

// Opcjonalne pole — użytkownik może nie mieć drugiego imienia
Optional<string> MiddleName { get; }

// Lookup — brak wyniku to informacja, nie awaria
Optional<Config> FindOverride(string key);
```

W tych przypadkach `Result<T>` z `Error.NotFoundError(...)` jest semantycznie nieprecyzyjny — wymusza traktowanie normalnej sytuacji jako błędu.

## Decyzja

**Nie dodawać typu `Optional<T>` do biblioteki.**

Brak wartości modelujemy poprzez istniejące mechanizmy:
1. `Result<T>` z `ErrorType.NotFound` — gdy brak jest wynikiem operacji, która powinna zwrócić wartość
2. `T?` (nullable reference types) — gdy brak jest naturalnym stanem
3. `GetValueOrDefault()` — bezpieczne wyciągnięcie wartości z fallbackiem

## Uzasadnienie

### 1. Duplikacja API (80% pokrycia)

`Optional<T>` wymagałby praktycznie identycznego zestawu operatorów jak `Result<T>`:

| Operator | `Result<T>` | `Optional<T>` |
|----------|------------|---------------|
| `Map` | ✅ | wymagany |
| `Bind` | ✅ | wymagany |
| `Match` | ✅ | wymagany |
| `GetValueOrDefault` | ✅ | wymagany |
| `OrElse` | ✅ | wymagany |
| `Tap` | ✅ | wymagany |

Różnica sprowadza się do braku `Error` w gałęzi `None` — to jedyny semantyczny zysk, kosztem zduplikowanego kodu.

### 2. Eksplozja kombinatoryczna

Dodanie `Optional<T>` wymaga obsługi konwersji i kombinacji z istniejącymi typami:

```
Optional<T> → Result<T>          // ToResult(Error ifNone)
Result<T>   → Optional<T>        // ToOptional() (gubi Error)
Task<Optional<T>>                // MapAsync, BindAsync, MatchAsync...
IEnumerable<Optional<T>>         // Combine, Partition, Traverse...
Optional<T> + Result<T>          // .Bind(x => resultOp(x))  — jaki typ zwracany?
```

Każdy nowy typ podnosi liczbę kombinacji **kwadratowo**. Dla jednego typu (`Result<T>`) mamy jeden zestaw operatorów. Dla dwóch typów potrzebujemy:
- Operatory `Optional<T>` → `Optional<T>`
- Operatory `Optional<T>` → `Result<T>`
- Operatory `Result<T>` → `Optional<T>`
- Warianty async dla każdego z powyższych
- Warianty kolekcyjne dla każdego z powyższych

### 3. C# Nullable Reference Types pokrywają główny use case

Od C# 8.0 kompilator zapewnia statyczną analizę nulli:

```csharp
// Kompilator ostrzega gdy T? użyty bez sprawdzenia null
User? user = _cache.Find(id);
user.Name;  // ⚠ CS8602: Dereference of a possibly null reference

if (user is not null)
    user.Name;  // ✅ OK
```

NRT rozwiązują problem "wartość może nie istnieć" na poziomie kompilatora, bez dodatkowego wrappera. `Optional<T>` miałby sens w Javie lub starszym C#, ale NRT znacząco redukują potrzebę.

### 4. Filozofia biblioteki: brak wartości = failure

Railway Oriented Programming celowo **wymusza obsługę** ścieżki błędu. `Optional<T>` z `None` jest łatwiejszy do zignorowania niż `Result<T>` z `Failure(error)`:

```csharp
// Optional — łatwo zignorować brak
var user = FindUser(id);
user.Map(u => u.Name);  // None → None, cicho propagowane

// Result — wymusza świadomą decyzję (VCR0010 analyzer!)
var user = GetUser(id);  // ⚠ VCR0010 jeśli nie skonsumujesz
user.Map(u => u.Name);   // Failure propaguje Error z kontekstem
```

Analyzer VCR0010 wymusza konsumpcję `Result`. Analogiczny analyzer dla `Optional` byłby mniej użyteczny — `None` nie niesie kontekstu, więc "skonsumowanie" go jest mniej wartościowe.

### 5. Scope creep

Biblioteka jest "zero dependencies", mała i fokusowa. Dodanie `Optional<T>` to:
- Nowy typ z pełnym API (Map, Bind, Match, Ensure, Tap, OrElse, Finally)
- Warianty async (MapAsync, BindAsync, MatchAsync, TapAsync, OrElseAsync)
- Operacje kolekcyjne (Combine, Partition, Traverse)
- Konwersje Optional↔Result
- Nowe analyzery (analogiczne do VCR0010–VCR0060)
- Dokumentacja, testy, README
- Utrzymanie i wersjonowanie

To **podwojenie** surface area biblioteki.

## Alternatywy rozważone

### Alternatywa 1: Pełny typ `Optional<T>` w bibliotece

Nowy `readonly record struct Optional<T>` z `Some(T)` / `None`, pełnym zestawem operatorów i analyzerów.

**Odrzucona** — powody opisane powyżej (duplikacja, kombinatoryczna eksplozja, scope creep).

### Alternatywa 2: `Optional<T>` jako osobny pakiet NuGet

`Voyager.Common.Optional` — oddzielny pakiet z zależnością na `Voyager.Common.Results` dla konwersji.

**Odrzucona** — zmniejsza scope creep w głównym pakiecie, ale nie eliminuje problemu duplikacji API i kombinatorycznej eksplozji konwersji. Dwa pakiety do utrzymania zamiast jednego.

### Alternatywa 3: Extension method `ToResult<T>` dla nullable (wybrana jako uzupełnienie)

Zamiast nowego typu, prosty helper konwertujący nullable na Result:

```csharp
public static Result<T> ToResult<T>(this T? value, Error error) where T : class
    => value is not null ? value : error;

public static Result<T> ToResult<T>(this T? value, Func<Error> errorFactory) where T : class
    => value is not null ? value : errorFactory();

// Użycie:
User? user = _cache.Find(id);
Result<User> result = user.ToResult(Error.NotFoundError($"User {id} not found"));
```

**Status:** Do rozważenia w przyszłości jako lekkie uzupełnienie istniejącego API, bez wprowadzania nowego typu. Nie wymaga osobnego ADR — to standardowy extension method.

### Alternatywa 4: Użycie `Result<T?>` z nullable T

```csharp
// Success z null oznacza "operacja się powiodła, ale wartości nie ma"
Result<User?> FindUser(int id) =>
    Result<User?>.Success(_cache.Find(id));  // Success(null) = znaleziono brak

// Sprawdzenie:
result.Map(user => user?.Name ?? "unknown");
```

**Odrzucona** — semantycznie nieczytelne. `Success(null)` to sprzeczność (sukces bez wartości?). Narusza zasadę że Success zawiera wartość.

## Konsekwencje

### Pozytywne
- Biblioteka pozostaje mała, spójna i łatwa do nauki (jeden główny typ)
- Brak duplikacji API i kombinatorycznej eksplozji
- Istniejące analyzery (VCR0010–VCR0060) wystarczają
- Użytkownicy korzystają z NRT (`T?`) dla scenariuszy "wartość opcjonalna"

### Negatywne
- Scenariusze "brak wartości nie jest błędem" wymagają użycia `ErrorType.NotFound` lub `T?` poza łańcuchem Result
- Brak idiomatic way na wyrażenie "optional value" w Railway chain — przejście z `T?` na `Result<T>` wymaga jawnej konwersji (`.ToResult(error)`)
- Użytkownicy przyzwyczajeni do F# `Option<'T>` lub Rust `Option<T>` mogą odczuć brak

### Mitigacja

Dla użytkowników, którzy potrzebują semantyki "wartość opcjonalna":

```csharp
// Wzorzec 1: Ensure na null
Result<User>.Try(() => _repo.Find(id))
    .Ensure(u => u is not null, Error.NotFoundError($"User {id}"));

// Wzorzec 2: Ternary z implicit conversion
Result<User> FindUser(int id) =>
    _repo.Find(id) is { } user ? user : Error.NotFoundError($"User {id}");

// Wzorzec 3: OrElse jako fallback
GetFromCache(id)
    .OrElse(() => GetFromDatabase(id))
    .OrElse(() => Error.NotFoundError($"User {id}"));
```

---

**Powiązane:**
- [ADR-0005: Error Classification for Resilience](./ADR-0005-error-classification-for-resilience.md) — klasyfikacja ErrorType.NotFound
- [ADR-0010: Roslyn Analyzer wymuszający konsumpcję Result](./ADR-0010-result-consumption-analyzer.md) — VCR0010 wymusza obsługę Result
- F# Option type: https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/options
- Rust Option: https://doc.rust-lang.org/std/option/
