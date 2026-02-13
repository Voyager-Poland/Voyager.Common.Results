# ADR-0010: Roslyn Analyzer wymuszający konsumpcję Result

**Status:** Zaproponowano
**Data:** 2026-02-11
**Kontekst:** Voyager.Common.Results

## Problem

Metoda zwracająca `Result` lub `Result<T>` może zostać wywołana bez sprawdzenia wyniku. W przeciwieństwie do wyjątków, które przerywają wykonanie, zignorowany `Result` powoduje **ciche zgubienie błędu**:

```csharp
// Wyjątek - nie da się zignorować
void UpdateUser(User u) { throw new InvalidOperationException("DB error"); }
UpdateUser(user); // 💥 crash — wiadomo że się nie udało

// Result - łatwo zignorować
Result UpdateUser(User u) => Result.Failure(Error.DatabaseError("Connection refused"));
UpdateUser(user); // ✅ kompiluje się — błąd przepadł w ciszy
```

**Konsekwencje zignorowanego Result:**
1. Użytkownik nie widzi informacji o błędzie
2. Dalszy kod operuje na niespójnym stanie (np. zakłada że user został zapisany)
3. Debugging jest utrudniony — brak śladu po operacji, która się nie powiodła
4. Podważa sens stosowania wzorca Result zamiast wyjątków

**Problem jest szczególnie groźny przy migracji z void/exception na Result** — programista zmienia sygnaturę metody z `void` na `Result`, ale callsite'y nie są zaktualizowane i kompilują się bez ostrzeżeń.

## Decyzja

Stworzyć Roslyn Analyzer dostarczany jako część pakietu NuGet `Voyager.Common.Results`, który generuje **warning** gdy wartość `Result` lub `Result<T>` nie jest skonsumowana.

### Diagnostyka

| ID | Severity | Komunikat |
|---|---|---|
| `VCR0010` | Warning | Result of '{methodName}' must be checked. Ignoring a Result silently discards potential errors. |

### Co jest traktowane jako konsumpcja

Analyzer **NIE** zgłasza warningów w następujących przypadkach:

```csharp
// 1. Przypisanie do zmiennej
var result = UpdateUser(user);

// 2. Jawny discard
_ = UpdateUser(user);

// 3. Użycie w wyrażeniu (method chaining)
UpdateUser(user).Switch(
    () => Console.WriteLine("OK"),
    err => Console.WriteLine(err.Message));

// 4. Przekazanie jako argument
LogResult(UpdateUser(user));

// 5. Return
return UpdateUser(user);

// 6. Użycie w warunku
if (UpdateUser(user).IsSuccess) { ... }

// 7. Await na Task<Result>
var result = await UpdateUserAsync(user);
await UpdateUserAsync(user); // ⚠ TO powinno być wykrywane — Task<Result> skonsumowany, ale Result nie
```

### Co jest traktowane jako niekonsumpcja

```csharp
// ExpressionStatement, gdzie wyrażenie zwraca Result/Result<T>
UpdateUser(user);                    // ⚠ VCR0010
await UpdateUserAsync(user);         // ⚠ VCR0010 (Task skonsumowany, Result nie)
```

### Struktura projektu

```
src/
  Voyager.Common.Results.Analyzers/
    Voyager.Common.Results.Analyzers.csproj                    // netstandard2.0 (wymagane dla Roslyn)
    ResultMustBeConsumedAnalyzer.cs                            // VCR0010 DiagnosticAnalyzer
    ResultMustBeConsumedCodeFixProvider.cs                     // VCR0010 CodeFix: `_ = ` lub `var result = `
    ResultValueAccessedWithoutCheckAnalyzer.cs                 // VCR0020 DiagnosticAnalyzer
    ResultValueAccessedWithoutCheckCodeFixProvider.cs          // VCR0020 CodeFix: `.GetValueOrThrow()` lub `if (IsSuccess)`
    NestedResultCodeFixProvider.cs                             // VCR0030 CodeFix: `Map` → `Bind`
  Voyager.Common.Results.Analyzers.Tests/
    Voyager.Common.Results.Analyzers.Tests.csproj
    ResultMustBeConsumedAnalyzerTests.cs
    ResultValueAccessedWithoutCheckAnalyzerTests.cs
    NestedResultAnalyzerTests.cs
```

