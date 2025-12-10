# Voyager.Common.Results - AI Coding Instructions

> Railway Oriented Programming library for .NET - functional error handling via `Result<T>` and `Result` types.

## Architecture at a Glance

```
src/Voyager.Common.Results/
├── Result.cs          # Success/failure without value (void operations)
├── ResultT.cs         # Result<T> with value, inherits Result - ALL operators here
├── Error.cs           # Immutable record: ErrorType + Code + Message
├── ErrorType.cs       # Enum: Validation, NotFound, Business, Unauthorized, etc.
└── Extensions/
    ├── TaskResultExtensions.cs        # Async: MapAsync, BindAsync, OrElseAsync
    └── ResultCollectionExtensions.cs  # Combine, Partition, GetSuccessValues
```

## Multi-Framework: .NET 8.0 + .NET 4.8

**Always add conditional imports for .NET 4.8:**
```csharp
#if NET48
using System;
using System.Threading.Tasks;
#endif
```
- .NET 8.0: ImplicitUsings, C# latest
- .NET 4.8: Explicit imports, C# 10.0

**Verify both frameworks:** `dotnet build -f net8.0 && dotnet build -f net48`

## Core Patterns

### Operator Selection (Critical)
| Operator | Use When | Example |
|----------|----------|---------|
| `Map` | Transform value (non-Result return) | `.Map(x => x.ToString())` |
| `Bind` | Chain Result-returning operations | `.Bind(x => Validate(x))` → `Result<T>` |
| `Tap` | Side effects (logging, metrics) | `.Tap(x => Log(x))` |
| `Ensure` | Validation that may fail | `.Ensure(x => x > 0, error)` |
| `OrElse` | Fallback on failure (lazy) | `.OrElse(() => GetFromDb())` |

### Implicit Conversions (Use Them!)
```csharp
public Result<User> GetUser(int id)
{
    if (id <= 0) return Error.ValidationError("Invalid ID");  // Error → Result
    return _repo.Find(id) ?? Error.NotFoundError($"User {id}");  // User → Result
}
```

### Async: Always Use ConfigureAwait(false)
```csharp
var value = await task.ConfigureAwait(false);  // Required in library code
```

## Build & Versioning

### MinVer: Git Tags Required!
```powershell
# ❌ No tags → Version 0.0.0.0
# ✅ git tag v1.2.3 → Version 1.2.3

git tag v1.2.3                    # Create release version
dotnet clean -c Release           # Required before rebuild
dotnet build -c Release           # MinVer reads tag
```

### Common Commands
```powershell
dotnet test --collect:"XPlat Code Coverage"
dotnet pack src/Voyager.Common.Results/Voyager.Common.Results.csproj -c Release
```

### Fix: NU5119 XML Error
```powershell
dotnet clean -c Release && dotnet build -c Release
```

## Testing (xUnit)

**Naming:** `MethodName_Scenario_ExpectedBehavior`
```csharp
[Fact]
public void Map_Success_TransformsValue() { }

[Fact]
public void Map_Failure_PropagatesError() { }
```

**Always test both success AND failure paths for every operator.**

## Error Types Quick Reference

| Factory Method | When to Use |
|----------------|-------------|
| `ValidationError(msg)` | Invalid input |
| `NotFoundError(msg)` | Entity doesn't exist |
| `UnauthorizedError(msg)` | Not authenticated (401) |
| `PermissionError(msg)` | Not authorized (403) |
| `BusinessError(code, msg)` | Domain rule violation |
| `FromException(ex)` | Convert caught exception |

## Key Pitfalls

1. **`Map` vs `Bind`**: Use `Bind` for `Result<T>`-returning functions (Map causes `Result<Result<T>>`)
2. **Don't access `.Value` without checking `IsSuccess`**
3. **Include context in errors**: `$"User {id} not found"` not `"Not found"`
4. **Test both frameworks**: .NET 4.8 has different implicit usings
5. **Modifier order**: `public static new` (not `public new static`)

## CI/CD

- **Push to main** → Preview build + publish to GitHub Packages
- **Git tag `v*`** → Release build + GitHub Release + NuGet.org
- **Critical**: Workflow needs `fetch-depth: 0` for MinVer

## Key Files

- [ResultT.cs](../src/Voyager.Common.Results/ResultT.cs) - All operators implementation
- [TaskResultExtensions.cs](../src/Voyager.Common.Results/Extensions/TaskResultExtensions.cs) - Async patterns
- [build/Build.Versioning.props](../build/Build.Versioning.props) - MinVer configuration
- [docs/best-practices.md](../docs/best-practices.md) - Comprehensive DO/DON'T guide
