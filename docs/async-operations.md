# Async Operations

Voyager.Common.Results provides full async/await support through extension methods. All railway operators have async variants for working with `Task<Result<T>>`.

## Extension Methods Location

```csharp
using Voyager.Common.Results.Extensions;
```

## TryAsync - Safe Async Exception Handling

Convert exception-throwing async code into Result pattern. Use `Result<T>.TryAsync` proxy for cleaner syntax.

**Signatures:**
```csharp
// Basic - wraps exceptions with Error.FromException
Result<T>.TryAsync(Func<Task<T>> func)

// With custom error mapping
Result<T>.TryAsync(Func<Task<T>> func, Func<Exception, Error> errorMapper)

// With CancellationToken - returns ErrorType.Cancelled if cancelled
Result<T>.TryAsync(Func<CancellationToken, Task<T>> func, CancellationToken ct)

// With CancellationToken and custom error mapping
Result<T>.TryAsync(Func<CancellationToken, Task<T>> func, CancellationToken ct, Func<Exception, Error> errorMapper)
```

**Examples:**

```csharp
// Basic usage
var config = await Result<Config>.TryAsync(async () => 
    await JsonSerializer.DeserializeAsync<Config>(stream));

// With CancellationToken (recommended for HTTP calls)
var response = await Result<string>.TryAsync(
    async ct => await httpClient.GetStringAsync(url, ct),
    cancellationToken);

// If cancelled: result.Error.Type == ErrorType.Cancelled

// Custom error mapping
var config = await Result<Config>.TryAsync(
    async () => await LoadConfigAsync(),
    ex => ex is FileNotFoundException 
        ? Error.NotFoundError("Config file not found")
        : ex is JsonException
        ? Error.ValidationError("Invalid config format")
        : Error.FromException(ex));

// Chain with other operations
var userData = await Result<string>.TryAsync(
        async ct => await File.ReadAllTextAsync(path, ct),
        cancellationToken)
    .BindAsync(json => ParseUserAsync(json))
    .MapAsync(user => user.Email);
```

**Real-world example - HTTP with retry fallback:**
```csharp
public async Task<Result<WeatherData>> GetWeatherAsync(
    string city, 
    CancellationToken ct)
{
    return await Result<WeatherData>.TryAsync(
            async token => await _primaryApi.GetWeatherAsync(city, token),
            ct)
        .OrElseAsync(() => Result<WeatherData>.TryAsync(
            async token => await _fallbackApi.GetWeatherAsync(city, token),
            ct))
        .TapAsync(data => _cache.SetAsync($"weather:{city}", data, ct));
}
```

## Retry - Transient Failure Handling

Handle temporary failures (network issues, service unavailability, timeouts) with automatic retry logic and exponential backoff.

### Why Retry?

In distributed systems, transient failures are common:
- Network hiccups
- Database connection pools temporarily exhausted
- Services briefly unavailable during deployments
- Rate limiting (429 responses)
- Temporary deadlocks

Retry logic automatically handles these scenarios without cluttering your business logic.

### Basic Usage

**Signatures:**
```csharp
// Retry with policy
Task<Result<TOut>> BindWithRetryAsync<TIn, TOut>(
    this Result<TIn> result,
    Func<TIn, Task<Result<TOut>>> func,
    RetryPolicy policy
)

// Task<Result> overload
Task<Result<TOut>> BindWithRetryAsync<TIn, TOut>(
    this Task<Result<TIn>> resultTask,
    Func<TIn, Task<Result<TOut>>> func,
    RetryPolicy policy
)

// RetryPolicy delegate
public delegate Result<int> RetryPolicy(int attemptNumber, Error error);
```

**Default Policy - TransientErrors:**

```csharp
using Voyager.Common.Results.Extensions;

// Retry with defaults (3 attempts, exponential backoff)
var result = await GetDatabaseConnection()
    .BindWithRetryAsync(
        conn => ExecuteQuery(conn),
        RetryPolicies.TransientErrors()
    );

// Custom configuration
var result = await FetchDataAsync()
    .BindWithRetryAsync(
        data => ProcessData(data),
        RetryPolicies.TransientErrors(maxAttempts: 5, baseDelayMs: 500)
    );
```

**What Gets Retried?**

By default, only **transient errors**:
- ✅ `ErrorType.Unavailable` - Service down, network issues, deadlocks
- ✅ `ErrorType.Timeout` - Operation exceeded time limit
- ❌ `ErrorType.Validation` - Permanent, don't retry
- ❌ `ErrorType.NotFound` - Permanent, don't retry
- ❌ `ErrorType.Permission` - Permanent, don't retry
- ❌ All other error types - Permanent by default

**Exponential Backoff:**

Delays grow exponentially to avoid overwhelming failing services:

| Attempt | Delay (baseDelayMs=1000) |
|---------|--------------------------|
| 1 → 2   | 1000ms (1s)             |
| 2 → 3   | 2000ms (2s)             |
| 3 → 4   | 4000ms (4s)             |
| 4 → 5   | 8000ms (8s)             |

Formula: `baseDelayMs * 2^(attempt-1)`

### Custom Retry Policies

**Custom predicate and delay strategy:**

```csharp
// Retry specific errors with linear backoff
var policy = RetryPolicies.Custom(
    maxAttempts: 10,
    shouldRetry: e => e.Type == ErrorType.Unavailable || e.Code == "RATE_LIMIT",
    delayStrategy: attempt => 500 * attempt // 500ms, 1000ms, 1500ms...
);

var result = await apiCall.BindWithRetryAsync(ProcessResponse, policy);
```

**Advanced: Jitter for distributed systems:**

```csharp
private static readonly Random _random = new Random();

var policy = RetryPolicies.Custom(
    maxAttempts: 5,
    shouldRetry: e => e.Type == ErrorType.Unavailable,
    delayStrategy: attempt =>
    {
        int exponential = 1000 * (int)Math.Pow(2, attempt - 1);
        int jitter = _random.Next(0, 500); // Add randomness
        return exponential + jitter;
    }
);
```

