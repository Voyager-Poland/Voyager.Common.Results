# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

_TBD_

## [1.4.0] - 2025-12-15

### Changed
- **README improvements**: Updated documentation for clarity and accuracy
  - Removed hardcoded test counts in Testing section (now generic descriptions)
  - Enhanced TryAsync section to clarify automatic `OperationCanceledException` → `ErrorType.Cancelled` mapping
  - Added comment to Quick Start example explaining repository doesn't throw exceptions
  - Added robust database example showing `Try()` + `Ensure()` pattern for handling both exceptions and null values
  - Updated Features list with new capabilities (contextual errors, deadlock-safe async, instance proxies)
  - Use implicit conversions in examples instead of explicit `Result<T>.Success()` calls
- **Build tooling**: Disabled implicit usings and added explicit global usings for net6/net8 targets to ensure consistent compilation across frameworks
- **Strong Name signing**: Added assembly signing configuration for distributed library
  - Generated `Voyager.Common.Results.snk` key file for strong name signing
  - Configured `Build.Signing.props` to enable assembly signing in both net8.0 and net48 targets
  - Ensures compatibility and trust verification in secure environments

### Added
- **`Ensure` with contextual error factory**: New overload that receives the value to create context-aware error messages
  ```csharp
  result.Ensure(
      user => user.Age >= 18,
      user => Error.ValidationError($"User {user.Name} is {user.Age} years old, must be 18+"));
  ```
- **`EnsureAsync` with contextual error factory**: 3 new async overloads with `Func<TValue, Error>` for contextual errors
  - `Task<Result<T>>.EnsureAsync(predicate, errorFactory)` - sync predicate
  - `Result<T>.EnsureAsync(asyncPredicate, errorFactory)` - async predicate on sync result
  - `Task<Result<T>>.EnsureAsync(asyncPredicate, errorFactory)` - async predicate on async result
- **Instance method proxies in `Result<T>`**: No need to import `Extensions` namespace
  - `EnsureAsync(asyncPredicate, error)` - async validation
  - `EnsureAsync(asyncPredicate, errorFactory)` - async validation with contextual error
  - `TapAsync(asyncAction)` - async side effects
  - `OrElseAsync(asyncAlternativeFunc)` - async fallback
- **`ErrorType.Cancelled`**: New error type for cancelled async operations
  - `Error.CancelledError(string message)` - Creates cancelled error with default code
  - `Error.CancelledError(string code, string message)` - Creates cancelled error with custom code
  - Enables proper handling of `OperationCanceledException` in async workflows
- **`TryAsync` methods**: Safe async exception-to-Result conversion with optional custom error mapping
  - `TryAsync(Func<Task> action)` - Wraps async action exceptions with `Error.FromException`
  - `TryAsync(Func<Task> action, Func<Exception, Error> errorMapper)` - Custom exception mapping for async actions
  - `TryAsync(Func<CancellationToken, Task> action, CancellationToken)` - Async action with cancellation support
  - `TryAsync(Func<CancellationToken, Task> action, CancellationToken, Func<Exception, Error>)` - Async action with cancellation and custom mapping
  - `TryAsync<TValue>(Func<Task<TValue>> func)` - Wraps async function exceptions with `Error.FromException`
  - `TryAsync<TValue>(Func<Task<TValue>> func, Func<Exception, Error> errorMapper)` - Custom exception mapping for async functions
  - `TryAsync<TValue>(Func<CancellationToken, Task<TValue>> func, CancellationToken)` - Async function with cancellation support
  - `TryAsync<TValue>(Func<CancellationToken, Task<TValue>> func, CancellationToken, Func<Exception, Error>)` - Async function with cancellation and custom mapping
