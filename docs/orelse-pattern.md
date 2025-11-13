# OrElse - Fallback Pattern

The `OrElse` methods provide a powerful fallback mechanism for handling alternative data sources or recovery strategies when operations fail. This pattern is essential for building resilient applications.

## Overview

`OrElse` returns the original result if successful, otherwise tries an alternative. It implements **lazy evaluation** - the alternative is only executed if needed.

## Synchronous OrElse

### Basic Syntax

```csharp
Result<T> OrElse(Result<T> alternative)
Result<T> OrElse(Func<Result<T>> alternativeFunc)
```

### Simple Example

```csharp
var user = GetUserFromCache(userId)
    .OrElse(GetDefaultUser());
```

### Lazy Evaluation

```csharp
// Function is called ONLY if first result fails
var user = GetUserFromCache(userId)
    .OrElse(() => GetUserFromDatabase(userId));  // Database query only if cache miss

// Without lazy evaluation, database would always be queried:
var dbUser = GetUserFromDatabase(userId);  // ❌ Always executes
var user = GetUserFromCache(userId).OrElse(dbUser);
```

### Chaining Multiple Alternatives

```csharp
Result<Config> GetConfiguration()
{
    return LoadFromEnvironment()
        .OrElse(() => LoadFromFile())
        .OrElse(() => LoadFromDatabase())
        .OrElse(() => GetDefaultConfig());
}
```

## Asynchronous OrElseAsync

### Signatures

```csharp
// Async result with sync alternative
Task<Result<T>> OrElseAsync(this Task<Result<T>> resultTask, Result<T> alternative)

// Async result with sync alternative function
Task<Result<T>> OrElseAsync(this Task<Result<T>> resultTask, Func<Result<T>> alternativeFunc)

// Sync result with async alternative function
Task<Result<T>> OrElseAsync(this Result<T> result, Func<Task<Result<T>>> alternativeFunc)

// Async result with async alternative function
Task<Result<T>> OrElseAsync(this Task<Result<T>> resultTask, Func<Task<Result<T>>> alternativeFunc)
```

### Basic Async Example

```csharp
var user = await GetUserFromCacheAsync(userId)
    .OrElseAsync(() => GetUserFromDatabaseAsync(userId))
    .OrElseAsync(() => GetUserFromApiAsync(userId));
```

## Common Patterns

### Pattern 1: Multi-Tier Cache Strategy

```csharp
public async Task<Result<Product>> GetProductAsync(string productId)
{
    return await GetFromL1CacheAsync(productId)      // Memory cache (fastest)
        .OrElseAsync(() => GetFromL2CacheAsync(productId))  // Redis cache
        .OrElseAsync(() => GetFromDatabaseAsync(productId)) // Database
        .TapAsync(product => WarmCachesAsync(productId, product));
}

private async Task WarmCachesAsync(string productId, Product product)
{
    await SetL2CacheAsync(productId, product);
    SetL1Cache(productId, product);
}
```

### Pattern 2: Primary/Fallback API

```csharp
public async Task<Result<ExchangeRate>> GetExchangeRateAsync(string currency)
{
    return await GetFromPrimaryApiAsync(currency)
        .OrElseAsync(() => GetFromFallbackApiAsync(currency))
        .OrElseAsync(() => GetFromCachedRatesAsync(currency))
        .OrElseAsync(Error.NotFoundError($"Exchange rate for {currency} unavailable"));
}
```

### Pattern 3: Configuration Hierarchy

```csharp
public async Task<Result<string>> GetSettingAsync(string key)
{
    return await GetUserSettingAsync(key)        // User preference
        .OrElseAsync(() => GetTeamSettingAsync(key))    // Team default
        .OrElseAsync(() => GetOrgSettingAsync(key))     // Organization default
        .OrElseAsync(() => GetSystemDefaultAsync(key)); // System default
}
```

### Pattern 4: Degraded Service Mode

