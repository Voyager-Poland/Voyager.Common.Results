# Async Operations

Voyager.Common.Results provides full async/await support through extension methods. All railway operators have async variants for working with `Task<Result<T>>`.

## Extension Methods Location

```csharp
using Voyager.Common.Results.Extensions;
```

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

### EnsureAsync - Async Validation

Validate with async predicates.

**Signatures:**
```csharp
// Async predicate on async result
Task<Result<T>> EnsureAsync(
    this Task<Result<T>> resultTask, 
    Func<T, Task<bool>> asyncPredicate, 
    Error error
)

// Sync predicate on async result
Task<Result<T>> EnsureAsync(
    this Task<Result<T>> resultTask, 
    Func<T, bool> predicate, 
    Error error
)

// Async predicate on sync result
Task<Result<T>> EnsureAsync(
    this Result<T> result, 
    Func<T, Task<bool>> asyncPredicate, 
    Error error
)
```

**Examples:**

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

### ConfigureAwait

When building libraries, use `ConfigureAwait(false)`:

```csharp
public async Task<Result<User>> GetUserAsync(int id)
{
    var user = await _database.Users.FindAsync(id).ConfigureAwait(false);
    // ...
}
```

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
