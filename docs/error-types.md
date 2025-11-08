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

Used when the user lacks necessary permissions.

```csharp
Error.PermissionError("You don't have permission to delete this resource")
Error.PermissionError("Access.Denied", "Access denied to admin panel")

// Common use cases
Error.PermissionError("Only administrators can perform this action")
Error.PermissionError("You can only edit your own profile")
```

**When to use:**
- Authorization failures
- Role-based access control
- Resource ownership checks
- API key validation failures

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

### DatabaseError

Used for database-related failures.

```csharp
Error.DatabaseError("Failed to save changes to database")
Error.DatabaseError("Database.ConnectionFailed", "Cannot connect to database", exception)

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
Error.UnexpectedError("System.Unexpected", "Unexpected system error", exception)

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
    public Exception Exception { get; } // Optional underlying exception
}
```

### Error Type Enum

```csharp
public enum ErrorType
{
    Validation,    // Input validation errors
    NotFound,      // Resource not found
    Permission,    // Authorization failures
    Conflict,      // Duplicate or conflict
    Database,      // Database errors
    Business,      // Business logic violations
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
}

// Use in code
public Result<User> CreateUser(string email)
{
    if (string.IsNullOrEmpty(email))
        return Error.ValidationError(ErrorCodes.EmailRequired, "Email is required");
    
    if (!IsValidEmail(email))
        return Error.ValidationError(ErrorCodes.EmailInvalid, "Invalid email format");
    
    if (_database.Users.Any(u => u.Email == email))
        return Error.ConflictError(ErrorCodes.EmailDuplicate, "Email already exists");
    
    // ...
}

// Handle in UI or API
var result = CreateUser(email);
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
public Result<Order> ProcessPayment(Order order)
{
    try
    {
        _paymentGateway.Charge(order.Amount);
        return order;
    }
    catch (PaymentDeclinedException ex)
    {
        // Expected failure → Business error
        return Error.BusinessError("Payment was declined", ex);
    }
    catch (TimeoutException ex)
    {
        // Infrastructure issue → Unexpected error
        return Error.UnexpectedError("Payment service timeout", ex);
    }
    catch (Exception ex)
    {
        // Unknown → Unexpected error
        return Error.UnexpectedError("Payment processing failed", ex);
    }
}
```

### Pattern 3: Contextual Error Messages

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
            ErrorType.Database => StatusCode(503, new { error.Message }),
            ErrorType.Business => BadRequest(new { error.Message, error.Code }),
            ErrorType.Unexpected => StatusCode(500, new { error.Message }),
            _ => StatusCode(500, new { error.Message })
        }
    );
}

// Usage
[HttpGet("{id}")]
public IActionResult GetUser(int id)
{
    var result = _userService.GetUser(id);
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

// Include context in messages
Error.NotFoundError($"Order {orderId} not found")   // ✅ Includes ID
Error.ValidationError($"Price must be > 0, got {price}") // ✅ Includes actual value

// Preserve exceptions when converting
Error.DatabaseError("Connection failed", ex)        // ✅ Keeps exception
```

### ❌ DON'T

```csharp
// Don't use generic errors for specific cases
Error.UnexpectedError("Not found")                  // ❌ Use NotFoundError
Error.ValidationError("Access denied")              // ❌ Use PermissionError

// Don't lose context
Error.NotFoundError("Not found")                    // ❌ What wasn't found?
Error.ValidationError("Invalid")                    // ❌ What's invalid?

// Don't discard exceptions
catch (Exception ex) {
    return Error.DatabaseError("Error");            // ❌ Lost exception info
}
```

## See Also

- **[Getting Started](getting-started.md)** - Basic usage examples
- **[Railway Oriented Programming](railway-oriented.md)** - Error propagation
- **[Best Practices](best-practices.md)** - When to use each error type
