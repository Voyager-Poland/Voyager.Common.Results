# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

Railway Oriented Programming library for .NET — functional error handling via `Result<T>` and `Result` types. Includes Roslyn analyzers (VCR0010-VCR0060) bundled in the NuGet package and a separate `Voyager.Common.Resilience` package (Circuit Breaker).

## Build & Test Commands

```bash
dotnet build -c Release                    # Build all targets (net48, net6.0, net8.0)
dotnet test -c Release                     # Run ALL tests across ALL frameworks
dotnet test --filter "ClassName"           # Run specific test class
dotnet clean -c Release                    # REQUIRED before rebuild (MinVer caches versions)

# Analyzer tests only (net8.0 only — not multi-target)
dotnet test src/Voyager.Common.Results.Analyzers.Tests/ --filter "ResultValueAccessedWithoutCheck"

# Coverage
dotnet test --collect:"XPlat Code Coverage"

# Pack NuGet (includes analyzer DLL in analyzers/dotnet/cs/)
dotnet pack -c Release src/Voyager.Common.Results/Voyager.Common.Results.csproj
```

## Critical Constraints

- **TreatWarningsAsErrors=true** — zero warnings allowed
- **ImplicitUsings=disable** — explicit `using` statements everywhere
- **Multi-target: net48, net6.0, net8.0** — .NET 4.8 needs `#if NET48` conditionals and `IsExternalInit` polyfill
- **LangVersion:** net8.0 uses `latest`, net6.0/net48 use `10.0`
- **ConfigureAwait(false)** on every `await` in library code (ADR-0001, hardcoded, no parameter)
- **MinVer versioning** — versions from git tags (`v1.2.3`), always `dotnet clean` before rebuild
- **Strong-named** — signed with `Voyager.Common.Results.snk`
- **Editorconfig:** tabs (width 2), CRLF, `insert_final_newline=true`, private fields `_camelCase`, interfaces `I` prefix, no `this.` qualifier, `csharp_prefer_braces=false` (single-statement if without braces)

## Architecture

```
src/
  Voyager.Common.Results/              # Core library (zero dependencies)
    Result.cs                          # Result (void operations)
    ResultT.cs                         # Result<T> — ALL functional operators (Map, Bind, Tap, Ensure, OrElse, Finally, Match)
    Error.cs                           # Immutable record(ErrorType, Code, Message) + InnerError chain
    ErrorType.cs                       # 15-value enum (Validation, NotFound, Timeout, CircuitBreakerOpen...)
    Extensions/
      TaskResultExtensions.cs          # Async: MapAsync, BindAsync, TapAsync, TryAsync, BindWithRetryAsync
      ResultCollectionExtensions.cs    # Combine, Partition, GetSuccessValues
      ResultErrorChainExtensions.cs    # WithInner, AddErrorContext, WrapError
      ResultRetryExtensions.cs         # BindWithRetryAsync with configurable policies
      ErrorTypeExtensions.cs           # IsTransient(), ToHttpStatusCode(), ShouldRetry()

  Voyager.Common.Results.Analyzers/    # Roslyn analyzers (netstandard2.0)
    ResultTypeHelper.cs                        # Shared: IsResultType, IsResultMethod, UnwrapTaskType, HelpLinkBase
    ResultMustBeConsumedAnalyzer.cs             # VCR0010: unconsumed Result
    ResultMustBeConsumedCodeFixProvider.cs      # VCR0010 CodeFix: `_ = ` or `var result = `
    ResultValueAccessedWithoutCheckAnalyzer.cs  # VCR0020: .Value without IsSuccess guard (+ Assert guards)
    ResultValueAccessedWithoutCheckCodeFixProvider.cs  # VCR0020 CodeFix: .Value → .GetValueOrThrow()
    NestedResultAnalyzer.cs                     # VCR0030: Result<Result<T>> (use Bind not Map)
    NestedResultCodeFixProvider.cs              # VCR0030 CodeFix: Map → Bind
    GetValueOrThrowAnalyzer.cs                  # VCR0040: GetValueOrThrow in railway chain
    FailureWithErrorNoneAnalyzer.cs             # VCR0050: Failure(Error.None) — always a bug
    FailureWithErrorNoneCodeFixProvider.cs      # VCR0050 CodeFix: Error.None → Error.UnexpectedError(...)
    PreferMatchSwitchAnalyzer.cs                # VCR0060: prefer Match/Switch (disabled by default)

  Voyager.Common.Resilience/           # Separate package — stateful resilience patterns
    CircuitBreakerPolicy.cs            # 3-state (Closed→Open→HalfOpen), thread-safe via SemaphoreSlim

build/                                 # Modular MSBuild (imported via Directory.Build.props)
  Build.Versioning.props               # MinVer config
  Build.CodeQuality.props              # TreatWarningsAsErrors, analyzers, deterministic
  Build.SourceLink.props               # Source linking for debugging
  Build.NuGet.props                    # Package metadata
  Build.Signing.props                  # Strong name key
```

**Build system pattern:** Modify `build/*.props` files, NOT individual `.csproj` files.

## TargetFrameworks Override Pattern (Important)

