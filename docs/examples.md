# Examples

Real-world examples demonstrating Voyager.Common.Results in various scenarios.

## Table of Contents

- [User Management](#user-management)
- [E-commerce Order Processing](#e-commerce-order-processing)
- [File Upload and Processing](#file-upload-and-processing)
- [Payment Processing](#payment-processing)
- [Authentication and Authorization](#authentication-and-authorization)
- [API Integration](#api-integration)
- [Data Import/Export](#data-importexport)
- [Multi-Step Workflows](#multi-step-workflows)

---

## User Management

### Example 1: User Registration

```csharp
public class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserService> _logger;

    public async Task<Result<UserDto>> RegisterUserAsync(RegisterDto dto)
    {
        return await ValidateEmail(dto.Email)
            .EnsureAsync(
                email => IsEmailUniqueAsync(email),
                Error.ConflictError("Email already registered")
            )
            .BindAsync(email => ValidatePassword(dto.Password))
            .MapAsync(password => new User
            {
                Email = dto.Email,
                PasswordHash = HashPassword(password),
                CreatedAt = DateTime.UtcNow
            })
            .BindAsync(user => SaveUserAsync(user))
            .TapAsync(user => _logger.LogInformationAsync($"User {user.Id} registered"))
            .TapAsync(user => SendWelcomeEmailAsync(user))
            .MapAsync(user => MapToDto(user));
    }

    private Result<string> ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Error.ValidationError("Email is required");

        if (!email.Contains("@"))
            return Error.ValidationError("Invalid email format");

        if (email.Length > 255)
            return Error.ValidationError("Email is too long");

        return email.ToLower();
    }

    private Result<string> ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return Error.ValidationError("Password is required");

        if (password.Length < 8)
            return Error.ValidationError("Password must be at least 8 characters");

        if (!password.Any(char.IsUpper))
            return Error.ValidationError("Password must contain uppercase letter");

        if (!password.Any(char.IsDigit))
            return Error.ValidationError("Password must contain a digit");

        return password;
    }

    private async Task<bool> IsEmailUniqueAsync(string email)
    {
        return !await _userRepository.ExistsAsync(u => u.Email == email);
    }

    private async Task<Result<User>> SaveUserAsync(User user)
    {
        try
        {
            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();
            return user;
        }
        catch (DbUpdateException ex)
        {
            return Error.DatabaseError("Failed to save user", ex);
        }
    }
}
```

### Example 2: User Profile Update

```csharp
public async Task<Result<User>> UpdateProfileAsync(int userId, UpdateProfileDto dto)
{
    return await GetUserAsync(userId)
        .EnsureAsync(
            user => user.IsActive,
            Error.BusinessError("Cannot update inactive user profile")
        )
        .BindAsync(user => ValidateProfileUpdate(user, dto))
        .TapAsync(user => _logger.LogInformationAsync($"Updating profile for user {user.Id}"))
        .BindAsync(user => UpdateUserFieldsAsync(user, dto))
        .TapAsync(user => InvalidateCacheAsync(user.Id))
        .TapAsync(user => _eventBus.PublishAsync(new ProfileUpdatedEvent(user.Id)));
}

private Result<User> ValidateProfileUpdate(User user, UpdateProfileDto dto)
{
    if (dto.Email != user.Email)
    {
        var emailValidation = ValidateEmail(dto.Email);
        if (emailValidation.IsFailure)
            return emailValidation.Error;
    }

    if (dto.Age.HasValue && (dto.Age < 13 || dto.Age > 120))
        return Error.ValidationError("Age must be between 13 and 120");

    return user;
}
```

---

## E-commerce Order Processing

### Example 1: Place Order

```csharp
public class OrderService
{
    public async Task<Result<OrderConfirmation>> PlaceOrderAsync(int userId, CreateOrderDto dto)
    {
        return await GetUserAsync(userId)
            .EnsureAsync(
                user => user.IsActive,
                Error.BusinessError("User account is not active")
            )
            .EnsureAsync(
                user => user.EmailVerified,
                Error.BusinessError("Email must be verified to place orders")
            )
            .BindAsync(user => ValidateOrderItemsAsync(dto.Items))
            .BindAsync(items => CreateOrderAsync(userId, items, dto.ShippingAddress))
            .EnsureAsync(
                order => order.TotalAmount > 0,
                Error.ValidationError("Order total must be greater than 0")
            )
            .TapAsync(order => _logger.LogInformationAsync($"Order {order.Id} created"))
            .BindAsync(order => ProcessPaymentAsync(order, dto.PaymentMethod))
            .TapAsync(order => ReserveInventoryAsync(order))
            .TapAsync(order => _eventBus.PublishAsync(new OrderPlacedEvent(order)))
            .MapAsync(order => new OrderConfirmation
            {
                OrderId = order.Id,
                ConfirmationNumber = GenerateConfirmationNumber(),
                EstimatedDelivery = CalculateDeliveryDate(order)
            })
            .TapAsync(conf => SendOrderConfirmationEmailAsync(userId, conf));
    }

    private async Task<Result<List<OrderItem>>> ValidateOrderItemsAsync(List<CreateOrderItemDto> items)
    {
        if (items == null || items.Count == 0)
            return Error.ValidationError("Order must contain at least one item");

        var results = new List<Result<OrderItem>>();

        foreach (var item in items)
        {
            var result = await ValidateOrderItemAsync(item);
            results.Add(result);
        }

        return results.Combine();
    }

    private async Task<Result<OrderItem>> ValidateOrderItemAsync(CreateOrderItemDto dto)
    {
        if (dto.Quantity <= 0)
            return Error.ValidationError("Quantity must be greater than 0");

        var product = await _productRepository.GetByIdAsync(dto.ProductId);
        if (product == null)
            return Error.NotFoundError($"Product {dto.ProductId} not found");

        if (product.Stock < dto.Quantity)
            return Error.BusinessError($"Insufficient stock for {product.Name}");

        return new OrderItem
        {
            ProductId = dto.ProductId,
            Quantity = dto.Quantity,
            UnitPrice = product.Price,
            TotalPrice = product.Price * dto.Quantity
        };
    }

    private async Task<Result<Order>> ProcessPaymentAsync(Order order, PaymentMethodDto paymentMethod)
    {
        try
        {
            var charge = await _paymentGateway.ChargeAsync(
                paymentMethod.Token,
                order.TotalAmount,
                order.Currency
            );

            order.PaymentId = charge.Id;
            order.PaymentStatus = PaymentStatus.Paid;
            await _orderRepository.UpdateAsync(order);

            return order;
        }
        catch (PaymentDeclinedException ex)
        {
            return Error.BusinessError("Payment was declined", ex);
        }
        catch (InsufficientFundsException ex)
        {
            return Error.BusinessError("Insufficient funds", ex);
        }
        catch (PaymentGatewayException ex)
        {
            _logger.LogError(ex, "Payment gateway error");
            return Error.UnexpectedError("Payment processing failed", ex);
        }
    }
}
```

### Example 2: Cancel Order

```csharp
public async Task<Result<Order>> CancelOrderAsync(int orderId, int userId)
{
    return await GetOrderAsync(orderId)
        .EnsureAsync(
            order => order.UserId == userId,
            Error.PermissionError("You can only cancel your own orders")
        )
        .EnsureAsync(
            order => order.Status == OrderStatus.Pending || order.Status == OrderStatus.Processing,
            Error.BusinessError("Only pending or processing orders can be cancelled")
        )
        .EnsureAsync(
            order => order.CreatedAt > DateTime.UtcNow.AddHours(-24),
            Error.BusinessError("Cannot cancel orders older than 24 hours")
        )
        .TapAsync(order => _logger.LogInformationAsync($"Cancelling order {order.Id}"))
        .BindAsync(order => RefundPaymentAsync(order))
        .BindAsync(order => ReleaseInventoryAsync(order))
        .BindAsync(order => MarkOrderAsCancelledAsync(order))
        .TapAsync(order => NotifyCustomerAsync(order.UserId, $"Order {order.Id} cancelled"))
        .TapAsync(order => _eventBus.PublishAsync(new OrderCancelledEvent(order)));
}
```

---

## File Upload and Processing

### Example 1: Upload and Validate File

```csharp
public class FileService
{
    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
    private static readonly string[] AllowedExtensions = { ".jpg", ".png", ".pdf", ".docx" };

    public async Task<Result<FileMetadata>> UploadFileAsync(
        int userId,
        Stream fileStream,
        string fileName)
    {
        return await GetUserAsync(userId)
            .EnsureAsync(
                user => user.HasPermission("files:upload"),
                Error.PermissionError("User doesn't have upload permission")
            )
            .BindAsync(user => ValidateFileAsync(fileStream, fileName))
            .TapAsync(file => _logger.LogInformationAsync($"Uploading {fileName} for user {userId}"))
            .BindAsync(file => ScanForVirusesAsync(file))
            .BindAsync(file => GenerateThumbnailIfImageAsync(file))
            .BindAsync(file => UploadToBlobStorageAsync(file))
            .TapAsync(url => _cache.InvalidateAsync($"user-files-{userId}"))
            .MapAsync(url => new FileMetadata
            {
                FileName = fileName,
                Url = url,
                Size = fileStream.Length,
                UploadedAt = DateTime.UtcNow,
                UploadedBy = userId
            })
            .BindAsync(metadata => SaveFileMetadataAsync(metadata))
            .TapAsync(metadata => _eventBus.PublishAsync(new FileUploadedEvent(metadata)));
    }

    private async Task<Result<FileInfo>> ValidateFileAsync(Stream stream, string fileName)
    {
        if (stream == null || stream.Length == 0)
            return Error.ValidationError("File is empty");

        if (stream.Length > MaxFileSize)
            return Error.ValidationError($"File size exceeds maximum of {MaxFileSize / 1024 / 1024} MB");

        var extension = Path.GetExtension(fileName).ToLower();
        if (!AllowedExtensions.Contains(extension))
            return Error.ValidationError($"File type {extension} is not allowed");

        var fileInfo = new FileInfo
        {
            Stream = stream,
            FileName = fileName,
            Extension = extension
        };

        return await Task.FromResult(Result<FileInfo>.Success(fileInfo));
    }

    private async Task<Result<FileInfo>> ScanForVirusesAsync(FileInfo file)
    {
        try
        {
            var scanResult = await _antivirusService.ScanAsync(file.Stream);

            if (scanResult.IsInfected)
            {
                _logger.LogWarning($"Virus detected in file {file.FileName}");
                return Error.ValidationError("File contains malware and was rejected");
            }

            return file;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virus scan failed");
            return Error.UnexpectedError("Unable to scan file for viruses", ex);
        }
    }
}
```

### Example 2: Parse and Process File (Using Try)

```csharp
public class DataImportService
{
    public async Task<Result<ImportResult>> ImportDataFromFileAsync(string filePath)
    {
        // Try method wraps exception-throwing operations
        return Result<string>.Try(
                () => File.ReadAllText(filePath),
                ex => ex is FileNotFoundException
                    ? Error.NotFoundError($"File not found: {filePath}")
                    : ex is UnauthorizedAccessException
                    ? Error.PermissionError("Access denied to file")
                    : Error.FromException(ex))
            .Bind(content => ParseJsonContent(content))
            .Bind(data => ValidateImportData(data))
            .BindAsync(data => ImportToDatabase(data))
            .TapAsync(result => _logger.LogInformationAsync($"Imported {result.RecordCount} records"));
    }

    private Result<ImportData> ParseJsonContent(string content)
    {
        return Result<ImportData>.Try(
            () => JsonSerializer.Deserialize<ImportData>(content)!,
            ex => ex is JsonException
                ? Error.ValidationError("Invalid JSON format in file")
                : Error.FromException(ex));
    }

    private Result<ImportData> ValidateImportData(ImportData data)
    {
        if (data.Records == null || data.Records.Count == 0)
            return Error.ValidationError("No records found in import file");

        if (data.Records.Count > 10000)
            return Error.ValidationError("Too many records (max 10,000)");

        return data;
    }

    public Result DeleteTemporaryFile(string path)
    {
        // Try for void operations
        return Result.Try(
            () => File.Delete(path),
            ex => ex is UnauthorizedAccessException
                ? Error.PermissionError("Cannot delete file - access denied")
                : Error.FromException(ex));
    }

    public Result<int> ParseConfiguration(string configValue)
    {
        // Try for parsing operations
        return Result<int>.Try(
            () => int.Parse(configValue),
            ex => ex is FormatException
                ? Error.ValidationError("Invalid number format in configuration")
                : ex is OverflowException
                ? Error.ValidationError("Number too large")
                : Error.FromException(ex));
    }
}
```

---

## Payment Processing

### Example 1: Process Subscription Payment

```csharp
public class SubscriptionService
{
    public async Task<Result<Subscription>> ProcessSubscriptionPaymentAsync(
        int userId,
        SubscriptionPlan plan,
        PaymentMethodDto paymentMethod)
    {
        return await GetUserAsync(userId)
            .EnsureAsync(
                user => user.IsActive,
                Error.BusinessError("User account is not active")
            )
            .BindAsync(user => ValidateSubscriptionEligibilityAsync(user, plan))
            .BindAsync(user => ChargeSubscriptionAsync(user, plan, paymentMethod))
            .BindAsync(charge => CreateSubscriptionAsync(userId, plan, charge))
            .TapAsync(sub => GrantSubscriptionBenefitsAsync(userId, plan))
            .TapAsync(sub => _logger.LogInformationAsync($"Subscription {sub.Id} created for user {userId}"))
            .TapAsync(sub => SendSubscriptionConfirmationAsync(userId, sub))
            .TapAsync(sub => _eventBus.PublishAsync(new SubscriptionCreatedEvent(sub)));
    }

    private async Task<Result<User>> ValidateSubscriptionEligibilityAsync(User user, SubscriptionPlan plan)
    {
        var existingSub = await _subscriptionRepository.GetActiveSubscriptionAsync(user.Id);

        if (existingSub != null)
        {
            if (existingSub.Plan == plan)
                return Error.ConflictError("User already has this subscription");

            if (!CanUpgradeSubscription(existingSub.Plan, plan))
                return Error.BusinessError("Cannot downgrade subscription. Please cancel current subscription first.");
        }

        return user;
    }

    private async Task<Result<PaymentCharge>> ChargeSubscriptionAsync(
        User user,
        SubscriptionPlan plan,
        PaymentMethodDto paymentMethod)
    {
        try
        {
            var amount = GetSubscriptionAmount(plan);
            var charge = await _paymentGateway.ChargeAsync(
                paymentMethod.Token,
                amount,
                "USD",
                $"Subscription: {plan}"
            );

            return charge;
        }
        catch (PaymentDeclinedException ex)
        {
            return Error.BusinessError("Payment was declined. Please check your payment method.", ex);
        }
        catch (CardExpiredException ex)
        {
            return Error.BusinessError("Your card has expired. Please update your payment method.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment processing failed");
            return Error.UnexpectedError("Payment processing failed", ex);
        }
    }
}
```

---

## Authentication and Authorization

### Example 1: User Login

```csharp
public class AuthService
{
    public async Task<Result<LoginResponse>> LoginAsync(LoginDto dto)
    {
        return await ValidateLoginRequest(dto)
            .BindAsync(d => FindUserByEmailAsync(d.Email))
            .EnsureAsync(
                user => user.IsActive,
                Error.BusinessError("Account is deactivated")
            )
            .EnsureAsync(
                user => !user.IsLocked,
                Error.BusinessError("Account is locked due to multiple failed login attempts")
            )
            .BindAsync(user => VerifyPasswordAsync(user, dto.Password))
            .TapAsync(user => ResetFailedLoginAttemptsAsync(user.Id))
            .TapAsync(user => _logger.LogInformationAsync($"User {user.Id} logged in"))
            .BindAsync(user => GenerateTokenAsync(user))
            .MapAsync(token => new LoginResponse
            {
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            })
            .TapAsync(response => _eventBus.PublishAsync(new UserLoggedInEvent(dto.Email)));
    }

    private Result<LoginDto> ValidateLoginRequest(LoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return Error.ValidationError("Email is required");

        if (string.IsNullOrWhiteSpace(dto.Password))
            return Error.ValidationError("Password is required");

        return dto;
    }

    private async Task<Result<User>> FindUserByEmailAsync(string email)
    {
        var user = await _userRepository.FindByEmailAsync(email);

        if (user == null)
        {
            // Don't reveal whether email exists
            return Error.ValidationError("Invalid email or password");
        }

        return user;
    }

    private async Task<Result<User>> VerifyPasswordAsync(User user, string password)
    {
        var isValid = await _passwordHasher.VerifyAsync(user.PasswordHash, password);

        if (!isValid)
        {
            await IncrementFailedLoginAttemptsAsync(user.Id);
            return Error.ValidationError("Invalid email or password");
        }

        return user;
    }
}
```

---

## API Integration

### Example 1: Call External API

```csharp
public class WeatherService
{
    private readonly HttpClient _httpClient;
    private readonly ICache _cache;

    public async Task<Result<WeatherData>> GetWeatherAsync(string city)
    {
        return await ValidateCity(city)
            .BindAsync(c => GetFromCacheOrApiAsync(c))
            .TapAsync(weather => _logger.LogInformationAsync($"Weather fetched for {city}"))
            .TapAsync(weather => CacheWeatherDataAsync(city, weather));
    }

    private Result<string> ValidateCity(string city)
    {
        if (string.IsNullOrWhiteSpace(city))
            return Error.ValidationError("City name is required");

        if (city.Length < 2 || city.Length > 100)
            return Error.ValidationError("City name must be between 2 and 100 characters");

        return city.Trim();
    }

    private async Task<Result<WeatherData>> GetFromCacheOrApiAsync(string city)
    {
        var cached = await _cache.GetAsync<WeatherData>($"weather:{city}");
        if (cached != null)
            return cached;

        return await CallWeatherApiAsync(city);
    }

    private async Task<Result<WeatherData>> CallWeatherApiAsync(string city)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/weather?city={city}");

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return Error.NotFoundError($"Weather data not found for {city}");

                return Error.UnexpectedError($"Weather API returned {response.StatusCode}");
            }

            var data = await response.Content.ReadFromJsonAsync<WeatherData>();
            return data;
        }
        catch (HttpRequestException ex)
        {
            return Error.UnexpectedError("Failed to connect to weather service", ex);
        }
        catch (TaskCanceledException ex)
        {
            return Error.UnexpectedError("Weather service request timed out", ex);
        }
        catch (Exception ex)
        {
            return Error.UnexpectedError("Unexpected error calling weather service", ex);
        }
    }
}
```

---

## Data Import/Export

### Example 1: Bulk User Import

```csharp
public class ImportService
{
    public async Task<Result<BulkImportResult>> ImportUsersFromCsvAsync(Stream csvStream)
    {
        return await ParseCsvAsync(csvStream)
            .BindAsync(rows => ValidateRowsAsync(rows))
            .TapAsync(validRows => _logger.LogInformationAsync($"Validated {validRows.Count} rows"))
            .BindAsync(validRows => ImportUsersAsync(validRows))
            .MapAsync(users => new BulkImportResult
            {
                TotalProcessed = users.Count,
                SuccessCount = users.Count(u => u.IsSuccess),
                FailureCount = users.Count(u => u.IsFailure),
                Errors = users.Where(u => u.IsFailure).Select(u => u.Error.Message).ToList()
            });
    }

    private async Task<Result<List<CsvRow>>> ParseCsvAsync(Stream stream)
    {
        try
        {
            var rows = new List<CsvRow>();
            using (var reader = new StreamReader(stream))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                await foreach (var row in csv.GetRecordsAsync<CsvRow>())
                {
                    rows.Add(row);
                }
            }

            if (rows.Count == 0)
                return Error.ValidationError("CSV file is empty");

            return rows;
        }
        catch (Exception ex)
        {
            return Error.ValidationError("Invalid CSV format", ex);
        }
    }

    private async Task<Result<List<CsvRow>>> ValidateRowsAsync(List<CsvRow> rows)
    {
        var validationResults = rows.Select(row => ValidateRow(row)).ToList();
        return validationResults.Combine();
    }

    private async Task<List<Result<User>>> ImportUsersAsync(List<CsvRow> rows)
    {
        var tasks = rows.Select(row => CreateUserFromRowAsync(row));
        return (await Task.WhenAll(tasks)).ToList();
    }
}
```

## See Also

- **[Getting Started](getting-started.md)** - Basic usage patterns
- **[Railway Oriented Programming](railway-oriented.md)** - Chaining operations
- **[Best Practices](best-practices.md)** - Dos and don'ts
