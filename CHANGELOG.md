# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.9.0] - 2026-02-18

### Added
- **`TraverseAsync`** — sequentially applies an async Result-returning function to each collection element (fail-fast on first error)
  - `IEnumerable<T>.TraverseAsync(Func<T, Task<Result<TOut>>>)` → `Task<Result<List<TOut>>>` — generic variant
  - `IEnumerable<T>.TraverseAsync(Func<T, Task<Result>>)` → `Task<Result>` — non-generic variant
  - Replaces manual `foreach + break + Combine()` pattern
  - Example:
    ```csharp
    var result = await operations.TraverseAsync(
        x => OperationUpdateResultAsync(ctx, x.Op, x.Data));
    ```
- **`TraverseAllAsync`** — like `TraverseAsync`, but collects ALL errors instead of stopping on the first one
  - `IEnumerable<T>.TraverseAllAsync(Func<T, Task<Result<TOut>>>)` → `Task<Result<List<TOut>>>` — generic variant
  - `IEnumerable<T>.TraverseAllAsync(Func<T, Task<Result>>)` → `Task<Result>` — non-generic variant
  - Errors aggregated via `InnerError` chain: first error → second error → ...
  - Example:
    ```csharp
    var result = await items.TraverseAllAsync(
        x => ValidateAndProcessAsync(x));
    // result.Error.InnerError contains subsequent errors
    ```
- **`CombineAsync`** — async version of existing `Combine`, awaits all tasks then combines results
  - `IEnumerable<Task<Result<TValue>>>.CombineAsync()` → `Task<Result<List<TValue>>>` — generic variant
  - `IEnumerable<Task<Result>>.CombineAsync()` → `Task<Result>` — non-generic variant
  - Uses `Task.WhenAll` for parallel awaiting, then fail-fast on first error
- **`PartitionAsync`** — async version of existing `Partition`, awaits all tasks then separates successes and failures
  - `IEnumerable<Task<Result<TValue>>>.PartitionAsync()` → `Task<(List<TValue>, List<Error>)>`
- **`Combine` tuple variants** — combine 2-4 `Result<T>` instances into a single `Result` containing a tuple
  - `Result<T1>.Combine(Result<T2>)` → `Result<(T1, T2)>`
  - `Result<T1>.Combine(Result<T2>, Result<T3>)` → `Result<(T1, T2, T3)>`
  - `Result<T1>.Combine(Result<T2>, Result<T3>, Result<T4>)` → `Result<(T1, T2, T3, T4)>`
  - Returns first error if any Result is a failure
- 34 new unit tests covering all new methods: happy path, fail-fast, empty collection, single element, order preservation, error aggregation chain verification

### Fixed
- **Documentation**: Fixed `Combine` tuple example in `docs/collection-operations.md` — customer lookup incorrectly depended on `order.Value` (violating ROP), changed to truly independent service calls

## [1.8.0] - 2026-02-16

### Added
- **VCR0050 CodeFix**: Replaces `Error.None` with `Error.UnexpectedError("TODO: provide error message")`
- **VCR0020 — Assert guard recognition**: Test assertions now recognized as valid guards before `.Value` access, eliminating false positives in test code
  - xUnit: `Assert.True(result.IsSuccess)`, `Assert.False(result.IsFailure)`
  - NUnit: `Assert.That(result.IsSuccess, ...)`, `Assert.IsTrue(...)`, `Assert.IsFalse(...)`
  - MSTest: `Assert.IsTrue(result.IsSuccess)`, `Assert.IsFalse(result.IsFailure)`
  - FluentAssertions: `result.IsSuccess.Should().BeTrue()`, `result.IsFailure.Should().BeFalse()`
  - Pattern matching is name-based — no framework references required in analyzer
- **HelpLinkUri for all analyzers (VCR0010–VCR0060)**: Clicking the diagnostic ID in Visual Studio or Rider opens the corresponding documentation page
  - Documentation: [`docs/analyzers/VCR0010.md`](docs/analyzers/VCR0010.md) through [`docs/analyzers/VCR0060.md`](docs/analyzers/VCR0060.md)
  - Each page includes: rule description, code examples (violation + fix), `.editorconfig` configuration

### Changed
- **`ResultTypeHelper`**: Added `HelpLinkBase` constant for shared documentation URL prefix

## [1.7.2] - 2026-02-16

