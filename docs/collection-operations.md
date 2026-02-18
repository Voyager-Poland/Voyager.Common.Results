# Collection Operations

Voyager.Common.Results provides extension methods for working with collections of Results, making it easy to handle multiple operations that can succeed or fail.

## Extension Methods Location

```csharp
using Voyager.Common.Results.Extensions;
```

## Core Collection Operations

### Combine - Merge Multiple Results

`Combine` merges multiple `Result<T>` into a single `Result<List<T>>`. If **any** result is a failure, the combined result is a failure with the first error encountered.

**Signature:**
```csharp
Result<List<T>> Combine<T>(this IEnumerable<Result<T>> results)
```

**Example:**

```csharp
var results = new[]
{
    Result<int>.Success(1),
    Result<int>.Success(2),
    Result<int>.Success(3)
};

var combined = results.Combine();
// Result<List<int>> with [1, 2, 3]

combined.Switch(
    onSuccess: values => Console.WriteLine($"Count: {values.Count}"),
    onFailure: error => Console.WriteLine($"Error: {error.Message}")
);
```

**With Failures:**

```csharp
var results = new[]
{
    Result<int>.Success(1),
    Result<int>.Failure(Error.ValidationError("Invalid")),
    Result<int>.Success(3)
};

var combined = results.Combine();
// Result<List<int>> - FAILURE with ValidationError
// Only the first error is returned
```

**Real-world example:**

```csharp
public Result<List<Order>> ValidateOrders(List<OrderDto> orderDtos)
{
    var validationResults = orderDtos
        .Select(dto => ValidateOrder(dto))
        .ToList();
    
    return validationResults.Combine();
    // If all valid: Result<List<Order>> with all orders
    // If any invalid: Result<List<Order>> - FAILURE with first error
}

public Result<Order> ValidateOrder(OrderDto dto)
{
    if (dto.Items.Count == 0)
        return Error.ValidationError("Order must have items");
    
    if (dto.TotalAmount <= 0)
        return Error.ValidationError("Total amount must be positive");
    
    return new Order { /* ... */ };
}
```

### Partition - Split Successes and Failures

`Partition` separates a collection of Results into two lists: successes and failures.

**Signature:**
```csharp
(List<T> Successes, List<Error> Failures) Partition<T>(this IEnumerable<Result<T>> results)
```

**Example:**

```csharp
var results = new[]
{
    Result<int>.Success(1),
    Result<int>.Failure(Error.ValidationError("Error 1")),
    Result<int>.Success(3),
    Result<int>.Failure(Error.ValidationError("Error 2")),
    Result<int>.Success(5)
};

var (successes, failures) = results.Partition();

Console.WriteLine($"Successes: {successes.Count}");  // 3
Console.WriteLine($"Failures: {failures.Count}");    // 2

foreach (var value in successes)
    Console.WriteLine($"Success: {value}");  // 1, 3, 5

foreach (var error in failures)
    Console.WriteLine($"Error: {error.Message}");  // "Error 1", "Error 2"
```

**Real-world example:**

```csharp
public async Task<BatchProcessResult> ProcessOrdersAsync(List<Order> orders)
{
    var processingTasks = orders.Select(order => ProcessOrderAsync(order));
    var results = await Task.WhenAll(processingTasks);
    
    var (successes, failures) = results.Partition();
    
    // Log successful orders
    foreach (var order in successes)
    {
        _logger.LogInformation($"Order {order.Id} processed successfully");
    }
    
    // Log failed orders
    foreach (var error in failures)
    {
        _logger.LogError($"Order processing failed: {error.Message}");
    }
    
    return new BatchProcessResult
    {
        SuccessCount = successes.Count,
        FailureCount = failures.Count,
        Errors = failures
    };
}
```

### GetSuccessValues - Extract All Success Values

`GetSuccessValues` extracts only the success values from a collection of Results, ignoring failures.