### Critical: Error Preservation

**Retry ALWAYS preserves the original error** - it never replaces it with generic messages:

```csharp
var result = await GetDatabaseConnection()
    .BindWithRetryAsync(
        conn => ExecuteQuery(conn),
        RetryPolicies.TransientErrors(maxAttempts: 3)
    );

// If all retries fail:
// ✅ result.Error = original error (e.g., "Database connection timeout")
// ❌ NOT "Max retries exceeded" - preserves context for debugging
```

This is **critical** for Railway-Oriented Programming - errors must carry business context, not infrastructure noise.

### Real-World Examples

**Database operations with retry:**

```csharp
public async Task<Result<User>> GetUserWithRetryAsync(int userId)
{
    return await Result<int>.Success(userId)
        .BindWithRetryAsync(
            async id =>
            {
                return await Result<User>.TryAsync(async () =>
                {
                    using var conn = await _db.OpenConnectionAsync();
                    return await conn.QuerySingleOrDefaultAsync<User>(
                        "SELECT * FROM Users WHERE Id = @id",
                        new { id });
                });
            },
            RetryPolicies.TransientErrors(maxAttempts: 5, baseDelayMs: 1000)
        )
        .Ensure(user => user != null, Error.NotFoundError($"User {userId} not found"));
}
```

**HTTP API with retry and fallback:**

```csharp
public async Task<Result<WeatherData>> GetWeatherWithResilienceAsync(string city)
{
    return await Result<string>.Success(city)
        .BindWithRetryAsync(
            async c => await Result<WeatherData>.TryAsync(
                async () => await _primaryApi.GetWeatherAsync(c)),
            RetryPolicies.TransientErrors(maxAttempts: 3)
        )
        .OrElseAsync(async () =>
            await Result<string>.Success(city)
                .BindWithRetryAsync(
                    async c => await Result<WeatherData>.TryAsync(
                        async () => await _secondaryApi.GetWeatherAsync(c)),
                    RetryPolicies.TransientErrors(maxAttempts: 3)
                )
        )
        .TapAsync(data => _cache.SetAsync($"weather:{city}", data));
}
```

**Rate-limited API:**

```csharp
public async Task<Result<ApiResponse>> CallRateLimitedApiAsync(ApiRequest request)
{
    var retryPolicy = RetryPolicies.Custom(
        maxAttempts: 10,
        shouldRetry: e => 
            e.Type == ErrorType.Unavailable || 
            e.Code == "RATE_LIMIT" ||
            e.Code == "429",
        delayStrategy: attempt => 
        {
            // Respect Retry-After header if available
            if (attempt == 1 && TryGetRetryAfter(e, out var retryAfter))
                return retryAfter;
            
            // Otherwise exponential backoff with cap
            return Math.Min(30000, 2000 * (int)Math.Pow(2, attempt - 1));
        }
    );

    return await Result<ApiRequest>.Success(request)
        .BindWithRetryAsync(
            async req => await CallApiAsync(req),
            retryPolicy
        );
}
```

**Complex chain with retry at specific points:**

```csharp
public async Task<Result<OrderConfirmation>> ProcessOrderAsync(CreateOrderDto dto)
{
    return await ValidateOrderItems(dto.Items)  // No retry - validation
        .BindAsync(items => CreateOrderAsync(items))  // No retry - creation
        .BindWithRetryAsync(  // Retry payment - external service
            order => ChargePaymentAsync(order),
            RetryPolicies.TransientErrors(maxAttempts: 3)
        )
        .TapAsync(order => _logger.LogInformationAsync($"Payment completed: {order.Id}"))
        .BindWithRetryAsync(  // Retry notification - external service
            order => SendConfirmationEmailAsync(order),
            RetryPolicies.TransientErrors(maxAttempts: 5, baseDelayMs: 500)
        )
        .MapAsync(order => new OrderConfirmation(order.Id, order.Total));
}
```

### When NOT to Use Retry

**Don't retry permanent failures:**
```csharp
// ❌ BAD - retrying validation makes no sense
var result = await ValidateInput(input)
    .BindWithRetryAsync(
        valid => ProcessData(valid),
        RetryPolicies.TransientErrors()  // Validation won't fix itself!
    );

// ✅ GOOD - only retry the operation that might be transient
var result = await ValidateInput(input)
    .BindWithRetryAsync(
        valid => SaveToDatabase(valid),  // Database might be temporarily down
        RetryPolicies.TransientErrors()
    );
```

**Use Circuit Breaker for cascading failures:**

Retry is for **isolated transient failures**. If you need to detect and prevent cascading failures, use a circuit breaker pattern (Polly or separate library).

```csharp
// Retry = "This specific call failed, try again"
// Circuit Breaker = "This service is unhealthy, stop calling it"
```

### Best Practices

1. **Default to TransientErrors()** - covers 90% of cases
2. **Log retry attempts** - use `TapError` for observability:
   ```csharp
   .BindWithRetryAsync(op, RetryPolicies.TransientErrors())
   .TapError(e => _logger.LogWarning("Operation failed after retries: {Error}", e))
   ```
3. **Set reasonable max attempts** - balance resilience vs latency
4. **Use jitter in distributed systems** - prevents thundering herd
5. **Respect rate limits** - parse `Retry-After` headers
6. **Don't retry everything** - only operations with transient failures
7. **Test failure scenarios** - ensure retries work as expected

### Alternatives

**For advanced resilience features:**
- Circuit Breaker: Use Polly or create separate `Voyager.Common.Resilience` package
- Bulkhead Isolation: Polly
- Fallback chains: Use `OrElse`/`OrElseAsync` (already in library)
- Timeout policies: Combine with `CancellationToken`

**Polly integration:**

While Polly is exception-centric, it supports `HandleResult<T>`:
```csharp
var policy = Policy<Result<User>>
    .HandleResult(r => r.IsFailure && r.Error.Type == ErrorType.Unavailable)
    .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(Math.Pow(2, i)));

var result = await policy.ExecuteAsync(async () => await GetUserAsync(id));
```