### Added
- **Roslyn Analyzer VCR0010 — Result must be consumed**: Warns when `Result`/`Result<T>` return values are silently discarded
  - Detects unconsumed results from method calls, factory methods, and awaited `Task<Result>`
  - Two code fixes: "Discard result" (`_ = ...`) and "Assign to variable" (`var result = ...`)
  - Bundled in the NuGet package under `analyzers/dotnet/cs` — no extra install needed
  - See [ADR-0010](docs/adr/ADR-0010-result-consumption-analyzer.md) for design rationale
- **Roslyn Analyzer VCR0020 — Value accessed without success check**: Warns when `Result<T>.Value` is accessed without checking `IsSuccess`/`IsFailure`
  - Detects guarded patterns: `if (IsSuccess)`, early-return, ternary, `&&` short-circuit
  - Code fix 1: replaces `.Value` with `.GetValueOrThrow()` (useful in tests, controllers, adapters)
  - Code fix 2: wraps statement with `if (result.IsSuccess) { ... }` guard
- **Roslyn Analyzer VCR0030 — Nested `Result<Result<T>>`**: Warns when `Map` produces nested Result (should use `Bind`)
  - Code fix: replaces `Map` with `Bind` (or `MapAsync` with `BindAsync`)
- **Roslyn Analyzer VCR0040 — `GetValueOrThrow` defeats Result pattern**: Info-level hint to use `Match`/`Bind`/`Map` instead
- **Roslyn Analyzer VCR0050 — `Failure(Error.None)`**: Error-level diagnostic for creating failure without an error
- **Roslyn Analyzer VCR0060 — Prefer Match/Switch**: Disabled-by-default suggestion to use `Match`/`Switch` over `if/else` branching on `IsSuccess`
- **Voyager package icon**: Added to both Voyager.Common.Results and Voyager.Common.Resilience NuGet packages

### Changed
- **Analyzer internals — extract `ResultTypeHelper`**: Consolidated duplicated `IsResultType`, `IsResultMethod`, `UnwrapTaskType`, and `ResultNamespace` from all 6 analyzers into a shared `internal static class ResultTypeHelper`
  - Fixes VCR0020 bug: `IsResultType` now traverses base type hierarchy (previously only checked the direct type, missing inherited Result types)
- **VCR0020 — guard traversal across parent blocks**: Analyzer now searches parent blocks for failure guards, not just the immediate enclosing block (pattern 7: guard in outer `if`/`foreach` protects `.Value` in nested blocks)
- **VCR0020 — reassignment to Success pattern**: Recognizes `result = Result<T>.Success(...)` as last statement in failure guard as ensuring success after the block (pattern 8)
- **VCR0020 — `continue`/`break` as guard exit**: `if (result.IsFailure) { continue; }` in loops now recognized as valid guard, same as `return`/`throw` (pattern 9)
- **ADR-0010**: Documented guard patterns 7, 8, and 9 with examples

### Fixed
- **`.editorconfig` naming rules**: `private const` and `private static readonly` fields now correctly require PascalCase instead of `_camelCase`
- **Analyzer .csproj cleanup**: Removed duplicate `TargetFramework`/`TargetFrameworks` in `Voyager.Common.Results.Analyzers.csproj`, removed empty `<TargetFrameworks>` in test project, added comments explaining `Directory.Build.props` override
- **NU5046**: Include package icon in Resilience NuGet package
- **VCR0020 CodeFix line endings**: Detect EOL from syntax trivia instead of hardcoding `\r\n` (fixes CI on Linux)

## [1.7.1] - 2026-02-03

### Added
- **Error Classification Extensions (ADR-005)**: Methods for classifying errors for resilience patterns
  - `ErrorType.TooManyRequests` - New error type for rate limiting (HTTP 429)
  - `Error.TooManyRequestsError(message)` - Factory method with default code "RateLimit.Exceeded"
  - `Error.TooManyRequestsError(code, message)` - Factory method with custom code
  - `ErrorTypeExtensions.IsTransient()` - Returns true for retryable errors (Timeout, Unavailable, CircuitBreakerOpen, TooManyRequests)
  - `ErrorTypeExtensions.IsBusinessError()` - Returns true for business errors (Validation, NotFound, Permission, etc.)
  - `ErrorTypeExtensions.IsInfrastructureError()` - Returns true for infrastructure errors (Database, Unexpected)
  - `ErrorTypeExtensions.ShouldCountForCircuitBreaker()` - Returns true if error should count toward CB threshold
  - `ErrorTypeExtensions.ShouldRetry()` - Returns true if operation should be retried
  - `ErrorTypeExtensions.ToHttpStatusCode()` - Maps ErrorType to HTTP status code
  - See [ADR-0005](docs/adr/ADR-0005-error-classification-for-resilience.md) for design rationale

