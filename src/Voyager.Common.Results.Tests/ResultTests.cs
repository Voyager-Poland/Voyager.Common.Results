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
}