For simple retry scenarios, `BindWithRetryAsync` is lighter and more idiomatic.

## Core Async Operators

### MapAsync - Transform Async Results

Transform the value inside an async Result.

**Signatures:**
```csharp
// Async mapper on async result
Task<Result<TOut>> MapAsync<TOut>(
    this Task<Result<T>> resultTask, 
    Func<T, Task<TOut>> asyncMapper
)

// Sync mapper on async result
Task<Result<TOut>> MapAsync<TOut>(
    this Task<Result<T>> resultTask, 
    Func<T, TOut> mapper
)

// Async mapper on sync result
Task<Result<TOut>> MapAsync<TOut>(
    this Result<T> result, 
    Func<T, Task<TOut>> asyncMapper
)
```

**Examples:**

```csharp
// Async to async
async Task<Result<User>> GetUserAsync(int id) { /* ... */ }
async Task<string> FormatNameAsync(User user) { /* ... */ }

var result = await GetUserAsync(123)
    .MapAsync(user => FormatNameAsync(user));
// Result<string>

// Async to sync
var result2 = await GetUserAsync(123)
    .MapAsync(user => user.Email.ToLower());
// Result<string>

// Sync to async
var userResult = GetUser(123);  // Sync method
var emailResult = await userResult
    .MapAsync(user => SendEmailAsync(user.Email));
// Result<void>
```

**Real-world example:**

```csharp
public async Task<Result<OrderDto>> GetOrderDetailsAsync(int orderId)
{
    return await GetOrderAsync(orderId)
        .MapAsync(order => EnrichWithCustomerDataAsync(order))
        .MapAsync(order => MapToDto(order));
}

async Task<Order> EnrichWithCustomerDataAsync(Order order)
{
    order.Customer = await _customerService.GetAsync(order.CustomerId);
    return order;
}
```

### BindAsync - Chain Async Operations

Chain operations that return async Results.

**Signatures:**
```csharp
// Async binder on async result
Task<Result<TOut>> BindAsync<TOut>(
    this Task<Result<T>> resultTask, 
    Func<T, Task<Result<TOut>>> asyncBinder
)

// Sync binder on async result
Task<Result<TOut>> BindAsync<TOut>(
    this Task<Result<T>> resultTask, 
    Func<T, Result<TOut>> binder
)

// Async binder on sync result
Task<Result<TOut>> BindAsync<TOut>(
    this Result<T> result, 
    Func<T, Task<Result<TOut>>> asyncBinder
)
```

**Examples:**

```csharp
async Task<Result<User>> GetUserAsync(int id) { /* ... */ }
async Task<Result<Order>> GetLatestOrderAsync(User user) { /* ... */ }
async Task<Result<decimal>> CalculateTotalAsync(Order order) { /* ... */ }

// Full async chain
var total = await GetUserAsync(123)
    .BindAsync(user => GetLatestOrderAsync(user))
    .BindAsync(order => CalculateTotalAsync(order));
// Result<decimal>

// Mix sync and async
var result = await GetUserAsync(123)
    .BindAsync(user => ValidateUser(user))  // Sync validation
    .BindAsync(user => SaveUserAsync(user)); // Async save
```

**Real-world example:**

```csharp
public async Task<Result<PaymentConfirmation>> ProcessPaymentAsync(PaymentRequest request)
{
    return await ValidatePaymentRequest(request)  // Sync validation
        .BindAsync(req => GetUserAsync(req.UserId))
        .BindAsync(user => ValidateCreditCardAsync(user.CreditCard))
        .BindAsync(card => ChargeCardAsync(card, request.Amount))
        .BindAsync(charge => CreatePaymentRecordAsync(charge))
        .MapAsync(payment => new PaymentConfirmation 
        { 
            PaymentId = payment.Id, 
            Amount = payment.Amount 
        });
}
```

### TapAsync - Async Side Effects

Execute async side effects without changing the Result.

**Signatures:**
```csharp
// Async action on async result
Task<Result<T>> TapAsync(
    this Task<Result<T>> resultTask, 
    Func<T, Task> asyncAction
)

// Sync action on async result
Task<Result<T>> TapAsync(
    this Task<Result<T>> resultTask, 
    Action<T> action
)

// Async action on sync result
Task<Result<T>> TapAsync(
    this Result<T> result, 
    Func<T, Task> asyncAction
)
```

**Examples:**

```csharp
var result = await GetUserAsync(123)
    .TapAsync(user => _logger.LogInformationAsync($"Found: {user.Name}"))
    .TapAsync(user => _cache.SetAsync($"user-{user.Id}", user))
    .TapAsync(user => _eventBus.PublishAsync(new UserRetrievedEvent(user)))
    .MapAsync(user => user.Email);
```

**Real-world example:**

```csharp
public async Task<Result<Order>> PlaceOrderAsync(CreateOrderDto dto)
{
    return await ValidateOrderItems(dto.Items)
        .BindAsync(items => CreateOrderAsync(items))
        .TapAsync(order => _logger.LogInformationAsync($"Order {order.Id} created"))
        .BindAsync(order => ProcessPaymentAsync(order))
        .TapAsync(order => SendConfirmationEmailAsync(order.UserId, order))
        .TapAsync(order => UpdateInventoryAsync(order.Items))
        .TapAsync(order => _eventBus.PublishAsync(new OrderPlacedEvent(order)));
}
```

### TapErrorAsync - Async Error Side Effects

Execute async side effects when the result is a failure, without changing the Result.

**Signatures:**
```csharp
// Async action on async result
Task<Result<T>> TapErrorAsync(
    this Task<Result<T>> resultTask,
    Func<Error, Task> asyncAction
)

// Sync action on async result
Task<Result<T>> TapErrorAsync(
    this Task<Result<T>> resultTask,
    Action<Error> action
)

// Async action on sync result
Task<Result<T>> TapErrorAsync(
    this Result<T> result,
    Func<Error, Task> asyncAction
)
```

