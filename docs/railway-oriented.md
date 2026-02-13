# Railway Oriented Programming

Railway Oriented Programming (ROP) is a functional programming pattern where operations flow on two parallel tracks: **success** and **failure**. Once an operation fails, subsequent operations are automatically skipped.

## The Railway Metaphor

Think of your code as a train journey:

```
Success Track:  [GetUser] ─→ [Validate] ─→ [Save] ─→ [Success]
                    │            │           │
                    ↓            ↓           ↓
Failure Track:  [Error] ─────→ [Error] ──→ [Error]
```

Once the train switches to the failure track (an error occurs), it stays on that track and skips all remaining operations.

## Core Railway Operators

### Map - Transform Success Values

`Map` transforms the value inside a successful Result without changing the Result wrapper.

**Signature:**
```csharp
Result<TOut> Map<TOut>(Func<T, TOut> mapper)
```

**Example:**
```csharp
Result<int> GetAge() => Result<int>.Success(25);

var result = GetAge()
    .Map(age => age + 10)           // Result<int> with value 35
    .Map(age => $"Age: {age}");     // Result<string> with value "Age: 35"

// If GetAge() failed, Map operations would be skipped
```

**Real-world example:**
```csharp
var email = GetUser(userId)
    .Map(user => user.Email)
    .Map(email => email.ToLower());

// email is Result<string>
```

**When to use:**
- Transform a success value to another type
- Extract properties from objects
- Format or normalize data

### Bind - Chain Operations Returning Result

`Bind` chains operations that themselves return a Result. It "flattens" `Result<Result<T>>` into `Result<T>`.

**Signature:**
```csharp
Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder)
```

**Example:**
```csharp
Result<User> GetUser(int id) { /* ... */ }
Result<Order> GetLatestOrder(User user) { /* ... */ }
Result<decimal> CalculateTotal(Order order) { /* ... */ }

var total = GetUser(123)
    .Bind(user => GetLatestOrder(user))    // Chain Result-returning method
    .Bind(order => CalculateTotal(order)); // Another Result-returning method

// total is Result<decimal>
// If any step fails, subsequent steps are skipped
```

**Real-world example:**
```csharp
public Result<OrderConfirmation> PlaceOrder(int userId, List<CartItem> items)
{
    return GetUser(userId)
        .Bind(user => ValidateUser(user))
        .Bind(user => CreateOrder(user, items))
        .Bind(order => ProcessPayment(order))
        .Bind(order => SendConfirmation(order));
}

// Clean, linear flow - no nested if statements!
```

**When to use:**
- Chain multiple operations that can fail
- Avoid nested Result checking
- Build data pipelines

### Tap - Execute Side Effects

`Tap` executes an action on a successful Result without changing it. Useful for logging, notifications, etc.

**Signature:**
```csharp
Result<T> Tap(Action<T> action)
Result TapError(Action<Error> action)
```

**Example:**
```csharp
var result = GetUser(123)
    .Tap(user => _logger.LogInformation($"Found user: {user.Name}"))
    .TapError(error => _logger.LogError($"User not found: {error.Message}"))
    .Map(user => user.Email);

// Tap doesn't change the result, just performs side effects
```

**Real-world example:**
```csharp
public Result<Order> ProcessOrder(Order order)
{
    return ValidateOrder(order)
        .Tap(o => _logger.LogInformation($"Order {o.Id} validated"))
        .Bind(o => SaveOrder(o))
        .Tap(o => _eventBus.Publish(new OrderCreatedEvent(o)))
        .Bind(o => SendConfirmationEmail(o))
        .Tap(o => _cache.Invalidate($"user-orders-{o.UserId}"));
}
```

**When to use:**
- Logging
- Sending events
- Updating caches
- Any side effect that shouldn't affect the result

### Ensure - Conditional Validation

`Ensure` validates a condition on a successful Result. If the condition fails, it returns an error.

**Signatures:**
```csharp
// Static error
Result<T> Ensure(Func<T, bool> predicate, Error error)

// Contextual error - can use value in error message
Result<T> Ensure(Func<T, bool> predicate, Func<T, Error> errorFactory)
```

**Example with static error:**
```csharp
var result = GetAge()
    .Ensure(
        age => age >= 18,
        Error.ValidationError("Must be 18 or older")
    )
    .Ensure(
        age => age <= 120,
        Error.ValidationError("Invalid age")
    );
```