- **`Result<T>.TryAsync` proxy methods**: Convenience static methods on `Result<T>` for cleaner syntax
  ```csharp
  // Proxy syntax (cleaner):
  var result = await Result<Config>.TryAsync(async () => 
      await JsonSerializer.DeserializeAsync<Config>(stream));
  
  // With CancellationToken:
  var result = await Result<string>.TryAsync(
      async ct => await httpClient.GetStringAsync(url, ct),
      cancellationToken);
  
  // Custom error mapping:
  var config = await Result<Config>.TryAsync(
      async () => await JsonSerializer.DeserializeAsync<Config>(stream),
      ex => ex is JsonException 
          ? Error.ValidationError("Invalid JSON")
          : Error.UnexpectedError(ex.Message));
  ```
- 26 unit tests for `TryAsync` methods covering:
  - Async action execution success and exception wrapping
  - Custom error mapping for various exception types (InvalidOperationException, IOException, FormatException)
  - Async functions returning values with success and exception scenarios
  - Complex object handling in async functions
  - Chaining with `MapAsync` and `BindAsync`
  - Error propagation through async chains
  - Cancellation token support and handling

### Documentation
- **Enhanced `ConfigureAwait` documentation** in [docs/async-operations.md](docs/async-operations.md):
  - Explained library's internal `ConfigureAwait(false)` behavior and deadlock prevention
  - Added ASP.NET 4.8 `HttpContext` preservation patterns with examples
  - Added `AsyncLocal<T>` pattern for context flowing through async boundaries
  - Comparison table: local variables vs parameter passing vs `AsyncLocal<T>`
- **Updated AI coding instructions** in [.github/copilot-instructions.md](.github/copilot-instructions.md):
  - Added `ConfigureAwait(false)` requirements for new async methods
  - Referenced [ADR-0001](docs/adr/ADR-0001-no-configureawait-parameter-in-tryasync.md) architectural decision

## [1.3.0] - 2025-01-16

### Added
- **`Result.Tap` method**: Executes side effect on success without modifying result
  ```csharp
  var result = SaveToDatabase(data)
      .Tap(() => _logger.LogInfo("Data saved"));
  ```
- **`Result.TapError` method**: Executes side effect on failure without modifying result
  ```csharp
  var result = SaveToDatabase(data)
      .TapError(error => _logger.LogError(error.Message));
  ```
- **`Result.MapError` method**: Transforms error without affecting success
  ```csharp
  var result = Operation()
      .MapError(error => Error.DatabaseError("DB_" + error.Code, error.Message));
  ```
- **`Result.Finally` method**: Executes action regardless of success/failure (like finally block)
  ```csharp
  var result = SaveToDatabase(data)
      .Finally(() => connection.Close());
  ```
- **`Result<T>.Finally` method**: Executes action regardless of success/failure for value operations
  ```csharp
  var userData = LoadFromFile(path)
      .Finally(() => fileStream.Dispose());
  ```
- **`Result.Try` methods**: Safe exception-to-Result conversion with optional custom error mapping
  - `Result.Try(Action action)` - Wraps exceptions with `Error.FromException`
  - `Result.Try(Action action, Func<Exception, Error> errorMapper)` - Custom exception mapping
  ```csharp
  // Basic: wraps exception with Error.FromException
  var result = Result.Try(() => File.Delete(path));
  
  // Custom: maps exceptions to specific error types
  var result = Result.Try(
      () => File.Delete(path),
      ex => ex is UnauthorizedAccessException 
          ? Error.PermissionError("Access denied")
          : Error.FromException(ex));
  ```
- **`Result<T>.Try` methods**: Safe exception-to-Result conversion for value-returning operations
  - `Result<T>.Try(Func<T> func)` - Wraps exceptions with `Error.FromException`
  - `Result<T>.Try(Func<T> func, Func<Exception, Error> errorMapper)` - Custom exception mapping
  ```csharp
  // Basic: wraps exception with Error.FromException
  var result = Result<int>.Try(() => int.Parse(input));
  
  // Custom: map FormatException to validation error
  var result = Result<int>.Try(
      () => int.Parse(input),
      ex => ex is FormatException 
          ? Error.ValidationError("Invalid number format")
          : Error.FromException(ex));
  ```