**Examples:**

```csharp
// Log errors to external service
var result = await GetUserAsync(123)
    .TapErrorAsync(async error => await _logger.LogErrorAsync($"Failed: {error.Message}"))
    .TapErrorAsync(async error => await _alertService.SendAsync($"User lookup failed: {error.Code}"));

// Sync error handling on async result
var result = await GetUserAsync(123)
    .TapErrorAsync(error => Console.WriteLine($"Error: {error.Message}"));

// Async error handling on sync result
var result = GetUser(123)
    .TapErrorAsync(async error => await _metrics.IncrementAsync("user_lookup_failures"));
```

**Real-world example:**

```csharp
public async Task<Result<PaymentConfirmation>> ProcessPaymentAsync(PaymentRequest request)
{
    return await ValidatePaymentRequest(request)
        .BindAsync(req => ChargeCardAsync(req))
        .TapAsync(confirmation => _logger.LogInformationAsync($"Payment {confirmation.Id} succeeded"))
        .TapErrorAsync(async error => await _alertService.NotifyPaymentFailureAsync(request.UserId, error))
        .TapErrorAsync(async error => await _metrics.RecordPaymentFailureAsync(error.Code));
}
```

**Combining TapAsync and TapErrorAsync:**

```csharp
public async Task<Result<User>> GetUserWithAuditAsync(int userId)
{
    return await GetUserAsync(userId)
        .TapAsync(user => _auditLog.LogAccessAsync(userId, "success"))
        .TapErrorAsync(error => _auditLog.LogAccessAsync(userId, $"failed: {error.Code}"));
}
```

### EnsureAsync - Async Validation

Validate with async predicates.

**Signatures:**
```csharp
// Sync predicate on async result - static error
Task<Result<T>> EnsureAsync(
    this Task<Result<T>> resultTask, 
    Func<T, bool> predicate, 
    Error error
)

// Sync predicate on async result - contextual error
Task<Result<T>> EnsureAsync(
    this Task<Result<T>> resultTask, 
    Func<T, bool> predicate, 
    Func<T, Error> errorFactory
)

// Async predicate on sync result - static error
Task<Result<T>> EnsureAsync(
    this Result<T> result, 
    Func<T, Task<bool>> asyncPredicate, 
    Error error
)

// Async predicate on sync result - contextual error
Task<Result<T>> EnsureAsync(
    this Result<T> result, 
    Func<T, Task<bool>> asyncPredicate, 
    Func<T, Error> errorFactory
)

// Async predicate on async result - static error
Task<Result<T>> EnsureAsync(
    this Task<Result<T>> resultTask, 
    Func<T, Task<bool>> asyncPredicate, 
    Error error
)

// Async predicate on async result - contextual error
Task<Result<T>> EnsureAsync(
    this Task<Result<T>> resultTask, 
    Func<T, Task<bool>> asyncPredicate, 
    Func<T, Error> errorFactory
)
```

**Examples with static error:**

```csharp
async Task<bool> IsEmailUniqueAsync(string email)
{
    return !await _database.Users.AnyAsync(u => u.Email == email);
}

var result = await GetUserAsync(123)
    .EnsureAsync(
        user => user.IsActive,  // Sync check
        Error.BusinessError("User is not active")
    )
    .EnsureAsync(
        user => IsEmailUniqueAsync(user.Email),  // Async check
        Error.ConflictError("Email already exists")
    );
```

**Examples with contextual error (recommended for better messages):**

```csharp
var result = await GetUserAsync(123)
    .EnsureAsync(
        user => user.IsActive,
        user => Error.BusinessError($"User {user.Name} (ID: {user.Id}) is inactive"))
    .EnsureAsync(
        async user => await IsEmailUniqueAsync(user.Email),
        user => Error.ConflictError($"Email {user.Email} is already in use"));
```

**Real-world example:**

```csharp
public async Task<Result<User>> UpdateUserEmailAsync(int userId, string newEmail)
{
    return await GetUserAsync(userId)
        .EnsureAsync(
            user => user.EmailVerified,
            Error.ValidationError("Current email must be verified")
        )
        .EnsureAsync(
            user => IsValidEmailFormatAsync(newEmail),
            Error.ValidationError("Invalid email format")
        )
        .EnsureAsync(
            user => IsEmailUniqueAsync(newEmail),
            Error.ConflictError("Email already in use")
        )
        .BindAsync(user => UpdateEmailAsync(user, newEmail))
        .TapAsync(user => SendVerificationEmailAsync(user));
}
```

### OrElseAsync - Async Fallback Pattern

Provide alternative async Results when the current operation fails. Perfect for implementing multi-tier data retrieval strategies.

**Signatures:**
```csharp
// Async result with sync alternative
Task<Result<T>> OrElseAsync(
    this Task<Result<T>> resultTask, 
    Result<T> alternative
}

// Async result with sync alternative function
Task<Result<T>> OrElseAsync(
    this Task<Result<T>> resultTask, 
    Func<Result<T>> alternativeFunc
)

// Sync result with async alternative function  
Task<Result<T>> OrElseAsync(
    this Result<T> result, 
    Func<Task<Result<T>>> alternativeFunc
)

// Async result with async alternative function
Task<Result<T>> OrElseAsync(
    this Task<Result<T>> resultTask, 
    Func<Task<Result<T>>> alternativeFunc
)
```

**Examples:**

```csharp
// Simple fallback
var user = await GetUserFromCacheAsync(id)
    .OrElseAsync(GetDefaultUser());

// Lazy evaluation - function called only if needed
var config = await LoadConfigAsync()
    .OrElseAsync(() => GetDefaultConfigAsync());

// Chained alternatives - tries each source until success
var data = await GetFromPrimaryCacheAsync(key)
    .OrElseAsync(() => GetFromSecondaryCacheAsync(key))
    .OrElseAsync(() => GetFromDatabaseAsync(key))
    .OrElseAsync(() => GetFromApiAsync(key));
```

