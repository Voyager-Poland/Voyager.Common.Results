using Voyager.Common.Results;

namespace Voyager.Common.Results.Tests;

public class ResultTTests
{
	[Fact]
	public void Success_CreatesSuccessfulResultWithValue()
	{
		// Act
		var result = Result<int>.Success(42);

		// Assert
		Assert.True(result.IsSuccess);
		Assert.False(result.IsFailure);
		Assert.Equal(42, result.Value);
		Assert.Equal(ErrorType.None, result.Error.Type);
	}

	[Fact]
	public void Failure_CreatesFailedResult()
	{
		// Arrange
		var error = Error.ValidationError("Test error");

		// Act
		var result = Result<int>.Failure(error);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.Equal(default(int), result.Value);
		Assert.Equal(error, result.Error);
	}

	// ========== TRY TESTS ==========

	[Fact]
	public void Try_WithSuccessfulFunc_ReturnsSuccessWithValue()
	{
		// Act
		var result = Result<int>.Try(() => 42);

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(42, result.Value);
	}

	[Fact]
	public void Try_WithExceptionThrowingFunc_ReturnsFailure()
	{
		// Act
		var result = Result<int>.Try(() => throw new InvalidOperationException("Test exception"));

		// Assert
		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorType.Unexpected, result.Error.Type);
		Assert.Equal("Exception", result.Error.Code);
		Assert.Equal("Test exception", result.Error.Message);
	}

	[Fact]
	public void Try_WithCustomErrorMapper_MapsException()
	{
		// Act
		var result = Result<int>.Try(
			() => int.Parse("invalid"),
			ex => ex is FormatException
				? Error.ValidationError("Invalid number format")
				: Error.UnexpectedError(ex.Message));

		// Assert
		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorType.Validation, result.Error.Type);
		Assert.Equal("Invalid number format", result.Error.Message);
	}

	[Fact]
	public void Try_RealWorldExample_SafeOperation()
	{
		// Act - safer operation that doesn't require System.Text.Json in .NET 4.8
		var result = Result<string>.Try(
			() => "test",
			ex => Error.ValidationError("Operation failed"));

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal("test", result.Value);
	}

	// ========== MATCH TESTS ==========

	[Fact]
	public void Match_WithSuccess_CallsOnSuccessWithValue()
	{
		// Arrange
		var result = Result<int>.Success(42);

		// Act
		var output = result.Match(
			onSuccess: value => $"Value: {value}",
			onFailure: _ => "Error"
		);

		// Assert
		Assert.Equal("Value: 42", output);
	}

	[Fact]
	public void Match_WithFailure_CallsOnFailureWithError()
	{
		// Arrange
		var error = Error.ValidationError("Test error");
		var result = Result<int>.Failure(error);

		// Act
		var output = result.Match(
			onSuccess: value => $"Value: {value}",
			onFailure: err => $"Error: {err.Message}"
		);

		// Assert
		Assert.Equal("Error: Test error", output);
	}

	[Fact]
	public void Map_WithSuccess_TransformsValue()
	{
		// Arrange
		var result = Result<int>.Success(5);

		// Act
		var mapped = result.Map(x => x * 2);

		// Assert
		Assert.True(mapped.IsSuccess);
		Assert.Equal(10, mapped.Value);
	}

	[Fact]
	public void Map_WithFailure_PropagatesError()
	{
		// Arrange
		var error = Error.ValidationError("Test");
		var result = Result<int>.Failure(error);

		// Act
		var mapped = result.Map(x => x * 2);

		// Assert
		Assert.True(mapped.IsFailure);
		Assert.Equal(error, mapped.Error);
	}

	[Fact]
	public void Bind_WithSuccess_ChainsResults()
	{
		// Arrange
		var result = Result<int>.Success(5);

		// Act
		var bound = result.Bind(x =>
			x > 0
				? Result<string>.Success(x.ToString())
				: Result<string>.Failure(Error.ValidationError("Must be positive"))
		);

		// Assert
		Assert.True(bound.IsSuccess);
		Assert.Equal("5", bound.Value);
	}

	[Fact]
	public void Bind_WithFailure_PropagatesError()
	{
		// Arrange
		var error = Error.ValidationError("Test");
		var result = Result<int>.Failure(error);

		// Act
		var bound = result.Bind(x => Result<string>.Success(x.ToString()));

		// Assert
		Assert.True(bound.IsFailure);
		Assert.Equal(error, bound.Error);
	}

	[Fact]
	public void Bind_WithSuccessButBinderFails_ReturnsBinderError()
	{
		// Arrange
		var result = Result<int>.Success(-5);
		var binderError = Error.ValidationError("Must be positive");

		// Act
		var bound = result.Bind(x =>
			x > 0
				? Result<string>.Success(x.ToString())
				: Result<string>.Failure(binderError)
		);

		// Assert
		Assert.True(bound.IsFailure);
		Assert.Equal(binderError, bound.Error);
	}

	[Fact]
	public void Tap_WithSuccess_ExecutesAction()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var actionExecuted = false;

		// Act
		var tapped = result.Tap(x => actionExecuted = true);

		// Assert
		Assert.True(actionExecuted);
		Assert.True(tapped.IsSuccess);
		Assert.Equal(42, tapped.Value);
	}

	[Fact]
	public void Tap_WithFailure_DoesNotExecuteAction()
	{
		// Arrange
		var error = Error.ValidationError("Test");
		var result = Result<int>.Failure(error);
		var actionExecuted = false;

		// Act
		var tapped = result.Tap(x => actionExecuted = true);

		// Assert
		Assert.False(actionExecuted);
		Assert.True(tapped.IsFailure);
	}

	[Fact]
	public void TapError_WithFailure_ExecutesAction()
	{
		// Arrange
		var error = Error.ValidationError("Test");
		var result = Result<int>.Failure(error);
		Error? capturedError = null;

		// Act
		var tapped = result.TapError(e => capturedError = e);

		// Assert
		Assert.Equal(error, capturedError);
		Assert.True(tapped.IsFailure);
	}

	[Fact]
	public void Ensure_WithValidPredicate_ReturnsSuccess()
	{
		// Arrange
		var result = Result<int>.Success(10);

		// Act
		var ensured = result.Ensure(x => x > 5, Error.ValidationError("Must be > 5"));

		// Assert
		Assert.True(ensured.IsSuccess);
		Assert.Equal(10, ensured.Value);
	}

	[Fact]
	public void Ensure_WithInvalidPredicate_ReturnsFailure()
	{
		// Arrange
		var result = Result<int>.Success(3);
		var error = Error.ValidationError("Must be > 5");

		// Act
		var ensured = result.Ensure(x => x > 5, error);

		// Assert
		Assert.True(ensured.IsFailure);
		Assert.Equal(error, ensured.Error);
	}

	[Fact]
	public void GetValueOrDefault_WithSuccess_ReturnsValue()
	{
		// Arrange
		var result = Result<int>.Success(42);

		// Act
		var value = result.GetValueOrDefault(0);

		// Assert
		Assert.Equal(42, value);
	}

	[Fact]
	public void GetValueOrDefault_WithFailure_ReturnsDefault()
	{
		// Arrange
		var result = Result<int>.Failure(Error.ValidationError("Test"));

		// Act
		var value = result.GetValueOrDefault(99);

		// Assert
		Assert.Equal(99, value);
	}

	[Fact]
	public void GetValueOrThrow_WithSuccess_ReturnsValue()
	{
		// Arrange
		var result = Result<int>.Success(42);

		// Act
		var value = result.GetValueOrThrow();

		// Assert
		Assert.Equal(42, value);
	}

	[Fact]
	public void GetValueOrThrow_WithFailure_ThrowsException()
	{
		// Arrange
		var result = Result<int>.Failure(Error.ValidationError("Test"));

		// Act & Assert
		Assert.Throws<InvalidOperationException>(() => result.GetValueOrThrow());
	}

	[Fact]
	public void ImplicitConversion_FromValue_CreatesSuccess()
	{
		// Act
		Result<int> result = 42;

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(42, result.Value);
	}

	[Fact]
	public void ImplicitConversion_FromError_CreatesFailure()
	{
		// Arrange
		var error = Error.ValidationError("Test");

		// Act
		Result<int> result = error;

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);
	}

	[Fact]
	public void ChainedOperations_WorkCorrectly()
	{
		// Arrange & Act
		var result = Result<int>.Success(5)
			.Map(x => x * 2)              // 10
			.Ensure(x => x > 5, Error.ValidationError("Must be > 5"))
			.Bind(x => Result<string>.Success($"Value: {x}"))
			.Tap(x => { /* side effect */ });

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal("Value: 10", result.Value);
	}

	// ========== ORELSE TESTS ==========

	[Fact]
	public void OrElse_WithAlternativeResult_ReturnsOriginalOnSuccess()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var alternative = Result<int>.Success(99);

		// Act
		var finalResult = result.OrElse(alternative);

		// Assert
		Assert.True(finalResult.IsSuccess);
		Assert.Equal(42, finalResult.Value);
	}

	[Fact]
	public void OrElse_WithAlternativeResult_ReturnsAlternativeOnFailure()
	{
		// Arrange
		var error = Error.ValidationError("First error");
		var result = Result<int>.Failure(error);
		var alternative = Result<int>.Success(99);

		// Act
		var finalResult = result.OrElse(alternative);

		// Assert
		Assert.True(finalResult.IsSuccess);
		Assert.Equal(99, finalResult.Value);
	}

	[Fact]
	public void OrElse_WithAlternativeFunc_ReturnsOriginalOnSuccess()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var funcCalled = false;

		// Act
		var finalResult = result.OrElse(() =>
		{
			funcCalled = true;
			return Result<int>.Success(99);
		});

		// Assert
		Assert.True(finalResult.IsSuccess);
		Assert.Equal(42, finalResult.Value);
		Assert.False(funcCalled); // Lazy evaluation - should not be called
	}

	[Fact]
	public void OrElse_WithAlternativeFunc_ReturnsAlternativeOnFailure()
	{
		// Arrange
		var error = Error.ValidationError("First error");
		var result = Result<int>.Failure(error);
		var funcCalled = false;

		// Act
		var finalResult = result.OrElse(() =>
		{
			funcCalled = true;
			return Result<int>.Success(99);
		});

		// Assert
		Assert.True(finalResult.IsSuccess);
		Assert.Equal(99, finalResult.Value);
		Assert.True(funcCalled);
	}

	[Fact]
	public void OrElse_ChainedAlternatives_ReturnsFirstSuccess()
	{
		// Arrange
		var error1 = Error.NotFoundError("First not found");
		var error2 = Error.NotFoundError("Second not found");
		var result = Result<int>.Failure(error1);

		// Act
		var finalResult = result
			.OrElse(() => Result<int>.Failure(error2))
			.OrElse(() => Result<int>.Success(99));

		// Assert
		Assert.True(finalResult.IsSuccess);
		Assert.Equal(99, finalResult.Value);
	}

	[Fact]
	public void OrElse_AllAlternativesFail_ReturnsLastError()
	{
		// Arrange
		var error1 = Error.NotFoundError("First not found");
		var error2 = Error.NotFoundError("Second not found");
		var error3 = Error.NotFoundError("Third not found");
		var result = Result<int>.Failure(error1);

		// Act
		var finalResult = result
			.OrElse(() => Result<int>.Failure(error2))
			.OrElse(() => Result<int>.Failure(error3));

		// Assert
		Assert.True(finalResult.IsFailure);
		Assert.Equal(error3, finalResult.Error);
	}

	[Fact]
	public void OrElse_InComplexChain_WorksCorrectly()
	{
		// Arrange
		Result<int> GetFromPrimary() => Error.NotFoundError("Not in primary");
		Result<int> GetFromSecondary() => Error.NotFoundError("Not in secondary");
		Result<int> GetFromFallback() => Result<int>.Success(42);

		// Act
		var result = GetFromPrimary()
			.OrElse(() => GetFromSecondary())
			.OrElse(() => GetFromFallback())
			.Map(x => x * 2)
			.Bind(x => x > 50
				? Result<string>.Success($"Large: {x}")
				: Result<string>.Success($"Small: {x}"));

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal("Large: 84", result.Value);  // 42 * 2 = 84, which is > 50
	}
}
