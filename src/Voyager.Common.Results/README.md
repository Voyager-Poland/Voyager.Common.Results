# Voyager.Common.Results

[![NuGet](https://img.shields.io/nuget/v/Voyager.Common.Results.svg)](https://www.nuget.org/packages/Voyager.Common.Results/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Voyager.Common.Results.svg)](https://www.nuget.org/packages/Voyager.Common.Results/)
[![Build Status](https://github.com/Voyager-Poland/Voyager.Common.Results/workflows/CI%20Build/badge.svg)](https://github.com/Voyager-Poland/Voyager.Common.Results/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Biblioteka implementująca **Result Pattern** (Railway Oriented Programming) dla projektów .NET.

Wspiera **.NET Framework 4.8** i **.NET 8** 🎯

## 📦 Instalacja

### Package Manager Console
```powershell
Install-Package Voyager.Common.Results
```

### .NET CLI
```bash
dotnet add package Voyager.Common.Results
```

### PackageReference (csproj)
```xml
<PackageReference Include="Voyager.Common.Results" Version="1.0.0" />
```

## 🎯 Podstawowe użycie

### Result bez wartości (void operations)

```csharp
using Voyager.Common.Results;

public Result ValidateUser(User user)
{
    if (string.IsNullOrEmpty(user.Email))
        return Error.ValidationError("Email jest wymagany");
    
    return Result.Success();
}

// Użycie
var result = ValidateUser(user);
result.Switch(
    onSuccess: () => Console.WriteLine("Walidacja OK"),
    onFailure: error => Console.WriteLine($"Błąd: {error.Message}")
);
```

### Result z wartością

```csharp
public Result<User> GetUser(int userId)
{
    var user = _repository.Find(userId);
    
    if (user is null)
        return Error.NotFoundError($"Nie znaleziono użytkownika {userId}");
    
    return Result<User>.Success(user);
}

// Użycie
var result = GetUser(123);
var userName = result.Match(
    onSuccess: user => user.Name,
    onFailure: error => "Nieznany użytkownik"
);
```

## 🔗 Łańcuchowanie operacji (Railway Oriented Programming)

### Map - transformacja wartości

```csharp
Result<int> GetAge() => Result<int>.Success(25);

var result = GetAge()
    .Map(age => age + 10)  // Result<int> z wartością 35
    .Map(age => $"Wiek: {age}");  // Result<string> z wartością "Wiek: 35"
```

### Bind - łańcuchowanie operacji zwracających Result

```csharp
Result<User> GetUser(int id) { ... }
Result<Order> GetLatestOrder(User user) { ... }
Result<decimal> CalculateTotal(Order order) { ... }

var total = GetUser(123)
    .Bind(user => GetLatestOrder(user))
    .Bind(order => CalculateTotal(order));
```

### Tap - side effects bez zmiany Result

```csharp
var result = GetUser(123)
    .Tap(user => _logger.LogInformation($"Znaleziono: {user.Name}"))
    .TapError(error => _logger.LogError($"Błąd: {error.Message}"))
    .Map(user => user.Email);
```

### Ensure - walidacja wartości

```csharp
var result = GetAge()
    .Ensure(
        age => age >= 18,
        Error.ValidationError("Musisz mieć minimum 18 lat")
    );
```

### OrElse - wartości alternatywne (fallback pattern)

```csharp
// Próba wielu źródeł danych - zwraca pierwszy sukces
var user = GetUserFromCache(userId)
    .OrElse(() => GetUserFromDatabase(userId))
    .OrElse(() => GetDefaultUser());

// Przykład z async
var config = await LoadConfigFromFileAsync()
    .OrElseAsync(() => LoadConfigFromDatabaseAsync())
    .OrElseAsync(() => GetDefaultConfigAsync());
```

**Użycie z lazy evaluation:**
```csharp
Result<int> GetFromPrimary() => Error.NotFoundError("Not in primary");
Result<int> GetFromSecondary() => Error.NotFoundError("Not in secondary");  
Result<int> GetFromFallback() => Result<int>.Success(42);

// Funkcje są wywoływane TYLKO gdy potrzebne (lazy evaluation)
var result = GetFromPrimary()
    .OrElse(() => GetFromSecondary())    // Wywołane bo primary failed
    .OrElse(() => GetFromFallback());     // Wywołane bo secondary failed
    // Zwraca Result<int>.Success(42)
```

**Scenariusze użycia:**
- Cache → Database → Default value
- Primary API → Fallback API → Cached data
- User preferences → Team defaults → System defaults

## ⚡ Operacje asynchroniczne

```csharp
using Voyager.Common.Results.Extensions;

async Task<Result<User>> GetUserAsync(int id) { ... }
async Task<Result<Order>> GetOrderAsync(int orderId) { ... }

// Map async
var result = await GetUserAsync(123)
    .MapAsync(user => user.Email);

// Bind async
var order = await GetUserAsync(123)
    .BindAsync(user => GetOrderAsync(user.LastOrderId));

// Łańcuchowanie async
var total = await GetUserAsync(123)
    .BindAsync(user => GetOrderAsync(user.LastOrderId))
    .MapAsync(order => order.TotalAmount);

// OrElse async - fallback pattern
var data = await GetFromPrimaryCacheAsync(key)
    .OrElseAsync(() => GetFromDatabaseAsync(key))
    .OrElseAsync(() => GetFromApiAsync(key))
    .OrElseAsync(GetDefaultValue());
```

## 📋 Operacje na kolekcjach

```csharp
using Voyager.Common.Results.Extensions;

var results = new[] {
    Result<int>.Success(1),
    Result<int>.Success(2),
    Result<int>.Success(3)
};

// Combine - łączy wszystkie w Result<List<int>>
var combined = results.Combine();

// Partition - rozdziela na sukcesy i błędy
var (successes, failures) = results.Partition();

// Pobierz tylko wartości sukcesu
var values = results.GetSuccessValues();
```

## 🎨 Typy błędów

```csharp
// Walidacja
Error.ValidationError("Email jest nieprawidłowy")
Error.ValidationError("User.Email.Invalid", "Email jest nieprawidłowy")

// Brak uprawnień
Error.PermissionError("Brak dostępu do zasobu")

// Nie znaleziono
Error.NotFoundError("Użytkownik nie istnieje")

// Konflikt (duplikat)
Error.ConflictError("Email już istnieje w systemie")

// Baza danych
Error.DatabaseError("Błąd podczas zapisu")

// Błąd logiki biznesowej
Error.BusinessError("Nie można anulować opłaconego zamówienia")

// Czasowa niedostępność (nowy typ!)
Error.UnavailableError("Serwis jest tymczasowo niedostępny")
Error.UnavailableError("RateLimit.Exceeded", "Zbyt wiele żądań - spróbuj za 60 sekund")

// Przekrocenie czasu (nowy typ!)
Error.TimeoutError("Zapytanie do bazy danych przekroczyło 30 sekund")
Error.TimeoutError("Api.Timeout", "Żądanie API przekroczyło limit czasu")

// Nieoczekiwany błąd
Error.UnexpectedError("Nieoczekiwany błąd systemowy")
Error.FromException(exception)
```

## 🔄 Implicit conversions

```csharp
// Automatyczna konwersja wartości na Result
Result<int> GetNumber() => 42;  // Zamiast Result<int>.Success(42)

// Automatyczna konwersja Error na Result
Result<User> GetUser(int id)
{
    if (id <= 0)
        return Error.ValidationError("ID must be positive");
    // ...
}
```

## 💡 Best Practices

### ✅ DO

```csharp
// Używaj Result dla OCZEKIWANYCH błędów biznesowych
public Result<User> CreateUser(string email)
{
    if (string.IsNullOrEmpty(email))
        return Error.ValidationError("Email is required");
    
    // ...
    return user;
}

// Łańcuchuj operacje Result bez zagnieżdżania
var result = GetUser(id)
    .Bind(user => ValidateUser(user))
    .Bind(user => SaveUser(user))
    .Tap(user => SendWelcomeEmail(user));

// Używaj OrElse dla wartości alternatywnych (fallback)
var config = LoadFromCache()
    .OrElse(() => LoadFromDatabase())
    .OrElse(() => GetDefaultConfig());

// ŁĄCZ Result Pattern z try-catch dla NIEOCZEKIWANYCH wyjątków technicznych
public Result<User> GetUser(int id)
{
    // Oczekiwane błędy biznesowe → Result
    if (id <= 0)
        return Error.ValidationError("ID must be positive");
    
    try
    {
        // Nieoczekiwane błędy techniczne (DB, sieć, itp.) → try-catch
        var user = _repository.GetUser(id);
        
        if (user is null)
            return Error.NotFoundError($"User {id} not found");
        
        return user;
    }
    catch (DbException ex)
    {
        // Konwertuj nieoczekiwany wyjątek techniczny na Result
        return Error.DatabaseError("Database connection failed", ex);
    }
    catch (Exception ex)
    {
        return Error.UnexpectedError("Unexpected error occurred", ex);
    }
}

// Obsługuj wyjątki infrastruktury i konwertuj na Result
public async Task<Result<Order>> PlaceOrderAsync(Order order)
{
    // Walidacja biznesowa
    if (order.Items.Count == 0)
        return Error.ValidationError("Order must contain at least one item");
    
    try
    {
        await _orderRepository.SaveAsync(order);
        await _emailService.SendConfirmationAsync(order);
        return order;
    }
    catch (TimeoutException ex)
    {
        return Error.UnexpectedError("Email service timeout", ex);
    }
    catch (InvalidOperationException ex)
    {
        return Error.BusinessError("Cannot place order in current state", ex);
    }
}
```

### ❌ DON'T

```csharp
// NIE używaj throw dla OCZEKIWANYCH błędów biznesowych
public Result<User> GetUser(int id)
{
    if (id <= 0)
        throw new ArgumentException("Invalid ID"); // ❌ BAD: użyj return Error.ValidationError()
    
    // ...
}

// NIE ignoruj nieoczekiwanych wyjątków technicznych
public Result<User> GetUser(int id)
{
    // ❌ BAD: brak try-catch - wyjątek DB może "wyskoczyć" i zepsuć Result Pattern
    var user = _repository.GetUser(id); // może rzucić DbException!
    
    if (user is null)
        return Error.NotFoundError("User not found");
    
    return user;
}

// NIE zwracaj null zamiast Result.Failure
public Result<User> FindUser(string email)
{
    var user = _repository.FindByEmail(email);
    return user; // ❌ BAD: jeśli user == null, zwróci Result.Success(null)!
    
    // ✅ GOOD:
    // if (user is null)
    //     return Error.NotFoundError("User not found");
    // return user;
}

// NIE używaj try-catch do kontroli przepływu biznesowego
public Result<decimal> CalculateDiscount(User user)
{
    try
    {
        if (!user.IsPremium)
            throw new Exception("Not premium"); // ❌ BAD
        
        return user.DiscountPercentage;
    }
    catch
    {
        return Error.BusinessError("User is not premium");
    }
    
    // ✅ GOOD:
    // if (!user.IsPremium)
    //     return Error.BusinessError("User is not premium");
    // return user.DiscountPercentage;
}
```

### 🎯 Zasada: Kiedy używać Result vs try-catch?

| Sytuacja | Użyj | Przykład |
|----------|------|----------|
| **Oczekiwany** błąd biznesowy/walidacyjny | `Result.Failure` | Nieprawidłowy email, brak uprawnień, zasób nie znaleziony |
| **Nieoczekiwany** błąd techniczny/infrastruktury | `try-catch` → `Error.FromException()` | Błąd DB, timeout sieci, OutOfMemoryException |
| Operacja może się **normalnie** nie udać | `Result Pattern` | Logowanie użytkownika (złe hasło to normalny scenariusz) |
| **Bug** w kodzie (null reference, itp.) | `throw` (w dev), `try-catch` (w prod) | NullReferenceException - powinien być naprawiony, nie obsłużony |

**Złota zasada**: 
- **Expected failures** (część logiki biznesowej) → **Result Pattern**
- **Unexpected exceptions** (problemy techniczne) → **try-catch + Error.FromException()**