**Signature:**
```csharp
List<T> GetSuccessValues<T>(this IEnumerable<Result<T>> results)
```

**Example:**

```csharp
var results = new[]
{
    Result<int>.Success(1),
    Result<int>.Failure(Error.ValidationError("Error")),
    Result<int>.Success(3),
    Result<int>.Success(5)
};

var values = results.GetSuccessValues();
// [1, 3, 5]

Console.WriteLine(string.Join(", ", values));  // "1, 3, 5"
```

**Real-world example:**

```csharp
public async Task<List<User>> GetValidUsersAsync(List<int> userIds)
{
    var userTasks = userIds.Select(id => GetUserAsync(id));
    var results = await Task.WhenAll(userTasks);
    
    // Get only valid users, ignore not found errors
    var validUsers = results.GetSuccessValues();
    
    _logger.LogInformation($"Retrieved {validUsers.Count} out of {userIds.Count} users");
    
    return validUsers;
}
```

## Advanced Patterns

### Pattern 1: Validate and Filter

Process a collection, validate each item, and return only valid ones:

```csharp
public Result<List<Order>> ValidateAndFilterOrders(List<OrderDto> orderDtos)
{
    var results = orderDtos
        .Select(dto => ValidateOrder(dto))
        .ToList();
    
    var (validOrders, errors) = results.Partition();
    
    if (errors.Any())
    {
        _logger.LogWarning($"Found {errors.Count} invalid orders");
        
        // Option 1: Return first error
        return Error.ValidationError($"Found {errors.Count} invalid orders");
        
        // Option 2: Return all valid orders anyway
        return validOrders;
    }
    
    return validOrders;
}
```

### Pattern 2: Fail-Fast Validation

Stop at first error using `Combine`:

```csharp
public Result<BatchImportResult> ImportUsersAsync(List<UserDto> userDtos)
{
    // Validate ALL users first
    var validationResults = userDtos
        .Select(dto => ValidateUser(dto))
        .ToList();
    
    // Combine fails fast - if any invalid, stop here
    var validatedUsers = validationResults.Combine();
    
    if (validatedUsers.IsFailure)
    {
        return Error.ValidationError(
            $"Validation failed: {validatedUsers.Error.Message}. No users imported."
        );
    }
    
    // All valid, proceed with import
    return await ImportValidatedUsersAsync(validatedUsers.Value);
}
```

### Pattern 3: Parallel Processing with Partial Success

Process items in parallel and report partial success:

```csharp
public async Task<BatchResult<Order>> ProcessOrderBatchAsync(List<CreateOrderDto> dtos)
{
    // Process all orders in parallel
    var tasks = dtos.Select(dto => ProcessOrderAsync(dto));
    var results = await Task.WhenAll(tasks);
    
    var (successes, failures) = results.Partition();
    
    // Accept partial success
    await _eventBus.PublishAsync(new OrderBatchProcessedEvent
    {
        SuccessCount = successes.Count,
        FailureCount = failures.Count
    });
    
    return new BatchResult<Order>
    {
        Successes = successes,
        Failures = failures.Select(e => e.Message).ToList(),
        TotalCount = dtos.Count
    };
}
```

### Pattern 4: Collecting Multiple Validation Errors

Collect all errors instead of failing at first:

```csharp
public class ValidationResult
{
    public List<Error> Errors { get; set; } = new();
    public bool IsValid => !Errors.Any();
}

public ValidationResult ValidateUserRegistration(RegisterDto dto)
{
    var validation = new ValidationResult();
    
    var emailResult = ValidateEmail(dto.Email);
    if (emailResult.IsFailure)
        validation.Errors.Add(emailResult.Error);
    
    var passwordResult = ValidatePassword(dto.Password);
    if (passwordResult.IsFailure)
        validation.Errors.Add(passwordResult.Error);
    
    var ageResult = ValidateAge(dto.Age);
    if (ageResult.IsFailure)
        validation.Errors.Add(ageResult.Error);
    
    return validation;
}

public Result<User> RegisterUser(RegisterDto dto)
{
    var validation = ValidateUserRegistration(dto);
    
    if (!validation.IsValid)
    {
        var messages = string.Join(", ", validation.Errors.Select(e => e.Message));
        return Error.ValidationError($"Validation failed: {messages}");
    }
    
    // All validations passed
    return CreateUser(dto);
}
```

