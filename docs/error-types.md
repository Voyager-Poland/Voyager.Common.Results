# Error Types

Voyager.Common.Results provides a comprehensive set of error types to represent different failure scenarios in your application.

## Available Error Types

### ValidationError

Used for input validation failures.

```csharp
// Simple validation error
Error.ValidationError("Email is required")

// With error code for localization
Error.ValidationError("User.Email.Required", "Email is required")

// Common use cases
Error.ValidationError("Password must be at least 8 characters")
Error.ValidationError("Invalid email format")
Error.ValidationError("Age must be between 18 and 120")
```

**When to use:**
- Form validation
- Input parameter validation
- Data format validation
- Business rule validation

### NotFoundError

Used when a requested resource doesn't exist.

```csharp
Error.NotFoundError($"User {userId} not found")
Error.NotFoundError("Product.NotFound", $"Product {id} not found")

// Common use cases
Error.NotFoundError("Order not found")
Error.NotFoundError("Customer with email 'test@example.com' not found")
```

**When to use:**
- Database queries returning null
- File not found
- API endpoint returning 404
- Entity lookup failures

### PermissionError

Used when the user lacks necessary permissions (authorized but forbidden).

```csharp
Error.PermissionError("You don't have permission to delete this resource")
Error.PermissionError("Access.Denied", "Access denied to admin panel")

// Common use cases
Error.PermissionError("Only administrators can perform this action")
Error.PermissionError("You can only edit your own profile")
```

**When to use:**
- Authorization failures (user authenticated but lacks permission)
- Role-based access control
- Resource ownership checks
- Feature flags disabled for user

**HTTP Status Code:** 403 Forbidden

### UnauthorizedError

Used when the user is not authenticated (not logged in or session expired).

```csharp
Error.UnauthorizedError("User not logged in")
Error.UnauthorizedError("Auth.TokenExpired", "Authentication token has expired")

// Common use cases
Error.UnauthorizedError("Please log in to access this resource")
Error.UnauthorizedError("Session expired. Please log in again")
Error.UnauthorizedError("Auth.InvalidToken", "Invalid authentication token")
Error.UnauthorizedError("Auth.MissingCredentials", "No authentication credentials provided")
```

**When to use:**
- Missing authentication credentials
- Expired authentication tokens
- Invalid authentication tokens
- Session timeout
- Login required for protected resources

**HTTP Status Code:** 401 Unauthorized

**Common distinction:**
- `UnauthorizedError` (401) = "Who are you?" (not logged in)
- `PermissionError` (403) = "I know who you are, but you can't do that" (insufficient permissions)

### ConflictError

Used for conflicts, typically duplicate entries.

```csharp
Error.ConflictError("Email already registered")
Error.ConflictError("User.Email.Duplicate", "This email is already in use")

// Common use cases
Error.ConflictError("Username already taken")
Error.ConflictError("Product code already exists")
Error.ConflictError("Cannot delete: record is referenced by other entities")
```