**Real-world examples:**

```csharp
// Example 1: Multi-tier data retrieval
public async Task<Result<UserProfile>> GetUserProfileAsync(int userId)
{
    return await GetProfileFromMemoryCacheAsync(userId)
        .OrElseAsync(() => GetProfileFromRedisCacheAsync(userId))
        .OrElseAsync(() => GetProfileFromDatabaseAsync(userId))
        .OrElseAsync(() => BuildDefaultProfileAsync(userId))
        .TapAsync(profile => CacheProfileAsync(userId, profile));
}

// Example 2: Resilient API calls
public async Task<Result<WeatherData>> GetWeatherAsync(string city)
{
    return await GetFromPrimaryApiAsync(city)
        .OrElseAsync(() => GetFromBackupApiAsync(city))
        .OrElseAsync(() => GetFromCachedDataAsync(city))
        .OrElseAsync(Error.NotFoundError($"Weather data for {city} unavailable"));
}

// Example 3: Configuration loading with fallbacks
public async Task<Result<AppConfig>> LoadConfigurationAsync()
{
    return await LoadFromEnvironmentVariablesAsync()
        .OrElseAsync(() => LoadFromConfigFileAsync())
        .OrElseAsync(() => LoadFromDatabaseAsync())
        .OrElseAsync(() => LoadFromDefaultsAsync())
        .TapAsync(config => ValidateConfigAsync(config));
}

// Example 4: User authentication with multiple providers
public async Task<Result<AuthToken>> AuthenticateUserAsync(string username, string password)
{
    return await AuthenticateWithPrimaryProviderAsync(username, password)
        .OrElseAsync(() => AuthenticateWithLdapAsync(username, password))
        .OrElseAsync(() => AuthenticateWithActiveDirectoryAsync(username, password))
        .TapAsync(token => LogSuccessfulLoginAsync(username))
        .TapError(error => LogFailedLoginAsync(username, error));
}
```

**Lazy Evaluation:**

```csharp
// Functions are called ONLY when needed
var result = await GetFromPrimaryAsync()    // Called first
    .OrElseAsync(() => GetFromSecondaryAsync())  // Only if primary fails
    .OrElseAsync(() => GetFromTertiaryAsync());   // Only if both fail

// If primary succeeds, secondary and tertiary are never called!
```

**Combining with other operators:**
```csharp
var processedData = await LoadDataFromSourceAsync(id)
    .OrElseAsync(() => LoadDataFromBackupAsync(id))
    .OrElseAsync(() => CreateDefaultDataAsync(id))
    .EnsureAsync(
        data => data.IsValid(),
        Error.ValidationError("Data validation failed")
    )
    .BindAsync(data => ProcessDataAsync(data))
    .TapAsync(result => CacheResultAsync(id, result))
    .MapAsync(result => result.ToDto());
```

## Complete Async Workflows

### Example 1: User Registration

```csharp
public async Task<Result<UserDto>> RegisterUserAsync(RegisterDto dto)
{
    return await ValidateEmail(dto.Email)  // Sync validation
        .EnsureAsync(
            email => IsEmailUniqueAsync(email),
            Error.ConflictError("Email already registered")
        )
        .BindAsync(email => ValidatePasswordAsync(dto.Password))
        .MapAsync(password => new User 
        { 
            Email = dto.Email, 
            PasswordHash = HashPassword(password) 
        })
        .BindAsync(user => SaveUserAsync(user))
        .TapAsync(user => _logger.LogInformationAsync($"User {user.Id} registered"))
        .TapAsync(user => SendWelcomeEmailAsync(user))
        .TapAsync(user => _cache.SetAsync($"user-{user.Id}", user))
        .MapAsync(user => MapToDto(user));
}

async Task<Result<string>> ValidatePasswordAsync(string password)
{
    if (string.IsNullOrEmpty(password))
        return Error.ValidationError("Password is required");
    
    if (password.Length < 8)
        return Error.ValidationError("Password must be at least 8 characters");
    
    // Check against compromised password list (async)
    if (await _passwordService.IsCompromisedAsync(password))
        return Error.ValidationError("Password has been compromised");
    
    return password;
}
```

### Example 2: Order Processing

```csharp
public async Task<Result<OrderConfirmation>> ProcessOrderAsync(int userId, CreateOrderDto dto)
{
    return await GetUserAsync(userId)
        .EnsureAsync(
            user => user.IsActive,
            Error.BusinessError("User account is not active")
        )
        .EnsureAsync(
            user => HasSufficientBalanceAsync(user, dto.TotalAmount),
            Error.BusinessError("Insufficient account balance")
        )
        .BindAsync(user => CreateOrderAsync(user, dto))
        .TapAsync(order => _logger.LogInformationAsync($"Order {order.Id} created"))
        .BindAsync(order => ProcessPaymentAsync(order))
        .TapAsync(order => DeductBalanceAsync(userId, order.TotalAmount))
        .BindAsync(order => ReserveInventoryAsync(order))
        .TapAsync(order => _eventBus.PublishAsync(new OrderPlacedEvent(order)))
        .TapAsync(order => SendOrderConfirmationAsync(order))
        .MapAsync(order => new OrderConfirmation 
        { 
            OrderId = order.Id, 
            ConfirmationNumber = Guid.NewGuid().ToString() 
        });
}

async Task<bool> HasSufficientBalanceAsync(User user, decimal amount)
{
    var balance = await _accountService.GetBalanceAsync(user.Id);
    return balance >= amount;
}
```

### Example 3: File Upload Processing