### Pattern 5: Map Then Combine

Transform and validate in one step:

```csharp
public Result<List<Order>> CreateOrdersFromDtos(List<CreateOrderDto> dtos)
{
    return dtos
        .Select(dto => CreateOrderFromDto(dto))  // Each returns Result<Order>
        .Combine();  // Combine into Result<List<Order>>
}

Result<Order> CreateOrderFromDto(CreateOrderDto dto)
{
    if (dto.Items.Count == 0)
        return Error.ValidationError("Order must have items");
    
    return new Order 
    { 
        CustomerId = dto.CustomerId, 
        Items = dto.Items 
    };
}
```

## Combine Tuple Variants (v1.9.0)

Combine 2-4 `Result<T>` instances into a single `Result` containing a tuple. Returns the first error if any Result is a failure.

**Signatures:**
```csharp
Result<(T1, T2)> Combine<T1, T2>(this Result<T1> first, Result<T2> second)
Result<(T1, T2, T3)> Combine<T1, T2, T3>(this Result<T1> first, Result<T2> second, Result<T3> third)
Result<(T1, T2, T3, T4)> Combine<T1, T2, T3, T4>(...)
```

**Example:**
```csharp
var name = Result<string>.Success("Alice");
var age = Result<int>.Success(30);
var email = Result<string>.Success("alice@example.com");

var combined = name.Combine(age, email);
// Result<(string, int, string)> with ("Alice", 30, "alice@example.com")

combined.Switch(
    onSuccess: tuple => Console.WriteLine($"{tuple.Item1}, {tuple.Item2}, {tuple.Item3}"),
    onFailure: error => Console.WriteLine($"Error: {error.Message}")
);
```

**Real-world example — combining independent service calls:**
```csharp
public Result<OrderSummary> GetOrderSummary(int orderId)
{
    var order = _orderRepo.GetById(orderId);
    var customer = _customerRepo.GetById(order.Value?.CustomerId ?? 0);
    var payment = _paymentRepo.GetByOrderId(orderId);

    return order.Combine(customer, payment)
        .Map(tuple => new OrderSummary
        {
            Order = tuple.Item1,
            Customer = tuple.Item2,
            Payment = tuple.Item3
        });
}
```

## Async Collection Operations (v1.9.0)

### TraverseAsync — Sequential Async with Fail-Fast

`TraverseAsync` sequentially applies an async Result-returning function to each element. Stops on the first failure.

**Signatures:**
```csharp
Task<Result<List<TOut>>> TraverseAsync<T, TOut>(this IEnumerable<T> source, Func<T, Task<Result<TOut>>> func)
Task<Result> TraverseAsync<T>(this IEnumerable<T> source, Func<T, Task<Result>> func)
```

**Example:**
```csharp
var items = new[] { "order1", "order2", "order3" };

var result = await items.TraverseAsync(
    id => ProcessOrderAsync(id));
// Result<List<ProcessedOrder>> — all values if all succeeded, or first error
```

**Real-world example — replaces `foreach + break + Combine()` pattern:**
```csharp
// BEFORE (v1.8.0):
var results = new List<Result>();
foreach (var (op, data) in operations)
{
    var result = await OperationUpdateResultAsync(ctx, op, data);
    results.Add(result);
    if (result.IsFailure) break;
}
return results.Combine();

// AFTER (v1.9.0):
return await operations.TraverseAsync(
    x => OperationUpdateResultAsync(ctx, x.op, x.data));
```