- `Map` method for `Result` (non-generic) class to transform void operations into value operations:
  - `Result.Map<TValue>(Func<TValue>)` - Transform Result → Result<TValue> (produces value from success)
- `Bind` methods for `Result` (non-generic) class for complete Railway Oriented Programming support:
  - `Result.Bind(Func<Result>)` - Chain void operations (Result → Result)
  - `Result.Bind<TValue>(Func<Result<TValue>>)` - Transform void operation to value operation (Result → Result<TValue>)
- 12 new unit tests for `Result.Map` and `Result.Bind` methods covering:
  - Map: success transformation and failure propagation
  - Bind: success and failure propagation
  - Operation chaining with early termination
  - Void-to-value transformations
  - Mixed operation chains
- 7 new unit tests for `Result.Try` and `Result<T>.Try` methods covering:
  - Try: successful operations, exception wrapping, custom error mapping
  - Real-world scenarios: JSON parsing, file operations
- 5 new unit tests for `Result.Tap` and `Result.TapError` methods covering:
  - Tap: execution on success/failure
  - TapError: execution on success/failure
  - Chaining with other operations
- 3 new unit tests for `Result.MapError` method covering:
  - MapError: error transformation on success/failure
  - Chaining with other operations
- 7 new unit tests for `Result.Finally` and `Result<T>.Finally` methods covering:
  - Finally: execution on success and failure
  - Resource cleanup simulation
  - Chaining with other operations
- **Comprehensive test suite** ensuring library correctness:
  - **MonadLawsTests.cs** (13 tests) - Verifies Result<T> satisfies Monad Laws:
    - Left Identity: `return a >>= f ≡ f a`
    - Right Identity: `m >>= return ≡ m`
    - Associativity: `(m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)`
    - Functor Laws: Map preserves composition and identity
  - **InvariantTests.cs** (34 tests) - Verifies critical invariants:
    - XOR Property: `IsSuccess XOR IsFailure` always true
    - Error Invariants: Success has Error.None, Failure has non-None error
    - Null Safety: Proper handling of nullable types and default values
    - Immutability: Operations don't mutate original Result
    - Match Invariants: Exactly one branch executes in Match/Switch
  - **ErrorPropagationTests.cs** (48 tests) - Verifies error propagation:
    - Map/Bind/Tap/Ensure preserve errors correctly
    - OrElse recovery and fallback error handling
    - MapError transformation behavior
    - Try exception wrapping
    - Finally cleanup with error propagation
    - Complex error propagation chains
  - **CompositionTests.cs** (60 tests) - Verifies operator composition:
    - Map/Bind/Tap/Ensure composition and chaining
    - Operation ordering and short-circuiting
    - OrElse fallback chains
    - Complex multi-operator scenarios
    - Non-generic Result composition
  - **Total: 464 tests** (was 298) ensuring comprehensive coverage of Railway Oriented Programming patterns
- Documentation for `Map` and `Bind` patterns in README.md with practical examples
- New error type `Unauthorized` for authentication failures (user not logged in)
- Factory methods for unauthorized errors:
  - `Error.UnauthorizedError(string message)` - with default code "Unauthorized"
  - `Error.UnauthorizedError(string code, string message)` - with custom code

### Fixed
- Fixed README.md not being included in NuGet package - moved from Build.NuGet.props to project file
- Fixed CI/CD pack command to target only the main project (`src/Voyager.Common.Results/Voyager.Common.Results.csproj`)
- Fixed IDE0036 code analyzer error: Corrected modifier ordering from `public new static` to `public static new` in `ResultT.cs`
- Fixed duplicate `IsExternalInit` package reference issue

### Changed
- **Comprehensive documentation updates** for `Try` and `Map` methods:
  - Updated `README.md` with Try exception handling section and enhanced Map examples
  - Enhanced `docs/getting-started.md` with Try alternative to try-catch pattern
  - Added Try examples to `docs/examples.md` (file parsing, data import scenarios)
  - Improved `docs/best-practices.md` with Try patterns and Map best practices
  - Updated `src/Voyager.Common.Results/README.md` (Polish) with full Try and Map documentation
