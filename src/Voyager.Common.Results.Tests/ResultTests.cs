using Voyager.Common.Results;

namespace Voyager.Common.Results.Tests;

public class ResultTests
{
	[Fact]
	public void Success_CreatesSuccessfulResult()
	{
		// Act
		var result = Result.Success();

		// Assert
		Assert.True(result.IsSuccess);
		Assert.False(result.IsFailure);
		Assert.True(result.Error.Type == ErrorType.None);
	}

	[Fact]
	public void Failure_CreatesFailedResult()
	{
		// Arrange
		var error = Error.ValidationError("Test error");

		// Act
		var result = Result.Failure(error);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);
	}

	[Fact]
	public void Match_WithSuccess_CallsOnSuccess()
	{
		// Arrange
		var result = Result.Success();
		var successCalled = false;
		var failureCalled = false;

		// Act
		result.Match(
			onSuccess: () => { successCalled = true; return 1; },
			onFailure: _ => { failureCalled = true; return 0; }
		);

		// Assert
		Assert.True(successCalled);
		Assert.False(failureCalled);
	}

	[Fact]
	public void Match_WithFailure_CallsOnFailure()
	{
		// Arrange
		var error = Error.ValidationError("Test error");
		var result = Result.Failure(error);
		var successCalled = false;
		var failureCalled = false;

		// Act
		result.Match(
			onSuccess: () => { successCalled = true; return 1; },
			onFailure: _ => { failureCalled = true; return 0; }
		);

		// Assert
		Assert.False(successCalled);
		Assert.True(failureCalled);
	}

	[Fact]
	public void ImplicitConversion_FromError_CreatesFailure()
	{
		// Arrange
		var error = Error.ValidationError("Test");

		// Act
		Result result = error;

		// Assert
		Assert.False(result.IsSuccess);
		Assert.Equal(error, result.Error);
	}

	[Fact]
	public void Map_WithSuccess_TransformsToResultWithValue()
	{
		// Arrange
		var result = Result.Success();

		// Act
		var mapped = result.Map(() => 42);

		// Assert
		Assert.True(mapped.IsSuccess);
		Assert.Equal(42, mapped.Value);
	}

	[Fact]
	public void Map_WithFailure_PropagatesError()
	{
		// Arrange
		var error = Error.ValidationError("Test error");
		var result = Result.Failure(error);

		// Act
		var mapped = result.Map(() => 42);

		// Assert
		Assert.False(mapped.IsSuccess);
		Assert.Equal(error, mapped.Error);
	}

	// ========== TRY TESTS ==========

	[Fact]
	public void Try_WithSuccessfulAction_ReturnsSuccess()
	{
		// Arrange
		var executed = false;

		// Act
		var result = Result.Try(() => executed = true);

		// Assert
		Assert.True(result.IsSuccess);
		Assert.True(executed);
	}

	[Fact]
	public void Try_WithExceptionThrowingAction_ReturnsFailure()
	{
		// Act
		var result = Result.Try(() => throw new InvalidOperationException("Test exception"));

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
		var result = Result.Try(
			() => throw new IOException("File not found"),
			ex => ex is IOException
				? Error.UnavailableError("File system error")
				: Error.UnexpectedError(ex.Message));

		// Assert
		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorType.Unavailable, result.Error.Type);
		Assert.Equal("File system error", result.Error.Message);
	}

	// ========== TAP TESTS ==========

	[Fact]
	public void Tap_WithSuccess_ExecutesAction()
	{
		// Arrange
		var result = Result.Success();
		var actionExecuted = false;

		// Act
		var tapped = result.Tap(() => actionExecuted = true);

		// Assert
		Assert.True(actionExecuted);
		Assert.True(tapped.IsSuccess);
	}

	[Fact]
	public void Tap_WithFailure_DoesNotExecuteAction()
	{
		// Arrange
		var error = Error.ValidationError("Test error");
		var result = Result.Failure(error);
		var actionExecuted = false;

		// Act
		var tapped = result.Tap(() => actionExecuted = true);

		// Assert
		Assert.False(actionExecuted);
		Assert.True(tapped.IsFailure);
		Assert.Equal(error, tapped.Error);
	}

	[Fact]
	public void TapError_WithSuccess_DoesNotExecuteAction()
	{
		// Arrange
		var result = Result.Success();
		Error? capturedError = null;

		// Act
		var tapped = result.TapError(e => capturedError = e);

		// Assert
		Assert.Null(capturedError);
		Assert.True(tapped.IsSuccess);
	}

	[Fact]
	public void TapError_WithFailure_ExecutesAction()
	{
		// Arrange
		var error = Error.ValidationError("Test error");
		var result = Result.Failure(error);
		Error? capturedError = null;

		// Act
		var tapped = result.TapError(e => capturedError = e);

		// Assert
		Assert.NotNull(capturedError);
		Assert.Equal(error, capturedError);
		Assert.True(tapped.IsFailure);
	}

	[Fact]
	public void Tap_ChainWithOtherOperations()
	{
		// Arrange
		var tapCalled = false;
		var tapErrorCalled = false;

		// Act
		var result = Result.Success()
			.Tap(() => tapCalled = true)
			.TapError(e => tapErrorCalled = true)
			.Bind(() => Result.Success());

		// Assert
		Assert.True(tapCalled);
		Assert.False(tapErrorCalled);
		Assert.True(result.IsSuccess);
	}

	// ========== MAPERROR TESTS ==========

	[Fact]
	public void MapError_WithSuccess_DoesNotTransformError()
	{
		// Arrange
		var result = Result.Success();

		// Act
		var mapped = result.MapError(e => Error.DatabaseError("Transformed"));

		// Assert
		Assert.True(mapped.IsSuccess);
	}

	[Fact]
	public void MapError_WithFailure_TransformsError()
	{
		// Arrange
		var originalError = Error.ValidationError("VAL_001", "Original");
		var result = Result.Failure(originalError);

		// Act
		var mapped = result.MapError(e => Error.DatabaseError("DB_" + e.Code, "Transformed: " + e.Message));

		// Assert
		Assert.True(mapped.IsFailure);
		Assert.Equal(ErrorType.Database, mapped.Error.Type);
		Assert.Equal("DB_VAL_001", mapped.Error.Code);
		Assert.Equal("Transformed: Original", mapped.Error.Message);
	}

	[Fact]
	public void MapError_ChainWithOtherOperations()
	{
		// Arrange
		var result = Result.Failure(Error.NotFoundError("NF_001", "User not found"));

		// Act
		var mapped = result
			.MapError(e => Error.BusinessError("USER_" + e.Code, e.Message))
			.TapError(e => { /* log error */ });

		// Assert
		Assert.True(mapped.IsFailure);
		Assert.Equal(ErrorType.Business, mapped.Error.Type);
		Assert.Equal("USER_NF_001", mapped.Error.Code);
	}

	// ========== BIND TESTS ==========

	[Fact]
	public void Bind_WithSuccessResult_ExecutesBinder()
	{
		// Arrange
		var result = Result.Success();
		var binderExecuted = false;

		// Act
		var boundResult = result.Bind(() =>
		{
			binderExecuted = true;
			return Result.Success();
		});

		// Assert
		Assert.True(binderExecuted);
		Assert.True(boundResult.IsSuccess);
	}

	[Fact]
	public void Bind_WithFailureResult_DoesNotExecuteBinder()
	{
		// Arrange
		var error = Error.ValidationError("Original error");
		var result = Result.Failure(error);
		var binderExecuted = false;

		// Act
		var boundResult = result.Bind(() =>
		{
			binderExecuted = true;
			return Result.Success();
		});

		// Assert
		Assert.False(binderExecuted);
		Assert.True(boundResult.IsFailure);
		Assert.Equal(error, boundResult.Error);
	}

	[Fact]
	public void Bind_WithSuccessResult_CanReturnFailure()
	{
		// Arrange
		var result = Result.Success();
		var error = Error.BusinessError("Binder failed");

		// Act
		var boundResult = result.Bind(() => Result.Failure(error));

		// Assert
		Assert.True(boundResult.IsFailure);
		Assert.Equal(error, boundResult.Error);
	}

	[Fact]
	public void Bind_ChainMultipleOperations_PropagatesSuccess()
	{
		// Arrange
		var step1Called = false;
		var step2Called = false;
		var step3Called = false;

		// Act
		var result = Result.Success()
			.Bind(() =>
			{
				step1Called = true;
				return Result.Success();
			})
			.Bind(() =>
			{
				step2Called = true;
				return Result.Success();
			})
			.Bind(() =>
			{
				step3Called = true;
				return Result.Success();
			});

		// Assert
		Assert.True(step1Called);
		Assert.True(step2Called);
		Assert.True(step3Called);
		Assert.True(result.IsSuccess);
	}

	[Fact]
	public void Bind_ChainMultipleOperations_StopsAtFirstFailure()
	{
		// Arrange
		var step1Called = false;
		var step2Called = false;
		var step3Called = false;
		var error = Error.ValidationError("Step 2 failed");

		// Act
		var result = Result.Success()
			.Bind(() =>
			{
				step1Called = true;
				return Result.Success();
			})
			.Bind(() =>
			{
				step2Called = true;
				return Result.Failure(error);
			})
			.Bind(() =>
			{
				step3Called = true;
				return Result.Success();
			});

		// Assert
		Assert.True(step1Called);
		Assert.True(step2Called);
		Assert.False(step3Called);
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);
	}

	[Fact]
	public void BindGeneric_WithSuccessResult_ReturnsValueResult()
	{
		// Arrange
		var result = Result.Success();
		var expectedValue = 42;

		// Act
		var boundResult = result.Bind(() => Result<int>.Success(expectedValue));

		// Assert
		Assert.True(boundResult.IsSuccess);
		Assert.Equal(expectedValue, boundResult.Value);
	}

	[Fact]
	public void BindGeneric_WithFailureResult_PropagatesError()
	{
		// Arrange
		var error = Error.ValidationError("Original error");
		var result = Result.Failure(error);
		var binderExecuted = false;

		// Act
		var boundResult = result.Bind(() =>
		{
			binderExecuted = true;
			return Result<int>.Success(42);
		});

		// Assert
		Assert.False(binderExecuted);
		Assert.True(boundResult.IsFailure);
		Assert.Equal(error, boundResult.Error);
	}

	[Fact]
	public void BindGeneric_WithSuccessResult_CanReturnFailure()
	{
		// Arrange
		var result = Result.Success();
		var error = Error.NotFoundError("User not found");

		// Act
		var boundResult = result.Bind(() => Result<string>.Failure(error));

		// Assert
		Assert.True(boundResult.IsFailure);
		Assert.Equal(error, boundResult.Error);
	}

	[Fact]
	public void BindGeneric_ChainVoidToValueOperations()
	{
		// Arrange
		var validationCalled = false;
		var userId = 123;

		// Act
		var result = Result.Success()
			.Bind(() =>
			{
				validationCalled = true;
				return Result.Success();
			})
			.Bind(() => Result<int>.Success(userId))
			.Bind(id => Result<string>.Success($"User_{id}"));

		// Assert
		Assert.True(validationCalled);
		Assert.True(result.IsSuccess);
		Assert.Equal("User_123", result.Value);
	}

	// ========== FINALLY TESTS ==========

	[Fact]
	public void Finally_WithSuccess_ExecutesAction()
	{
		// Arrange
		var result = Result.Success();
		var actionExecuted = false;

		// Act
		var finalResult = result.Finally(() => actionExecuted = true);

		// Assert
		Assert.True(actionExecuted);
		Assert.True(finalResult.IsSuccess);
	}

	[Fact]
	public void Finally_WithFailure_ExecutesAction()
	{
		// Arrange
		var result = Result.Failure(Error.ValidationError("Test error"));
		var actionExecuted = false;

		// Act
		var finalResult = result.Finally(() => actionExecuted = true);

		// Assert
		Assert.True(actionExecuted);
		Assert.True(finalResult.IsFailure);
	}

	[Fact]
	public void Finally_ChainWithOtherOperations()
	{
		// Arrange
		var cleanupCalled = false;
		var processCalled = false;

		// Act
		var result = Result.Success()
			.Bind(() =>
			{
				processCalled = true;
				return Result.Success();
			})
			.Finally(() => cleanupCalled = true);

		// Assert
		Assert.True(processCalled);
		Assert.True(cleanupCalled);
		Assert.True(result.IsSuccess);
	}
}