**Example with contextual error (recommended for better messages):**
```csharp
var result = GetUser(id)
    .Ensure(
        user => user.Age >= 18,
        user => Error.ValidationError($"User {user.Name} is {user.Age} years old, must be 18+"))
    .Ensure(
        user => user.IsActive,
        user => Error.BusinessError($"User {user.Name} (ID: {user.Id}) is inactive"));
```

**Real-world example:**
```csharp
public Result<User> ActivateUser(int userId)
{
    return GetUser(userId)
        .Ensure(
            user => !user.IsActive,
            Error.BusinessError("User is already active")
        )
        .Ensure(
            user => user.EmailVerified,
            Error.ValidationError("Email must be verified first")
        )
        .Bind(user => SetActive(user));
}
```

**When to use:**
- Validate conditions on success values
- Guard clauses in functional style
- Business rule validation

### Match / Switch - Handle Result

`Match` and `Switch` are the final step to extract the result or execute logic.

**Match** - Returns a value:
```csharp
TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
```

**Switch** - Executes actions (void):
```csharp
void Switch(Action<T> onSuccess, Action<Error> onFailure)
```

**Example:**
```csharp
// Match - transform to another type
var message = GetUser(123).Match(
    onSuccess: user => $"Hello, {user.Name}",
    onFailure: error => $"Error: {error.Message}"
);

// Switch - execute side effects
GetUser(123).Switch(
    onSuccess: user => Console.WriteLine($"User: {user.Name}"),
    onFailure: error => Console.WriteLine($"Error: {error.Message}")
);
```

**ASP.NET Core example:**
```csharp
[HttpGet("{id}")]
public IActionResult GetUser(int id)
{
    var result = _userService.GetUser(id);
    
    return result.Match<IActionResult>(
        onSuccess: user => Ok(user),
        onFailure: error => error.Type switch
        {
            ErrorType.NotFound => NotFound(error.Message),
            ErrorType.Validation => BadRequest(error.Message),
            _ => StatusCode(500, error.Message)
        }
    );
}
```

## Combining Operators

The power of ROP comes from combining operators into expressive pipelines:

### Example 1: User Registration

```csharp
public Result<User> RegisterUser(string email, string password)
{
    return ValidateEmail(email)
        .Bind(e => ValidatePassword(password))
        .Ensure(
            _ => !_database.Users.Any(u => u.Email == email),
            Error.ConflictError("Email already registered")
        )
        .Map(_ => new User { Email = email, Password = HashPassword(password) })
        .Bind(user => SaveUser(user))
        .Tap(user => _logger.LogInformation($"User {user.Id} registered"))
        .Tap(user => _emailService.SendWelcomeEmail(user));
}

Result<string> ValidateEmail(string email)
{
    if (string.IsNullOrEmpty(email))
        return Error.ValidationError("Email is required");
    
    if (!email.Contains("@"))
        return Error.ValidationError("Invalid email format");
    
    return email;
}

Result<string> ValidatePassword(string password)
{
    if (string.IsNullOrEmpty(password))
        return Error.ValidationError("Password is required");
    
    if (password.Length < 8)
        return Error.ValidationError("Password must be at least 8 characters");
    
    return password;
}
```

### Example 2: Order Processing

```csharp
public Result<OrderConfirmation> ProcessOrder(int userId, CreateOrderDto dto)
{
    return GetUser(userId)
        .Ensure(
            user => user.IsActive,
            Error.BusinessError("User account is not active")
        )
        .Bind(user => ValidateOrderItems(dto.Items))
        .Bind(items => CreateOrder(userId, items))
        .Ensure(
            order => order.TotalAmount > 0,
            Error.ValidationError("Order total must be greater than 0")
        )
        .Bind(order => ProcessPayment(order))
        .Tap(order => _logger.LogInformation($"Order {order.Id} paid"))
        .Bind(order => ReserveInventory(order))
        .Tap(order => _eventBus.Publish(new OrderPlacedEvent(order)))
        .Map(order => new OrderConfirmation 
        { 
            OrderId = order.Id, 
            ConfirmationNumber = GenerateConfirmationNumber() 
        })
        .Tap(conf => SendConfirmationEmail(userId, conf));
}
```

### Example 3: Data Pipeline

```csharp
public Result<ReportData> GenerateReport(int userId, DateTime startDate, DateTime endDate)
{
    return ValidateDateRange(startDate, endDate)
        .Bind(_ => GetUser(userId))
        .Ensure(
            user => user.HasPermission("reports:view"),
            Error.PermissionError("User doesn't have permission to view reports")
        )
        .Bind(user => FetchOrderData(user.Id, startDate, endDate))
        .Tap(data => _logger.LogInformation($"Fetched {data.Count} orders"))
        .Map(data => AggregateData(data))
        .Map(agg => CalculateStatistics(agg))
        .Tap(stats => _cache.Set($"report-{userId}-{startDate:yyyyMMdd}", stats))
        .Map(stats => new ReportData { Statistics = stats, GeneratedAt = DateTime.UtcNow });
}
```

