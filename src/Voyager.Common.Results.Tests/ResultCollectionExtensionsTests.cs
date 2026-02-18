using Voyager.Common.Results;
using Voyager.Common.Results.Extensions;

namespace Voyager.Common.Results.Tests;

public class ResultCollectionExtensionsTests
{
	// ========== COMBINE (GENERIC) TESTS ==========

	[Fact]
	public void Combine_WithAllSuccess_ReturnsSuccessWithAllValues()
	{
		// Arrange
		var results = new[]
		{
			Result<int>.Success(1),
			Result<int>.Success(2),
			Result<int>.Success(3)
		};

		// Act
		var combined = results.Combine();

		// Assert
		Assert.True(combined.IsSuccess);
		Assert.NotNull(combined.Value);
		Assert.Equal(3, combined.Value.Count);
		Assert.Equal(new[] { 1, 2, 3 }, combined.Value);
	}

	[Fact]
	public void Combine_WithOneFailure_ReturnsFirstFailure()
	{
		// Arrange
		var error1 = Error.ValidationError("First error");
		var error2 = Error.ValidationError("Second error");
		var results = new[]
		{
			Result<int>.Success(1),
			Result<int>.Failure(error1),
			Result<int>.Success(3),
			Result<int>.Failure(error2)
		};

		// Act
		var combined = results.Combine();

		// Assert
		Assert.True(combined.IsFailure);
		Assert.Equal(error1, combined.Error);
	}

	[Fact]
	public void Combine_WithAllFailures_ReturnsFirstFailure()
	{
		// Arrange
		var error1 = Error.ValidationError("First error");
		var error2 = Error.ValidationError("Second error");
		var results = new[]
		{
			Result<int>.Failure(error1),
			Result<int>.Failure(error2)
		};

		// Act
		var combined = results.Combine();

		// Assert
		Assert.True(combined.IsFailure);
		Assert.Equal(error1, combined.Error);
	}

	[Fact]
	public void Combine_WithEmptyCollection_ReturnsSuccessWithEmptyList()
	{
		// Arrange
		var results = Array.Empty<Result<int>>();

		// Act
		var combined = results.Combine();

		// Assert
		Assert.True(combined.IsSuccess);
		Assert.NotNull(combined.Value);
		Assert.Empty(combined.Value);
	}

	// ========== COMBINE (NON-GENERIC) TESTS ==========

	[Fact]
	public void Combine_NonGeneric_WithAllSuccess_ReturnsSuccess()
	{
		// Arrange
		var results = new[]
		{
			Result.Success(),
			Result.Success(),
			Result.Success()
		};

		// Act
		var combined = results.Combine();

		// Assert
		Assert.True(combined.IsSuccess);
	}

	[Fact]
	public void Combine_NonGeneric_WithOneFailure_ReturnsFirstFailure()
	{
		// Arrange
		var error1 = Error.ValidationError("First error");
		var error2 = Error.ValidationError("Second error");
		var results = new[]
		{
			Result.Success(),
			Result.Failure(error1),
			Result.Success(),
			Result.Failure(error2)
		};

		// Act
		var combined = results.Combine();

		// Assert
		Assert.True(combined.IsFailure);
		Assert.Equal(error1, combined.Error);
	}

	[Fact]
	public void Combine_NonGeneric_WithEmptyCollection_ReturnsSuccess()
	{
		// Arrange
		var results = Array.Empty<Result>();

		// Act
		var combined = results.Combine();

		// Assert
		Assert.True(combined.IsSuccess);
	}

	// ========== GET ERRORS TESTS ==========

	[Fact]
	public void GetErrors_ReturnsOnlyErrorsFromFailures()
	{
		// Arrange
		var error1 = Error.ValidationError("Error 1");
		var error2 = Error.ValidationError("Error 2");
		var results = new[]
		{
			Result<int>.Success(1),
			Result<int>.Failure(error1),
			Result<int>.Success(3),
			Result<int>.Failure(error2)
		};

		// Act
		var errors = results.GetErrors();

		// Assert
		Assert.Equal(2, errors.Count);
		Assert.Contains(error1, errors);
		Assert.Contains(error2, errors);
	}

