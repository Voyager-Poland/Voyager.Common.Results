# Voyager.Common.Results

[![NuGet](https://img.shields.io/nuget/v/Voyager.Common.Results.svg)](https://www.nuget.org/packages/Voyager.Common.Results/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Voyager.Common.Results.svg)](https://www.nuget.org/packages/Voyager.Common.Results/)
[![Build Status](https://github.com/Voyager-Poland/Voyager.Common.Results/workflows/.NET%20push/badge.svg)](https://github.com/Voyager-Poland/Voyager.Common.Results/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A lightweight, functional **Result Pattern** implementation for .NET that enables **Railway Oriented Programming**. Replace exceptions with explicit error handling, making your code more predictable and easier to test.

**Supports .NET Framework 4.8 and .NET 8** 🚀

## ✨ Features

- 🎯 **Type-safe error handling** without exceptions
- 🚂 **Railway Oriented Programming** with method chaining
- ⚡ **Async/await support** with extension methods
- 📦 **Zero dependencies** (except polyfills for .NET Framework)
- 🔍 **Source Link enabled** for debugging
- 📚 **Comprehensive XML documentation**
- 🧪 **Fully tested** with high code coverage
- 🎨 **Implicit conversions** for ergonomic API
- 🤖 **Automated publishing** via GitHub Actions

## 📦 Installation

```bash
dotnet add package Voyager.Common.Results
```

## 🚀 Quick Start

```csharp
using Voyager.Common.Results;

// Define operations that can fail
public Result<User> GetUser(int id)
{
    var user = _repository.Find(id);
    return user is not null 
        ? Result<User>.Success(user)
        : Error.NotFoundError($"User {id} not found");
}

public Result<Order> GetLatestOrder(User user)
{
    var order = _repository.GetLatestOrder(user.Id);
    return order is not null
        ? Result<Order>.Success(order)
        : Error.NotFoundError("No orders found");
}

// Chain operations with Railway Oriented Programming
var result = GetUser(123)
    .Bind(user => GetLatestOrder(user))
    .Map(order => order.TotalAmount)
    .Tap(total => _logger.LogInfo($"Total: {total}"));

// Handle the result
var message = result.Match(
    onSuccess: total => $"Order total: {total:C}",
    onFailure: error => $"Error: {error.Message}"
);
```

## 📖 Documentation

### Core Types

- **`Result<T>`** - Represents an operation that returns a value or an error
- **`Result`** - Represents an operation that returns success or an error (void operations)
- **`Error`** - Represents an error with type and message

### Error Types

```csharp
Error.ValidationError("Invalid email format")
Error.NotFoundError("User not found")
Error.UnauthorizedError("User not logged in")
Error.PermissionError("Access denied")
Error.ConflictError("Email already exists")
Error.DatabaseError("Connection failed")
Error.BusinessError("Cannot cancel paid order")
Error.UnavailableError("Service temporarily unavailable")
Error.TimeoutError("Request timed out")
Error.UnexpectedError("Something went wrong")
Error.FromException(exception)
```

### Railway Oriented Programming

```csharp
GetUser(id)
    .Map(user => user.Email)              // Transform success value
    .Bind(email => SendEmail(email))       // Chain another Result operation
    .Ensure(sent => sent, Error.BusinessError("Email not sent"))
    .Tap(() => _logger.LogInfo("Email sent"))  // Side effect
    .OrElse(() => GetDefaultUser())        // Fallback if failed
    .Match(
        onSuccess: () => "Success",
        onFailure: error => error.Message
    );
```

### OrElse - Fallback Pattern

```csharp
// Try multiple data sources - returns first success
var user = GetUserFromCache(userId)
    .OrElse(() => GetUserFromDatabase(userId))
    .OrElse(() => GetDefaultUser());

// Async version
var config = await LoadConfigFromFileAsync()
    .OrElseAsync(() => LoadConfigFromDatabaseAsync())
    .OrElseAsync(() => GetDefaultConfigAsync());

// Real-world example: Multi-tier data retrieval
var data = await GetFromPrimaryCacheAsync(key)
    .OrElseAsync(() => GetFromDatabaseAsync(key))
    .OrElseAsync(() => GetFromApiAsync(key))
    .OrElseAsync(Result<Data>.Success(defaultValue));
```

**Common use cases:**
- Cache → Database → Default value
- Primary API → Fallback API → Cached data  
- User preferences → Team defaults → System defaults

### Async Operations

```csharp
await GetUserAsync(id)
    .MapAsync(user => user.Email)
    .BindAsync(email => SendEmailAsync(email))
    .TapAsync(() => _logger.LogInfoAsync("Email sent"));
```

### Collection Operations

```csharp
var results = new[] {
    Result<int>.Success(1),
    Result<int>.Success(2),
    Result<int>.Success(3)
};

// Combine all results into one
Result<List<int>> combined = results.Combine();

// Partition into successes and failures
var (successes, failures) = results.Partition();

// Get only successful values
List<int> values = results.GetSuccessValues();
```

## 📚 More Examples

See the [full documentation](./src/Voyager.Common.Results/README.md) for detailed examples and best practices.

## 🏗️ Building and Publishing

See [BUILD.md](./BUILD.md) for comprehensive instructions on:
- 🤖 **Automatic publishing** with GitHub Actions (recommended)
- 🔨 Manual building and local testing
- 📦 Publishing to GitHub Packages and NuGet.org
- 🧪 Running tests with code coverage

**New to versioning?** See [Quick Start - Versioning](./docs/QUICK-START-VERSIONING.md) for a 3-step guide to create your first release.

### Quick Build

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build -c Release

# Run tests
dotnet test -c Release

# Pack the package
dotnet pack src/Voyager.Common.Results/Voyager.Common.Results.csproj -c Release
```

### Automatic Publishing

Simply push to `main` branch - GitHub Actions will:
1. ✅ Automatically bump version
2. ✅ Build for both .NET 8.0 and .NET Framework 4.8
3. ✅ Run all tests
4. ✅ Publish to GitHub Packages
5. ✅ Publish to NuGet.org (if configured)

```bash
git add .
git commit -m "Add new feature"
git push origin main
```

## 🧪 Running Tests

```bash
# Run all tests
dotnet test

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate coverage report (requires reportgenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Development Workflow

- Push to `main` triggers automatic version bump and publishing
- All tests must pass before merging
- Follow existing code style and conventions
- Add tests for new features
- Update documentation as needed

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

Inspired by:
- [Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/) by Scott Wlaschin
- [Result type in Rust](https://doc.rust-lang.org/std/result/)
- [Either type in functional programming](https://en.wikipedia.org/wiki/Either_type)

## 📝 Changelog

See [CHANGELOG.md](CHANGELOG.md) for a list of changes.

## 📚 Additional Resources

- [GitHub Actions Setup Guide](./GITHUB_ACTIONS_SETUP.md) - Detailed GitHub Actions configuration
- [Build Guide](./BUILD.md) - Building and publishing instructions
- [API Documentation](./src/Voyager.Common.Results/README.md) - Complete API reference

---

Made with ❤️ by [Voyager Poland](https://github.com/Voyager-Poland)
