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
}