	[Fact]
	public void GetErrors_WithAllSuccess_ReturnsEmptyList()
	{
		// Arrange
		var results = new[]
		{
			Result<int>.Success(1),
			Result<int>.Success(2),
			Result<int>.Success(3)
		};

		// Act
		var errors = results.GetErrors();

		// Assert
		Assert.Empty(errors);
	}

	[Fact]
	public void GetErrors_WithAllFailures_ReturnsAllErrors()
	{
		// Arrange
		var error1 = Error.ValidationError("Error 1");
		var error2 = Error.BusinessError("Error 2");
		var error3 = Error.NotFoundError("Error 3");
		var results = new[]
		{
			Result<int>.Failure(error1),
			Result<int>.Failure(error2),
			Result<int>.Failure(error3)
		};

		// Act
		var errors = results.GetErrors();

		// Assert
		Assert.Equal(3, errors.Count);
		Assert.Contains(error1, errors);
		Assert.Contains(error2, errors);
		Assert.Contains(error3, errors);
	}

	// ========== GET SUCCESS VALUES TESTS ==========

	[Fact]
	public void GetSuccessValues_ReturnsOnlyValuesFromSuccesses()
	{
		// Arrange
		var results = new[]
		{
			Result<int>.Success(1),
			Result<int>.Failure(Error.ValidationError("Error")),
			Result<int>.Success(3),
			Result<int>.Failure(Error.ValidationError("Error")),
			Result<int>.Success(5)
		};

		// Act
		var values = results.GetSuccessValues();

		// Assert
		Assert.Equal(3, values.Count);
		Assert.Equal(new[] { 1, 3, 5 }, values);
	}

	[Fact]
	public void GetSuccessValues_WithAllFailures_ReturnsEmptyList()
	{
		// Arrange
		var results = new[]
		{
			Result<int>.Failure(Error.ValidationError("Error 1")),
			Result<int>.Failure(Error.ValidationError("Error 2"))
		};

		// Act
		var values = results.GetSuccessValues();

		// Assert
		Assert.Empty(values);
	}

	[Fact]
	public void GetSuccessValues_WithAllSuccess_ReturnsAllValues()
	{
		// Arrange
		var results = new[]
		{
			Result<int>.Success(10),
			Result<int>.Success(20),
			Result<int>.Success(30)
		};

		// Act
		var values = results.GetSuccessValues();

		// Assert
		Assert.Equal(3, values.Count);
		Assert.Equal(new[] { 10, 20, 30 }, values);
	}

	// ========== ALL SUCCESS TESTS ==========

	[Fact]
	public void AllSuccess_WithAllSuccess_ReturnsTrue()
	{
		// Arrange
		var results = new[]
		{
			Result<int>.Success(1),
			Result<int>.Success(2),
			Result<int>.Success(3)
		};

		// Act
		var allSuccess = results.AllSuccess();

		// Assert
		Assert.True(allSuccess);
	}

	[Fact]
	public void AllSuccess_WithOneFailure_ReturnsFalse()
	{
		// Arrange
		var results = new[]
		{
			Result<int>.Success(1),
			Result<int>.Failure(Error.ValidationError("Error")),
			Result<int>.Success(3)
		};

		// Act
		var allSuccess = results.AllSuccess();

		// Assert
		Assert.False(allSuccess);
	}

	[Fact]
	public void AllSuccess_WithAllFailures_ReturnsFalse()
	{
		// Arrange
		var results = new[]
		{
			Result<int>.Failure(Error.ValidationError("Error 1")),
			Result<int>.Failure(Error.ValidationError("Error 2"))
		};

		// Act
		var allSuccess = results.AllSuccess();

		// Assert
		Assert.False(allSuccess);
	}