```csharp
public async Task<Result<FileMetadata>> ProcessFileUploadAsync(
    int userId, 
    Stream fileStream, 
    string fileName)
{
    return await GetUserAsync(userId)
        .EnsureAsync(
            user => user.HasPermission("files:upload"),
            Error.PermissionError("User doesn't have upload permission")
        )
        .BindAsync(user => ValidateFileAsync(fileStream, fileName))
        .TapAsync(file => _logger.LogInformationAsync($"Uploading {fileName}"))
        .BindAsync(file => ScanForVirusesAsync(file))
        .BindAsync(file => UploadToBlobStorageAsync(file))
        .TapAsync(url => _cache.InvalidateAsync($"user-files-{userId}"))
        .MapAsync(url => new FileMetadata 
        { 
            Url = url, 
            FileName = fileName, 
            UploadedAt = DateTime.UtcNow 
        })
        .BindAsync(metadata => SaveFileMetadataAsync(userId, metadata))
        .TapAsync(metadata => _eventBus.PublishAsync(new FileUploadedEvent(metadata)));
}
```

### Example 4: Resilient Data Retrieval with Fallbacks

```csharp
public async Task<Result<Product>> GetProductAsync(string productId)
{
    return await GetProductFromMemoryCacheAsync(productId)
        .OrElseAsync(() => GetProductFromRedisCacheAsync(productId))
        .OrElseAsync(() => GetProductFromDatabaseAsync(productId))
        .OrElseAsync(() => GetProductFromExternalApiAsync(productId))
        .TapAsync(product => CacheProductAsync(productId, product))
        .TapAsync(product => _logger.LogInformationAsync($"Product {productId} retrieved"))
        .TapError(error => _logger.LogWarningAsync($"Failed to get product {productId}: {error.Message}"));
}

private async Task<Result<Product>> GetProductFromMemoryCacheAsync(string productId)
{
    var product = _memoryCache.Get<Product>($"product:{productId}");
    return product is not null
        ? Result<Product>.Success(product)
        : Error.NotFoundError("Not in memory cache");
}

private async Task<Result<Product>> GetProductFromRedisCacheAsync(string productId)
{
    try
    {
        var product = await _redisCache.GetAsync<Product>($"product:{productId}");
        return product is not null
            ? Result<Product>.Success(product)
            : Error.NotFoundError("Not in Redis cache");
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError("Redis cache error", ex);
    }
}

private async Task<Result<Product>> GetProductFromDatabaseAsync(string productId)
{
    try
    {
        var product = await _database.Products.FindAsync(productId);
        return product is not null
            ? Result<Product>.Success(product)
            : Error.NotFoundError("Not in database");
    }
    catch (DbException ex)
    {
        return Error.DatabaseError("Database query failed", ex);
    }
}

private async Task<Result<Product>> GetProductFromExternalApiAsync(string productId)
{
    try
    {
        var product = await _productApiClient.GetProductAsync(productId);
        return product is not null
            ? Result<Product>.Success(product)
            : Error.NotFoundError($"Product {productId} not found");
    }
    catch (HttpRequestException ex)
    {
        return Error.UnexpectedError("External API error", ex);
    }
}

private async Task CacheProductAsync(string productId, Product product)
{
    _memoryCache.Set($"product:{productId}", product, TimeSpan.FromMinutes(15));
    await _redisCache.SetAsync($"product:{productId}", product, TimeSpan.FromHours(1));
}
```

### Example 5: Configuration Loading with Multiple Sources

```csharp
public async Task<Result<AppSettings>> LoadApplicationSettingsAsync()
{
    return await LoadSettingsFromEnvironmentAsync()
        .OrElseAsync(() => LoadSettingsFromAzureAppConfigAsync())
        .OrElseAsync(() => LoadSettingsFromLocalFileAsync())
        .OrElseAsync(() => LoadSettingsFromEmbeddedDefaultsAsync())
        .EnsureAsync(
            settings => settings.IsValid(),
            Error.ValidationError("Invalid application settings")
        )
        .TapAsync(settings => _logger.LogInformationAsync("Application settings loaded successfully"))
        .TapAsync(settings => CacheSettingsAsync(settings));
}

private async Task<Result<AppSettings>> LoadSettingsFromEnvironmentAsync()
{
    var settings = AppSettings.FromEnvironmentVariables();
    return settings.HasRequiredValues()
        ? Result<AppSettings>.Success(settings)
        : Error.NotFoundError("Required environment variables not set");
}

private async Task<Result<AppSettings>> LoadSettingsFromAzureAppConfigAsync()
{
    try
    {
        var settings = await _azureAppConfig.LoadAsync();
        return Result<AppSettings>.Success(settings);
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError("Failed to load from Azure App Configuration", ex);
    }
}

private async Task<Result<AppSettings>> LoadSettingsFromLocalFileAsync()
{
    try
    {
        if (!File.Exists("appsettings.json"))
            return Error.NotFoundError("Local configuration file not found");

        var json = await File.ReadAllTextAsync("appsettings.json");
        var settings = JsonSerializer.Deserialize<AppSettings>(json);
        
        return settings is not null
            ? Result<AppSettings>.Success(settings)
            : Error.ValidationError("Failed to deserialize settings");
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError("Failed to load local configuration", ex);
    }
}

private async Task<Result<AppSettings>> LoadSettingsFromEmbeddedDefaultsAsync()
{
    var defaults = AppSettings.CreateDefaults();
    await Task.CompletedTask; // Simulate async
    return Result<AppSettings>.Success(defaults);
}
```

## Parallel Async Operations

### Pattern 1: Independent Async Calls

```csharp
public async Task<Result<DashboardData>> GetDashboardDataAsync(int userId)
{
    // Execute independent calls in parallel
    var userTask = GetUserAsync(userId);
    var ordersTask = GetUserOrdersAsync(userId);
    var statsTask = GetUserStatsAsync(userId);
    
    await Task.WhenAll(userTask, ordersTask, statsTask);
    
    // Combine results
    var userResult = await userTask;
    if (userResult.IsFailure) return userResult.Error;
    
    var ordersResult = await ordersTask;
    if (ordersResult.IsFailure) return ordersResult.Error;
    
    var statsResult = await statsTask;
    if (statsResult.IsFailure) return statsResult.Error;
    
    return new DashboardData 
    { 
        User = userResult.Value, 
        Orders = ordersResult.Value, 
        Stats = statsResult.Value 
    };
}
```