### TraverseAllAsync — Sequential Async, Collect All Errors

`TraverseAllAsync` is like `TraverseAsync`, but continues on failure and collects ALL errors. Errors are aggregated using the `InnerError` chain: first error → second error → ...

**Signatures:**
```csharp
Task<Result<List<TOut>>> TraverseAllAsync<T, TOut>(this IEnumerable<T> source, Func<T, Task<Result<TOut>>> func)
Task<Result> TraverseAllAsync<T>(this IEnumerable<T> source, Func<T, Task<Result>> func)
```

**Example:**
```csharp
var result = await items.TraverseAllAsync(
    x => ValidateAndProcessAsync(x));

if (result.IsFailure)
{
    // result.Error — first error
    // result.Error.InnerError — second error
    // result.Error.InnerError.InnerError — third error, etc.
    var rootCause = result.Error.GetRootCause(); // last error in chain
}
```

**Real-world example — batch validation with all errors:**
```csharp
public async Task<Result<List<ProcessedFee>>> ProcessFeesAsync(List<FeeDto> fees)
{
    var result = await fees.TraverseAllAsync(async fee =>
    {
        var validated = ValidateFee(fee);
        if (validated.IsFailure) return validated;
        return await _paymentService.ChargeFeeAsync(validated.Value);
    });

    // On failure: Error contains InnerError chain with all fee processing errors
    // On success: Result<List<ProcessedFee>> with all results
    return result;
}
```

### CombineAsync — Async Combine

`CombineAsync` awaits all tasks (using `Task.WhenAll`) and combines their results. Fail-fast on the first error.

**Signatures:**
```csharp
Task<Result<List<TValue>>> CombineAsync<TValue>(this IEnumerable<Task<Result<TValue>>> tasks)
Task<Result> CombineAsync(this IEnumerable<Task<Result>> tasks)
```

**Example:**
```csharp
var tasks = userIds.Select(id => GetUserAsync(id));
var result = await tasks.CombineAsync();
// Result<List<User>> — all users if all found, or first error
```

**Real-world example:**
```csharp
public async Task<Result<List<User>>> GetMultipleUsersAsync(int[] userIds)
{
    var tasks = userIds.Select(id => GetUserAsync(id));
    return await tasks.CombineAsync();
    // Cleaner than: await Task.WhenAll(tasks) then .Combine()
}
```

### PartitionAsync — Async Partition

`PartitionAsync` awaits all tasks and partitions the results into successes and failures.

**Signature:**
```csharp
Task<(List<TValue> Successes, List<Error> Failures)> PartitionAsync<TValue>(
    this IEnumerable<Task<Result<TValue>>> tasks)
```

**Example:**
```csharp
public async Task<ProcessingReport> ProcessItemsAsync(List<Item> items)
{
    var tasks = items.Select(item => ProcessItemAsync(item));
    var (successes, failures) = await tasks.PartitionAsync();

    return new ProcessingReport
    {
        ProcessedItems = successes,
        FailedItems = failures.Select(e => new FailedItem
        {
            Error = e.Message
        }).ToList()
    };
}
```

## Real-World Examples

### Example 1: Bulk User Import

```csharp
public async Task<BulkImportResult> ImportUsersAsync(List<UserDto> userDtos)
{
    _logger.LogInformation($"Importing {userDtos.Count} users");
    
    // Validate all users
    var validationResults = userDtos
        .Select(dto => ValidateUserDto(dto))
        .ToList();
    
    var (validDtos, validationErrors) = validationResults.Partition();
    
    // Import valid users
    var importTasks = validDtos.Select(dto => CreateUserAsync(dto));
    var importResults = await Task.WhenAll(importTasks);
    
    var (importedUsers, importErrors) = importResults.Partition();
    
    // Combine all errors
    var allErrors = validationErrors.Concat(importErrors).ToList();
    
    return new BulkImportResult
    {
        TotalCount = userDtos.Count,
        SuccessCount = importedUsers.Count,
        FailureCount = allErrors.Count,
        ImportedUsers = importedUsers,
        Errors = allErrors.Select(e => e.Message).ToList()
    };
}
```