## Railway Operators Comparison

| Operator | Input Type | Output Type | Purpose | Skipped on Failure? |
|----------|------------|-------------|---------|---------------------|
| `Map` | `Func<T, TOut>` | `Result<TOut>` | Transform value | Yes |
| `Bind` | `Func<T, Result<TOut>>` | `Result<TOut>` | Chain operations | Yes |
| `Tap` | `Action<T>` | `Result<T>` | Side effects | Yes |
| `TapError` | `Action<Error>` | `Result<T>` | Error side effects | No (only runs on failure) |
| `Ensure` | `Func<T, bool>` | `Result<T>` | Validate condition | Yes |
| `Match` | 2 functions | `TOut` | Extract value | No (handles both) |
| `Switch` | 2 actions | `void` | Execute actions | No (handles both) |

## Advanced Patterns

### Pattern 1: Early Return with Ensure

```csharp
public Result<Order> CancelOrder(int orderId)
{
    return GetOrder(orderId)
        .Ensure(
            order => order.Status == OrderStatus.Pending,
            Error.BusinessError("Only pending orders can be cancelled")
        )
        .Ensure(
            order => order.CreatedAt > DateTime.UtcNow.AddHours(-24),
            Error.BusinessError("Cannot cancel orders older than 24 hours")
        )
        .Bind(order => MarkAsCancelled(order))
        .Bind(order => RefundPayment(order))
        .Tap(order => NotifyCustomer(order));
}
```

### Pattern 2: Parallel Validation

```csharp
public Result<User> UpdateProfile(User user, UpdateProfileDto dto)
{
    var emailResult = ValidateEmail(dto.Email);
    var phoneResult = ValidatePhone(dto.Phone);
    var ageResult = ValidateAge(dto.Age);
    
    // If any validation fails, return first error
    if (emailResult.IsFailure) return emailResult.Error;
    if (phoneResult.IsFailure) return phoneResult.Error;
    if (ageResult.IsFailure) return ageResult.Error;
    
    // All validations passed
    return UpdateUser(user, dto);
}

// Or use collection extensions
var results = new[] { emailResult, phoneResult, ageResult };
var combined = results.Combine();  // Result<List<string>> or first error
```

### Pattern 3: Conditional Logic

```csharp
public Result<Discount> CalculateDiscount(User user, Order order)
{
    return GetUser(user.Id)
        .Bind(u => u.IsPremium 
            ? CalculatePremiumDiscount(order)
            : CalculateStandardDiscount(order)
        )
        .Ensure(
            discount => discount.Amount <= order.TotalAmount,
            Error.BusinessError("Discount cannot exceed order total")
        );
}
```

## Best Practices

### ✅ DO

```csharp
// Use railway operators for clear flow
var result = GetUser(id)
    .Bind(user => ValidateUser(user))
    .Bind(user => SaveUser(user))
    .Tap(user => SendEmail(user));

// Use Ensure for validations
result.Ensure(
    user => user.Age >= 18,
    Error.ValidationError("Must be 18+")
);

// Use Tap for side effects
result.Tap(user => _logger.Log($"Success: {user.Name}"));
```

### ❌ DON'T

```csharp
// Don't nest Result checking
var userResult = GetUser(id);
if (userResult.IsSuccess)
{
    var validateResult = ValidateUser(userResult.Value);
    if (validateResult.IsSuccess)
    {
        // ❌ Use Bind instead!
    }
}

// Don't use Map for operations that can fail
result.Map(user => SaveUser(user));  // ❌ SaveUser returns Result - use Bind!

// Don't ignore Tap errors
result.Tap(user =>
{
    throw new Exception();  // ❌ Tap exceptions are not caught!
});

// Don't ignore Result return values
GetUser(id);  // ⚠️ VCR0010: Result silently discarded - errors are lost!
```

> **Tip:** The built-in Roslyn analyzer **VCR0010** catches unconsumed Result values at compile time. Every `Result` must be assigned, returned, passed as argument, or used in a chain.

## See Also

- **[Async Operations](async-operations.md)** - Async versions of all operators
- **[Collection Operations](collection-operations.md)** - Working with multiple Results
- **[Best Practices](best-practices.md)** - Patterns and anti-patterns
- **[Examples](examples.md)** - Real-world usage examples