### Pattern 2: Using Collection Extensions

```csharp
public async Task<Result<List<User>>> GetMultipleUsersAsync(int[] userIds)
{
    var tasks = userIds.Select(id => GetUserAsync(id));
    var results = await Task.WhenAll(tasks);
    
    // Combine all results - fails if any failed
    return results.Combine();
}
```

## Error Handling in Async Operations

### Pattern 1: Try-Catch with Async

```csharp
public async Task<Result<User>> GetUserAsync(int userId)
{
    if (userId <= 0)
        return Error.ValidationError("Invalid user ID");
    
    try
    {
        var user = await _database.Users.FindAsync(userId);
        
        if (user is null)
            return Error.NotFoundError($"User {userId} not found");
        
        return user;
    }
    catch (DbUpdateException ex)
    {
        return Error.DatabaseError("Database error", ex);
    }
    catch (TimeoutException ex)
    {
        return Error.UnexpectedError("Database timeout", ex);
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError("Unexpected error", ex);
    }
}
```

### Pattern 2: Wrapping External Async APIs

```csharp
public async Task<Result<PaymentResponse>> ChargeCardAsync(CreditCard card, decimal amount)
{
    try
    {
        var response = await _paymentGateway.ChargeAsync(card, amount);
        return response;
    }
    catch (PaymentDeclinedException ex)
    {
        return Error.BusinessError("Payment was declined", ex);
    }
    catch (PaymentGatewayException ex)
    {
        return Error.UnexpectedError("Payment gateway error", ex);
    }
    catch (HttpRequestException ex)
    {
        return Error.UnexpectedError("Network error communicating with payment gateway", ex);
    }
}
```

## Best Practices

### ✅ DO

```csharp
// Chain async operations smoothly
var result = await GetUserAsync(id)
    .BindAsync(user => ValidateUserAsync(user))
    .BindAsync(user => SaveUserAsync(user));

// Use TapAsync for async side effects
await result.TapAsync(user => SendEmailAsync(user));

// Use EnsureAsync for async validations
await result.EnsureAsync(
    user => IsUniqueEmailAsync(user.Email),
    Error.ConflictError("Email exists")
);

// Handle both sync and async in the same chain
var result = await GetUserAsync(id)
    .BindAsync(user => ValidateUser(user))  // Sync
    .BindAsync(user => SaveUserAsync(user)) // Async
    .TapAsync(user => _logger.Log(user));   // Sync
```

### ❌ DON'T

```csharp
// Don't await intermediate results unnecessarily
var user = await GetUserAsync(id);
var validated = await ValidateUserAsync(user.Value);  // ❌ Nested awaits

// Use chain instead:
var result = await GetUserAsync(id)
    .BindAsync(user => ValidateUserAsync(user));  // ✅

// Don't forget to await async Taps
result.TapAsync(user => SendEmailAsync(user));  // ❌ Fire and forget!
await result.TapAsync(user => SendEmailAsync(user));  // ✅

// Don't mix async and sync incorrectly
result.Map(user => SaveUserAsync(user));  // ❌ Returns Result<Task<Result<User>>>
result.BindAsync(user => SaveUserAsync(user));  // ✅
```

## Performance Considerations

### ConfigureAwait - Library Behavior

**All async methods in Voyager.Common.Results internally use `ConfigureAwait(false)`.**

This means:
- ✅ **No deadlocks** - Safe to use in WinForms, WPF, legacy ASP.NET
- ✅ **Better performance** - No unnecessary thread context switches
- ✅ **You don't need to add `ConfigureAwait(false)`** when calling this library

**If you need UI thread context after calling library methods:**

```csharp
// Library internally handles ConfigureAwait(false)
var result = await Result<string>.TryAsync(
    async ct => await httpClient.GetStringAsync(url, ct),
    cancellationToken);

// Return to UI thread for UI updates
await UpdateUIAsync(result).ConfigureAwait(true);

// Or use synchronization context explicitly
await Task.Run(() => { }).ConfigureAwait(true);  // Back to UI thread
lblStatus.Text = result.Match(
    onSuccess: data => $"Loaded: {data.Length} bytes",
    onFailure: error => $"Error: {error.Message}");
```

### ASP.NET 4.8 - Preserving HttpContext

In legacy ASP.NET (.NET Framework 4.8), `HttpContext.Current` flows through `SynchronizationContext`. Since library methods use `ConfigureAwait(false)`, you must capture HttpContext **before** the await:

```csharp
// ❌ WRONG - HttpContext.Current is null after await
public async Task<ActionResult> GetUserAsync(int id)
{
    var result = await Result<User>.TryAsync(
        async ct => await _userService.GetByIdAsync(id, ct),
        HttpContext.RequestAborted);
    
    // HttpContext.Current is NULL here!
    var userName = HttpContext.Current.User.Identity.Name;  // NullReferenceException!
    
    return Json(result);
}

// ✅ CORRECT - Capture HttpContext BEFORE await
public async Task<ActionResult> GetUserAsync(int id)
{
    // Capture context before any await
    var httpContext = HttpContext.Current;
    var currentUser = httpContext.User.Identity.Name;
    var requestAborted = httpContext.Response.ClientDisconnectedToken;
    
    var result = await Result<User>.TryAsync(
        async ct => await _userService.GetByIdAsync(id, ct),
        requestAborted);
    
    // Use captured values - safe!
    _logger.Log($"User {currentUser} requested user {id}");
    
    return Json(result.Match(
        onSuccess: user => new { success = true, data = user },
        onFailure: error => new { success = false, message = error.Message }));
}

// ✅ ALTERNATIVE - Extract all needed values into local variables
public async Task<ActionResult> ProcessOrderAsync(OrderRequest request)
{
    // Extract everything you need from HttpContext upfront
    var context = HttpContext.Current;
    var userId = context.User.Identity.Name;
    var sessionId = context.Session?.SessionID;
    var clientIp = context.Request.UserHostAddress;
    var cancellationToken = context.Response.ClientDisconnectedToken;
    
    var result = await GetUser(userId)
        .BindAsync(user => ValidateOrder(request, user))
        .BindAsync(order => ProcessPaymentAsync(order, cancellationToken))
        .TapAsync(order => LogOrderAsync(order, clientIp, sessionId));
    
    return Json(result);
}
```