**When to use:**
- Unique constraint violations
- Concurrent modification conflicts
- Duplicate resource creation attempts
- State conflicts (e.g., can't delete active record)

### UnavailableError

Used when a service or resource is temporarily unavailable.

```csharp
Error.UnavailableError("External API is temporarily unavailable")
Error.UnavailableError("RateLimit.Exceeded", "Too many requests. Try again in 60 seconds")

// Common use cases
Error.UnavailableError("Service is under maintenance")
Error.UnavailableError("Payment gateway is currently down")
Error.UnavailableError("Cache.Unavailable", "Redis cache is not responding")
Error.UnavailableError("API rate limit exceeded - retry after 2024-01-15 10:30:00")
```

**When to use:**
- Rate limiting (429 Too Many Requests)
- Service maintenance mode
- Circuit breaker open state
- Temporary service outages
- Resource exhaustion (connection pool full)
- Third-party API unavailability

**HTTP Status Code:** 503 Service Unavailable or 429 Too Many Requests

### TimeoutError

Used when an operation exceeds its time limit.

```csharp
Error.TimeoutError("Database query exceeded 30 seconds")
Error.TimeoutError("Http.Timeout", "API request timed out after 60 seconds")

// Common use cases
Error.TimeoutError("External API request timeout")
Error.TimeoutError("Database.Timeout", "Query execution timeout")
Error.TimeoutError("Gateway.Timeout", "Upstream service did not respond in time")
Error.TimeoutError("Lock acquisition timeout - resource is busy")
```

**When to use:**
- HTTP request timeouts
- Database query timeouts
- Lock acquisition timeouts
- Message queue timeouts
- Long-running operation timeouts
- Gateway timeouts (504)

**HTTP Status Code:** 408 Request Timeout or 504 Gateway Timeout

### CancelledError

Used when an operation was cancelled via CancellationToken.

```csharp
Error.CancelledError("Operation was cancelled by user")
Error.CancelledError("Request.Cancelled", "The request was cancelled")

// Common use cases
Error.CancelledError("Download cancelled")
Error.CancelledError("Search.Cancelled", "Search operation was cancelled")
Error.CancelledError("Upload cancelled by user")
```

**When to use:**
- CancellationToken triggered cancellation
- User-initiated cancellation
- Timeout-based cancellation
- Background task cancellation

**Note:** This error type is automatically returned by `TryAsync` methods when `OperationCanceledException` is caught.

```csharp
// TryAsync automatically handles cancellation
var result = await Result<string>.TryAsync(
    async ct => await httpClient.GetStringAsync(url, ct),
    cancellationToken);

// If cancelled, result.Error.Type == ErrorType.Cancelled
```

### DatabaseError

Used for database-related failures.

```csharp
Error.DatabaseError("Failed to save changes to database")
Error.DatabaseError("Database.ConnectionFailed", "Cannot connect to database")

// Common use cases with exceptions
try
{
    _database.SaveChanges();
}
catch (DbUpdateException ex)
{
    return Error.DatabaseError("Failed to update database", ex);
}
catch (SqlException ex)
{
    return Error.DatabaseError("Database connection error", ex);
}
```

**When to use:**
- Connection failures
- Query execution errors
- Transaction failures
- Deadlock situations

### BusinessError

Used for business logic violations.

```csharp
Error.BusinessError("Cannot cancel an already shipped order")
Error.BusinessError("Order.AlreadyShipped", "This order has already been shipped")

// Common use cases
Error.BusinessError("Insufficient account balance")
Error.BusinessError("Cannot process refund for unpaid invoice")
Error.BusinessError("Appointment slot is no longer available")
Error.BusinessError("Cannot delete category with existing products")
```

**When to use:**
- Domain rule violations
- State machine violations
- Workflow constraints
- Business process errors

### UnexpectedError

Used for unexpected technical errors.

```csharp
Error.UnexpectedError("An unexpected error occurred")
Error.UnexpectedError("System.Unexpected", "Unexpected system error")

// Common use cases with exceptions
try
{
    // operation
}
catch (Exception ex)
{
    return Error.UnexpectedError("Unexpected error during processing", ex);
}
```

**When to use:**
- Unhandled exceptions
- System failures
- Third-party service errors
- Unknown error conditions

### FromException

Converts an exception to an error.

```csharp
try
{
    // risky operation
}
catch (TimeoutException ex)
{
    return Error.TimeoutError("Operation timed out", ex);
}
catch (HttpRequestException ex)
{
    return Error.UnavailableError("Service unavailable", ex);
}
catch (Exception ex)
{
    return Error.FromException(ex);
}

// Preserves exception type in error message
// TimeoutException → "TimeoutException: Operation timed out"
```

**When to use:**
- Converting exceptions to Results
- Logging and error tracking
- Preserving exception details

## Error Properties

Every error has these properties:

```csharp
public class Error
{
    public ErrorType Type { get; }      // Category of error
    public string Code { get; }         // Optional error code
    public string Message { get; }      // Human-readable message
}
```

### Error Type Enum

```csharp
public enum ErrorType
{
    None,          // No error (success state)
    Validation,    // Input validation errors
    Permission,    // Authorization failures
    Database,      // Database errors
    Business,      // Business logic violations
    NotFound,      // Resource not found
    Conflict,      // Duplicate or conflict
    Unavailable,   // Temporary unavailability
    Timeout,       // Operation timeout
    Unexpected     // Unexpected errors
}
```

## Using Error Codes

Error codes are useful for:
- **Localization** - Map codes to translated messages
- **Client handling** - Specific error handling in UI
- **Logging** - Track specific error types

```csharp
// Define error codes as constants
public static class ErrorCodes
{
    public const string EmailRequired = "User.Email.Required";
    public const string EmailInvalid = "User.Email.Invalid";
    public const string EmailDuplicate = "User.Email.Duplicate";
    public const string PasswordTooShort = "User.Password.TooShort";
    
    // Timeout codes
    public const string DatabaseTimeout = "Database.Timeout";
    public const string ApiTimeout = "Api.Timeout";
    public const string GatewayTimeout = "Gateway.Timeout";
    
    // Unavailable codes
    public const string RateLimitExceeded = "RateLimit.Exceeded";
    public const string ServiceMaintenance = "Service.Maintenance";
    public const string CircuitBreakerOpen = "Circuit.BreakerOpen";
}

// Use in code
public async Task<Result<User>> CreateUserAsync(string email)
{
    if (string.IsNullOrEmpty(email))
        return Error.ValidationError(ErrorCodes.EmailRequired, "Email is required");
    
    if (!IsValidEmail(email))
        return Error.ValidationError(ErrorCodes.EmailInvalid, "Invalid email format");
    
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var user = await _database.Users.AddAsync(new User { Email = email }, cts.Token);
        await _database.SaveChangesAsync(cts.Token);
        return user;
    }
    catch (OperationCanceledException)
    {
        return Error.TimeoutError(ErrorCodes.DatabaseTimeout, "Database operation timed out");
    }
    catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 2601)
    {
        return Error.ConflictError(ErrorCodes.EmailDuplicate, "Email already exists");
    }
}

// Handle in UI or API
var result = await CreateUserAsync(email);
if (result.IsFailure)
{
    var localizedMessage = _localizer[result.Error.Code] ?? result.Error.Message;
    return BadRequest(new { code = result.Error.Code, message = localizedMessage });
}
```

## Error Patterns

### Pattern 1: Validation with Multiple Errors

```csharp
public Result<User> ValidateUser(User user)
{
    if (string.IsNullOrEmpty(user.Email))
        return Error.ValidationError("Email is required");
    
    if (!IsValidEmail(user.Email))
        return Error.ValidationError("Invalid email format");
    
    if (string.IsNullOrEmpty(user.Password))
        return Error.ValidationError("Password is required");
    
    if (user.Password.Length < 8)
        return Error.ValidationError("Password must be at least 8 characters");
    
    return Result<User>.Success(user);
}
```

### Pattern 2: Convert Exception to Appropriate Error Type

```csharp
public async Task<Result<Order>> ProcessPaymentAsync(Order order)
{
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _paymentGateway.ChargeAsync(order.Amount, cts.Token);
        return order;
    }
    catch (OperationCanceledException)
    {
        // Timeout → Timeout error
        return Error.TimeoutError("Payment.Timeout", "Payment processing timed out");
    }
    catch (PaymentDeclinedException ex)
    {
        // Expected failure → Business error
        return Error.BusinessError("Payment was declined", ex);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
    {
        // Service down → Unavailable error
        return Error.UnavailableError("Payment.ServiceUnavailable", "Payment service is temporarily unavailable");
    }
    catch (HttpRequestException ex) when (ex.StatusCode == (System.Net.HttpStatusCode)429)
    {
        // Rate limit → Unavailable error
        return Error.UnavailableError("Payment.RateLimited", "Too many payment requests");
    }
    catch (TimeoutException ex)
    {
        // Infrastructure timeout → Timeout error
        return Error.TimeoutError("Payment service timeout", ex);
    }
    catch (Exception ex)
    {
        // Unknown → Unexpected error
        return Error.UnexpectedError("Payment processing failed", ex);
    }
}
```

### Pattern 3: Retry Logic with Unavailable/Timeout Errors

```csharp
public async Task<Result<T>> ExecuteWithRetryAsync<T>(
    Func<Task<Result<T>>> operation,
    int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        var result = await operation();
        
        // Success or non-retryable error
        if (result.IsSuccess || 
            (result.Error.Type != ErrorType.Unavailable && 
             result.Error.Type != ErrorType.Timeout))
        {
            return result;
        }
        
        // Last attempt
        if (attempt == maxRetries)
            return result;
        
        // Wait before retry (exponential backoff)
        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
    }
    
    return Error.UnexpectedError("Max retry attempts exceeded");
}

// Usage
var result = await ExecuteWithRetryAsync(
    () => _externalService.GetDataAsync(id)
);
```

### Pattern 4: Contextual Error Messages

```csharp
public Result<User> GetUser(int userId)
{
    if (userId <= 0)
        return Error.ValidationError($"Invalid user ID: {userId}");
    
    var user = _database.Users.Find(userId);
    
    if (user is null)
        return Error.NotFoundError($"User with ID {userId} not found");
    
    if (!user.IsActive)
        return Error.BusinessError($"User {userId} is inactive");
    
    return user;
}
```

### Pattern 5: Circuit Breaker with Unavailable Error

```csharp
public class CircuitBreakerService
{
    private bool _isOpen = false;
    private DateTime _openedAt;
    private readonly TimeSpan _timeout = TimeSpan.FromMinutes(1);
    
    public async Task<Result<T>> ExecuteAsync<T>(Func<Task<Result<T>>> operation)
    {
        // Check if circuit breaker is open
        if (_isOpen)
        {
            if (DateTime.UtcNow - _openedAt < _timeout)
            {
                return Error.UnavailableError(
                    "Circuit.BreakerOpen",
                    $"Service is temporarily unavailable. Retry after {_openedAt.Add(_timeout):HH:mm:ss}"
                );
            }
            
            _isOpen = false; // Try half-open
        }
        
        var result = await operation();
        
        // Open circuit on specific errors
        if (result.IsFailure && 
            (result.Error.Type == ErrorType.Timeout || 
             result.Error.Type == ErrorType.Unavailable))
        {
            _isOpen = true;
            _openedAt = DateTime.UtcNow;
        }
        
        return result;
    }
}
```

## Mapping Errors to HTTP Status Codes

```csharp
public IActionResult ToHttpResult<T>(Result<T> result)
{
    return result.Match<IActionResult>(
        onSuccess: value => Ok(value),
        onFailure: error => error.Type switch
        {
            ErrorType.Validation => BadRequest(new { error.Message, error.Code }),
            ErrorType.NotFound => NotFound(new { error.Message, error.Code }),
            ErrorType.Permission => Forbid(),
            ErrorType.Conflict => Conflict(new { error.Message, error.Code }),
            ErrorType.Unavailable => error.Code?.Contains("RateLimit") == true
                ? StatusCode(429, new { error.Message, error.Code, RetryAfter = "60" })
                : StatusCode(503, new { error.Message, error.Code }),
            ErrorType.Timeout => error.Code?.Contains("Gateway") == true
                ? StatusCode(504, new { error.Message, error.Code })
                : StatusCode(408, new { error.Message, error.Code }),
            ErrorType.Database => StatusCode(503, new { error.Message }),
            ErrorType.Business => BadRequest(new { error.Message, error.Code }),
            ErrorType.Unexpected => StatusCode(500, new { error.Message }),
            _ => StatusCode(500, new { error.Message })
        }
    );
}

// Usage
[HttpGet("{id}")]
public async Task<IActionResult> GetUserAsync(int id)
{
    var result = await _userService.GetUserAsync(id);
    return ToHttpResult(result);
}
```

## Best Practices

### ✅ DO

```csharp
// Use specific error types
Error.ValidationError("Email is required")          // ✅ Clear intent
Error.NotFoundError($"User {id} not found")        // ✅ Specific resource
Error.BusinessError("Insufficient balance")         // ✅ Business context
Error.TimeoutError("API request timed out after 30s") // ✅ Timeout context
Error.UnavailableError("Service under maintenance") // ✅ Temporary issue

// Include context in messages
Error.NotFoundError($"Order {orderId} not found")   // ✅ Includes ID
Error.ValidationError($"Price must be > 0, got {price}") // ✅ Includes actual value
Error.TimeoutError($"Query timeout after {seconds}s") // ✅ Includes duration

// Use appropriate error for each scenario
catch (OperationCanceledException) => TimeoutError   // ✅ Correct type
catch (HttpRequestException { StatusCode: 503 }) => UnavailableError // ✅ Correct type

// Preserve exceptions when converting
Error.DatabaseError("Connection failed", ex)        // ✅ Keeps exception
Error.TimeoutError("Operation timed out", ex)       // ✅ Keeps exception
```

### ❌ DON'T

```csharp
// Don't use generic errors for specific cases
Error.UnexpectedError("Not found")                  // ❌ Use NotFoundError
Error.ValidationError("Access denied")              // ❌ Use PermissionError
Error.UnexpectedError("Timeout")                    // ❌ Use TimeoutError
Error.DatabaseError("Service unavailable")          // ❌ Use UnavailableError

// Don't lose context
Error.NotFoundError("Not found")                    // ❌ What wasn't found?
Error.ValidationError("Invalid")                    // ❌ What's invalid?
Error.TimeoutError("Timeout")                       // ❌ What timed out?
Error.UnavailableError("Unavailable")               // ❌ What's unavailable?

// Don't confuse Timeout and Unavailable
Error.UnavailableError("Query timeout")             // ❌ Use TimeoutError
Error.TimeoutError("Service down")                  // ❌ Use UnavailableError

// Don't discard exceptions
catch (Exception ex) {
    return Error.DatabaseError("Error");            // ❌ Lost exception info
}
```

## Error Type Decision Tree

```
Is the error expected in normal business flow?
├─ Yes → Is it related to...
│  ├─ Input validation? → ValidationError
│  ├─ Missing resource? → NotFoundError
│  ├─ Authorization? → PermissionError
│  ├─ Duplicate/conflict? → ConflictError
│  ├─ Business rule? → BusinessError
│  └─ Other? → Consider specific type
│
└─ No (Infrastructure/Technical) → What happened?
   ├─ Time limit exceeded? → TimeoutError
   ├─ Service/resource temporarily down? → UnavailableError
   ├─ Database issue? → DatabaseError
   └─ Unknown/unexpected? → UnexpectedError
```

## Common Exception to Error Mappings

| Exception Type | Recommended Error Type | Example |
|---------------|----------------------|---------|
| `ArgumentException` | `ValidationError` | Invalid input parameters |
| `ArgumentNullException` | `ValidationError` | Required parameter missing |
| `InvalidOperationException` | `BusinessError` | Operation not allowed in current state |
| `UnauthorizedAccessException` | `PermissionError` | Access denied |
| `FileNotFoundException` | `NotFoundError` | File not found |
| `DbUpdateException` (duplicate key) | `ConflictError` | Unique constraint violation |
| `DbUpdateException` (other) | `DatabaseError` | Database update failed |
| `SqlException` | `DatabaseError` | SQL error |
| `TimeoutException` | `TimeoutError` | Operation timed out |
| `OperationCanceledException` | `TimeoutError` | Cancellation due to timeout |
| `HttpRequestException` (503) | `UnavailableError` | Service unavailable |
| `HttpRequestException` (429) | `UnavailableError` | Rate limit exceeded |
| `HttpRequestException` (408) | `TimeoutError` | Request timeout |
| `HttpRequestException` (504) | `TimeoutError` | Gateway timeout |
| `Exception` (unknown) | `UnexpectedError` | Catch-all for unexpected errors |

## See Also

- **[Getting Started](getting-started.md)** - Basic usage examples
- **[Best Practices](best-practices.md)** - When to use each error type
- **[Async Operations](async-operations.md)** - Handling async errors