### Dostarczanie via NuGet

Analyzer jest pakowany razem z biblioteką w `Voyager.Common.Results.csproj`:

```xml
<ItemGroup>
  <None Include="..\Voyager.Common.Results.Analyzers\bin\$(Configuration)\netstandard2.0\Voyager.Common.Results.Analyzers.dll"
        Pack="true"
        PackagePath="analyzers/dotnet/cs"
        Visible="false" />
</ItemGroup>
```

Dzięki temu każdy konsument pakietu automatycznie otrzymuje analyzer — nie trzeba instalować dodatkowego NuGet.

### Implementacja analyzera (szkic)

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResultMustBeConsumedAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "VCR0010";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Result must be consumed",
        messageFormat: "Result of '{0}' must be checked. Ignoring a Result silently discards potential errors.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Methods returning Result or Result<T> must have their return value checked. "
                   + "Ignoring the result means errors are silently lost.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeExpressionStatement, OperationKind.ExpressionStatement);
    }

    private static void AnalyzeExpressionStatement(OperationAnalysisContext context)
    {
        var expressionStatement = (IExpressionStatementOperation)context.Operation;
        var operation = expressionStatement.Operation;

        // Obsługa await — sprawdź typ wewnętrzny Task<Result>
        if (operation is IAwaitOperation awaitOp)
            operation = awaitOp.Operation;

        var returnType = operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod.ReturnType,
            IPropertyReferenceOperation prop => prop.Type,
            _ => null
        };

        if (returnType is null) return;

        // Unwrap Task<T> → T
        if (returnType is INamedTypeSymbol { IsGenericType: true } namedType
            && namedType.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.Task<TResult>")
        {
            returnType = namedType.TypeArguments[0];
        }

        if (!IsResultType(returnType)) return;

        var methodName = operation switch
        {
            IInvocationOperation inv => inv.TargetMethod.Name,
            _ => returnType.Name
        };

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, expressionStatement.Syntax.GetLocation(), methodName));
    }

    private static bool IsResultType(ITypeSymbol? type)
    {
        if (type is null) return false;

        // Sprawdź Result i Result<T> z namespace Voyager.Common.Results
        return type.Name is "Result"
            && type.ContainingNamespace?.ToDisplayString() == "Voyager.Common.Results";
    }
}
```

### Code Fix Provider

```csharp
[ExportCodeFixProvider(LanguageNames.CSharp)]
public sealed class ResultMustBeConsumedCodeFixProvider : CodeFixProvider
{
    // Oferuje dwie opcje naprawy:
    // 1. "Assign to variable" → var result = UpdateUser(user);
    // 2. "Discard result"     → _ = UpdateUser(user);
}
```

## Alternatywy rozważone

### Alternatywa 1: Atrybut `[MustUseReturnValue]` z JetBrains.Annotations

```csharp
[MustUseReturnValue("Result must be checked")]
public Result UpdateUser(User u) { ... }
```

**Odrzucona:**
- Działa **tylko** w ReSharper/Rider — nie w VS Code, Visual Studio bez ReSharper, ani na CI (`dotnet build`)
- Wymaga dodania atrybutu na każdej metodzie zwracającej Result (łatwo zapomnieć)
- Zależność od pakietu JetBrains.Annotations

### Alternatywa 2: Reguła `.editorconfig` CA1806

```ini
dotnet_diagnostic.CA1806.severity = warning
```

**Odrzucona:**
- CA1806 ("Do not ignore method return values") domyślnie dotyczy tylko wybranych metod BCL
- Konfiguracja typów jest ograniczona i nieelegancka
- Nie można dostosować komunikatu błędu

### Alternatywa 3: Destruktor/Finalizer w Result

```csharp
public class Result : IDisposable
{
    private bool _consumed;
    ~Result() { if (!_consumed) Debug.Fail("Result not consumed"); }
}
```

**Odrzucona:**
- Result jest `record` (value semantics) — dodanie finalizer zmienia semantykę
- Wydajność: finalizer queue, GC pressure
- Nieprzewidywalny timing (GC non-deterministic)
- Nie działa w compile-time — błąd dopiero w runtime (i to z opóźnieniem)

### Alternatywa 4: Brak mechanizmu (status quo)

**Odrzucona:**
- Problem jest realny — ciche gubienie błędów podważa sens wzorca Result
- Inne ekosystemy rozwiązały to (Rust `#[must_use]`, C++ `[[nodiscard]]`)
- Koszt implementacji analyzera jest niski, a wartość wysoka

