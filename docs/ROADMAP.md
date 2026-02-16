# Roadmap — Voyager.Common.Results

Plan rozwoju komponentu. Priorytety mogą się zmieniać w zależności od potrzeb projektowych.

---

## v1.8.0 — Analyzery: CodeFix providers + ulepszenia

**Cel:** Zamknięcie luk w analyzerach z ADR-0010.

- [ ] **VCR0040 CodeFix** — `GetValueOrThrow()` → `Match(onSuccess, onFailure)` refactoring
- [ ] **VCR0050 CodeFix** — `Failure(Error.None)` → `Failure(Error.UnexpectedError(...))` / usunięcie `Error.None`
- [ ] **VCR0060 CodeFix** — `if (result.IsSuccess) { ... } else { ... }` → `result.Match(...)` / `result.Switch(...)`
- [ ] **VCR0010 — inteligentna supresja w testach** — nie zgłaszaj warningów dla `.Tap()` / `.Switch()` w projektach testowych (heurystyka: xUnit/NUnit/MSTest attributes w scope)
- [ ] **VCR0020 — rozpoznawanie Assert jako guard** — `Assert.That(result.IsSuccess, Is.True)` powinien wyciszać VCR0020 (xUnit `Assert.True`, NUnit `Assert.That`, FluentAssertions `result.IsSuccess.Should().BeTrue()`)
- [ ] **HelpLinkUri + strony dokumentacji** — dodanie `helpLinkUri` do wszystkich 6 `DiagnosticDescriptor` (VCR0010-VCR0060) tak aby kliknięcie na identyfikator w VS/Rider otwierało stronę z opisem reguły, przykładami i sugerowanymi poprawkami (GitHub Pages lub `docs/analyzers/VCR00xx.md` w repo)

## v2.0.0 — .NET 9 + usunięcie .NET 6

**Cel:** Modernizacja target frameworks, breaking change w wersji major.

- [ ] **Dodanie net9.0** do `Directory.Build.props`
- [ ] **Usunięcie net6.0** (EOL: listopad 2024) — breaking change
- [ ] **Target frameworks:** net48, net8.0, net9.0
- [ ] Wykorzystanie nowych API z .NET 9 (jeśli dają wartość)
- [ ] Aktualizacja CI matrixa (ubuntu + windows)
- [ ] Aktualizacja zależności: xUnit, Microsoft.CodeAnalysis, coverlet

## v2.1.0 — Result<T> dla kolekcji i batch operations

**Cel:** Lepsze wsparcie dla operacji na kolekcjach w architekturze railway.

- [ ] **`TraverseAsync`** — `IEnumerable<T>` → `Result<List<TOut>>` z operacją Result-returning per element (fail-fast)
- [ ] **`TraverseAllAsync`** — jak wyżej, ale zbiera WSZYSTKIE błędy (nie fail-fast)
- [ ] **`PartitionAsync`** — async wersja `Partition`
- [ ] **`CombineAsync`** — async wersja `Combine`
- [ ] **`Result<T>.Combine(Result<T2>)`** — łączenie dwóch Result w tuple `Result<(T1, T2)>`
- [ ] Rozszerzenie `Combine` o warianty 3-4 argumentów: `Result<(T1, T2, T3)>`

## v2.2.0 — Resilience v2: Timeout + Bulkhead

**Cel:** Rozszerzenie `Voyager.Common.Resilience` o nowe wzorce.

- [ ] **TimeoutPolicy** — `BindWithTimeoutAsync(func, timeout)` z automatyczną konwersją na `ErrorType.Timeout`
- [ ] **BulkheadPolicy** — ograniczenie równoległych wywołań (SemaphoreSlim-based), `ErrorType.TooManyRequests` przy przekroczeniu
- [ ] **PolicyWrap** — łączenie policies: `Retry + CircuitBreaker + Timeout` w pipeline
- [ ] **Circuit Breaker sliding window** — zamiast consecutive failures, okno czasowe z progiem procentowym

## v2.3.0 — Diagnostyka i obserwowalność

**Cel:** Integracja z ekosystemem .NET observability.

- [ ] **`Result<T>.ToActivityTags()`** — OpenTelemetry: automatyczne tagi `result.status`, `error.type`, `error.code` na `Activity`
- [ ] **Middleware ASP.NET Core** — automatyczna konwersja `Result<T>` na HTTP response z właściwym status code (bazując na `ErrorType.ToHttpStatusCode()`)
- [ ] **Health check integration** — Circuit Breaker → `IHealthCheck` adapter
- [ ] **Structured logging** — `Error.ToLogProperties()` zwracający `Dictionary<string, object>` dla Serilog/NLog

## v3.0.0 — Discriminated Unions (C# 13+)

**Cel:** Natywna integracja z planned C# discriminated unions (jeśli wejdą do języka).

- [ ] Migracja `Result<T>` na discriminated union (jeśli feature wejdzie do C#)
- [ ] Zachowanie backward compatibility przez implicit conversion operators
- [ ] Pattern matching `result is Success<T>(var value)` zamiast `.Match()`
- [ ] Rozważenie `Result<TValue, TError>` z generycznym typem błędu

---

## Backlog (bez ustalonego terminu)

### Analyzery
- [ ] **VCR0070** — wykrywanie `.Error` access bez sprawdzenia `IsFailure` (odwrotność VCR0020)
- [ ] **VCR0080** — wykrywanie `async void` metod zwracających Result (powinny zwracać `Task<Result>`)
- [ ] **Analyzer performance benchmarks** — mierzenie wpływu analyzerów na IDE responsiveness

### Core library
- [ ] **`Result<T>.GetValueOrDefault(T defaultValue)`** — bezpieczne wyciąganie wartości z fallback
- [ ] **`Result<T>.ToOption()`** / **`Option<T>`** — lekki optional type (jeśli będzie zapotrzebowanie)
- [ ] **Source generator** — auto-generowanie `FromException` mappingów z atrybutów
- [ ] **`Result.Validate()`** builder — fluent API do walidacji wielu reguł naraz z agregacją błędów

### Dokumentacja
- [ ] **Migration guide** v1.x → v2.0 (breaking changes)
- [ ] **Cookbook** — recepty na typowe scenariusze (CQRS, MediatR, minimal API)
- [ ] **Performance benchmarks** — porównanie Result vs exceptions vs nullable

### Infrastruktura
- [ ] **Benchmark project** (BenchmarkDotNet) — śledzenie regresji wydajności
- [ ] **Mutation testing** (Stryker.NET) — weryfikacja jakości testów
- [ ] **API surface tracking** — Microsoft.CodeAnalysis.PublicApiAnalyzers (złamanie API = build error)