	[Fact]
	public void AllSuccess_WithEmptyCollection_ReturnsTrue()
	{
		// Arrange
		var results = Array.Empty<Result<int>>();

		// Act
		var allSuccess = results.AllSuccess();

		// Assert
		Assert.True(allSuccess); // LINQ All() returns true for empty sequences
	}

	// ========== ANY SUCCESS TESTS ==========

	[Fact]
	public void AnySuccess_WithAtLeastOneSuccess_ReturnsTrue()
	{
		// Arrange
		var results = new[]
		{
			Result<int>.Failure(Error.ValidationError("Error")),
			Result<int>.Success(2),
			Result<int>.Failure(Error.ValidationError("Error"))
		};

		// Act
		var anySuccess = results.AnySuccess();

		// Assert
		Assert.True(anySuccess);
	}

	[Fact]
	public void AnySuccess_WithAllFailures_ReturnsFalse()
	{
		// Arrange
		var results = new[]
		{
			Result<int>.Failure(Error.ValidationError("Error 1")),
			Result<int>.Failure(Error.ValidationError("Error 2"))
		};

		// Act
		var anySuccess = results.AnySuccess();

		// Assert
		Assert.False(anySuccess);
	}

	[Fact]
	public void AnySuccess_WithAllSuccess_ReturnsTrue()
	{
		// Arrange
		var results = new[]
		{
			Result<int>.Success(1),
			Result<int>.Success(2),
			Result<int>.Success(3)
		};

		// Act
		var anySuccess = results.AnySuccess();

		// Assert
		Assert.True(anySuccess);
	}

	[Fact]
	public void AnySuccess_WithEmptyCollection_ReturnsFalse()
	{
		// Arrange
		var results = Array.Empty<Result<int>>();

		// Act
		var anySuccess = results.AnySuccess();

		// Assert
		Assert.False(anySuccess); // LINQ Any() returns false for empty sequences
	}

	// ========== PARTITION TESTS ==========

	[Fact]
	public void Partition_SeparatesSuccessesAndFailures()
	{
		// Arrange
		var error1 = Error.ValidationError("Error 1");
		var error2 = Error.ValidationError("Error 2");
		var results = new[]
		{
			Result<int>.Success(1),
			Result<int>.Failure(error1),
			Result<int>.Success(3),
			Result<int>.Failure(error2),
			Result<int>.Success(5)
		};

		// Act
		var (successes, failures) = results.Partition();

		// Assert
		Assert.Equal(3, successes.Count);
		Assert.Equal(new[] { 1, 3, 5 }, successes);
		Assert.Equal(2, failures.Count);
		Assert.Contains(error1, failures);
		Assert.Contains(error2, failures);
	}

	[Fact]
	public void Partition_WithAllSuccess_ReturnsAllSuccessesAndNoFailures()
	{
		// Arrange
		var results = new[]
		{
			Result<int>.Success(1),
			Result<int>.Success(2),
			Result<int>.Success(3)
		};

		// Act
		var (successes, failures) = results.Partition();

		// Assert
		Assert.Equal(3, successes.Count);
		Assert.Equal(new[] { 1, 2, 3 }, successes);
		Assert.Empty(failures);
	}

	[Fact]
	public void Partition_WithAllFailures_ReturnsNoSuccessesAndAllFailures()
	{
		// Arrange
		var error1 = Error.ValidationError("Error 1");
		var error2 = Error.BusinessError("Error 2");
		var results = new[]
		{
			Result<int>.Failure(error1),
			Result<int>.Failure(error2)
		};

		// Act
		var (successes, failures) = results.Partition();

		// Assert
		Assert.Empty(successes);
		Assert.Equal(2, failures.Count);
		Assert.Contains(error1, failures);
		Assert.Contains(error2, failures);
	}

	[Fact]
	public void Partition_WithEmptyCollection_ReturnsBothEmpty()
	{
		// Arrange
		var results = Array.Empty<Result<int>>();

		// Act
		var (successes, failures) = results.Partition();

		// Assert
		Assert.Empty(successes);
		Assert.Empty(failures);
	}

	// ========== INTEGRATION TESTS ==========