## Konfiguracja

Użytkownicy mogą wyłączyć lub zmienić severity w `.editorconfig`:

```ini
# Zmień na error (blokuje build)
dotnet_diagnostic.VCR0010.severity = error

# Wyłącz (niezalecane)
dotnet_diagnostic.VCR0010.severity = none
```

Lub per-linia za pomocą pragma:

```csharp
#pragma warning disable VCR0010
UpdateUser(user); // Celowo ignorujemy wynik
#pragma warning restore VCR0010
```

## Testy

```csharp
public class ResultMustBeConsumedAnalyzerTests
{
    // ✅ Powinien zgłosić warning
    [Fact] Task ReportsWarning_WhenResultIgnored()
    [Fact] Task ReportsWarning_WhenResultOfGenericIgnored()
    [Fact] Task ReportsWarning_WhenAwaitedTaskResultIgnored()

    // ✅ Nie powinien zgłosić warning
    [Fact] Task NoWarning_WhenAssignedToVariable()
    [Fact] Task NoWarning_WhenDiscarded()
    [Fact] Task NoWarning_WhenUsedInMethodChain()
    [Fact] Task NoWarning_WhenPassedAsArgument()
    [Fact] Task NoWarning_WhenReturned()
    [Fact] Task NoWarning_WhenUsedInCondition()
    [Fact] Task NoWarning_ForNonResultTypes()

    // ✅ Code fix
    [Fact] Task CodeFix_AddsDiscard()
    [Fact] Task CodeFix_AddsVariableAssignment()
}
```

## Implementacja

- [x] Utworzyć projekt `Voyager.Common.Results.Analyzers` (netstandard2.0)
- [x] Zaimplementować `ResultMustBeConsumedAnalyzer`
- [x] Zaimplementować `ResultMustBeConsumedCodeFixProvider`
- [x] Testy z `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`
- [x] Skonfigurować pakowanie analyzera w `Voyager.Common.Results.csproj`
- [ ] Wydać jako część kolejnej wersji
- [x] VCR0020: Value accessed without success check
- [x] VCR0030: Nested `Result<Result<T>>`
- [x] VCR0040: `GetValueOrThrow` in railway chain
- [x] VCR0050: `Failure(Error.None)`
- [x] VCR0060: Prefer Match/Switch

## Planowane rozszerzenia (kolejne analizatory)

### VCR0020: Value accessed without success check (Warning)

Dostęp do `Result<T>.Value` bez uprzedniego sprawdzenia `IsSuccess`/`IsFailure` — analogia do Rust'owego zakazu bezpośredniego dostępu do `Option<T>` bez `unwrap`/`match`:

```csharp
// ⚠ VCR0020 — Value może być default/null gdy IsFailure
var result = GetUser(id);
Console.WriteLine(result.Value.Name);

// ✅ Sprawdzenie przed dostępem
var result = GetUser(id);
if (result.IsSuccess)
    Console.WriteLine(result.Value.Name);

// ✅ Match wymusza obsługę obu ścieżek
GetUser(id).Match(
    user => user.Name,
    error => "unknown");
```

**Implementacja:** Rejestracja na `OperationKind.PropertyReference` dla `.Value`, sprawdzenie czy w enclosing block (lub blokach nadrzędnych) istnieje wcześniejszy branch na `IsSuccess`/`IsFailure`. Code fix: zaproponuj `Match` lub dodaj guard `if (result.IsSuccess)`.

#### Rozpoznawane wzorce guard (VCR0020)

Analyzer rozpoznaje następujące wzorce jako prawidłowe sprawdzenie przed `.Value`:

