# Voyager.Common.Results - AI Coding Instructions

> Railway Oriented Programming library for .NET - functional error handling via `Result<T>` and `Result` types.

## Architecture

```
src/Voyager.Common.Results/
├── Result.cs          # Void operations (no value)
├── ResultT.cs         # Result<T> - ALL functional operators here
├── Error.cs           # Immutable record(ErrorType, Code, Message)
├── ErrorType.cs       # Enum for error categorization
└── Extensions/
    ├── TaskResultExtensions.cs        # Async: MapAsync, BindAsync, TryAsync
    └── ResultCollectionExtensions.cs  # Combine, Partition, GetSuccessValues

build/                 # Modular MSBuild configuration (imported by Directory.Build.props)
├── Build.Versioning.props   # MinVer git-based versioning
├── Build.CodeQuality.props  # Warnings as errors, analyzers, deterministic builds
├── Build.SourceLink.props   # Source linking for debugging
├── Build.NuGet.props        # Package metadata, licensing
└── Build.Signing.props      # Strong naming (.snk)
```

**Build system pattern:** All `.props` files are imported automatically via `Directory.Build.props` - modify modular files, NOT individual `.csproj` files.

## Multi-Framework: `netstandard2.0` + `net10.0` (per ADR-0014)

**CRITICAL: ImplicitUsings is DISABLED for all targets** — always add explicit `using` statements. Global usings are provided by `GlobalUsings.cs` per project (unconditional, applies to both TFMs).

**No `#if NET48` / `#if NETSTANDARD` directives in source files** — the library compiles identically for both TFMs. If you need TFM-specific code in the future, prefer `#if NET10_0_OR_GREATER` for opt-in modern API usage and document why.

**Key TFM matrix:**

| TFM             | LangVersion | ImplicitUsings | Notes |
|-----------------|-------------|----------------|-------|
| `netstandard2.0` | 10.0        | disabled (SDK does not support it) | Covers .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5/6/7/8/9, Mono, Xamarin, Unity. Requires `IsExternalInit` polyfill (record/init properties) |
| `net10.0`       | latest      | disabled       | TFM-specific assembly for .NET 10 LTS — AOT-readiness, BCL nullable annotations, forward-compat for ValueTask/IAsyncEnumerable overloads |

**Test projects override the global TFM matrix** — they multi-target `net48;net8.0;net10.0` to verify the netstandard2.0 binary on each declared runtime (net48 .NET Framework, net8.0 LTS, net10.0).

**Per-target file location:** Check [Directory.Build.props](../Directory.Build.props) for framework-specific settings.

**Always verify both TFMs:** `dotnet build -c Release && dotnet test -c Release --no-build`

## Critical Operator Selection

| Operator | Return Type | Use When |
|----------|-------------|----------|
| `Map` | `Result<TOut>` | Transform value: `.Map(x => x.ToString())` |
| `Bind` | `Result<TOut>` | Chain Result-returning: `.Bind(x => Validate(x))` |
| `Tap` / `TapError` | Same `Result<T>` | Side effects (logging): `.Tap(x => Log(x))` |
| `Ensure` / `EnsureAsync` | Same `Result<T>` | Conditional fail: `.Ensure(x => x > 0, error)` |
| `OrElse` / `OrElseAsync` | Same `Result<T>` | Fallback/alternative: `.OrElse(() => GetDefault())` |
| `Finally` | Same `Result<T>` | Cleanup (always runs): `.Finally(() => Dispose())` |
| `Match` / `Switch` | TResult / void | Pattern match: `.Match(v => $"OK: {v}", e => $"Error: {e}")` |

**Common mistake:** Using `Map` for Result-returning functions creates `Result<Result<T>>`. Use `Bind` instead.

## OrElse Pattern (Fallback/Alternatives)

Use `OrElse` to provide alternatives when a result fails. Lazy evaluation avoids expensive operations if not needed.

```csharp
// Sync: Try primary source, fall back to secondary
var result = _cache.GetUser(id)
    .OrElse(() => _database.GetUser(id))
    .OrElse(() => Error.NotFoundError($"User {id} not found"));

// Async: Multi-tier retrieval (cache → database → external API → default)
var user = await _cache.GetAsync(id)
    .OrElseAsync(() => _db.GetAsync(id))
    .OrElseAsync(async () => await _externalApi.GetAsync(id))
    .OrElseAsync(() => Task.FromResult(GetDefaultUser()));

// Real-world: Resilient config loading with fallbacks
var config = Config.LoadFromFile("config.json")
    .OrElse(() => Config.LoadFromEnv())
    .OrElse(() => Config.LoadDefaults())
    .Ensure(c => c.IsValid, Error.ValidationError("Config invalid"));
```

**Key difference from if-checks:** OrElse uses Result<T> chains, avoiding nested conditionals and making error propagation explicit.

## Implicit Conversions (Prefer These)

```csharp
public Result<User> GetUser(int id)
{
    if (id <= 0) return Error.ValidationError("Invalid ID");  // Error → Result<User>
    return _repo.Find(id) ?? Error.NotFoundError($"User {id}");  // User → Result<User>
}
```

## Async Patterns & ConfigureAwait