	[Fact]
	public void Combine_WithComplexObjects_WorksCorrectly()
	{
		// Arrange
		var results = new[]
		{
			Result<string>.Success("Hello"),
			Result<string>.Success("World"),
			Result<string>.Success("!")
		};

		// Act
		var combined = results.Combine();

		// Assert
		Assert.True(combined.IsSuccess);
		Assert.Equal(new[] { "Hello", "World", "!" }, combined.Value);
	}

	[Fact]
	public void GetErrors_PreservesErrorOrder()
	{
		// Arrange
		var error1 = Error.ValidationError("CODE1", "First");
		var error2 = Error.BusinessError("CODE2", "Second");
		var error3 = Error.NotFoundError("CODE3", "Third");
		var results = new[]
		{
			Result<int>.Failure(error1),
			Result<int>.Success(1),
			Result<int>.Failure(error2),
			Result<int>.Success(2),
			Result<int>.Failure(error3)
		};

		// Act
		var errors = results.GetErrors();

		// Assert
		Assert.Equal(3, errors.Count);
		Assert.Equal(error1, errors[0]);
		Assert.Equal(error2, errors[1]);
		Assert.Equal(error3, errors[2]);
	}

	[Fact]
	public void GetSuccessValues_PreservesValueOrder()
	{
		// Arrange
		var results = new[]
		{
			Result<int>.Success(10),
			Result<int>.Failure(Error.ValidationError("Error")),
			Result<int>.Success(20),
			Result<int>.Failure(Error.ValidationError("Error")),
			Result<int>.Success(30)
		};

		// Act
		var values = results.GetSuccessValues();

		// Assert
		Assert.Equal(3, values.Count);
		Assert.Equal(10, values[0]);
		Assert.Equal(20, values[1]);
		Assert.Equal(30, values[2]);
	}

	[Fact]
	public void Partition_PreservesBothOrders()
	{
		// Arrange
		var error1 = Error.ValidationError("E1");
		var error2 = Error.ValidationError("E2");
		var results = new[]
		{
			Result<int>.Success(1),
			Result<int>.Failure(error1),
			Result<int>.Success(2),
			Result<int>.Failure(error2),
			Result<int>.Success(3)
		};

		// Act
		var (successes, failures) = results.Partition();

		// Assert
		Assert.Equal(new[] { 1, 2, 3 }, successes);
		Assert.Equal(new[] { error1, error2 }, failures);
	}

	// ========== COMBINE (TUPLE 2) TESTS ==========

	[Fact]
	public void CombineTuple2_WithBothSuccess_ReturnsTupleResult()
	{
		// Arrange
		var first = Result<string>.Success("Alice");
		var second = Result<int>.Success(30);

		// Act
		var combined = first.Combine(second);

		// Assert
		Assert.True(combined.IsSuccess);
		Assert.Equal(("Alice", 30), combined.Value);
	}

	[Fact]
	public void CombineTuple2_WithFirstFailure_ReturnsFirstError()
	{
		// Arrange
		var error = Error.ValidationError("First failed");
		var first = Result<string>.Failure(error);
		var second = Result<int>.Success(30);

		// Act
		var combined = first.Combine(second);

		// Assert
		Assert.True(combined.IsFailure);
		Assert.Equal(error, combined.Error);
	}

	[Fact]
	public void CombineTuple2_WithSecondFailure_ReturnsSecondError()
	{
		// Arrange
		var first = Result<string>.Success("Alice");
		var error = Error.ValidationError("Second failed");
		var second = Result<int>.Failure(error);

		// Act
		var combined = first.Combine(second);

		// Assert
		Assert.True(combined.IsFailure);
		Assert.Equal(error, combined.Error);
	}

	[Fact]
	public void CombineTuple2_WithBothFailure_ReturnsFirstError()
	{
		// Arrange
		var error1 = Error.ValidationError("First failed");
		var error2 = Error.ValidationError("Second failed");
		var first = Result<string>.Failure(error1);
		var second = Result<int>.Failure(error2);

		// Act
		var combined = first.Combine(second);

		// Assert
		Assert.True(combined.IsFailure);
		Assert.Equal(error1, combined.Error);
	}

