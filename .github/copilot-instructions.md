# Voyager.Common.Results - AI Coding Assistant Instructions

## Project Overview
This is a **Railway Oriented Programming** library implementing the Result Pattern for .NET. It provides functional error handling without exceptions through type-safe `Result<T>` and `Result` types.

**Key characteristics:**
- Multi-target: .NET 8.0 and .NET Framework 4.8
- Zero dependencies (except polyfills for .NET 4.8)
- Functional programming patterns (Map, Bind, Tap, Ensure, OrElse)
- Async/await support via extension methods
- Struct-based for performance (no heap allocations)

## Architecture

### Core Types (src/Voyager.Common.Results/)
- **`Result<T>`**: Represents success with value OR failure with error (inherits from `Result`)
- **`Result`**: Represents success/failure without a value (void operations)
- **`Error`**: Immutable record with `ErrorType`, `Code`, and `Message`

### Extension Methods
- **`TaskResultExtensions`**: Async operators (`MapAsync`, `BindAsync`, `TapAsync`, `OrElseAsync`, etc.)
- **`ResultCollectionExtensions`**: Collection operations (`Combine`, `Partition`, `GetSuccessValues`)

### Error Types (ErrorType enum)
`None`, `Validation`, `Permission`, `Database`, `Business`, `NotFound`, `Conflict`, `Unavailable`, `Timeout`, `Unexpected`

## Critical Patterns

### 1. Multi-Framework Compatibility
**Always use conditional compilation for .NET 4.8:**
```csharp
#if NET48
using System;
using System.Threading.Tasks;
#endif
```
- .NET 8.0: `ImplicitUsings` enabled, C# latest
- .NET 4.8: Must explicitly import, C# 10.0, uses `IsExternalInit` polyfill for records

### 2. Railway Oriented Programming
Chain operations that return `Result<T>` without nesting:
```csharp
GetUser(id)
    .Bind(user => ValidateUser(user))    // Result<T> → Result<T>
    .Map(user => user.Email)              // Transform value
    .Ensure(email => email.Contains("@"), Error.ValidationError("Invalid email"))
    .Tap(email => _logger.Log(email))     // Side effects
    .OrElse(() => GetDefaultUser())       // Fallback on failure
```

### 3. OrElse Fallback Pattern
Chain fallback operations with lazy evaluation for resilient APIs:
```csharp
GetFromCache(key)
    .OrElse(() => GetFromDatabase(key))           // Only called if cache fails
    .OrElse(() => GetFromRemoteApi(key))         // Only called if DB fails  
    .OrElse(() => Result<T>.Success(defaultValue)) // Final fallback

// Async version
await GetFromCacheAsync(key)
    .OrElseAsync(() => GetFromDatabaseAsync(key))
    .OrElseAsync(() => GetFromRemoteApiAsync(key))
```

### 4. Implicit Conversions
`Result<T>` supports implicit conversions from values and errors:
```csharp
public Result<User> GetUser(int id)
{
    if (id <= 0) 
        return Error.ValidationError("Invalid ID");  // Implicit from Error
    
    var user = _repository.Find(id);
    if (user is null)
        return Error.NotFoundError($"User {id} not found");
    
    return user;  // Implicit from User
}
```

