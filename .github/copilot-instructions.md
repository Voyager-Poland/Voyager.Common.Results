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
```

## Multi-Framework: .NET 8.0 + .NET 6.0 + .NET 4.8

**Required conditional imports for .NET 4.8 compatibility:**
```csharp
#if NET48
using System;
using System.Threading.Tasks;
#endif
```

**Key differences:**

| Framework | Targets | ImplicitUsings | LangVersion | Notes |
|-----------|---------|---------------|-------------|-------|
| .NET 8.0  | net8.0  | enabled       | latest      | Modern, no compatibility layer needed |
| .NET 6.0  | net6.0  | disabled      | 10.0        | Source Link enabled, no polyfills needed |
| .NET 4.8  | net48   | disabled      | 10.0        | Requires `IsExternalInit` polyfill, strict type inference |

**Per-target file location:** Check [Directory.Build.props](../Directory.Build.props) for framework-specific settings.

**Always verify both:** `dotnet build -c Release && dotnet test -c Release --no-build`

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

**Test organization:** Files mirror source structure ([MonadLawsTests.cs](../src/Voyager.Common.Results.Tests/MonadLawsTests.cs), [ErrorPropagationTests.cs](../src/Voyager.Common.Results.Tests/ErrorPropagationTests.cs), [CompositionTests.cs](../src/Voyager.Common.Results.Tests/CompositionTests.cs), etc.)

**Always test both frameworks:** `dotnet test -c Release` runs against net8.0, net6.0, and net48

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
3. **Test both frameworks** - .NET 4.8 lacks implicit usings
4. **Warnings are errors** - `TreatWarningsAsErrors` is enabled