- Updated GitHub Actions workflow to pack only the library project, not test projects
- Enhanced MinVer versioning documentation in `docs/QUICK-START-VERSIONING.md`
- Updated build documentation in `BUILD.md` with improved MinVer guidance
- Improved AI coding instructions in `.github/copilot-instructions.md` with latest patterns

### Technical
- Migrated to MinVer-based Git tag versioning system
- Added ADR-001 documenting MinVer Git-based versioning strategy with Major-only AssemblyVersion approach
- Updated CI workflow to use artifacts for better package handling between jobs
- Refactored build configuration into modular props files (`build/Build.*.props`)

## [1.2.0] - 2025-01-15

### Added
- New error types for better error categorization:
  - `UnavailableError` - for temporary service unavailability (rate limiting, maintenance, circuit breaker)
  - `TimeoutError` - for operation timeouts (HTTP, database, gateway timeouts)
- Factory methods for new error types:
  - `Error.UnavailableError(string message)`
  - `Error.UnavailableError(string code, string message)`
  - `Error.TimeoutError(string message)`
  - `Error.TimeoutError(string code, string message)`
- Comprehensive documentation for new error types in `docs\error-types.md`
- Real-world examples for timeout and unavailability scenarios
- 4 new unit tests for `UnavailableError` and `TimeoutError`
- HTTP status code mapping guidance (503, 429, 408, 504)
- Exception to error type mapping table
- Circuit breaker pattern example with `UnavailableError`
- Retry logic pattern for transient errors

### Changed
- Updated `ErrorType` enum with `Unavailable` and `Timeout` values
- Enhanced error type decision tree in documentation
- Improved best practices for error type selection

### Technical
- Upgraded C# language version to 10.0 for .NET Framework 4.8 projects
- Added `IsExternalInit` package for C# 10 record support on .NET Framework 4.8
- Fixed CS8630 error (nullable reference types require C# 8.0+)
- Fixed CS8773 error (file-scoped namespaces and global usings require C# 10.0+)

## [1.1.0] - 2025-01-13

### Added
- `OrElse` methods for fallback pattern with lazy evaluation
- `OrElseAsync` methods for async fallback operations (4 overloads)
- Comprehensive documentation for OrElse pattern in `docs\orelse-pattern.md`
- Real-world examples for multi-tier caching, resilient APIs, and configuration hierarchies
- 16 new unit tests for OrElse/OrElseAsync patterns

## [1.0.0] - 2025-01-10

### Added
- Initial release of Voyager.Common.Results
- `Result<T>` type for functional error handling
- `Result` (non-generic) type for void operations
- `Error` type with multiple error categories:
  - ValidationError
  - NotFoundError
  - PermissionError
  - ConflictError
  - DatabaseError
  - BusinessError
  - UnexpectedError
- Railway Oriented Programming support:
  - `Map` - transform success values
  - `Bind` - chain operations returning Result
  - `Tap` - side effects without changing Result
  - `Ensure` - validation with predicates
  - `Match` - pattern matching
  - `Switch` - void pattern matching
- Async extensions:
  - `MapAsync`
  - `BindAsync`
  - `TapAsync`
  - `EnsureAsync`
- Collection extensions:
  - `Combine` - merge multiple Results
  - `Partition` - split into successes and failures
  - `GetSuccessValues` - extract all success values
  - `GetErrors` - extract all errors
  - `AllSuccess` / `AnySuccess` - check collection state
- Implicit conversions for ergonomic API
- Multi-targeting support for .NET Framework 4.8 and .NET 8
- Full XML documentation
- Source Link support for debugging
- Comprehensive unit tests (212 tests)

### Supported Frameworks
- .NET Framework 4.8
- .NET 8.0

[Unreleased]: https://github.com/Voyager-Poland/Voyager.Common.Results/compare/v1.3.0...HEAD
[1.3.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.3.0
[1.2.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.2.0
[1.1.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.1.0
[1.0.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.0.0