- **Error Chaining for Distributed Systems (ADR-006)**: Track error origin across service calls
  - `Error.InnerError` - Optional inner error property (like Exception.InnerException)
  - `Error.WithInner(error)` - Creates copy with inner error attached
  - `Error.GetRootCause()` - Traverses chain to find original error
  - `Error.HasInChain(predicate)` - Checks if any error in chain matches predicate
  - `Result<T>.WrapError(factory)` - Wraps error with new error, preserving original as InnerError
  - `Result<T>.AddErrorContext(serviceName, operation)` - Adds service context while preserving error type
  - Async versions: `WrapErrorAsync()`, `AddErrorContextAsync()`
  - See [ADR-0006](docs/adr/ADR-0006-error-chaining-for-distributed-systems.md) for design rationale
  - Example:
    ```csharp
    var result = await _productService.GetAsync(id)
        .AddErrorContextAsync("ProductService", "GetProduct");

    // Access root cause
    var rootCause = result.Error.GetRootCause();
    ```

- **Exception Details Preservation (ADR-007)**: Enhanced `FromException()` with full diagnostic information
  - `Error.StackTrace` - Stack trace preserved as string (GC-safe, no reference to Exception)
  - `Error.ExceptionType` - Full type name (e.g., "System.InvalidOperationException")
  - `Error.Source` - Source assembly/module name
  - `Error.FromException(exception)` - Now auto-maps exception types to ErrorType:
    - `OperationCanceledException` → `Cancelled`
    - `TimeoutException` → `Timeout`
    - `ArgumentException` → `Validation`
    - `InvalidOperationException` → `Business`
    - `KeyNotFoundException` → `NotFound`
    - `UnauthorizedAccessException` → `Permission`
    - `*Sql*/*Db*` exceptions → `Database`
    - `HttpRequestException`/`*Socket*`/`WebException` → `Unavailable`
  - `Error.FromException(exception, errorType)` - Override auto-mapping with custom type
  - `Error.ToDetailedString()` - Returns formatted error with stack trace and chain
  - Automatically chains `InnerException` to `InnerError`
  - See [ADR-0007](docs/adr/ADR-0007-exception-details-preservation.md) for design rationale
  - Example:
    ```csharp
    try { /* ... */ }
    catch (Exception ex)
    {
        var error = Error.FromException(ex);
        _logger.LogError(error.ToDetailedString());
    }
    // Output:
    // [Database] Exception.SqlException: Connection failed
    //   Exception: System.Data.SqlClient.SqlException
    //   Stack Trace:
    //     at Repository.Query() in Repository.cs:line 42
    //   Caused by:
    //     [Unavailable] Exception.SocketException: Network unreachable
    ```

- **Circuit Breaker State Change Callbacks (ADR-008)**: Get notified when circuit breaker state changes
  - `CircuitBreakerPolicy.OnStateChanged` - Callback invoked on state transitions
  - Parameters: `(CircuitState oldState, CircuitState newState, int failureCount, Error? lastError)`
  - Triggered on: Closed→Open, Open→HalfOpen, HalfOpen→Closed, HalfOpen→Open, Reset
  - Use cases: logging, alerting, metrics integration
  - See [ADR-0008](docs/adr/ADR-0008-circuit-breaker-state-change-callbacks.md) for design rationale
  - Example:
    ```csharp
    var circuitBreaker = new CircuitBreakerPolicy(failureThreshold: 5);
    circuitBreaker.OnStateChanged = (oldState, newState, failures, lastError) =>
    {
        _logger.LogWarning("Circuit breaker: {Old} → {New}, failures: {Count}",
            oldState, newState, failures);

        if (newState == CircuitState.Open)
            _alertService.SendAlert($"Circuit OPEN: {lastError?.Message}");
    };
    ```

