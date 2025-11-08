# Introduction to Result Pattern

The Result Pattern is a functional programming approach to error handling that makes success and failure explicit in your code. Instead of relying on exceptions for control flow, you return a `Result` object that encapsulates either a success value or an error.

## Why Use Result Pattern?

### Traditional Exception Handling

```csharp
public User GetUser(int userId)
{
    var user = _repository.Find(userId);
    
    if (user is null)
        throw new NotFoundException($"User {userId} not found");
    
    if (!user.IsActive)
        throw new ValidationException("User is not active");
    
    return user;
}

// Caller has no idea this might throw
var user = GetUser(123);  // Could throw NotFoundException or ValidationException
```

**Problems with exceptions:**
- ❌ Hidden control flow - not visible in method signature
- ❌ Expensive - stack trace generation
- ❌ Easy to forget to catch
- ❌ Hard to compose operations

### Result Pattern Approach

```csharp
public Result<User> GetUser(int userId)
{
    var user = _repository.Find(userId);
    
    if (user is null)
        return Error.NotFoundError($"User {userId} not found");
    
    if (!user.IsActive)
        return Error.ValidationError("User is not active");
    
    return user;
}

// Compiler forces you to handle the result
var result = GetUser(123);
result.Match(
    onSuccess: user => Console.WriteLine($"Found: {user.Name}"),
    onFailure: error => Console.WriteLine($"Error: {error.Message}")
);
```

**Benefits of Result Pattern:**
- ✅ Explicit in method signature
- ✅ Type-safe error handling
- ✅ Composable operations
- ✅ Forces error handling
- ✅ Better performance

## Core Concepts

### Result Types

**`Result`** - For operations without a return value (like void):

```csharp
public Result ValidateEmail(string email)
{
    if (string.IsNullOrEmpty(email))
        return Error.ValidationError("Email is required");
    
    if (!email.Contains("@"))
        return Error.ValidationError("Invalid email format");
    
    return Result.Success();
}
```

**`Result<T>`** - For operations that return a value:

```csharp
public Result<User> CreateUser(string email)
{
    if (string.IsNullOrEmpty(email))
        return Error.ValidationError("Email is required");
    
    var user = new User { Email = email };
    return user;  // Implicit conversion to Result<User>
}
```

### Error Types

All errors have:
- **Type** - Category of error (Validation, NotFound, Permission, etc.)
- **Code** - Optional error code for localization
- **Message** - Human-readable description
- **Exception** - Optional underlying exception

```csharp
var error = Error.ValidationError(
    code: "User.Email.Invalid",
    message: "Email format is invalid"
);

// Error properties
var type = error.Type;        // ErrorType.Validation
var code = error.Code;        // "User.Email.Invalid"
var message = error.Message;  // "Email format is invalid"
```

### Success and Failure States

Every Result is either successful or failed, never both:

```csharp
var success = Result<int>.Success(42);
success.IsSuccess;  // true
success.IsFailure;  // false
success.Value;      // 42

var failure = Result<int>.Failure(Error.NotFoundError("Not found"));
failure.IsSuccess;  // false
failure.IsFailure;  // true
// failure.Value    // throws InvalidOperationException
failure.Error;      // Error object
```

## When to Use Result Pattern

### ✅ Use Result Pattern for:

1. **Expected business failures**
   - Validation errors
   - Resource not found
   - Permission denied
   - Business rule violations

2. **Operations that commonly fail**
   - User login (wrong password is expected)
   - Payment processing
   - External API calls

3. **Composable workflows**
   - Multi-step processes
   - Data pipelines
   - Validation chains

### ❌ Don't Use Result Pattern for:

1. **Programming errors**
   - `NullReferenceException`
   - `ArgumentNullException`
   - `IndexOutOfRangeException`
   - These indicate bugs and should be fixed

2. **Truly exceptional conditions**
   - `OutOfMemoryException`
   - `StackOverflowException`
   - Fatal system errors

3. **Simple utility methods**
   - `int.Parse()` - use `int.TryParse()` instead
   - Simple calculations without business logic

## Railway Oriented Programming

The Result Pattern enables "Railway Oriented Programming" - a functional approach where your code flows on two tracks: success and failure.

```csharp
var result = GetUser(userId)           // Result<User>
    .Bind(user => ValidateUser(user))  // Stays on success track or switches to failure
    .Bind(user => SaveUser(user))      // Only executes if still on success track
    .Tap(user => LogSuccess(user))     // Side effect only on success
    .Map(user => user.Id);              // Transform to Result<int>
```

**Key principle**: Once an operation fails, all subsequent operations are skipped, and the error propagates through the chain.

## Next Steps

- **[Getting Started](getting-started.md)** - Install and write your first Result code
- **[Error Types](error-types.md)** - Learn all available error types
- **[Railway Oriented Programming](railway-oriented.md)** - Master the railway operators
- **[Best Practices](best-practices.md)** - Patterns and anti-patterns

## Further Reading

- [Railway Oriented Programming by Scott Wlaschin](https://fsharpforfunandprofit.com/rop/)
- [Functional Error Handling in .NET](https://enterprisecraftsmanship.com/posts/functional-c-handling-failures-input-errors/)