**Library convention: All async methods use `ConfigureAwait(false)` internally.**

This is hardcoded per [ADR-0001](../docs/adr/ADR-0001-no-configureawait-parameter-in-tryasync.md) - no parameter to override.

### Why ConfigureAwait(false)?
- Prevents deadlocks in UI contexts (WinForms, WPF, legacy ASP.NET)
- Avoids unnecessary thread context switches
- Microsoft best practice for library code

### When adding new async methods:
```csharp
// ✅ CORRECT - always use ConfigureAwait(false)
public static async Task<Result<T>> MapAsync<T, TOut>(...)
{
    var result = await resultTask.ConfigureAwait(false);
    // ...
}

// ❌ WRONG - missing ConfigureAwait
var result = await resultTask;  // May deadlock in sync contexts!
```

### Consumer context capture (if needed):
```csharp
// Library handles its own context internally
var result = await Result<string>.TryAsync(
    async ct => await httpClient.GetStringAsync(url, ct),
    cancellationToken);

// Consumer controls context AFTER the call
await UpdateUIAsync(result).ConfigureAwait(true);  // Back to UI thread
```

### TryAsync with CancellationToken:
```csharp
// Automatically converts OperationCanceledException → Error.CancelledError
var result = await TaskResultExtensions.TryAsync(
    async ct => await httpClient.GetAsync(url, ct),
    cancellationToken);
```

## Build & Versioning (MinVer)

```powershell
# Version from git tags (v prefix required)
git tag v1.2.3
dotnet clean -c Release && dotnet build -c Release

# Common commands
dotnet test --collect:"XPlat Code Coverage"
dotnet pack src/Voyager.Common.Results/Voyager.Common.Results.csproj -c Release
```

**NU5119 fix:** Always `dotnet clean -c Release` before rebuild - MinVer caches versions.

**Versioning workflow:**
- **Push to main** → GitHub Actions builds with preview version (e.g., `0.1.0-preview.5`)
- **Create tag `v1.2.3`** → Automatic release to NuGet.org + GitHub Packages
- **CI requires `fetch-depth: 0`** for MinVer to access all git history

## Testing Conventions (xUnit)

**Naming:** `MethodName_Scenario_ExpectedBehavior`
```csharp
[Fact]
public void Map_Success_TransformsValue() { }

[Fact]
public void Map_Failure_PropagatesError() { }
```

**Test categories (mirror source architecture):**
- **MonadLawsTests.cs** - Mathematical properties (left/right identity, associativity, functor laws)
- **ErrorPropagationTests.cs** - Error flow through Map, Bind, Tap, Ensure, OrElse, MapError chains
- **CompositionTests.cs** - Operator chaining behavior and complex scenarios
- **InvariantTests.cs** - XOR property, null safety, immutability guarantees
- **ResultTests.cs / ResultTTests.cs** - Core factory methods, Match/Switch, conversions
- **TaskResultExtensionsTests.cs** - Async operators, cancellation token handling
- **ResultCollectionExtensionsTests.cs** - Combine, Partition, collection operations

**Always test all targets:** `dotnet test -c Release` validates the netstandard2.0 binary on net8.0 + net10.0 (Linux) and net48 (Windows job)

**Coverage requirement:** Run `dotnet test --collect:"XPlat Code Coverage"` - no significant decreases allowed

## Error Factory Methods

| Method | ErrorType | HTTP | Use Case |
|--------|-----------|------|----------|
| `ValidationError(msg)` | Validation | 400 | Input validation failures |
| `NotFoundError(msg)` | NotFound | 404 | Resource doesn't exist |
| `UnauthorizedError(msg)` | Unauthorized | 401 | User not authenticated |
| `PermissionError(msg)` | Permission | 403 | User lacks authorization |
| `ConflictError(msg)` | Conflict | 409 | Duplicate/collision (e.g., username taken) |
| `BusinessError(msg)` | Business | 422 | Business rule violation |
| `DatabaseError(msg)` | Database | 500 | Database operation failed |
| `UnavailableError(msg)` | Unavailable | 503 | Service temporarily down/rate limited |
| `TimeoutError(msg)` | Timeout | 504 | Operation exceeded timeout |
| `CancelledError(msg)` | Cancelled | 499 | Operation cancelled by user/token |
| `UnexpectedError(msg)` | Unexpected | 500 | Catch-all for unhandled exceptions |

**Always include context:** `$"User {id} not found"` not `"Not found"`. Both code and message overloads exist - use message version for simplicity.

## CI/CD Pipeline

- **Push to main/master** → Build + Test + GitHub Packages (preview)
- **Git tag `v*`** → Release + NuGet.org publish
- **Critical:** `fetch-depth: 0` required for MinVer

## Key Pitfalls

1. **Modifier order:** `public static new` (not `public new static`)
2. **Don't access `.Value` without checking `IsSuccess`** - use `Match` or `GetValueOrDefault`
3. **Test all frameworks** - .NET 4.8 lacks implicit usings, requires `#if NET48` conditionals
4. **Warnings are errors** - `TreatWarningsAsErrors` is enabled
5. **MinVer versioning caching** - Always `dotnet clean -c Release` before rebuild to clear cached versions