### Example 2: Multi-Step Validation Pipeline

```csharp
public async Task<Result<ProcessedOrder>> ProcessOrderPipelineAsync(OrderDto dto)
{
    var validationSteps = new[]
    {
        ValidateOrderItems(dto.Items),
        ValidateShippingAddress(dto.ShippingAddress),
        ValidatePaymentMethod(dto.PaymentMethod)
    };
    
    // All validations must pass
    var validationResult = validationSteps.Combine();
    
    if (validationResult.IsFailure)
        return validationResult.Error;
    
    // All valid, proceed with order
    return await CreateAndProcessOrderAsync(dto);
}
```

### Example 3: Dependent Service Calls (with Combine tuple)

```csharp
public async Task<Result<DashboardData>> LoadDashboardAsync(int userId)
{
    var userResult = await GetUserAsync(userId);
    if (userResult.IsFailure)
        return userResult.Error;

    var user = userResult.Value;

    // Load independent data in parallel
    var orders = await GetUserOrdersAsync(user.Id);
    var payments = await GetUserPaymentsAsync(user.Id);
    var notifications = await GetUserNotificationsAsync(user.Id);

    // Combine with type-safe tuples (v1.9.0)
    return orders.Combine(payments, notifications)
        .Map(tuple => new DashboardData
        {
            User = user,
            Orders = tuple.Item1,
            Payments = tuple.Item2,
            Notifications = tuple.Item3
        });
}
```

## Best Practices

### ✅ DO

```csharp
// Use Combine when all items must succeed
var results = items.Select(item => Validate(item));
var allValid = results.Combine();  // ✅ Fails if any fails

// Use Partition when you want to process both successes and failures
var (successes, failures) = results.Partition();  // ✅
LogFailures(failures);
ProcessSuccesses(successes);

// Use GetSuccessValues when failures are acceptable
var validItems = results.GetSuccessValues();  // ✅ Ignores failures

// Use TraverseAsync for sequential async with fail-fast (v1.9.0)
var result = await items.TraverseAsync(x => ProcessAsync(x));  // ✅

// Use TraverseAllAsync when you need all errors (v1.9.0)
var result = await items.TraverseAllAsync(x => ValidateAsync(x));  // ✅

// Use Combine tuple for type-safe combining of different Results (v1.9.0)
var combined = name.Combine(age, email);  // ✅ Result<(string, int, string)>
```

### ❌ DON'T

```csharp
// Don't manually iterate and check
foreach (var result in results)  // ❌ Use Combine/Partition instead
{
    if (result.IsSuccess)
        successes.Add(result.Value);
    else
        failures.Add(result.Error);
}

// Don't use foreach+break for async fail-fast — use TraverseAsync
var results = new List<Result>();
foreach (var item in items)  // ❌ Use TraverseAsync instead
{
    var result = await ProcessAsync(item);
    results.Add(result);
    if (result.IsFailure) break;
}
return results.Combine();

// Don't ignore failures silently
var values = results
    .Where(r => r.IsSuccess)  // ❌ Failures lost
    .Select(r => r.Value);
// Use GetSuccessValues() or Partition() instead

// Don't combine if you need all errors
var combined = results.Combine();  // ❌ Only returns first error
// Use Partition() or TraverseAllAsync() to get all errors
```

## See Also

- **[Railway Oriented Programming](railway-oriented.md)** - Chaining individual Results
- **[Async Operations](async-operations.md)** - Async collection processing
- **[Best Practices](best-practices.md)** - General Result Pattern best practices
- **[Examples](examples.md)** - More real-world examples
