# Voyager.Common.Results

[![NuGet](https://img.shields.io/nuget/v/Voyager.Common.Results.svg)](https://www.nuget.org/packages/Voyager.Common.Results/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Voyager.Common.Results.svg)](https://www.nuget.org/packages/Voyager.Common.Results/)
[![Build Status](https://github.com/Voyager-Poland/Voyager.Common.Results/workflows/CI%20Build/badge.svg)](https://github.com/Voyager-Poland/Voyager.Common.Results/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET library implementing the **Result Pattern** (Railway Oriented Programming) for robust error handling.

Supports **.NET Framework 4.8** and **.NET 8** 🎯

## What is Result Pattern?

The Result Pattern is a functional programming approach to error handling that makes success and failure explicit in your code. Instead of throwing exceptions for expected failures, you return a `Result` object that encapsulates either a success value or an error.

### Benefits

- ✅ **Explicit error handling** - Compiler forces you to handle errors
- ✅ **No hidden control flow** - No exceptions jumping up the call stack
- ✅ **Composable** - Chain operations with Map, Bind, and more
- ✅ **Type-safe** - Errors are part of the method signature
- ✅ **Testable** - Easy to test both success and failure paths

## Quick Start

### Installation

```bash
dotnet add package Voyager.Common.Results
```

### Basic Usage

```csharp
using Voyager.Common.Results;

public Result<User> GetUser(int userId)
{
    var user = _repository.Find(userId);
    
    if (user is null)
        return Error.NotFoundError($"User {userId} not found");
    
    return user;  // Implicit conversion to Result<User>
}

// Use the result
var result = GetUser(123);
var userName = result.Match(
    onSuccess: user => user.Name,
    onFailure: error => "Unknown user"
);
```

### Railway Oriented Programming

Chain operations without nested if statements:

```csharp
var result = GetUser(123)
    .Bind(user => ValidateUser(user))
    .Bind(user => SaveUser(user))
    .Tap(user => SendWelcomeEmail(user))
    .Map(user => user.Id);
```

## Documentation

- **[Introduction](introduction.md)** - Learn about the Result Pattern
- **[Getting Started](getting-started.md)** - Installation and first steps
- **[Error Types](error-types.md)** - All available error types
- **[Railway Oriented Programming](railway-oriented.md)** - Map, Bind, Tap, Ensure
- **[Async Operations](async-operations.md)** - Working with async/await
- **[OrElse - Fallback Pattern](orelse-pattern.md)** - Resilient fallback strategies
- **[Collection Operations](collection-operations.md)** - Combine, Partition, and more
- **[Best Practices](best-practices.md)** - Patterns and anti-patterns
- **[Examples](examples.md)** - Real-world usage examples

## Features at a Glance

### Result Types

- `Result` - For operations without a return value
- `Result<T>` - For operations that return a value

### Error Handling

- Validation errors
- Permission errors
- Not found errors
- Conflict errors
- Database errors
- Business logic errors
- Unexpected errors

### Railway Operators

- `Map` - Transform success values
- `Bind` - Chain operations that return Result
- `Tap` - Execute side effects
- `Ensure` - Validate conditions
- `Match` / `Switch` - Handle success or failure

### Async Support

All operators have async variants:
- `MapAsync`
- `BindAsync`
- `TapAsync`
- `EnsureAsync`

### Collection Operations

- `Combine` - Merge multiple results
- `Partition` - Split successes and failures
- `GetSuccessValues` - Extract all success values

## Community and Support

- **GitHub Repository**: [Voyager-Poland/Voyager.Common.Results](https://github.com/Voyager-Poland/Voyager.Common.Results)
- **Issues**: Report bugs or request features on [GitHub Issues](https://github.com/Voyager-Poland/Voyager.Common.Results/issues)
- **NuGet Package**: [Voyager.Common.Results](https://www.nuget.org/packages/Voyager.Common.Results/)

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/Voyager-Poland/Voyager.Common.Results/blob/master/LICENSE) file for details.