	// ========== COMBINE (TUPLE 3) TESTS ==========

	[Fact]
	public void CombineTuple3_WithAllSuccess_ReturnsTupleResult()
	{
		// Arrange
		var first = Result<string>.Success("Alice");
		var second = Result<int>.Success(30);
		var third = Result<bool>.Success(true);

		// Act
		var combined = first.Combine(second, third);

		// Assert
		Assert.True(combined.IsSuccess);
		Assert.Equal(("Alice", 30, true), combined.Value);
	}

	[Fact]
	public void CombineTuple3_WithMiddleFailure_ReturnsMiddleError()
	{
		// Arrange
		var first = Result<string>.Success("Alice");
		var error = Error.ValidationError("Second failed");
		var second = Result<int>.Failure(error);
		var third = Result<bool>.Success(true);

		// Act
		var combined = first.Combine(second, third);

		// Assert
		Assert.True(combined.IsFailure);
		Assert.Equal(error, combined.Error);
	}

	// ========== COMBINE (TUPLE 4) TESTS ==========

	[Fact]
	public void CombineTuple4_WithAllSuccess_ReturnsTupleResult()
	{
		// Arrange
		var first = Result<string>.Success("Alice");
		var second = Result<int>.Success(30);
		var third = Result<bool>.Success(true);
		var fourth = Result<double>.Success(1.5);

		// Act
		var combined = first.Combine(second, third, fourth);

		// Assert
		Assert.True(combined.IsSuccess);
		Assert.Equal(("Alice", 30, true, 1.5), combined.Value);
	}

	[Fact]
	public void CombineTuple4_WithLastFailure_ReturnsLastError()
	{
		// Arrange
		var first = Result<string>.Success("Alice");
		var second = Result<int>.Success(30);
		var third = Result<bool>.Success(true);
		var error = Error.ValidationError("Fourth failed");
		var fourth = Result<double>.Failure(error);

		// Act
		var combined = first.Combine(second, third, fourth);

		// Assert
		Assert.True(combined.IsFailure);
		Assert.Equal(error, combined.Error);
	}

	// ========== TRAVERSE ASYNC (GENERIC, FAIL-FAST) TESTS ==========

	[Fact]
	public async Task TraverseAsync_WithAllSuccess_ReturnsSuccessWithAllValues()
	{
		// Arrange
		var items = new[] { 1, 2, 3 };

		// Act
		var result = await items.TraverseAsync(
			x => Task.FromResult(Result<string>.Success(x.ToString())));

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(new[] { "1", "2", "3" }, result.Value);
	}

