# Voyager.Common.Results

[![NuGet](https://img.shields.io/nuget/v/Voyager.Common.Results.svg)](https://www.nuget.org/packages/Voyager.Common.Results/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Voyager.Common.Results.svg)](https://www.nuget.org/packages/Voyager.Common.Results/)
[![Build Status](https://github.com/Voyager-Poland/Voyager.Common.Results/workflows/.NET%20push/badge.svg)](https://github.com/Voyager-Poland/Voyager.Common.Results/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A lightweight, functional **Result Pattern** implementation for .NET that enables **Railway Oriented Programming**. Replace exceptions with explicit error handling, making your code more predictable and easier to test.

**Supports .NET Framework 4.8 and .NET 8** 🚀

## ✨ Features

- 🎯 **Type-safe error handling** without exceptions
- 🚂 **Railway Oriented Programming** with method chaining
- ⚡ **Async/await support** with extension methods and instance proxies
- 🧩 **Contextual errors** with Ensure/EnsureAsync error factories
- 🧵 **Deadlock-safe async** - library uses ConfigureAwait(false) internally
- 📦 **Zero dependencies** (except polyfills for .NET Framework)
- 🔍 **Source Link enabled** for debugging
- 📚 **Comprehensive XML documentation**
- 🧪 **Fully tested** with high code coverage
- 🎨 **Implicit conversions** for ergonomic API
- 🤖 **Automated publishing** via GitHub Actions

## 📦 Installation

```bash
# Core Result pattern library
dotnet add package Voyager.Common.Results

# Advanced resilience patterns (Circuit Breaker)
dotnet add package Voyager.Common.Resilience
```

## 🚀 Quick Start

```csharp
using Voyager.Common.Results;

// Define operations that can fail (assumes repository doesn't throw exceptions)
public Result<User> GetUser(int id)
{
    var user = _repository.Find(id);
    return user is not null 
        ? user  // Implicit conversion: User → Result<User>
        : Error.NotFoundError($"User {id} not found");
}

public Result<Order> GetLatestOrder(User user)
{
    var order = _repository.GetLatestOrder(user.Id);
    return order is not null
        ? order 
        : Error.NotFoundError("No orders found");
}

// Chain operations with Railway Oriented Programming
var result = GetUser(123)
    .Bind(user => GetLatestOrder(user))
    .Map(order => order.TotalAmount)
    .Tap(total => _logger.LogInfo($"Total: {total}"));

// Handle the result
var message = result.Match(
    onSuccess: total => $"Order total: {total:C}",
    onFailure: error => $"Error: {error.Message}"
);
```

## 🧪 Testing

The library includes a comprehensive test suite ensuring correctness across multiple dimensions:

- **Monad Laws** - Verifies mathematical properties of Result<T> (identity, composition)
- **Invariants** - XOR property, null safety, immutability guarantees
- **Error Propagation** - Correct error flow through all operators and chains
- **Composition** - Operator chaining and combination behavior in complex scenarios
- **Unit Tests** - Core functionality, extension methods, edge cases, and cancellation

All tests validate behavior on both **.NET 8.0** and **.NET Framework 4.8** to ensure cross-platform compatibility.

## 📖 Documentation

### Core Types

- **`Result<T>`** - Represents an operation that returns a value or an error
- **`Result`** - Represents an operation that returns success or an error (void operations)
- **`Error`** - Represents an error with type and message

### Error Types

```csharp
Error.ValidationError("Invalid email format")
Error.NotFoundError("User not found")
Error.UnauthorizedError("User not logged in")
Error.PermissionError("Access denied")
Error.ConflictError("Email already exists")
Error.DatabaseError("Connection failed")
Error.BusinessError("Cannot cancel paid order")
Error.UnavailableError("Service temporarily unavailable")
Error.TimeoutError("Request timed out")
Error.CancelledError("Operation was cancelled")
Error.CircuitBreakerOpenError(lastError) // Circuit breaker open (from Resilience library)
Error.UnexpectedError("Something went wrong")
Error.FromException(exception)
```

### Railway Oriented Programming

```csharp
GetUser(id)
    .Map(user => user.Email)              // Transform success value
    .Bind(email => SendEmail(email))       // Chain another Result operation
    .Ensure(sent => sent, Error.BusinessError("Email not sent"))
    .Tap(() => _logger.LogInfo("Email sent"))  // Side effect
    .OrElse(() => GetDefaultUser())        // Fallback if failed
    .Match(
        onSuccess: () => "Success",
        onFailure: error => error.Message
    );
```

### Finally - Resource Cleanup

Executes an action regardless of success or failure (like finally block):

```csharp
// Chain with other operations
var result = GetUser(id)
    .Map(user => user.Email)
    .Tap(email => _logger.LogInfo(email))
    .Finally(() => _metrics.RecordOperation());
```

**When to use Finally:**
- ✅ Resource cleanup (close connections, dispose streams)
- ✅ Logging/metrics regardless of outcome
- ✅ Releasing locks or semaphores
- ✅ Any cleanup that must happen in both success and failure paths

### Try - Exception Handling

Safely convert exception-throwing code into Result pattern:

```csharp
// Basic: wraps exceptions with Error.FromException
var result = Result<int>.Try(() => int.Parse(userInput));

// Custom error mapping
var result = Result<int>.Try(
    () => int.Parse(userInput),
    ex => ex is FormatException 
        ? Error.ValidationError("Invalid number format")
        : Error.FromException(ex));

// Void operations
var result = Result.Try(() => File.Delete(path));

// With custom error handling
var result = Result.Try(
    () => File.Delete(path),
    ex => ex is UnauthorizedAccessException
        ? Error.PermissionError("Access denied")
        : Error.FromException(ex));

// Chain with other operations
var userData = Result<string>.Try(() => File.ReadAllText(path))
    .Bind(json => ParseJson(json))
    .Map(data => data.UserId);

// Robust database operations (handles both exceptions and null)
public Result<User> GetUser(int id)
{
    return Result<User>.Try(() => _repository.Find(id))
        .Ensure(user => user is not null, Error.NotFoundError($"User {id} not found"));
}
```

**When to use Try:**
- ✅ Wrapping third-party APIs that throw exceptions
- ✅ File I/O, parsing, network calls
- ✅ Converting legacy exception-based code to Result pattern
- ✅ Custom exception-to-error mapping

### TryAsync - Async Exception Handling

Safely convert async exception-throwing code into Result pattern. Automatically maps `OperationCanceledException` to `ErrorType.Cancelled` when using CancellationToken:

```csharp
// Preferred: Use Result<T>.TryAsync proxy for cleaner syntax
var result = await Result<Config>.TryAsync(async () => 
    await JsonSerializer.DeserializeAsync<Config>(stream));

// With CancellationToken support (auto-maps OperationCanceledException → ErrorType.Cancelled)
var result = await Result<string>.TryAsync(
    async ct => await httpClient.GetStringAsync(url, ct),
    cancellationToken);

// Custom error mapping
var result = await Result<Config>.TryAsync(
    async () => await JsonSerializer.DeserializeAsync<Config>(stream),
    ex => ex is JsonException 
        ? Error.ValidationError("Invalid JSON")
        : Error.UnexpectedError(ex.Message));

// With CancellationToken and custom error mapping
var result = await Result<string>.TryAsync(
    async ct => await httpClient.GetStringAsync(url, ct),
    cancellationToken,
    ex => ex is HttpRequestException 
        ? Error.UnavailableError("Service unavailable")
        : Error.UnexpectedError(ex.Message));

// Chain with other async operations
var userData = await Result<string>.TryAsync(async () => 
        await File.ReadAllTextAsync(path))
    .BindAsync(json => ParseJsonAsync(json))
    .MapAsync(data => data.UserId);
```

**When to use TryAsync:**
- ✅ Async file I/O, database operations
- ✅ HTTP/API calls with cancellation support
- ✅ Async parsing and serialization
- ✅ Converting async exception-based code to Result pattern
- ✅ Operations that need proper cancellation handling

### Retry - Transient Failure Handling

Handle temporary failures (network issues, service unavailability) with automatic retry logic:

```csharp
using Voyager.Common.Results.Extensions;

// Basic retry with default policy (3 attempts, exponential backoff)
var result = await GetDatabaseConnection()
    .BindWithRetryAsync(
        conn => ExecuteQuery(conn),
        RetryPolicies.TransientErrors()
    );

// Custom retry configuration
var result = await FetchDataAsync()
    .BindWithRetryAsync(
        data => ProcessData(data),
        RetryPolicies.TransientErrors(maxAttempts: 5, baseDelayMs: 500)
    );

// Custom retry policy for specific errors
var policy = RetryPolicies.Custom(
    maxAttempts: 10,
    shouldRetry: e => e.Type == ErrorType.Unavailable || e.Code == "RATE_LIMIT",
    delayStrategy: attempt => 500 // Fixed 500ms delay
);

var result = await apiCall.BindWithRetryAsync(ProcessResponse, policy);

// Retry automatically handles:
// ✅ ErrorType.Unavailable - Service down, network issues, deadlocks
// ✅ ErrorType.Timeout - Operation exceeded time limit
// ❌ Permanent errors (Validation, NotFound, etc.) - NOT retried
```

**Key features:**
- 🔄 Exponential backoff by default (1s → 2s → 4s → ...)
- 🎯 Only retries transient errors (`Unavailable`, `Timeout`)
- 📝 **Always preserves original error** - never generic "max retries exceeded"
- ⚡ Zero external dependencies
- 🔧 Fully customizable via `RetryPolicies.Custom()`

**When to use Retry:**
- ✅ Network calls with temporary failures
- ✅ Database operations during brief unavailability
- ✅ API calls that may be rate-limited or temporarily down
- ❌ NOT for permanent errors (Validation, NotFound)
- 💡 For cascading failure prevention, use Circuit Breaker from Voyager.Common.Resilience

### Circuit Breaker - Cascading Failure Prevention

**(Requires `Voyager.Common.Resilience` package)**

Prevent cascading failures by temporarily blocking calls to failing services:

```csharp
using Voyager.Common.Resilience;

// Create a circuit breaker policy
var policy = new CircuitBreakerPolicy(
    failureThreshold: 5,      // Open after 5 consecutive failures
    openTimeout: TimeSpan.FromSeconds(30),  // Stay open for 30s
    halfOpenMaxAttempts: 3    // Allow 3 test attempts when half-open
);

// Execute operations through the circuit breaker
var result = await GetUser(userId)
    .ExecuteAsync(
        user => CallExternalServiceAsync(user),
        policy
    );

// Circuit breaker states:
// 🟢 Closed - Normal operation, requests flow through
// 🔴 Open - Too many failures, requests immediately fail with CircuitBreakerOpenError
// 🟡 HalfOpen - Testing if service recovered, limited attempts allowed

// Check circuit state
if (policy.State == CircuitBreakerState.Open)
{
    _logger.LogWarning("Circuit breaker is open, service unavailable");
}

// Manual reset if needed
policy.Reset();
```

**Key features:**
- 🛡️ Prevents cascading failures across distributed systems
- ⚡ Fast-fail when service is down (no wasted retries)
- 🔄 Automatic recovery testing via half-open state
- 🧵 Thread-safe with SemaphoreSlim for async operations
- 📝 Preserves last error context via `CircuitBreakerOpenError(lastError)`
- 🎯 Returns `ErrorType.CircuitBreakerOpen` when circuit is open

**When to use Circuit Breaker:**
- ✅ External API/service calls that may fail
- ✅ Database operations during outages
- ✅ Microservice communication
- ✅ Any operation where cascading failures must be prevented
- 💡 Combine with Retry for comprehensive resilience

**When to use Retry:**
- ✅ Network calls with temporary failures
- ✅ Database operations during brief unavailability
- ✅ API calls that may be rate-limited or temporarily down
- ❌ NOT for permanent errors (Validation, NotFound)
- 💡 For cascading failure prevention, use Circuit Breaker from Voyager.Common.Resilience

### Map - Value Transformations

Transform success values or convert void operations to value operations:

```csharp
// Transform Result<T> values
var emailResult = GetUser(id)
    .Map(user => user.Email);              // Result<User> → Result<string>

// Convert Result (void) to Result<T> (value)
var numberResult = ValidateInput()
    .Map(() => 42);                         // Result → Result<int>

// Chain transformations
var result = GetUser(id)
    .Map(user => user.Email)
    .Map(email => email.ToLower())
    .Map(email => email.Trim());
```

**When to use Map:**
- ✅ Transform success value to another type
- ✅ Convert void success to value success
- ✅ Simple, non-failing transformations
- ❌ Don't use for operations that return Result (use `Bind` instead)

### MapError - Error Transformation

Transform errors without affecting success:

```csharp
// Add context to errors
var result = Operation()
    .MapError(error => Error.DatabaseError("DB_" + error.Code, error.Message));

// Convert error types
var result = ValidateUser()
    .MapError(error => Error.BusinessError("USER_" + error.Code, error.Message));

// Chain transformations
var result = GetData()
    .MapError(e => Error.UnavailableError("Service unavailable: " + e.Message))
    .TapError(e => _logger.LogError(e.Message));
```

**When to use MapError:**
- ✅ Add prefixes or context to error codes/messages
- ✅ Convert error types for different layers (API → Domain → Infrastructure)
- ✅ Enrich errors with additional information
- ✅ Standardize error formats

### Bind - Chaining Operations

The `Bind` method is available on both `Result` and `Result<T>` for seamless operation chaining:

```csharp
// Chain void operations (Result → Result)
var result = ValidateInput()
    .Bind(() => AuthorizeUser())
    .Bind(() => SaveToDatabase())
    .Bind(() => SendNotification());

// Transform void operation to value operation (Result → Result<T>)
var userResult = ValidateRequest()
    .Bind(() => GetUser(userId))
    .Map(user => user.Email);

// Mix void and value operations
var orderResult = AuthenticateUser()      // Result
    .Bind(() => GetShoppingCart(userId))  // Result → Result<Cart>
    .Bind(cart => ProcessOrder(cart))     // Result<Cart> → Result<Order>
    .Map(order => order.Id);              // Result<Order> → Result<int>
```

**When to use Bind:**
- ✅ Chain operations that return `Result<T>`
- ✅ Transform `Result` (void) to `Result<T>` (value)
- ✅ Maintain railway oriented flow
- ❌ Don't use for simple value transformations (use `Map` instead)

### OrElse - Fallback Pattern

```csharp
// Try multiple data sources - returns first success
var user = GetUserFromCache(userId)
    .OrElse(() => GetUserFromDatabase(userId))
    .OrElse(() => GetDefaultUser());

// Async version
var config = await LoadConfigFromFileAsync()
    .OrElseAsync(() => LoadConfigFromDatabaseAsync())
    .OrElseAsync(() => GetDefaultConfigAsync());

// Real-world example: Multi-tier data retrieval
var data = await GetFromPrimaryCacheAsync(key)
    .OrElseAsync(() => GetFromDatabaseAsync(key))
    .OrElseAsync(() => GetFromApiAsync(key))
    .OrElseAsync(Result<Data>.Success(defaultValue));
```

**Common use cases:**
- Cache → Database → Default value
- Primary API → Fallback API → Cached data  
- User preferences → Team defaults → System defaults

### Ensure - Contextual Validation

Validate with error messages that include the actual value:

```csharp
// Static error (old way)
var result = GetUser(id)
    .Ensure(
        user => user.Age >= 18,
        Error.ValidationError("Must be 18 or older"));

// Contextual error (recommended - provides better error messages)
var result = GetUser(id)
    .Ensure(
        user => user.Age >= 18,
        user => Error.ValidationError($"User {user.Name} is {user.Age} years old, must be 18+"));
```

### EnsureAsync - Async Contextual Validation

Validate with async predicates and contextual errors:

```csharp
// With sync predicate
var result = await GetUserAsync(id)
    .EnsureAsync(
        user => user.Age >= 18,
        user => Error.ValidationError($"User {user.Name} is {user.Age}, must be 18+"));

// With async predicate
var result = await GetUserAsync(id)
    .EnsureAsync(
        async user => await _repo.IsActiveAsync(user.Id),
        user => Error.ValidationError($"User {user.Name} is inactive"));
```

### Instance Method Proxies

No need to import `Extensions` namespace - common async methods are available directly on `Result<T>`:

```csharp
var result = await GetUser(id)              // Result<User>
    .EnsureAsync(
        async u => await _repo.IsActiveAsync(u.Id),
        u => Error.ValidationError($"User {u.Name} inactive"))
    .TapAsync(async u => await _audit.LogAsync($"Access: {u.Id}"))
    .OrElseAsync(() => GetDefaultUserAsync());
```

Available instance proxies:
- `EnsureAsync(asyncPredicate, error)` - async validation
- `EnsureAsync(asyncPredicate, errorFactory)` - async validation with contextual error
- `TapAsync(asyncAction)` - async side effects
- `OrElseAsync(asyncAlternativeFunc)` - async fallback

### Async Operations

```csharp
await GetUserAsync(id)
    .MapAsync(user => user.Email)
    .BindAsync(email => SendEmailAsync(email))
    .TapAsync(() => _logger.LogInfoAsync("Email sent"));
```

### Collection Operations

```csharp
var results = new Result<int>[] {
    1,  // Implicit conversion: int → Result<int>
    2,
    3
};

// Combine all results into one
Result<List<int>> combined = results.Combine();

// Partition into successes and failures
var (successes, failures) = results.Partition();

// Get only successful values
List<int> values = results.GetSuccessValues();
```

## 📚 More Examples

See the [full documentation](./src/Voyager.Common.Results/README.md) for detailed examples and best practices.

## 🏗️ Building and Publishing

See [BUILD.md](./BUILD.md) for comprehensive instructions on:
- 🤖 **Automatic publishing** with GitHub Actions (recommended)
- 🔨 Manual building and local testing
- 📦 Publishing to GitHub Packages and NuGet.org
- 🧪 Running tests with code coverage

**New to versioning?** See [Quick Start - Versioning](./docs/QUICK-START-VERSIONING.md) for a 3-step guide to create your first release.

### Quick Build

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build -c Release

# Run tests
dotnet test -c Release

# Pack the package
dotnet pack src/Voyager.Common.Results/Voyager.Common.Results.csproj -c Release
```

### Automatic Publishing

Simply push to `main` branch - GitHub Actions will:
1. ✅ Automatically bump version
2. ✅ Build for both .NET 8.0 and .NET Framework 4.8
3. ✅ Run all tests
4. ✅ Publish to GitHub Packages
5. ✅ Publish to NuGet.org (if configured)

```bash
git add .
git commit -m "Add new feature"
git push origin main
```

## 🧪 Running Tests

```bash
# Run all tests
dotnet test

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate coverage report (requires reportgenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html
```

### Development Workflow

- Push to `main` triggers automatic version bump and publishing
- All tests must pass before merging
- Follow existing code style and conventions
- Add tests for new features
- Update documentation as needed

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

Inspired by:
- [Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/) by Scott Wlaschin
- [Result type in Rust](https://doc.rust-lang.org/std/result/)
- [Either type in functional programming](https://en.wikipedia.org/wiki/Either_type)

## 📝 Changelog

See [CHANGELOG.md](CHANGELOG.md) for a list of changes.

## 📚 Additional Resources

- [GitHub Actions Setup Guide](./GITHUB_ACTIONS_SETUP.md) - Detailed GitHub Actions configuration
- [Build Guide](./BUILD.md) - Building and publishing instructions
- [API Documentation](./src/Voyager.Common.Results/README.md) - Complete API reference

---

Made with ❤️ by [Voyager Poland](https://github.com/Voyager-Poland)
