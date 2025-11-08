# Getting Started

This guide will help you install and start using Voyager.Common.Results in your .NET projects.

## Prerequisites

- .NET Framework 4.8 or higher
- .NET 8 or higher
- Visual Studio 2022, Rider, or VS Code

## Installation

### Using .NET CLI

```bash
dotnet add package Voyager.Common.Results
```

### Using Package Manager Console

```powershell
Install-Package Voyager.Common.Results
```

### Using PackageReference

Add this to your `.csproj` file:

```xml
<PackageReference Include="Voyager.Common.Results" Version="1.0.0" />
```

## Your First Result

### Step 1: Add Using Statement

```csharp
using Voyager.Common.Results;
```

### Step 2: Create a Method Returning Result

```csharp
public Result<User> GetUser(int userId)
{
    // Validation
    if (userId <= 0)
        return Error.ValidationError("User ID must be positive");
    
    // Fetch from database
    var user = _database.Users.Find(userId);
    
    // Handle not found
    if (user is null)
        return Error.NotFoundError($"User {userId} not found");
    
    // Success - implicit conversion
    return user;
}
```

### Step 3: Use the Result

```csharp
var result = GetUser(123);

// Option 1: Match - transform to another type
string message = result.Match(
    onSuccess: user => $"Welcome, {user.Name}!",
    onFailure: error => $"Error: {error.Message}"
);

// Option 2: Switch - execute actions
result.Switch(
    onSuccess: user => Console.WriteLine($"Found: {user.Name}"),
    onFailure: error => Console.WriteLine($"Error: {error.Message}")
);

// Option 3: Check manually
if (result.IsSuccess)
{
    var user = result.Value;
    Console.WriteLine($"User: {user.Name}");
}
else
{
    var error = result.Error;
    Console.WriteLine($"Error: {error.Message}");
}
```

## Result Without Value

For operations that don't return a value (like void methods), use `Result`:

```csharp
public Result DeleteUser(int userId)
{
    if (userId <= 0)
        return Error.ValidationError("User ID must be positive");
    
    var user = _database.Users.Find(userId);
    if (user is null)
        return Error.NotFoundError("User not found");
    
    _database.Users.Remove(user);
    _database.SaveChanges();
    
    return Result.Success();
}

// Usage
var result = DeleteUser(123);
result.Switch(
    onSuccess: () => Console.WriteLine("User deleted"),
    onFailure: error => Console.WriteLine($"Error: {error.Message}")
);
```

## Common Patterns

### 1. Validation Chain

```csharp
public Result<User> RegisterUser(string email, string password)
{
    // Validate email
    if (string.IsNullOrEmpty(email))
        return Error.ValidationError("Email is required");
    
    if (!email.Contains("@"))
        return Error.ValidationError("Invalid email format");
    
    // Validate password
    if (string.IsNullOrEmpty(password))
        return Error.ValidationError("Password is required");
    
    if (password.Length < 8)
        return Error.ValidationError("Password must be at least 8 characters");
    
    // Check if email exists
    if (_database.Users.Any(u => u.Email == email))
        return Error.ConflictError("Email already registered");
    
    // Create user
    var user = new User 
    { 
        Email = email, 
        Password = HashPassword(password) 
    };
    
    _database.Users.Add(user);
    _database.SaveChanges();
    
    return user;
}
```

### 2. Error Handling with Try-Catch

Combine Result Pattern with exceptions for unexpected errors:

```csharp
public Result<User> GetUser(int userId)
{
    // Expected business errors → Result
    if (userId <= 0)
        return Error.ValidationError("Invalid user ID");
    
    try
    {
        // Unexpected technical errors → try-catch
        var user = _database.Users.Find(userId);
        
        if (user is null)
            return Error.NotFoundError("User not found");
        
        return user;
    }
    catch (SqlException ex)
    {
        // Convert unexpected exception to Result
        return Error.DatabaseError("Database error occurred", ex);
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError("Unexpected error", ex);
    }
}
```

### 3. ASP.NET Core Integration

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
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
                ErrorType.Permission => Forbid(),
                _ => StatusCode(500, error.Message)
            }
        );
    }
    
    [HttpPost]
    public IActionResult CreateUser([FromBody] CreateUserDto dto)
    {
        var result = _userService.CreateUser(dto.Email, dto.Password);
        
        return result.Match<IActionResult>(
            onSuccess: user => CreatedAtAction(nameof(GetUser), new { id = user.Id }, user),
            onFailure: error => error.Type switch
            {
                ErrorType.Validation => BadRequest(error.Message),
                ErrorType.Conflict => Conflict(error.Message),
                _ => StatusCode(500, error.Message)
            }
        );
    }
}
```

## Next Steps

Now that you know the basics, explore more advanced features:

- **[Error Types](error-types.md)** - All available error types and when to use them
- **[Railway Oriented Programming](railway-oriented.md)** - Chain operations with Map, Bind, Tap
- **[Async Operations](async-operations.md)** - Working with async/await
- **[Best Practices](best-practices.md)** - Patterns to follow and anti-patterns to avoid

## Quick Reference

### Creating Results

```csharp
// Success with value
Result<int>.Success(42)
42  // Implicit conversion

// Success without value
Result.Success()

// Failure
Result<int>.Failure(Error.NotFoundError("Not found"))
Error.NotFoundError("Not found")  // Implicit conversion
```

### Checking Results

```csharp
result.IsSuccess    // bool
result.IsFailure    // bool
result.Value        // T (throws if IsFailure)
result.Error        // Error (null if IsSuccess)
```

### Handling Results

```csharp
// Match - returns a value
var output = result.Match(
    onSuccess: value => /* transform */,
    onFailure: error => /* handle error */
);

// Switch - executes actions (void)
result.Switch(
    onSuccess: value => /* do something */,
    onFailure: error => /* handle error */
);
```

### Common Errors

```csharp
Error.ValidationError("message")
Error.NotFoundError("message")
Error.PermissionError("message")
Error.ConflictError("message")
Error.DatabaseError("message")
Error.BusinessError("message")
Error.UnexpectedError("message")
Error.FromException(exception)