```csharp
// 1. Bezpośredni if (IsSuccess) — .Value w gałęzi true
if (result.IsSuccess) { var x = result.Value; }

// 2. Negacja IsFailure — .Value w gałęzi true
if (!result.IsFailure) { var x = result.Value; }

// 3. Else po IsFailure — .Value w gałęzi false
if (result.IsFailure) { } else { var x = result.Value; }

// 4. Ternary z guardem
var x = result.IsSuccess ? result.Value : 0;

// 5. Short-circuit && z guardem
if (result.IsSuccess && result.Value > 0) { }

// 6. Early return (guard clause) — guard i .Value na tym samym poziomie
if (result.IsFailure) return;
var x = result.Value;

// 7. Guard w bloku nadrzędnym — .Value zagnieżdżony w wewnętrznym if/foreach/etc.
if (result.IsFailure) return Result.Failure(result.Error);
if (result.Value != null)
{
    list.Add(result.Value);  // ✅ guard jest w bloku nadrzędnym
}

// 9. Guard z continue/break w pętli — analogicznie do return/throw
foreach (var item in items)
{
    var result = Process(item);
    if (result.IsFailure) { errors.Add(result.Error); continue; }
    list.Add(result.Value);  // ✅ continue gwarantuje wyjście z iteracji
}

// 8. Guard z reassignment do Success — gałąź failure naprawia zmienną
var result = Compute();
if (result.IsFailure)
{
    var fallback = GetFallback();
    if (fallback.IsFailure) return Result.Failure(fallback.Error);
    result = Result<T>.Success(fallback.Value);  // ← ostatnia instrukcja to reassignment
}
var x = result.Value;  // ✅ po bloku zmienna gwarantuje success
```

**Wzorzec 9 (continue/break w pętli):** Analyzer traktuje `continue` i `break` tak samo jak `return`/`throw` — jako gwarancję wyjścia z bieżącego scope. Guard `if (result.IsFailure) { continue; }` w pętli `foreach`/`for`/`while` chroni dalszy kod w tej iteracji.

**Wzorzec 7 (guard w bloku nadrzędnym):** Analyzer przeszukuje nie tylko bezpośrednio otaczający blok, ale traversuje w górę drzewa bloków. Dzięki temu guard `if (x.IsFailure) return;` w bloku `foreach` lub metody chroni `.Value` wewnątrz zagnieżdżonego `if`.

**Wzorzec 8 (reassignment do Success):** Gdy gałąź `IsFailure` nie zawiera bezwarunkowego `return`/`throw`, ale jej **ostatnia instrukcja** to przypisanie `result = Result<T>.Success(...)`, analyzer uznaje to za gwarancję sukcesu po bloku — zmienna jest albo oryginalna (success, bo guard się nie uruchomił) albo nadpisana nową wartością success.

#### Code Fix 1: `GetValueOrThrow()`

VCR0020 oferuje Code Fix, który zamienia niesprawdzony `.Value` na `GetValueOrThrow()`:

```csharp
// Przed (⚠ VCR0020)
var x = result.Value;

// Po zastosowaniu code fix (✅ jawna intencja)
var x = result.GetValueOrThrow();
```

**Kiedy `GetValueOrThrow()` jest uzasadnione:**
- **Testy** — w testach chcemy szybko wyciągnąć wartość, a `GetValueOrThrow` daje czytelny stack trace
- **Kontrolery/Handlery** — na granicy systemu, gdzie i tak obsługujemy wyjątki (middleware)
- **Top-level code** — skrypty, konsolowe narzędzia, seedy bazy danych
- **Adaptery** — integracja z kodem, który nie rozumie Result pattern

Code fix zachowuje dalsze wywołania łańcuchowe:

```csharp
// Przed
var len = result.Value.Length;

// Po
var len = result.GetValueOrThrow().Length;
```

#### Code Fix 2: Add `IsSuccess` guard

Drugi Code Fix opakowuje instrukcję zawierającą `.Value` w blok `if (result.IsSuccess)`:

```csharp
// Przed (⚠ VCR0020)
var x = result.Value;

// Po zastosowaniu code fix (✅ guard chroni dostęp)
if (result.IsSuccess)
{
	var x = result.Value;
}
```