```csharp
public async Task<Result<RecommendedProducts>> GetRecommendationsAsync(int userId)
{
    return await GetPersonalizedRecommendationsAsync(userId)  // AI-powered
        .OrElseAsync(() => GetCollaborativeRecommendationsAsync(userId))  // Collaborative filtering
        .OrElseAsync(() => GetPopularProductsAsync())  // Simple popular items
        .TapError(error => _logger.LogWarningAsync($"Recommendations degraded: {error.Message}"));
}
```

### Pattern 5: Feature Flags with Fallback

```csharp
public async Task<Result<PaymentResponse>> ProcessPaymentAsync(Payment payment)
{
    if (await _featureFlags.IsEnabledAsync("UseNewPaymentGateway"))
    {
        return await ProcessWithNewGatewayAsync(payment)
            .OrElseAsync(() => ProcessWithLegacyGatewayAsync(payment));  // Fallback if new fails
    }
    
    return await ProcessWithLegacyGatewayAsync(payment);
}
```

## Real-World Examples

### Example 1: Document Retrieval System

```csharp
public async Task<Result<Document>> GetDocumentAsync(Guid documentId)
{
    return await GetFromLocalCacheAsync(documentId)
        .OrElseAsync(() => GetFromCdnAsync(documentId))
        .OrElseAsync(() => GetFromBlobStorageAsync(documentId))
        .OrElseAsync(() => GetFromArchiveAsync(documentId))
        .TapAsync(doc => CacheDocumentAsync(documentId, doc))
        .TapAsync(doc => _metrics.RecordDocumentAccessAsync(documentId));
}

private async Task<Result<Document>> GetFromLocalCacheAsync(Guid documentId)
{
    var doc = _memoryCache.Get<Document>(documentId.ToString());
    return doc is not null
        ? Result<Document>.Success(doc)
        : Error.NotFoundError("Not in local cache");
}

private async Task<Result<Document>> GetFromCdnAsync(Guid documentId)
{
    try
    {
        var doc = await _cdnClient.GetAsync(documentId);
        return doc is not null
            ? Result<Document>.Success(doc)
            : Error.NotFoundError("Not in CDN");
    }
    catch (HttpRequestException ex)
    {
        return Error.UnexpectedError("CDN unavailable", ex);
    }
}

private async Task<Result<Document>> GetFromBlobStorageAsync(Guid documentId)
{
    try
    {
        var blob = await _blobClient.GetBlobAsync(documentId.ToString());
        var doc = await DeserializeDocumentAsync(blob);
        return Result<Document>.Success(doc);
    }
    catch (Azure.RequestFailedException ex) when (ex.Status == 404)
    {
        return Error.NotFoundError("Not in blob storage");
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError("Blob storage error", ex);
    }
}

private async Task<Result<Document>> GetFromArchiveAsync(Guid documentId)
{
    try
    {
        // Archive retrieval is slow but comprehensive
        var doc = await _archiveService.RetrieveAsync(documentId);
        return doc is not null
            ? Result<Document>.Success(doc)
            : Error.NotFoundError($"Document {documentId} not found anywhere");
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError("Archive retrieval failed", ex);
    }
}
```

### Example 2: User Authentication with Multiple Providers

