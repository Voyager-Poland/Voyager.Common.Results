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

## Multi-Framework: .NET 8.0 + .NET 4.8

**Required conditional imports for .NET 4.8 compatibility:**
```csharp
#if NET48
using System;
using System.Threading.Tasks;
#endif
```

| Framework | ImplicitUsings | LangVersion |
|-----------|---------------|-------------|
| .NET 8.0  | enabled       | latest      |
| .NET 4.8  | disabled      | 10.0        |

**Always verify both:** `dotnet build -f net8.0 && dotnet build -f net48`

## Critical Operator Selection

| Operator | Return Type | Use When |
|----------|-------------|----------|
| `Map` | `Result<TOut>` | Transform value: `.Map(x => x.ToString())` |
| `Bind` | `Result<TOut>` | Chain Result-returning: `.Bind(x => Validate(x))` |
| `Tap` | Same `Result<T>` | Side effects: `.Tap(x => Log(x))` |
| `Ensure` | Same `Result<T>` | Conditional fail: `.Ensure(x => x > 0, error)` |
| `OrElse` | Same `Result<T>` | Fallback: `.OrElse(() => GetDefault())` |
| `Finally` | Same `Result<T>` | Cleanup (always runs): `.Finally(() => Dispose())` |

**Common mistake:** Using `Map` for Result-returning functions creates `Result<Result<T>>`. Use `Bind` instead.

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

**NU5119 fix:** `dotnet clean -c Release` before rebuild.

## Testing Conventions (xUnit)

**Naming:** `MethodName_Scenario_ExpectedBehavior`
```csharp
[Fact]
public void Map_Success_TransformsValue() { }

[Fact]
public void Map_Failure_PropagatesError() { }
```

**Test categories in codebase:** MonadLawsTests, InvariantTests, ErrorPropagationTests, CompositionTests

## Error Factory Methods

| Method | ErrorType | HTTP |
|--------|-----------|------|
| `ValidationError(msg)` | Validation | 400 |
| `NotFoundError(msg)` | NotFound | 404 |
| `UnauthorizedError(msg)` | Unauthorized | 401 |
| `PermissionError(msg)` | Permission | 403 |
| `ConflictError(msg)` | Conflict | 409 |
| `BusinessError(code, msg)` | Business | 400/422 |
| `FromException(ex)` | Unexpected | 500 |

**Always include context:** `$"User {id} not found"` not `"Not found"`

## CI/CD Pipeline

- **Push to main/master** → Build + Test + GitHub Packages (preview)
- **Git tag `v*`** → Release + NuGet.org publish
- **Critical:** `fetch-depth: 0` required for MinVer

## Key Pitfalls

1. **Modifier order:** `public static new` (not `public new static`)
2. **Don't access `.Value` without checking `IsSuccess`** - use `Match` or `GetValueOrDefault`
3. **Test both frameworks** - .NET 4.8 lacks implicit usings
4. **Warnings are errors** - `TreatWarningsAsErrors` is enabled
