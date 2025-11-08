# Best Practices

This guide covers patterns to follow and anti-patterns to avoid when using Voyager.Common.Results.

## Result Pattern vs Exceptions

### The Golden Rule

**Use Result Pattern for expected failures (business logic), and try-catch for unexpected failures (technical errors).**

| Scenario | Use | Reasoning |
|----------|-----|-----------|
| **Expected** business failure | `Result.Failure` | Part of normal flow, should be handled |
| **Unexpected** technical error | `try-catch` → `Error.FromException()` | Infrastructure issue, convert to Result |
| **Programming bug** | `throw` (dev) / `try-catch` (prod) | Should be fixed, not handled |

### ✅ DO: Combine Result Pattern with try-catch

```csharp
public Result<User> GetUser(int id)
{
    // Expected business errors → Result
    if (id <= 0)
        return Error.ValidationError("ID must be positive");
    
    try
    {
        // Unexpected technical errors → try-catch
        var user = _repository.GetUser(id);
        
        if (user is null)
            return Error.NotFoundError($"User {id} not found");
        
        return user;
    }
    catch (DbException ex)
    {
        // Convert unexpected exception to Result
        return Error.DatabaseError("Database connection failed", ex);
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError("Unexpected error occurred", ex);
    }
}
```

### ❌ DON'T: Use throw for expected business failures

```csharp
public Result<User> GetUser(int id)
{
    if (id <= 0)
        throw new ArgumentException("Invalid ID");  // ❌ BAD
    
    // ✅ GOOD:
    // return Error.ValidationError("ID must be positive");
}
```

### ❌ DON'T: Ignore unexpected technical exceptions

```csharp
public Result<User> GetUser(int id)
{
    // ❌ BAD: No try-catch - DbException can escape
    var user = _repository.GetUser(id);
    
    if (user is null)
        return Error.NotFoundError("User not found");
    
    return user;
}
```

## Railway Operators

### ✅ DO: Chain operations instead of nesting

```csharp
// ✅ GOOD: Clean, linear flow
var result = GetUser(id)
    .Bind(user => ValidateUser(user))
    .Bind(user => SaveUser(user))
    .Tap(user => SendWelcomeEmail(user));
```

### ❌ DON'T: Nest Result checking

```csharp
// ❌ BAD: Nested pyramid of doom
var userResult = GetUser(id);
if (userResult.IsSuccess)
{
    var validateResult = ValidateUser(userResult.Value);
    if (validateResult.IsSuccess)
    {
        var saveResult = SaveUser(validateResult.Value);
        if (saveResult.IsSuccess)
        {
            SendWelcomeEmail(saveResult.Value);
        }
    }
}
```

### ✅ DO: Use appropriate operator for the job

```csharp
// Map for transformations
.Map(user => user.Email)

// Bind for operations returning Result
.Bind(user => SaveUser(user))

// Tap for side effects
.Tap(user => _logger.Log($"User: {user.Name}"))

// Ensure for validation
.Ensure(user => user.Age >= 18, Error.ValidationError("Must be 18+"))
```

### ❌ DON'T: Use Map for operations that return Result

```csharp
// ❌ BAD: Returns Result<Result<User>>
result.Map(user => SaveUser(user))

// ✅ GOOD: Returns Result<User>
result.Bind(user => SaveUser(user))
```

## Error Handling

### ✅ DO: Return specific error types

```csharp
// ✅ GOOD: Clear intent
Error.ValidationError("Email is required")
Error.NotFoundError($"User {id} not found")
Error.PermissionError("Access denied")
Error.BusinessError("Insufficient balance")
```

### ❌ DON'T: Use generic errors for specific cases

```csharp
// ❌ BAD: Unclear intent
Error.UnexpectedError("Not found")  // Use NotFoundError
Error.ValidationError("Access denied")  // Use PermissionError
```

### ✅ DO: Include context in error messages

```csharp
// ✅ GOOD: Descriptive with context
Error.NotFoundError($"Order {orderId} not found")
Error.ValidationError($"Price must be > 0, got {price}")
Error.BusinessError($"Cannot refund order {orderId}: already shipped")
```

### ❌ DON'T: Lose error context

```csharp
// ❌ BAD: No context
Error.NotFoundError("Not found")  // What wasn't found?
Error.ValidationError("Invalid")  // What's invalid?
```

### ✅ DO: Preserve exceptions when converting to Result

```csharp
try
{
    // operation
}
catch (DbException ex)
{
    // ✅ GOOD: Preserves exception
    return Error.DatabaseError("Database error", ex);
}
```

### ❌ DON'T: Discard exception information

```csharp
catch (Exception ex)
{
    // ❌ BAD: Lost exception info
    return Error.DatabaseError("Error");
}
```

## Return Values

### ✅ DO: Use implicit conversions

```csharp
// ✅ GOOD: Concise
public Result<int> GetAge() => 42;

public Result<User> GetUser(int id)
{
    if (id <= 0)
        return Error.ValidationError("Invalid ID");
    
    return user;
}
```

### ❌ DON'T: Use explicit Success() unnecessarily

