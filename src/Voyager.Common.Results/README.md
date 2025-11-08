# Voyager.Common.Results

Biblioteka implementująca **Result Pattern** (Railway Oriented Programming) dla projektów Voyager.

## 📦 Instalacja

```bash
dotnet add reference ../Voyager.Common.Results/Voyager.Common.Results.csproj
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

// Błąd bazy danych
Error.DatabaseError("Błąd podczas zapisu")

// Błąd logiki biznesowej
Error.BusinessError("Nie można anulować opłaconego zamówienia")

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
// Używaj Result dla operacji, które mogą się nie powieść
public Result<User> CreateUser(string email)
{
    if (string.IsNullOrEmpty(email))
        return Error.ValidationError("Email is required");
    
    // ...
    return user;
}

// Łańcuch operacji bez zagnieżdżania
var result = GetUser(id)
    .Bind(user => ValidateUser(user))
    .Bind(user => SaveUser(user))
    .Tap(user => SendWelcomeEmail(user));
```

### ❌ DON'T

```csharp
// NIE używaj wyjątków do kontroli przepływu gdy masz Result
public Result<User> GetUser(int id)
{
    try
    {
        // BAD: Result Pattern ma zastąpić try-catch
        var user = _repo.GetUser(id);
        return user;
    }
    catch (Exception ex)
    {
        return Error.FromException(ex);
    }
}

// NIE mieszaj Result z throw
public Result<User> GetUser(int id)
{
    if (id <= 0)
        throw new ArgumentException(); // BAD: użyj return Error.ValidationError()
}
```

## 🧪 Testowanie

```csharp
[Fact]
public void CreateUser_WithInvalidEmail_ReturnsValidationError()
{
    // Arrange
    var service = new UserService();
    
    // Act
    var result = service.CreateUser("");
    
    // Assert
    Assert.True(result.IsFailure);
    Assert.Equal(ErrorType.Validation, result.Error!.Type);
}

[Fact]
public void CreateUser_WithValidData_ReturnsSuccess()
{
    // Arrange
    var service = new UserService();
    
    // Act
    var result = service.CreateUser("test@example.com");
    
    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
}
```

## 📚 Więcej przykładów

Sprawdź projekt testowy `Voyager.Common.Results.Tests` po więcej przykładów użycia.

## 🤝 Contributing

Pull requesty mile widziane! Przed dużymi zmianami, otwórz issue aby przedyskutować propozycje.

## 📄 Licencja

Własnościowa - Voyager Poland
