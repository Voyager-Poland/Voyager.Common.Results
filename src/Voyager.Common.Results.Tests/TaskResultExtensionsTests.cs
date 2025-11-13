using Voyager.Common.Results;
using Voyager.Common.Results.Extensions;

namespace Voyager.Common.Results.Tests;

public class TaskResultExtensionsTests
{
	// ========== MAP ASYNC TESTS ==========

	[Fact]
	public async Task MapAsync_TaskResultWithSyncMapper_TransformsValue()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(5));

		// Act
		var mapped = await resultTask.MapAsync(x => x * 2);

		// Assert
		Assert.True(mapped.IsSuccess);
		Assert.Equal(10, mapped.Value);
	}

	[Fact]
	public async Task MapAsync_TaskResultWithSyncMapper_PropagatesError()
	{
		// Arrange
		var error = Error.ValidationError("Test error");
		var resultTask = Task.FromResult(Result<int>.Failure(error));

		// Act
		var mapped = await resultTask.MapAsync(x => x * 2);

		// Assert
		Assert.True(mapped.IsFailure);
		Assert.Equal(error, mapped.Error);
	}

	[Fact]
	public async Task MapAsync_ResultWithAsyncMapper_TransformsValue()
	{
		// Arrange
		var result = Result<int>.Success(5);

		// Act
		var mapped = await result.MapAsync(async x =>
		{
			await Task.Delay(10);
			return x * 2;
		});

		// Assert
		Assert.True(mapped.IsSuccess);
		Assert.Equal(10, mapped.Value);
	}

	[Fact]
	public async Task MapAsync_ResultWithAsyncMapper_PropagatesError()
	{
		// Arrange
		var error = Error.ValidationError("Test error");
		var result = Result<int>.Failure(error);

		// Act
		var mapped = await result.MapAsync(async x =>
		{
			await Task.Delay(10);
			return x * 2;
		});

		// Assert
		Assert.True(mapped.IsFailure);
		Assert.Equal(error, mapped.Error);
	}

	[Fact]
	public async Task MapAsync_TaskResultWithAsyncMapper_TransformsValue()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(5));

		// Act
		var mapped = await resultTask.MapAsync(async x =>
		{
			await Task.Delay(10);
			return x * 2;
		});

		// Assert
		Assert.True(mapped.IsSuccess);
		Assert.Equal(10, mapped.Value);
	}

	// ========== BIND ASYNC TESTS ==========

	[Fact]
	public async Task BindAsync_TaskResultWithSyncBinder_ChainsCorrectly()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(5));

		// Act
		var bound = await resultTask.BindAsync(x =>
			x > 0
				? Result<string>.Success(x.ToString())
				: Result<string>.Failure(Error.ValidationError("Must be positive"))
		);

		// Assert
		Assert.True(bound.IsSuccess);
		Assert.Equal("5", bound.Value);
	}

	[Fact]
	public async Task BindAsync_TaskResultWithSyncBinder_PropagatesError()
	{
		// Arrange
		var error = Error.ValidationError("Test error");
		var resultTask = Task.FromResult(Result<int>.Failure(error));

		// Act
		var bound = await resultTask.BindAsync(x => Result<string>.Success(x.ToString()));

		// Assert
		Assert.True(bound.IsFailure);
		Assert.Equal(error, bound.Error);
	}

	[Fact]
	public async Task BindAsync_ResultWithAsyncBinder_ChainsCorrectly()
	{
		// Arrange
		var result = Result<int>.Success(5);

		// Act
		var bound = await result.BindAsync(async x =>
		{
			await Task.Delay(10);
			return x > 0
				? Result<string>.Success(x.ToString())
				: Result<string>.Failure(Error.ValidationError("Must be positive"));
		});

		// Assert
		Assert.True(bound.IsSuccess);
		Assert.Equal("5", bound.Value);
	}

	[Fact]
	public async Task BindAsync_ResultWithAsyncBinder_PropagatesError()
	{
		// Arrange
		var error = Error.ValidationError("Test error");
		var result = Result<int>.Failure(error);

		// Act
		var bound = await result.BindAsync(async x =>
		{
			await Task.Delay(10);
			return Result<string>.Success(x.ToString());
		});

		// Assert
		Assert.True(bound.IsFailure);
		Assert.Equal(error, bound.Error);
	}

	[Fact]
	public async Task BindAsync_TaskResultWithAsyncBinder_ChainsCorrectly()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(5));

		// Act
		var bound = await resultTask.BindAsync(async x =>
		{
			await Task.Delay(10);
			return Result<string>.Success(x.ToString());
		});

		// Assert
		Assert.True(bound.IsSuccess);
		Assert.Equal("5", bound.Value);
	}

	// ========== TAP ASYNC TESTS ==========

	[Fact]
	public async Task TapAsync_TaskResultWithSyncAction_ExecutesAction()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(42));
		var actionExecuted = false;

		// Act
		var tapped = await resultTask.TapAsync(x => actionExecuted = true);

		// Assert
		Assert.True(actionExecuted);
		Assert.True(tapped.IsSuccess);
		Assert.Equal(42, tapped.Value);
	}

	[Fact]
	public async Task TapAsync_TaskResultWithSyncAction_DoesNotExecuteOnFailure()
	{
		// Arrange
		var error = Error.ValidationError("Test error");
		var resultTask = Task.FromResult(Result<int>.Failure(error));
		var actionExecuted = false;

		// Act
		var tapped = await resultTask.TapAsync(x => actionExecuted = true);

		// Assert
		Assert.False(actionExecuted);
		Assert.True(tapped.IsFailure);
	}

	[Fact]
	public async Task TapAsync_ResultWithAsyncAction_ExecutesAction()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var actionExecuted = false;

		// Act
		var tapped = await result.TapAsync(async x =>
		{
			await Task.Delay(10);
			actionExecuted = true;
		});

		// Assert
		Assert.True(actionExecuted);
		Assert.True(tapped.IsSuccess);
		Assert.Equal(42, tapped.Value);
	}

	[Fact]
	public async Task TapAsync_ResultWithAsyncAction_DoesNotExecuteOnFailure()
	{
		// Arrange
		var error = Error.ValidationError("Test error");
		var result = Result<int>.Failure(error);
		var actionExecuted = false;

		// Act
		var tapped = await result.TapAsync(async x =>
		{
			await Task.Delay(10);
			actionExecuted = true;
		});

		// Assert
		Assert.False(actionExecuted);
		Assert.True(tapped.IsFailure);
	}

	[Fact]
	public async Task TapAsync_TaskResultWithAsyncAction_ExecutesAction()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(42));
		var actionExecuted = false;

		// Act
		var tapped = await resultTask.TapAsync(async x =>
		{
			await Task.Delay(10);
			actionExecuted = true;
		});

		// Assert
		Assert.True(actionExecuted);
		Assert.True(tapped.IsSuccess);
		Assert.Equal(42, tapped.Value);
	}

	// ========== MATCH ASYNC TESTS ==========

	[Fact]
	public async Task MatchAsync_TaskResultWithSyncHandlers_CallsOnSuccess()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(42));

		// Act
		var output = await resultTask.MatchAsync(
			onSuccess: x => $"Value: {x}",
			onFailure: err => $"Error: {err.Message}"
		);

		// Assert
		Assert.Equal("Value: 42", output);
	}

	[Fact]
	public async Task MatchAsync_TaskResultWithSyncHandlers_CallsOnFailure()
	{
		// Arrange
		var error = Error.ValidationError("Test error");
		var resultTask = Task.FromResult(Result<int>.Failure(error));

		// Act
		var output = await resultTask.MatchAsync(
			onSuccess: x => $"Value: {x}",
			onFailure: err => $"Error: {err.Message}"
		);

		// Assert
		Assert.Equal("Error: Test error", output);
	}

	[Fact]
	public async Task MatchAsync_ResultWithAsyncHandlers_CallsOnSuccess()
	{
		// Arrange
		var result = Result<int>.Success(42);

		// Act
		var output = await result.MatchAsync(
			onSuccess: async x =>
			{
				await Task.Delay(10);
				return $"Value: {x}";
			},
			onFailure: async err =>
			{
				await Task.Delay(10);
				return $"Error: {err.Message}";
			}
		);

		// Assert
		Assert.Equal("Value: 42", output);
	}

	[Fact]
	public async Task MatchAsync_ResultWithAsyncHandlers_CallsOnFailure()
	{
		// Arrange
		var error = Error.ValidationError("Test error");
		var result = Result<int>.Failure(error);

		// Act
		var output = await result.MatchAsync(
			onSuccess: async x =>
			{
				await Task.Delay(10);
				return $"Value: {x}";
			},
			onFailure: async err =>
			{
				await Task.Delay(10);
				return $"Error: {err.Message}";
			}
		);

		// Assert
		Assert.Equal("Error: Test error", output);
	}

	[Fact]
	public async Task MatchAsync_TaskResultWithAsyncHandlers_CallsOnSuccess()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(42));

		// Act
		var output = await resultTask.MatchAsync(
			onSuccess: async x =>
			{
				await Task.Delay(10);
				return $"Value: {x}";
			},
			onFailure: async err =>
			{
				await Task.Delay(10);
				return $"Error: {err.Message}";
			}
		);

		// Assert
		Assert.Equal("Value: 42", output);
	}

	// ========== ENSURE ASYNC TESTS ==========

	[Fact]
	public async Task EnsureAsync_TaskResultWithSyncPredicate_PassesValidation()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(10));

		// Act
		var ensured = await resultTask.EnsureAsync(
			x => x > 5,
			Error.ValidationError("Must be > 5")
		);

		// Assert
		Assert.True(ensured.IsSuccess);
		Assert.Equal(10, ensured.Value);
	}

	[Fact]
	public async Task EnsureAsync_TaskResultWithSyncPredicate_FailsValidation()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(3));
		var error = Error.ValidationError("Must be > 5");

		// Act
		var ensured = await resultTask.EnsureAsync(x => x > 5, error);

		// Assert
		Assert.True(ensured.IsFailure);
		Assert.Equal(error, ensured.Error);
	}

	[Fact]
	public async Task EnsureAsync_ResultWithAsyncPredicate_PassesValidation()
	{
		// Arrange
		var result = Result<int>.Success(10);

		// Act
		var ensured = await result.EnsureAsync(
			async x =>
			{
				await Task.Delay(10);
				return x > 5;
			},
			Error.ValidationError("Must be > 5")
		);

		// Assert
		Assert.True(ensured.IsSuccess);
		Assert.Equal(10, ensured.Value);
	}

	[Fact]
	public async Task EnsureAsync_ResultWithAsyncPredicate_FailsValidation()
	{
		// Arrange
		var result = Result<int>.Success(3);
		var error = Error.ValidationError("Must be > 5");

		// Act
		var ensured = await result.EnsureAsync(
			async x =>
			{
				await Task.Delay(10);
				return x > 5;
			},
			error
		);

		// Assert
		Assert.True(ensured.IsFailure);
		Assert.Equal(error, ensured.Error);
	}

	[Fact]
	public async Task EnsureAsync_TaskResultWithAsyncPredicate_PassesValidation()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(10));

		// Act
		var ensured = await resultTask.EnsureAsync(
			async x =>
			{
				await Task.Delay(10);
				return x > 5;
			},
			Error.ValidationError("Must be > 5")
		);

		// Assert
		Assert.True(ensured.IsSuccess);
		Assert.Equal(10, ensured.Value);
	}

	[Fact]
	public async Task EnsureAsync_WithFailureResult_DoesNotExecutePredicate()
	{
		// Arrange
		var error = Error.ValidationError("Original error");
		var result = Result<int>.Failure(error);
		var predicateExecuted = false;

		// Act
		var ensured = await result.EnsureAsync(
			async x =>
			{
				predicateExecuted = true;
				await Task.Delay(10);
				return true;
			},
			Error.ValidationError("Should not happen")
		);

		// Assert
		Assert.False(predicateExecuted);
		Assert.True(ensured.IsFailure);
		Assert.Equal(error, ensured.Error);
	}

	// ========== ORELSE ASYNC TESTS ==========

	[Fact]
	public async Task OrElseAsync_TaskResultWithAlternativeResult_ReturnsOriginalOnSuccess()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(42));
		var alternative = Result<int>.Success(99);

		// Act
		var result = await resultTask.OrElseAsync(alternative);

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(42, result.Value);
	}

	[Fact]
	public async Task OrElseAsync_TaskResultWithAlternativeResult_ReturnsAlternativeOnFailure()
	{
		// Arrange
		var error = Error.ValidationError("First error");
		var resultTask = Task.FromResult(Result<int>.Failure(error));
		var alternative = Result<int>.Success(99);

		// Act
		var result = await resultTask.OrElseAsync(alternative);

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(99, result.Value);
	}

	[Fact]
	public async Task OrElseAsync_TaskResultWithSyncAlternativeFunc_ReturnsOriginalOnSuccess()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(42));
		var funcCalled = false;

		// Act
		var result = await resultTask.OrElseAsync(() =>
		{
			funcCalled = true;
			return Result<int>.Success(99);
		});

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(42, result.Value);
		Assert.False(funcCalled); // Lazy evaluation - should not be called
	}

	[Fact]
	public async Task OrElseAsync_TaskResultWithSyncAlternativeFunc_ReturnsAlternativeOnFailure()
	{
		// Arrange
		var error = Error.ValidationError("First error");
		var resultTask = Task.FromResult(Result<int>.Failure(error));
		var funcCalled = false;

		// Act
		var result = await resultTask.OrElseAsync(() =>
		{
			funcCalled = true;
			return Result<int>.Success(99);
		});

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(99, result.Value);
		Assert.True(funcCalled);
	}

	[Fact]
	public async Task OrElseAsync_ResultWithAsyncAlternativeFunc_ReturnsOriginalOnSuccess()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var funcCalled = false;

		// Act
		var finalResult = await result.OrElseAsync(async () =>
		{
			funcCalled = true;
			await Task.Delay(10);
			return Result<int>.Success(99);
		});

		// Assert
		Assert.True(finalResult.IsSuccess);
		Assert.Equal(42, finalResult.Value);
		Assert.False(funcCalled); // Lazy evaluation
	}

	[Fact]
	public async Task OrElseAsync_ResultWithAsyncAlternativeFunc_ReturnsAlternativeOnFailure()
	{
		// Arrange
		var error = Error.ValidationError("First error");
		var result = Result<int>.Failure(error);
		var funcCalled = false;

		// Act
		var finalResult = await result.OrElseAsync(async () =>
		{
			funcCalled = true;
			await Task.Delay(10);
			return Result<int>.Success(99);
		});

		// Assert
		Assert.True(finalResult.IsSuccess);
		Assert.Equal(99, finalResult.Value);
		Assert.True(funcCalled);
	}

	[Fact]
	public async Task OrElseAsync_TaskResultWithAsyncAlternativeFunc_ReturnsOriginalOnSuccess()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(42));
		var funcCalled = false;

		// Act
		var result = await resultTask.OrElseAsync(async () =>
		{
			funcCalled = true;
			await Task.Delay(10);
			return Result<int>.Success(99);
		});

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(42, result.Value);
		Assert.False(funcCalled);
	}

	[Fact]
	public async Task OrElseAsync_TaskResultWithAsyncAlternativeFunc_ReturnsAlternativeOnFailure()
	{
		// Arrange
		var error = Error.ValidationError("First error");
		var resultTask = Task.FromResult(Result<int>.Failure(error));
		var funcCalled = false;

		// Act
		var result = await resultTask.OrElseAsync(async () =>
		{
			funcCalled = true;
			await Task.Delay(10);
			return Result<int>.Success(99);
		});

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(99, result.Value);
		Assert.True(funcCalled);
	}

	[Fact]
	public async Task OrElseAsync_ChainedAlternatives_ReturnsFirstSuccess()
	{
		// Arrange
		var error1 = Error.NotFoundError("First not found");
		var error2 = Error.NotFoundError("Second not found");
		var resultTask = Task.FromResult(Result<int>.Failure(error1));

		// Act
		var result = await resultTask
			.OrElseAsync(() => Result<int>.Failure(error2))
			.OrElseAsync(async () =>
			{
				await Task.Delay(10);
				return Result<int>.Success(99);
			});

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(99, result.Value);
	}

	[Fact]
	public async Task OrElseAsync_AllAlternativesFail_ReturnsLastError()
	{
		// Arrange
		var error1 = Error.NotFoundError("First not found");
		var error2 = Error.NotFoundError("Second not found");
		var error3 = Error.NotFoundError("Third not found");
		var resultTask = Task.FromResult(Result<int>.Failure(error1));

		// Act
		var result = await resultTask
			.OrElseAsync(() => Result<int>.Failure(error2))
			.OrElseAsync(async () =>
			{
				await Task.Delay(10);
				return Result<int>.Failure(error3);
			});

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(error3, result.Error);
	}

	// ========== COMPLEX CHAIN TESTS ==========

	[Fact]
	public async Task ComplexAsyncChain_WithOrElse_WorksCorrectly()
	{
		// Arrange
		async Task<Result<int>> GetFromPrimaryAsync()
		{
			await Task.Delay(10);
			return Error.NotFoundError("Not in primary");
		}

		async Task<Result<int>> GetFromSecondaryAsync()
		{
			await Task.Delay(10);
			return Error.NotFoundError("Not in secondary");
		}

		async Task<Result<int>> GetFromFallbackAsync()
		{
			await Task.Delay(10);
			return Result<int>.Success(42);
		}

		// Act
		var result = await GetFromPrimaryAsync()
			.OrElseAsync(() => GetFromSecondaryAsync())
			.OrElseAsync(() => GetFromFallbackAsync())
			.MapAsync(x => x * 2)
			.BindAsync(async x =>
			{
				await Task.Delay(10);
				return x > 50
					? Result<string>.Success($"Large: {x}")
					: Result<string>.Success($"Small: {x}");
			});

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal("Large: 84", result.Value);  // 42 * 2 = 84, which is > 50
	}

	[Fact]
	public async Task AsyncChain_MixingSyncAndAsync_WorksCorrectly()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(5));

		// Act
		var result = await resultTask
			.MapAsync(x => x * 2)              // Sync mapper
			.BindAsync(async x =>              // Async binder
			{
				await Task.Delay(10);
				return Result<int>.Success(x + 10);
			})
			.TapAsync(async x =>               // Async tap
			{
				await Task.Delay(10);
			})
			.EnsureAsync(                      // Sync predicate
				x => x > 15,
				Error.ValidationError("Must be > 15")
			)
			.MapAsync(async x =>               // Async mapper
			{
				await Task.Delay(10);
				return x.ToString();
			});

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal("20", result.Value);
	}
}