**Kiedy guard jest lepszy niż `GetValueOrThrow()`:**
- **Kod produkcyjny** — gdy chcemy obsłużyć oba przypadki (success + failure)
- **Railway Oriented Programming** — gdy kod powinien kontynuować łańcuch bez wyjątków

### VCR0030: Nested `Result<Result<T>>` (Warning)

Podwójne owinięcie wynika prawie zawsze z użycia `Map` zamiast `Bind`:

```csharp
// ⚠ VCR0030 — Result<Result<Order>>
var nested = userId.Map(id => GetOrder(id));

// ✅ Bind spłaszcza strukturę
var flat = userId.Bind(id => GetOrder(id));
```

**Implementacja:** Rejestracja na `OperationKind.Invocation` dla metod `Map`/`MapAsync`. Sprawdzenie czy typ zwracany to `Result<Result<T>>`.

#### Code Fix: Replace `Map` with `Bind`

```csharp
// Przed (⚠ VCR0030)
var nested = userId.Map(id => GetOrder(id));

// Po zastosowaniu code fix (✅ spłaszczone)
var flat = userId.Bind(id => GetOrder(id));
```

Analogicznie `MapAsync` jest zamieniany na `BindAsync`.

### VCR0040: `GetValueOrThrow` in railway chain (Info)

Użycie `GetValueOrThrow()` przywraca semantykę wyjątków, niwelując sens Result pattern:

```csharp
// ⚠ VCR0040 — przywraca wyjątki w środku łańcucha
var user = GetUser(id).GetValueOrThrow();
var order = GetOrder(user.Id).GetValueOrThrow();

// ✅ Railway — błędy propagują się automatycznie
var order = GetUser(id)
    .Bind(user => GetOrder(user.Id));
```

**Severity:** `Info` — na granicach systemu (kontrolery, handlery) `GetValueOrThrow` bywa uzasadniony.

### VCR0050: `Failure` with `Error.None` (Error)

Semantyczny nonsens — tworzenie failure bez błędu:

```csharp
// ⚠ VCR0050 — failure z Error.None nie ma sensu
return Result.Failure(Error.None);
return Result<User>.Failure(Error.None);
```

**Implementacja:** Rejestracja na `OperationKind.Invocation` dla metod `Failure`. Sprawdzenie czy argument to `Error.None`. Severity: `Error` — to zawsze bug.

### VCR0060: Prefer `Match`/`Switch` over `IsSuccess` branching (Suggestion, domyślnie wyłączony)

Wskazówka stylistyczna promująca functional approach:

```csharp
// OK ale mniej idiomatyczne
if (result.IsSuccess)
    DoA(result.Value);
else
    DoB(result.Error);

// ✅ Idiomatyczne — exhaustive, kompilator wymusza oba ramiona
result.Switch(
    value => DoA(value),
    error => DoB(error));
```

**Severity:** `Suggestion`, `isEnabledByDefault: false` — to preferencja stylistyczna, nie bug.

### Podsumowanie planowanych reguł

| ID | Nazwa | Severity | CodeFix |
|---|---|---|---|
| VCR0010 | Result must be consumed | Warning | `_ = ...` / `var result = ...` |
| VCR0020 | Value accessed without success check | Warning | `.GetValueOrThrow()` / `if (IsSuccess)` guard |
| VCR0030 | Nested `Result<Result<T>>` | Warning | `Map` → `Bind` |
| VCR0040 | `GetValueOrThrow` in railway chain | Info | — |
| VCR0050 | `Failure(Error.None)` | Error | — |
| VCR0060 | Prefer Match/Switch | Suggestion | — |

## Kompatybilność wsteczna

- **Nie jest breaking change** — analyzer emituje warning, nie error
- Istniejący kod, który ignoruje Result, zobaczy nowe warningi
- Użytkownicy mogą wyłączyć via `.editorconfig` lub `#pragma`
- Dodanie `_ = ` (jawny discard) jest minimalną zmianą żeby uciszyć warning

---

**Powiązane:**
- [ADR-0005: Error Classification for Resilience](./ADR-0005-error-classification-for-resilience.md)
- [Roslyn Analyzer Tutorial](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix)
- Rust `#[must_use]`: https://doc.rust-lang.org/reference/attributes/diagnostics.html#the-must_use-attribute