### 5. Operator Selection
- **`Map`**: Transform success value (doesn't return Result)
- **`Bind`**: Chain operations that return Result<T>
- **`Tap`/`TapError`**: Side effects without changing Result
- **`Ensure`**: Validation that may convert success to failure
- **`OrElse`**: Fallback when result is failure with lazy evaluation (cache → DB → default)

### 6. Async Patterns
Extension methods handle 4 async combinations:
```csharp
// 1. Task<Result<T>> + sync function
await resultTask.MapAsync(x => x * 2)

// 2. Result<T> + async function  
await result.MapAsync(async x => await GetAsync(x))

// 3. Task<Result<T>> + async function
await resultTask.BindAsync(async x => await SaveAsync(x))

// 4. ConfigureAwait(false) is used throughout for library code
```

### 7. Error Factory Methods
Use specific error types with optional custom codes:
```csharp
Error.ValidationError("Email required")                           // Default code
Error.NotFoundError($"Order {id} not found")                     // Context in message
Error.BusinessError("INSUFFICIENT_BALANCE", "Balance too low")   // Custom code
Error.UnavailableError("Service temporarily unavailable")        // Service down/rate limit
Error.TimeoutError("Request timed out after 30s")               // Operation timeout
Error.FromException(exception)                                    // Convert exception
```

## Development Workflow

### Build System Architecture
Project uses **modular MSBuild configuration** in `build/*.props` files:
- `Build.Versioning.props` - MinVer Git-based versioning (CRITICAL dependency)
- `Build.CodeQuality.props` - Analyzers, deterministic builds, warnings as errors
- `Build.NuGet.props` - Package metadata, licensing, publishing settings
- `Build.SourceLink.props` - Debugging support for published packages

All projects inherit from `Directory.Build.props` which imports these modules.

### Build & Test Commands
```powershell
dotnet restore
dotnet build -c Release                    # Builds both net8.0 and net48
dotnet test --collect:"XPlat Code Coverage"

# Fix common build issues
dotnet clean -c Release                    # If XML documentation errors occur
dotnet pack src/Voyager.Common.Results/Voyager.Common.Results.csproj -c Release
```

**Common Build Issues & Solutions**:
- **NU5119 XML file error**: `dotnet clean -c Release` then rebuild 
- **0.0.0.0 version**: Create Git tag (`git tag v1.2.3`) for proper MinVer versioning
- **Multi-framework issues**: Test both `dotnet build -f net8.0` and `dotnet build -f net48`

### Critical: MinVer Versioning Dependency
**PROJECT WILL NOT BUILD CORRECTLY WITHOUT GIT TAGS!**

```powershell
# ❌ WITHOUT Git tags: Version = 0.0.0.0 (WRONG!)
# ✅ WITH Git tag v1.2.6: Version = 1.0.0.0 (AssemblyVersion), 1.2.6.0 (FileVersion)

# Check current MinVer calculation
dotnet build -c Release --verbosity normal | findstr MinVer

# Create version tag (triggers proper versioning)
git tag v1.2.6
dotnet clean && dotnet build -c Release

# Verify assembly version
[System.Reflection.Assembly]::LoadFrom("$PWD\src\Voyager.Common.Results\bin\Release\net8.0\Voyager.Common.Results.dll").GetName()
```

**MinVer Configuration** (in `build/Build.Versioning.props`):
- `MinVerTagPrefix=v` - Looks for tags like `v1.2.3`
- `MinVerMinimumMajorMinor=0.1` - Default when no tags exist
- Auto-preview builds for commits without tags

See [docs/QUICK-START-VERSIONING.md](../docs/QUICK-START-VERSIONING.md).

### Multi-Framework Verification
Always test both frameworks when changing core types:
```powershell
dotnet build -f net8.0 -c Release
dotnet build -f net48 -c Release
dotnet test -f net8.0
dotnet test -f net48
```

### Version Management (Automated)
- **All versioning**: Controlled by Git tags via MinVer - no manual version editing needed
- **Release**: Create Git tag `v1.2.3` → triggers release build with version 1.2.3
- **Preview**: Commits without tags → auto-preview builds (e.g., `0.1.0-preview.5+abc1234`)
- **Assembly versioning**: Major.0.0.0 (AssemblyVersion), Major.Minor.Patch.0 (FileVersion)

### CI/CD Pipeline (GitHub Actions)
**Workflow file**: `.github/workflows/ci.yml`

**Triggers**:
- Push to `main`/`master` → Preview build + publish
- Git tags `v*` → Release build + GitHub Release
- Pull requests → Build and test only

**Pipeline jobs**:
1. **build** - Multi-target compile, tests, coverage, pack artifacts, uploads to GitHub artifacts
2. **deploy** - Downloads artifacts, publishes to GitHub Packages + NuGet.org (main/master push only)  
3. **release** - Downloads artifacts, creates GitHub Release with .nupkg files (tags `v*` only)

**Critical CI requirements**:
- `fetch-depth: 0` for MinVer to access full Git history
- Ubuntu + Mono for .NET Framework 4.8 testing  
- Codecov integration for coverage reports (public repos only)
- Secrets: `VOY_ACTIONLOGIN`, `VOY_ACTIONLOGINPASS`, `VOY_AND_API_KEY`

See [BUILD.md](../BUILD.md) for manual publishing steps and troubleshooting.

## Testing Conventions

### Test Structure (NUnit)
Tests are in `src/Voyager.Common.Results.Tests/`:
- `ErrorTests.cs`: Error factory methods
- `ResultTests.cs`: Non-generic Result operations
- `ResultTTests.cs`: Generic Result<T> operations
- `TaskResultExtensionsTests.cs`: Async extension methods
- `ResultCollectionExtensionsTests.cs`: Collection operations

### Test Naming
```csharp
[Test]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var input = ...;
    
    // Act
    var result = ...;
    
    // Assert
    Assert.That(result.IsSuccess, Is.True);
    Assert.That(result.Value, Is.EqualTo(...));
}
```

### Testing Both Paths
Always test success AND failure for each operation:
```csharp
[Test]
public void OrElse_Success_ReturnsOriginal() { ... }

[Test]
public void OrElse_Failure_ReturnsAlternative() { ... }
```

### Async Test Pattern
```csharp
[Test]
public async Task MapAsync_TaskResult_SyncMapper_MapsValue()
{
    var result = Task.FromResult(Result<int>.Success(5));
    var mapped = await result.MapAsync(x => x * 2);
    
    Assert.That(mapped.IsSuccess, Is.True);
    Assert.That(mapped.Value, Is.EqualTo(10));
}
```

## Code Style

### Immutability
All public types are immutable:
- `Error` is a `record` (immutable by default)
- `Result<T>` has `private` constructors, `public` factory methods
- Properties are `{ get; }` only

### Constructor Protection
Use factory methods instead of public constructors:
```csharp
// ✅ Correct
public static Result<TValue> Success(TValue value) => new(value);
public static Result<TValue> Failure(Error error) => new(error);

// ❌ Don't expose
public Result(TValue value) // private or protected
```

### XML Documentation
Document all public APIs with `<summary>`, `<param>`, `<returns>`, `<example>`:
```csharp
/// <summary>
/// Maps the success value to another type (functor map)
/// </summary>
/// <example>
/// var result = Result&lt;int&gt;.Success(5);
/// var mapped = result.Map(x => x * 2); // Result&lt;int&gt; with value 10
/// </example>
/// <param name="mapper">Mapping function applied to the success value.</param>
/// <typeparam name="TOut">Target type.</typeparam>
/// <returns>Mapped Result of type Result&lt;TOut&gt;.</returns>
```

### Null Handling
Use nullable reference types (enabled in .csproj):
- `TValue?` for potentially null values
- `!` null-forgiving operator when IsSuccess guarantees non-null
- Guard against null in factory methods

## Common Pitfalls to Avoid

1. **Don't access `Value` without checking `IsSuccess`** - leads to runtime errors
2. **Use `Bind` not `Map` for Result-returning operations** - `Map` causes `Result<Result<T>>`
3. **Don't use `Task.FromResult` unnecessarily** - keep sync operations sync
4. **Include context in error messages** - `Error.NotFoundError($"User {id} not found")` not `"Not found"`
5. **Use `ConfigureAwait(false)` in library code** - prevents deadlocks
6. **Test both .NET 8.0 and 4.8** - framework differences can cause issues
7. **Don't throw exceptions for business logic** - use `Result.Failure(error)` instead
8. **MinVer dependency**: Project REQUIRES Git tags for correct versioning - without tags, version becomes 0.0.0.0
9. **XML documentation build errors**: Use `dotnet clean` before `dotnet pack` if packaging fails with XML file errors

## Key Files for Understanding Patterns

- `src/Voyager.Common.Results/ResultT.cs`: Core Result<T> implementation with all operators
- `src/Voyager.Common.Results/Extensions/TaskResultExtensions.cs`: Async pattern reference
- `build/Build.Versioning.props`: MinVer configuration and assembly versioning strategy
- `Directory.Build.props`: Global project settings and imports
- `docs/best-practices.md`: Comprehensive DO/DON'T guide
- `docs/railway-oriented.md`: Railway Oriented Programming explanation
- `.github/workflows/ci.yml`: Build, test, and deployment pipeline

## When Adding New Features

1. Update core types in `src/Voyager.Common.Results/`
2. Add extension methods to appropriate `Extensions/*.cs` file
3. Write tests covering success/failure/edge cases
4. Update documentation in `docs/` (especially `error-types.md` for new error types)
5. Add examples to `README.md` if user-facing  
6. Verify both .NET 8.0 and 4.8 compilation
7. Update `CHANGELOG.md` with changes under `[Unreleased]` section
8. For new error types: Add factory methods to `Error.cs`, update `ErrorType.cs` enum, add comprehensive tests