```csharp
public async Task<Result<User>> AuthenticateAsync(string username, string password)
{
    return await AuthenticateWithDatabaseAsync(username, password)
        .OrElseAsync(() => AuthenticateWithLdapAsync(username, password))
        .OrElseAsync(() => AuthenticateWithActiveDirectoryAsync(username, password))
        .OrElseAsync(() => AuthenticateWithSamlAsync(username))
        .TapAsync(user => CreateSessionAsync(user))
        .TapAsync(user => LogSuccessfulLoginAsync(username))
        .TapError(error => LogFailedLoginAsync(username, error));
}

private async Task<Result<User>> AuthenticateWithDatabaseAsync(string username, string password)
{
    var user = await _database.Users.FirstOrDefaultAsync(u => u.Username == username);
    
    if (user is null)
        return Error.NotFoundError("User not in local database");
    
    if (!VerifyPassword(password, user.PasswordHash))
        return Error.PermissionError("Invalid password");
    
    return Result<User>.Success(user);
}

private async Task<Result<User>> AuthenticateWithLdapAsync(string username, string password)
{
    try
    {
        var ldapUser = await _ldapService.AuthenticateAsync(username, password);
        if (ldapUser is null)
            return Error.NotFoundError("User not in LDAP");
        
        // Sync user to local database
        var localUser = await SyncLdapUserAsync(ldapUser);
        return Result<User>.Success(localUser);
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError("LDAP authentication failed", ex);
    }
}

private async Task<Result<User>> AuthenticateWithActiveDirectoryAsync(string username, string password)
{
    try
    {
        var adUser = await _adService.AuthenticateAsync(username, password);
        if (adUser is null)
            return Error.NotFoundError("User not in Active Directory");
        
        var localUser = await SyncAdUserAsync(adUser);
        return Result<User>.Success(localUser);
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError("AD authentication failed", ex);
    }
}

private async Task<Result<User>> AuthenticateWithSamlAsync(string username)
{
    // SAML doesn't use password, just validates existing session
    try
    {
        var samlAssertion = await _samlService.ValidateSessionAsync(username);
        if (samlAssertion is null)
            return Error.NotFoundError("No valid SAML session");
        
        var user = await GetOrCreateSamlUserAsync(samlAssertion);
        return Result<User>.Success(user);
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError("SAML validation failed", ex);
    }
}
```

### Example 3: Geo-Distributed Data Retrieval

```csharp
public async Task<Result<CustomerData>> GetCustomerDataAsync(string customerId)
{
    var region = await _geoLocator.GetCurrentRegionAsync();
    
    return await GetFromRegionalDataCenterAsync(customerId, region)
        .OrElseAsync(() => GetFromNearestDataCenterAsync(customerId, region))
        .OrElseAsync(() => GetFromPrimaryDataCenterAsync(customerId))
        .OrElseAsync(() => GetFromBackupDataCenterAsync(customerId))
        .TapAsync(data => CacheRegionallyAsync(customerId, region, data))
        .TapError(error => _metrics.RecordDataRetrievalFailureAsync(customerId, error));
}

private async Task<Result<CustomerData>> GetFromRegionalDataCenterAsync(
    string customerId, 
    string region)
{
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // Fast timeout
        var data = await _regionalClients[region].GetCustomerAsync(customerId, cts.Token);
        
        return data is not null
            ? Result<CustomerData>.Success(data)
            : Error.NotFoundError($"Customer not in {region} datacenter");
    }
    catch (OperationCanceledException)
    {
        return Error.UnexpectedError($"Regional datacenter {region} timeout");
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError($"Regional datacenter {region} error", ex);
    }
}

private async Task<Result<CustomerData>> GetFromNearestDataCenterAsync(
    string customerId, 
    string currentRegion)
{
    var nearestRegion = GetNearestRegion(currentRegion);
    return await GetFromRegionalDataCenterAsync(customerId, nearestRegion);
}

private async Task<Result<CustomerData>> GetFromPrimaryDataCenterAsync(string customerId)
{
    try
    {
        var data = await _primaryDataCenter.GetCustomerAsync(customerId);
        return data is not null
            ? Result<CustomerData>.Success(data)
            : Error.NotFoundError("Customer not in primary datacenter");
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError("Primary datacenter error", ex);
    }
}

private async Task<Result<CustomerData>> GetFromBackupDataCenterAsync(string customerId)
{
    try
    {
        var data = await _backupDataCenter.GetCustomerAsync(customerId);
        return data is not null
            ? Result<CustomerData>.Success(data)
            : Error.NotFoundError($"Customer {customerId} not found in any datacenter");
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError("All datacenters unavailable", ex);
    }
}
```

## Best Practices

### ✅ DO