- **Retry Attempt Callbacks (ADR-009)**: Get notified on each retry attempt
  - `BindWithRetryAsync(..., onRetryAttempt)` - New overload with callback parameter
  - Parameters: `(int attemptNumber, Error error, int delayMs)`
  - `delayMs > 0`: retry will happen after this delay
  - `delayMs = 0`: no more retries (max attempts reached)
  - Use cases: logging, metrics, debugging slow operations
  - See [ADR-0009](docs/adr/ADR-0009-retry-attempt-callbacks.md) for design rationale
  - Example:
    ```csharp
    var result = await operation.BindWithRetryAsync(
        async value => await _httpClient.GetAsync(value),
        RetryPolicies.TransientErrors(maxAttempts: 3),
        onRetryAttempt: (attempt, error, delayMs) =>
        {
            _logger.LogWarning("Attempt {Attempt} failed: {Error}. Retrying in {Delay}ms",
                attempt, error.Message, delayMs);
        });
    ```

## [1.6.0] - 2026-01-30

### Added
- **`TapErrorAsync` extensions for `Result<T>`**: Execute async side effects on failure without modifying the result
  - `Task<Result<T>>.TapErrorAsync(Action<Error>)` - sync action on async result
  - `Result<T>.TapErrorAsync(Func<Error, Task>)` - async action on sync result
  - `Task<Result<T>>.TapErrorAsync(Func<Error, Task>)` - async action on async result
  - Instance method proxy `Result<T>.TapErrorAsync(Func<Error, Task>)` for fluent usage
  - Example: `.TapErrorAsync(async error => await _alertService.SendAsync($"Failed: {error.Message}"))`

- **NEW LIBRARY: Voyager.Common.Resilience** - Separate package for advanced resilience patterns
  - **Circuit Breaker pattern**: Prevents cascading failures by temporarily blocking calls to failing operations
    - `CircuitBreakerPolicy` - Thread-safe implementation with 3-state model (Closed/Open/HalfOpen)
    - `BindWithCircuitBreakerAsync(func, policy)` extension methods for Result&lt;T&gt; integration
    - Configurable thresholds: failure threshold, open timeout, half-open max attempts
    - Automatic state transitions based on success/failure patterns
    - **Smart error filtering**: Only infrastructure errors (Unavailable, Timeout, Database, Unexpected) open the circuit
    - **Business errors ignored**: Validation, NotFound, Permission, Business, Conflict errors do NOT affect circuit state
    - ErrorType.CircuitBreakerOpen with last error preservation for context
    - See [ADR-0004](docs/adr/ADR-0004-circuit-breaker-pattern-for-resilience.md) for architectural rationale
  - **NuGet dependency**: Resilience library depends on Voyager.Common.Results package
  - **Installation**: `dotnet add package Voyager.Common.Resilience`

- **Retry extensions for transient failures**: Lightweight retry functionality without external dependencies
  - `BindWithRetryAsync(func, policy)` - Executes operations with configurable retry logic
  - `RetryPolicy` delegate for flexible retry strategies
  - `RetryPolicies.TransientErrors()` - Default policy retrying only `Unavailable` and `Timeout` errors with exponential backoff
  - `RetryPolicies.Custom()` - Build custom retry policies with predicates and delay strategies
  - `RetryPolicies.Default()` - Convenient default (3 attempts, 1s base delay)
  - **CRITICAL**: Always preserves original error context - never replaces with generic "max retries exceeded"
  - Task&lt;Result&gt; overload for async result chains
  - See [ADR-0003](docs/adr/ADR-0003-retry-extensions-for-transient-failures.md) for design rationale

- **ErrorType.CircuitBreakerOpen**: New error type for circuit breaker open state
  - `CircuitBreakerOpenError(lastError)` - Preserves context from original failure
  - HTTP mapping: 503 Service Unavailable

## [1.5.0] - 2026-01-27

### Added
- **`Bind(Func<TValue, Result>)` overload for `Result<T>`**: Chains `Result<T>` with operations returning void `Result`
  - Enables natural composition: `Result<T>` → `Result` → `Result<T>` → ...
  - Propagates errors from void operations (unlike `Tap`)
  - Example: `.Bind(user => SendNotification(user))` safely chains operations that can fail
  - Completes the monad pattern for void operations

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

[Unreleased]: https://github.com/Voyager-Poland/Voyager.Common.Results/compare/v1.9.0...HEAD
[1.9.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/compare/v1.8.0...v1.9.0
[1.8.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/compare/v1.7.2...v1.8.0
[1.7.2]: https://github.com/Voyager-Poland/Voyager.Common.Results/compare/v1.7.1...v1.7.2
[1.7.1]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.7.1
[1.6.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.6.0
[1.5.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.5.0
[1.4.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.4.0
[1.3.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.3.0
[1.2.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.2.0
[1.1.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.1.0
[1.0.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.0.0