`Directory.Build.props` sets `<TargetFrameworks>net48;net6.0;net8.0</TargetFrameworks>` globally. Projects that need different targets MUST use `<TargetFrameworks>` (plural, not singular `<TargetFramework>`) to override:

```xml
<!-- Analyzer project — netstandard2.0 required by Roslyn -->
<TargetFrameworks>netstandard2.0</TargetFrameworks>  <!-- ✅ overrides global -->
<TargetFramework>netstandard2.0</TargetFramework>    <!-- ❌ BREAKS — assets.json mismatch -->

<!-- Test project — net8.0 only -->
<TargetFrameworks>net8.0</TargetFrameworks>
```

## Operator Selection (Critical)

| Operator | Use When |
|----------|----------|
| `Map` | Transform value: `.Map(x => x.ToString())` |
| `Bind` | Chain Result-returning: `.Bind(x => Validate(x))` — **NOT Map** (creates nested Result) |
| `Tap` / `TapError` | Side effects: `.Tap(x => Log(x))` |
| `Ensure` | Conditional fail: `.Ensure(x => x > 0, error)` |
| `OrElse` | Fallback: `.OrElse(() => GetDefault())` |
| `Match` / `Switch` | Exhaustive pattern matching (two branches) |

**Implicit conversions:** `Error` and `T` convert to `Result<T>` — use `return Error.ValidationError(...)` directly.

## Analyzers — CodeFix Providers

| Analyzer | CodeFix | Description |
|----------|---------|-------------|
| VCR0010 | `_ = ...` or `var result = ...` | Discard or assign unconsumed Result |
| VCR0020 | `.GetValueOrThrow()` / `if (IsSuccess)` guard | Replace unchecked Value or wrap in guard |
| VCR0030 | `Map` → `Bind` | Replace Map with Bind to flatten nested Result |
| VCR0050 | `Error.None` → `Error.UnexpectedError(...)` | Replace contradictory Error.None in Failure |
| VCR0040, VCR0060 | — | No CodeFix providers (transformations too contextual) |

## VCR0020 Analyzer — Guard Pattern Recognition

The analyzer recognizes `IsFailure` early-return guards (not just `IsSuccess`), including:
- Guard in the same block: `if (result.IsFailure) return; ... result.Value`
- Guard in parent block: guard in outer scope protects `.Value` inside nested `if {}`
- Guard with reassignment: `if (result.IsFailure) { result = Result<T>.Success(...); }` as last statement
- **Assert guards** (name-based, no framework references needed):
  - xUnit: `Assert.True(result.IsSuccess)`, `Assert.False(result.IsFailure)`
  - NUnit: `Assert.That(result.IsSuccess, ...)`, `Assert.IsTrue(...)`, `Assert.IsFalse(...)`
  - MSTest: `Assert.IsTrue(result.IsSuccess)`, `Assert.IsFalse(result.IsFailure)`
  - FluentAssertions: `result.IsSuccess.Should().BeTrue()`, `result.IsFailure.Should().BeFalse()`

## Roslyn Analyzer Development Constraints

- **Target:** netstandard2.0 (required by Roslyn), `ImplicitUsings=disable`
- **No LINQ in analyzers** — analyzers run on every keystroke in IDE; `foreach` with early return is preferred over `.Any()`, `.Where()` etc. to avoid delegate allocations on hot paths. CodeFixProviders (on-demand only) are less critical but keep consistent style.
- **Cross-platform line endings** — CodeFix providers must detect EOL from syntax trivia, never hardcode `\r\n` (CI runs on Linux)
- **`out var` scoping** — be careful when flattening nested `if` statements: `out var` in a combined `if` condition scopes to the enclosing block, which may conflict with other `out var` declarations in sibling `if` blocks within the same loop
- **ResultTypeHelper** — shared `IsResultType` (with base type traversal), `IsResultMethod`, `UnwrapTaskType`. All analyzers use this, never duplicate these checks.
- **Analyzer test project** does NOT reference the core `Voyager.Common.Results` library — tests use inline `ResultStubs` (stub type definitions inside test strings)

## Testing Conventions

- **xUnit**, naming: `MethodName_Scenario_ExpectedBehavior`
- **Analyzer tests** use `CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>` with inline `ResultStubs` (stub types, not real library). CodeFix tests use `CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>`
- **Stub pitfall:** stubs returning `Value` from `GetValueOrThrow()` trigger VCR0020 — use `default` instead
- Tests run on all 3 target frameworks — net48 requires Windows runner in CI
- `MonadLawsTests` verify mathematical monad properties (left/right identity, associativity)

## CI/CD

- **Matrix:** net6.0/net8.0 on ubuntu-latest, net48 on windows-latest (separate job)
- **Analyzer tests:** net8.0 only (not multi-target)
- `git tag v1.2.3 && git push origin v1.2.3` — CI publishes to NuGet.org + GitHub Packages
- Push to main without tag → preview version (`0.1.0-preview.X`)
- CI requires `fetch-depth: 0` for MinVer to access full git history

## ADRs (docs/adr/)

Key decisions: ConfigureAwait(false) always (001), ImplicitUsings disabled (002), retry extensions (003), circuit breaker separate package (004), error classification (005), error chaining (006), exception details preservation (007), circuit breaker callbacks (008), retry callbacks (009), Roslyn analyzers (010).