```csharp
// Use lazy evaluation (functions, not values)
var result = Primary()
    .OrElse(() => Secondary())  // ✅ Called only if Primary fails
    .OrElse(() => Tertiary());

// Order alternatives by preference (fastest/cheapest first)
var data = await GetFromCacheAsync()       // Fastest
    .OrElseAsync(() => GetFromDbAsync())   // Medium
    .OrElseAsync(() => GetFromApiAsync()); // Slowest

// Log each level of fallback
var result = await PrimarySourceAsync()
    .TapError(e => _logger.LogWarning($"Primary failed: {e.Message}"))
    .OrElseAsync(() => SecondarySourceAsync())
    .TapError(e => _logger.LogWarning($"Secondary failed: {e.Message}"))
    .OrElseAsync(() => FallbackSourceAsync());

// Provide meaningful final fallback or error
var config = LoadFromFile()
    .OrElse(() => LoadFromDefaults())
    .OrElse(Error.UnexpectedError("No configuration available"));
```

### ❌ DON'T

```csharp
// Don't execute alternatives eagerly
var primary = await PrimaryAsync();
var secondary = await SecondaryAsync();  // ❌ Always executes
var result = primary.OrElse(secondary);

// Use this instead:
var result = await PrimaryAsync()
    .OrElseAsync(() => SecondaryAsync());  // ✅ Only if needed

// Don't ignore errors in fallbacks
var result = await PrimaryAsync()
    .OrElseAsync(() => SecondaryAsync())  // ❌ Secondary errors are silent
    .OrElseAsync(() => TertiaryAsync());

// Add error logging:
var result = await PrimaryAsync()
    .TapError(e => _logger.LogError($"Primary: {e}"))
    .OrElseAsync(() => SecondaryAsync())
    .TapError(e => _logger.LogError($"Secondary: {e}"))  // ✅ Track failures
    .OrElseAsync(() => TertiaryAsync());

// Don't create infinite loops
var result = GetData()
    .OrElse(() => GetData());  // ❌ Infinite recursion if GetData always fails
```

## Error Handling

### All Alternatives Fail

```csharp
var result = await Primary()
    .OrElseAsync(() => Secondary())
    .OrElseAsync(() => Tertiary());

// If all fail, returns the error from the last alternative (Tertiary)
result.Match(
    onSuccess: value => ProcessValue(value),
    onFailure: error => LogError(error)  // Error from Tertiary
);
```

### Collecting All Errors

```csharp
var errors = new List<Error>();

var result = await Primary()
    .TapError(e => errors.Add(e))
    .OrElseAsync(() => Secondary())
    .TapError(e => errors.Add(e))
    .OrElseAsync(() => Tertiary())
    .TapError(e => errors.Add(e));

if (result.IsFailure)
{
    // Log all errors that occurred
    _logger.LogError($"All sources failed: {string.Join(", ", errors.Select(e => e.Message))}");
}
```

## Performance Considerations

### Circuit Breaker Pattern

```csharp
public async Task<Result<Data>> GetDataWithCircuitBreakerAsync(string key)
{
    if (_circuitBreaker.IsOpen("ExternalApi"))
    {
        // Skip calling external API if circuit is open
        return await GetFromCacheAsync(key)
            .OrElseAsync(() => GetDefaultDataAsync());
    }
    
    return await GetFromExternalApiAsync(key)
        .TapError(error => _circuitBreaker.RecordFailure("ExternalApi"))
        .OrElseAsync(() => GetFromCacheAsync(key))
        .OrElseAsync(() => GetDefaultDataAsync());
}
```

### Timeout per Alternative

```csharp
public async Task<Result<Product>> GetProductWithTimeoutsAsync(string id)
{
    using var fastTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
    using var mediumTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
    using var slowTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    
    return await GetFromCacheAsync(id, fastTimeout.Token)
        .OrElseAsync(() => GetFromDbAsync(id, mediumTimeout.Token))
        .OrElseAsync(() => GetFromApiAsync(id, slowTimeout.Token));
}
```

## See Also

- **[Async Operations](async-operations.md)** - Full async/await patterns
- **[Railway Oriented Programming](railway-oriented.md)** - Core Result chaining patterns
- **[Best Practices](best-practices.md)** - General guidelines
- **[Examples](examples.md)** - More real-world scenarios