	[Fact]
	public async Task TraverseAsync_WithFailure_StopsOnFirstError()
	{
		// Arrange
		var items = new[] { 1, 2, 3, 4 };
		var error = Error.ValidationError("Failed at 2");
		var processedItems = new List<int>();

		// Act
		var result = await items.TraverseAsync(async x =>
		{
			processedItems.Add(x);
			await Task.CompletedTask;
			if (x == 2)
				return Result<string>.Failure(error);
			return Result<string>.Success(x.ToString());
		});

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);
		Assert.Equal(new[] { 1, 2 }, processedItems); // stopped after 2
	}

	[Fact]
	public async Task TraverseAsync_WithEmptyCollection_ReturnsSuccessWithEmptyList()
	{
		// Arrange
		var items = Array.Empty<int>();

		// Act
		var result = await items.TraverseAsync(
			x => Task.FromResult(Result<string>.Success(x.ToString())));

		// Assert
		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Empty(result.Value);
	}

	[Fact]
	public async Task TraverseAsync_WithSingleSuccess_ReturnsSingleElementList()
	{
		// Arrange
		var items = new[] { 42 };

		// Act
		var result = await items.TraverseAsync(
			x => Task.FromResult(Result<string>.Success(x.ToString())));

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!);
		Assert.Equal("42", result.Value![0]);
	}

	[Fact]
	public async Task TraverseAsync_WithSingleFailure_ReturnsError()
	{
		// Arrange
		var items = new[] { 42 };
		var error = Error.ValidationError("Failed");

		// Act
		var result = await items.TraverseAsync(
			x => Task.FromResult(Result<string>.Failure(error)));

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);
	}

	[Fact]
	public async Task TraverseAsync_PreservesValueOrder()
	{
		// Arrange
		var items = new[] { 3, 1, 4, 1, 5 };

		// Act
		var result = await items.TraverseAsync(
			x => Task.FromResult(Result<int>.Success(x * 10)));

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(new[] { 30, 10, 40, 10, 50 }, result.Value);
	}

	// ========== TRAVERSE ASYNC (NON-GENERIC, FAIL-FAST) TESTS ==========

	[Fact]
	public async Task TraverseAsync_NonGeneric_WithAllSuccess_ReturnsSuccess()
	{
		// Arrange
		var items = new[] { 1, 2, 3 };

		// Act
		var result = await items.TraverseAsync(
			x => Task.FromResult(Result.Success()));

		// Assert
		Assert.True(result.IsSuccess);
	}

	[Fact]
	public async Task TraverseAsync_NonGeneric_WithFailure_StopsOnFirstError()
	{
		// Arrange
		var items = new[] { 1, 2, 3, 4 };
		var error = Error.ValidationError("Failed at 3");
		var processedItems = new List<int>();

		// Act
		var result = await items.TraverseAsync(async x =>
		{
			processedItems.Add(x);
			await Task.CompletedTask;
			if (x == 3)
				return Result.Failure(error);
			return Result.Success();
		});

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);
		Assert.Equal(new[] { 1, 2, 3 }, processedItems); // stopped after 3
	}

	[Fact]
	public async Task TraverseAsync_NonGeneric_WithEmptyCollection_ReturnsSuccess()
	{
		// Arrange
		var items = Array.Empty<int>();

		// Act
		var result = await items.TraverseAsync(
			x => Task.FromResult(Result.Success()));

		// Assert
		Assert.True(result.IsSuccess);
	}

	// ========== TRAVERSE ALL ASYNC (GENERIC, COLLECT ALL) TESTS ==========

	[Fact]
	public async Task TraverseAllAsync_WithAllSuccess_ReturnsSuccessWithAllValues()
	{
		// Arrange
		var items = new[] { 1, 2, 3 };

		// Act
		var result = await items.TraverseAllAsync(
			x => Task.FromResult(Result<string>.Success(x.ToString())));

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(new[] { "1", "2", "3" }, result.Value);
	}

	[Fact]
	public async Task TraverseAllAsync_WithFailures_ContinuesAndCollectsAllErrors()
	{
		// Arrange
		var items = new[] { 1, 2, 3, 4 };
		var error2 = Error.ValidationError("Failed at 2");
		var error4 = Error.ValidationError("Failed at 4");
		var processedItems = new List<int>();

		// Act
		var result = await items.TraverseAllAsync(async x =>
		{
			processedItems.Add(x);
			await Task.CompletedTask;
			if (x % 2 == 0)
				return Result<string>.Failure(x == 2 ? error2 : error4);
			return Result<string>.Success(x.ToString());
		});

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(new[] { 1, 2, 3, 4 }, processedItems); // processed ALL items
		Assert.Equal(error2.Type, result.Error!.Type);
		Assert.Equal(error2.Code, result.Error!.Code);
		Assert.Equal(error2.Message, result.Error!.Message);
		Assert.NotNull(result.Error!.InnerError);
		Assert.Equal(error4.Type, result.Error!.InnerError!.Type);
		Assert.Equal(error4.Message, result.Error!.InnerError!.Message);
	}

	[Fact]
	public async Task TraverseAllAsync_WithSingleFailure_ReturnsErrorWithoutInnerError()
	{
		// Arrange
		var items = new[] { 1, 2, 3 };
		var error = Error.ValidationError("Failed at 2");

		// Act
		var result = await items.TraverseAllAsync(
			x => Task.FromResult(
				x == 2
					? Result<string>.Failure(error)
					: Result<string>.Success(x.ToString())));

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);
		Assert.Null(result.Error!.InnerError);
	}

	[Fact]
	public async Task TraverseAllAsync_WithEmptyCollection_ReturnsSuccessWithEmptyList()
	{
		// Arrange
		var items = Array.Empty<int>();

		// Act
		var result = await items.TraverseAllAsync(
			x => Task.FromResult(Result<string>.Success(x.ToString())));

		// Assert
		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Empty(result.Value);
	}

	[Fact]
	public async Task TraverseAllAsync_WithAllFailures_ChainsAllErrors()
	{
		// Arrange
		var items = new[] { 1, 2, 3 };
		var error1 = Error.ValidationError("Error 1");
		var error2 = Error.BusinessError("Error 2");
		var error3 = Error.NotFoundError("Error 3");

		// Act
		var result = await items.TraverseAllAsync(
			x => Task.FromResult(Result<string>.Failure(
				x == 1 ? error1 : x == 2 ? error2 : error3)));

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(error1.Type, result.Error!.Type);
		Assert.Equal(error1.Message, result.Error!.Message);
		Assert.NotNull(result.Error!.InnerError);
		Assert.Equal(error2.Type, result.Error!.InnerError!.Type);
		Assert.Equal(error2.Message, result.Error!.InnerError!.Message);
		Assert.NotNull(result.Error!.InnerError!.InnerError);
		Assert.Equal(error3.Type, result.Error!.InnerError!.InnerError!.Type);
		Assert.Equal(error3.Message, result.Error!.InnerError!.InnerError!.Message);
	}

	// ========== TRAVERSE ALL ASYNC (NON-GENERIC, COLLECT ALL) TESTS ==========

	[Fact]
	public async Task TraverseAllAsync_NonGeneric_WithAllSuccess_ReturnsSuccess()
	{
		// Arrange
		var items = new[] { 1, 2, 3 };

		// Act
		var result = await items.TraverseAllAsync(
			x => Task.FromResult(Result.Success()));

		// Assert
		Assert.True(result.IsSuccess);
	}

	[Fact]
	public async Task TraverseAllAsync_NonGeneric_WithFailures_CollectsAllErrors()
	{
		// Arrange
		var items = new[] { 1, 2, 3 };
		var error1 = Error.ValidationError("Error 1");
		var error3 = Error.ValidationError("Error 3");
		var processedItems = new List<int>();

		// Act
		var result = await items.TraverseAllAsync(async x =>
		{
			processedItems.Add(x);
			await Task.CompletedTask;
			if (x % 2 != 0)
				return Result.Failure(x == 1 ? error1 : error3);
			return Result.Success();
		});

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(new[] { 1, 2, 3 }, processedItems); // processed ALL items
		Assert.Equal(error1.Type, result.Error!.Type);
		Assert.Equal(error1.Message, result.Error!.Message);
		Assert.NotNull(result.Error!.InnerError);
		Assert.Equal(error3.Type, result.Error!.InnerError!.Type);
		Assert.Equal(error3.Message, result.Error!.InnerError!.Message);
	}

	[Fact]
	public async Task TraverseAllAsync_NonGeneric_WithEmptyCollection_ReturnsSuccess()
	{
		// Arrange
		var items = Array.Empty<int>();

		// Act
		var result = await items.TraverseAllAsync(
			x => Task.FromResult(Result.Success()));

		// Assert
		Assert.True(result.IsSuccess);
	}

	// ========== COMBINE ASYNC (GENERIC) TESTS ==========

	[Fact]
	public async Task CombineAsync_WithAllSuccess_ReturnsSuccessWithAllValues()
	{
		// Arrange
		var tasks = new[]
		{
			Task.FromResult(Result<int>.Success(1)),
			Task.FromResult(Result<int>.Success(2)),
			Task.FromResult(Result<int>.Success(3))
		};

		// Act
		var result = await tasks.CombineAsync();

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(new[] { 1, 2, 3 }, result.Value);
	}

	[Fact]
	public async Task CombineAsync_WithOneFailure_ReturnsFirstFailure()
	{
		// Arrange
		var error = Error.ValidationError("Failed");
		var tasks = new[]
		{
			Task.FromResult(Result<int>.Success(1)),
			Task.FromResult(Result<int>.Failure(error)),
			Task.FromResult(Result<int>.Success(3))
		};

		// Act
		var result = await tasks.CombineAsync();

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);
	}

	[Fact]
	public async Task CombineAsync_WithEmptyCollection_ReturnsSuccessWithEmptyList()
	{
		// Arrange
		var tasks = Array.Empty<Task<Result<int>>>();

		// Act
		var result = await tasks.CombineAsync();

		// Assert
		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Empty(result.Value);
	}

	// ========== COMBINE ASYNC (NON-GENERIC) TESTS ==========

	[Fact]
	public async Task CombineAsync_NonGeneric_WithAllSuccess_ReturnsSuccess()
	{
		// Arrange
		var tasks = new[]
		{
			Task.FromResult(Result.Success()),
			Task.FromResult(Result.Success()),
			Task.FromResult(Result.Success())
		};

		// Act
		var result = await tasks.CombineAsync();

		// Assert
		Assert.True(result.IsSuccess);
	}

	[Fact]
	public async Task CombineAsync_NonGeneric_WithOneFailure_ReturnsFirstFailure()
	{
		// Arrange
		var error = Error.ValidationError("Failed");
		var tasks = new[]
		{
			Task.FromResult(Result.Success()),
			Task.FromResult(Result.Failure(error)),
			Task.FromResult(Result.Success())
		};

		// Act
		var result = await tasks.CombineAsync();

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);
	}

	[Fact]
	public async Task CombineAsync_NonGeneric_WithEmptyCollection_ReturnsSuccess()
	{
		// Arrange
		var tasks = Array.Empty<Task<Result>>();

		// Act
		var result = await tasks.CombineAsync();

		// Assert
		Assert.True(result.IsSuccess);
	}

	// ========== PARTITION ASYNC TESTS ==========

	[Fact]
	public async Task PartitionAsync_SeparatesSuccessesAndFailures()
	{
		// Arrange
		var error1 = Error.ValidationError("Error 1");
		var error2 = Error.ValidationError("Error 2");
		var tasks = new[]
		{
			Task.FromResult(Result<int>.Success(1)),
			Task.FromResult(Result<int>.Failure(error1)),
			Task.FromResult(Result<int>.Success(3)),
			Task.FromResult(Result<int>.Failure(error2)),
			Task.FromResult(Result<int>.Success(5))
		};

		// Act
		var (successes, failures) = await tasks.PartitionAsync();

		// Assert
		Assert.Equal(new[] { 1, 3, 5 }, successes);
		Assert.Equal(2, failures.Count);
		Assert.Contains(error1, failures);
		Assert.Contains(error2, failures);
	}

	[Fact]
	public async Task PartitionAsync_WithAllSuccess_ReturnsAllSuccessesAndNoFailures()
	{
		// Arrange
		var tasks = new[]
		{
			Task.FromResult(Result<int>.Success(1)),
			Task.FromResult(Result<int>.Success(2)),
			Task.FromResult(Result<int>.Success(3))
		};

		// Act
		var (successes, failures) = await tasks.PartitionAsync();

		// Assert
		Assert.Equal(new[] { 1, 2, 3 }, successes);
		Assert.Empty(failures);
	}

	[Fact]
	public async Task PartitionAsync_WithEmptyCollection_ReturnsBothEmpty()
	{
		// Arrange
		var tasks = Array.Empty<Task<Result<int>>>();

		// Act
		var (successes, failures) = await tasks.PartitionAsync();

		// Assert
		Assert.Empty(successes);
		Assert.Empty(failures);
	}
}