```csharp
// ❌ Verbose
public Result<int> GetAge() => Result<int>.Success(42);

// ✅ GOOD
public Result<int> GetAge() => 42;
```

### ❌ DON'T: Return null in a Result

```csharp
public Result<User> FindUser(string email)
{
    var user = _repository.FindByEmail(email);
    return user;  // ❌ BAD: If user is null, returns Result.Success(null)!
    
    // ✅ GOOD:
    // if (user is null)
    //     return Error.NotFoundError("User not found");
    // return user;
}
```

## Async Operations

### ✅ DO: Use async operators for async operations

```csharp
// ✅ GOOD: Proper async chain
var result = await GetUserAsync(id)
    .BindAsync(user => SaveUserAsync(user))
    .TapAsync(user => SendEmailAsync(user));
```

### ❌ DON'T: Mix sync and async incorrectly

```csharp
// ❌ BAD: Returns Result<Task<Result<User>>>
result.Map(user => SaveUserAsync(user))

// ✅ GOOD
result.BindAsync(user => SaveUserAsync(user))
```

### ✅ DO: Use ConfigureAwait in libraries

```csharp
public async Task<Result<User>> GetUserAsync(int id)
{
    var user = await _database.Users
        .FindAsync(id)
        .ConfigureAwait(false);  // ✅ GOOD for libraries
    
    // ...
}
```

### ❌ DON'T: Make methods async unnecessarily

```csharp
// ❌ BAD: No actual async work
public async Task<Result<int>> CalculateAsync(int x, int y)
{
    return await Task.FromResult(x + y);
}

// ✅ GOOD: Just make it sync
public Result<int> Calculate(int x, int y)
{
    return x + y;
}
```

## Collection Operations

### ✅ DO: Use Combine when all must succeed

```csharp
// ✅ GOOD: All or nothing
var results = items.Select(item => Validate(item));
var allValid = results.Combine();

if (allValid.IsFailure)
    return allValid.Error;  // First error
```

### ✅ DO: Use Partition for partial success

```csharp
// ✅ GOOD: Process successes, log failures
var (successes, failures) = results.Partition();

foreach (var item in successes)
    Process(item);

foreach (var error in failures)
    _logger.LogError(error.Message);
```

### ❌ DON'T: Manually iterate when Combine/Partition exists

```csharp
// ❌ BAD: Manual iteration
var successes = new List<User>();
var failures = new List<Error>();
foreach (var result in results)
{
    if (result.IsSuccess)
        successes.Add(result.Value);
    else
        failures.Add(result.Error);
}

// ✅ GOOD
var (successes, failures) = results.Partition();
```

## ASP.NET Core Integration

### ✅ DO: Map errors to appropriate HTTP status codes

```csharp
[HttpGet("{id}")]
public IActionResult GetUser(int id)
{
    var result = _userService.GetUser(id);
    
    return result.Match<IActionResult>(
        onSuccess: user => Ok(user),
        onFailure: error => error.Type switch
        {
            ErrorType.NotFound => NotFound(new { error.Message }),
            ErrorType.Validation => BadRequest(new { error.Message }),
            ErrorType.Permission => Forbid(),
            ErrorType.Conflict => Conflict(new { error.Message }),
            ErrorType.Database => StatusCode(503, new { error.Message }),
            ErrorType.Business => BadRequest(new { error.Message }),
            _ => StatusCode(500, new { error.Message })
        }
    );
}
```

### ✅ DO: Include error codes for client handling

```csharp
[HttpPost]
public IActionResult CreateUser([FromBody] CreateUserDto dto)
{
    var result = _userService.CreateUser(dto);
    
    return result.Match<IActionResult>(
        onSuccess: user => CreatedAtAction(nameof(GetUser), new { id = user.Id }, user),
        onFailure: error => BadRequest(new 
        { 
            code = error.Code,  // Client can handle specific errors
            message = error.Message 
        })
    );
}
```

### ❌ DON'T: Let Results escape to client

```csharp
// ❌ BAD: Exposes Result structure to API
[HttpGet("{id}")]
public Result<User> GetUser(int id)
{
    return _userService.GetUser(id);
}

// ✅ GOOD: Convert to IActionResult
[HttpGet("{id}")]
public IActionResult GetUser(int id)
{
    var result = _userService.GetUser(id);
    return result.Match<IActionResult>(
        onSuccess: user => Ok(user),
        onFailure: error => NotFound(error.Message)
    );
}
```

## Validation Patterns

### ✅ DO: Validate early in the pipeline

```csharp
public Result<Order> CreateOrder(CreateOrderDto dto)
{
    return ValidateDto(dto)  // Validate first
        .Bind(dto => CreateOrderFromDto(dto))
        .Bind(order => SaveOrder(order));
}
```

### ✅ DO: Use Ensure for runtime validations

```csharp
var result = GetUser(id)
    .Ensure(
        user => user.IsActive,
        Error.BusinessError("User is not active")
    )
    .Ensure(
        user => user.EmailVerified,
        Error.ValidationError("Email not verified")
    );
```