**Best practice for ASP.NET 4.8 controllers:**

```csharp
public abstract class BaseApiController : ApiController
{
    // Capture in property - available throughout request
    protected string CurrentUserId => HttpContext.Current?.User?.Identity?.Name;
    protected string ClientIp => HttpContext.Current?.Request?.UserHostAddress;
    
    // Or capture once in action and pass to methods
    protected RequestContext CaptureContext()
    {
        var ctx = HttpContext.Current;
        return new RequestContext
        {
            UserId = ctx?.User?.Identity?.Name,
            SessionId = ctx?.Session?.SessionID,
            ClientIp = ctx?.Request?.UserHostAddress,
            CancellationToken = ctx?.Response?.ClientDisconnectedToken ?? CancellationToken.None
        };
    }
}

public class OrderController : BaseApiController
{
    public async Task<ActionResult> CreateAsync(OrderDto dto)
    {
        var ctx = CaptureContext();  // Capture BEFORE any await
        
        var result = await Result<Order>.TryAsync(
                async ct => await _orderService.CreateAsync(dto, ctx.UserId, ct),
                ctx.CancellationToken)
            .TapAsync(order => _auditLog.LogAsync(
                $"Order {order.Id} created by {ctx.UserId} from {ctx.ClientIp}"));
        
        return Json(result);
    }
}

public record RequestContext
{
    public string UserId { get; init; }
    public string SessionId { get; init; }
    public string ClientIp { get; init; }
    public CancellationToken CancellationToken { get; init; }
}
```

> ⚠️ **Note:** This issue does NOT affect ASP.NET Core. In ASP.NET Core, `HttpContext` is injected via `IHttpContextAccessor` and doesn't rely on `SynchronizationContext`.

### AsyncLocal&lt;T&gt; - Context That Flows Through Async

`AsyncLocal<T>` preserves values across async/await boundaries regardless of `ConfigureAwait`. It's ideal for cross-cutting concerns like correlation IDs, user context, or request tracing.

```csharp
// Define ambient context that flows through async calls
public static class AmbientContext
{
    private static readonly AsyncLocal<RequestContext> _current = new();
    
    public static RequestContext Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}

public record RequestContext
{
    public string UserId { get; init; }
    public string CorrelationId { get; init; }
    public string ClientIp { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

// Set once at request start (e.g., in Global.asax or ActionFilter)
public class ContextInitializerAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        var httpContext = HttpContext.Current;
        AmbientContext.Current = new RequestContext
        {
            UserId = httpContext.User?.Identity?.Name,
            CorrelationId = httpContext.Request.Headers["X-Correlation-ID"] 
                            ?? Guid.NewGuid().ToString(),
            ClientIp = httpContext.Request.UserHostAddress,
            CancellationToken = httpContext.Response.ClientDisconnectedToken
        };
    }
}

// Use anywhere - even after ConfigureAwait(false)!
[ContextInitializer]
public class OrderController : ApiController
{
    public async Task<ActionResult> CreateAsync(OrderDto dto)
    {
        // AmbientContext.Current is available throughout the async flow
        var result = await Result<Order>.TryAsync(
                async ct => await _orderService.CreateAsync(dto, ct),
                AmbientContext.Current.CancellationToken)
            .TapAsync(order => LogOrderCreatedAsync(order));
        
        return Json(result);
    }
    
    private async Task LogOrderCreatedAsync(Order order)
    {
        // Still works! AsyncLocal flows through ConfigureAwait(false)
        var ctx = AmbientContext.Current;
        await _auditLog.WriteAsync(
            $"[{ctx.CorrelationId}] User {ctx.UserId} from {ctx.ClientIp} " +
            $"created order {order.Id}");
    }
}

// Works in any layer - services, repositories, etc.
public class OrderService
{
    public async Task<Order> CreateAsync(OrderDto dto, CancellationToken ct)
    {
        var userId = AmbientContext.Current.UserId;  // ✅ Available!
        var correlationId = AmbientContext.Current.CorrelationId;
        
        _logger.LogInformation("[{CorrelationId}] Creating order for {UserId}", 
            correlationId, userId);
        
        // ... implementation
    }
}
```

**When to use each approach:**

| Approach | Best For |
|----------|----------|
| **Local variables** | Simple cases, single controller action |
| **Parameter passing** | Explicit dependencies, easy to test |
| **AsyncLocal&lt;T&gt;** | Cross-cutting concerns (logging, tracing, audit), deep call stacks |

**When building your own libraries on top of Voyager.Common.Results:**

```csharp
public async Task<Result<User>> GetUserAsync(int id)
{
    // Your code should also use ConfigureAwait(false)
    var user = await _database.Users.FindAsync(id).ConfigureAwait(false);
    // ...
}
```

> 📖 See [ADR-0001](adr/ADR-0001-no-configureawait-parameter-in-tryasync.md) for the architectural decision behind this behavior.

### Avoid Async When Not Needed

```csharp
// Don't make it async if it's not
public Result<int> Calculate(int x, int y)  // ✅ Sync is fine
{
    return x + y;
}

// Not this:
public async Task<Result<int>> CalculateAsync(int x, int y)  // ❌ Unnecessary
{
    return await Task.FromResult(x + y);
}
```

## See Also

- **[Railway Oriented Programming](railway-oriented.md)** - Sync operator details
- **[Collection Operations](collection-operations.md)** - Combine async results
- **[Best Practices](best-practices.md)** - General Result Pattern best practices
- **[Examples](examples.md)** - More real-world examples