### ✅ DO: Collect multiple validation errors when needed

```csharp
public Result<User> ValidateUser(UserDto dto)
{
    var errors = new List<Error>();
    
    if (string.IsNullOrEmpty(dto.Email))
        errors.Add(Error.ValidationError("Email is required"));
    
    if (dto.Age < 18)
        errors.Add(Error.ValidationError("Must be 18+"));
    
    if (errors.Any())
    {
        var messages = string.Join(", ", errors.Select(e => e.Message));
        return Error.ValidationError($"Validation failed: {messages}");
    }
    
    return CreateUser(dto);
}
```

## Performance Considerations

### ✅ DO: Avoid unnecessary allocations

```csharp
// ✅ GOOD: Reuse error instances if appropriate
private static readonly Error InvalidIdError = 
    Error.ValidationError("ID must be positive");

public Result<User> GetUser(int id)
{
    if (id <= 0)
        return InvalidIdError;
    
    // ...
}
```

### ✅ DO: Use struct Result when boxing is not needed

The library uses struct for `Result` to avoid heap allocations:

```csharp
// Result<T> is a struct - no heap allocation
var result = GetUser(id);  // ✅ Stack allocation
```

### ❌ DON'T: Create Result in hot paths unnecessarily

```csharp
// ❌ BAD: In tight loop
for (int i = 0; i < 1000000; i++)
{
    var result = Result<int>.Success(i);  // Unnecessary allocation
    ProcessResult(result);
}

// ✅ GOOD: Process directly if Result not needed
for (int i = 0; i < 1000000; i++)
{
    ProcessValue(i);
}
```

## Testing

### ✅ DO: Test both success and failure paths

```csharp
[Test]
public void GetUser_ValidId_ReturnsUser()
{
    var result = _service.GetUser(123);
    
    Assert.That(result.IsSuccess, Is.True);
    Assert.That(result.Value.Id, Is.EqualTo(123));
}

[Test]
public void GetUser_InvalidId_ReturnsValidationError()
{
    var result = _service.GetUser(-1);
    
    Assert.That(result.IsFailure, Is.True);
    Assert.That(result.Error.Type, Is.EqualTo(ErrorType.Validation));
}

[Test]
public void GetUser_NotFound_ReturnsNotFoundError()
{
    var result = _service.GetUser(999);
    
    Assert.That(result.IsFailure, Is.True);
    Assert.That(result.Error.Type, Is.EqualTo(ErrorType.NotFound));
}
```

### ✅ DO: Use Match/Switch for assertions

```csharp
[Test]
public void CreateUser_Success_ReturnsUser()
{
    var result = _service.CreateUser("test@example.com");
    
    result.Switch(
        onSuccess: user => Assert.That(user.Email, Is.EqualTo("test@example.com")),
        onFailure: error => Assert.Fail($"Expected success but got error: {error.Message}")
    );
}
```

## Common Pitfalls

### ❌ Pitfall 1: Accessing Value on Failure

```csharp
var result = GetUser(id);
var name = result.Value.Name;  // ❌ Throws if IsFailure!

// ✅ GOOD
result.Switch(
    onSuccess: user => Console.WriteLine(user.Name),
    onFailure: error => Console.WriteLine(error.Message)
);
```

### ❌ Pitfall 2: Not Handling Results

```csharp
GetUser(id);  // ❌ Result ignored!

// ✅ GOOD
var result = GetUser(id);
result.Switch(
    onSuccess: user => ProcessUser(user),
    onFailure: error => LogError(error)
);
```

### ❌ Pitfall 3: Using try-catch for control flow

```csharp
// ❌ BAD
try
{
    if (!user.IsPremium)
        throw new Exception("Not premium");
    
    return ApplyDiscount(user);
}
catch
{
    return Error.BusinessError("Not premium");
}

// ✅ GOOD
if (!user.IsPremium)
    return Error.BusinessError("Not premium");

return ApplyDiscount(user);
```

## Summary Checklist

✅ **Use Result Pattern for:**
- Expected business failures
- Validation errors
- Operations that commonly fail
- Composable workflows

✅ **Use try-catch for:**
- Unexpected technical errors
- Infrastructure failures
- Converting exceptions to Results

✅ **Railway Operators:**
- `Map` for transformations
- `Bind` for Result-returning operations
- `Tap` for side effects
- `Ensure` for validations

✅ **Collections:**
- `Combine` when all must succeed
- `Partition` for partial success
- `GetSuccessValues` when failures are acceptable

✅ **Error Handling:**
- Use specific error types
- Include context in messages
- Preserve exceptions

❌ **Avoid:**
- Throwing for expected failures
- Ignoring unexpected exceptions
- Nesting Result checks
- Using Map for Bind operations
- Returning null in Results
- Accessing Value without checking IsSuccess

## See Also

- **[Introduction](introduction.md)** - Understanding the Result Pattern
- **[Railway Oriented Programming](railway-oriented.md)** - Chaining operations
- **[Error Types](error-types.md)** - Choosing the right error type
- **[Examples](examples.md)** - Real-world usage patterns
