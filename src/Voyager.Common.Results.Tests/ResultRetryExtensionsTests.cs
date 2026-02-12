using Voyager.Common.Results;
using Voyager.Common.Results.Extensions;

namespace Voyager.Common.Results.Tests;

public class ResultRetryExtensionsTests
{
	[Fact]
	public async Task BindWithRetryAsync_WithDefaultPolicy_ReturnsSuccessImmediately()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var callCount = 0;

		// Act - uses default policy (no second parameter)
		var outcome = await result.BindWithRetryAsync(
			async value =>
			{
				callCount++;
				await Task.Delay(1);
				return Result<string>.Success(value.ToString());
			}
		);

		// Assert
		Assert.True(outcome.IsSuccess);
		Assert.Equal("42", outcome.Value);
		Assert.Equal(1, callCount);
	}

	[Fact]
	public async Task BindWithRetryAsync_DefaultPolicy_RetriesTransientErrors()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var callCount = 0;

		// Act - default policy should retry transient errors
		var outcome = await result.BindWithRetryAsync(
			async value =>
			{
				callCount++;
				await Task.Delay(1);

				if (callCount < 2)
					return Result<string>.Failure(Error.UnavailableError("Temporary failure"));

				return Result<string>.Success(value.ToString());
			}
		);

		// Assert
		Assert.True(outcome.IsSuccess);
		Assert.Equal("42", outcome.Value);
		Assert.Equal(2, callCount); // Should retry once
	}

	[Fact]
	public async Task BindWithRetryAsync_TaskOverload_WithDefaultPolicy_WorksCorrectly()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(42));
		var callCount = 0;

		// Act - Task overload with default policy
		var outcome = await resultTask.BindWithRetryAsync(
			async value =>
			{
				callCount++;
				await Task.Delay(1);

				if (callCount < 2)
					return Result<string>.Failure(Error.TimeoutError("Temporary timeout"));

				return Result<string>.Success(value.ToString());
			}
		);

		// Assert
		Assert.True(outcome.IsSuccess);
		Assert.Equal("42", outcome.Value);
		Assert.Equal(2, callCount);
	}

	[Fact]
	public async Task BindWithRetryAsync_WithSuccess_ReturnsSuccessImmediately()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var callCount = 0;

		// Act
		var outcome = await result.BindWithRetryAsync(
			async value =>
			{
				callCount++;
				await Task.Delay(1);
				return Result<string>.Success(value.ToString());
			},
			RetryPolicies.TransientErrors()
		);

		// Assert
		Assert.True(outcome.IsSuccess);
		Assert.Equal("42", outcome.Value);
		Assert.Equal(1, callCount); // Should only call once for success
	}

	[Fact]
	public async Task BindWithRetryAsync_WithFailureInput_DoesNotExecuteFunction()
	{
		// Arrange
		var error = Error.ValidationError("Invalid input");
		var result = Result<int>.Failure(error);
		var callCount = 0;

		// Act
		var outcome = await result.BindWithRetryAsync(
			async value =>
			{
				callCount++;
				await Task.Delay(1);
				return Result<string>.Success(value.ToString());
			},
			RetryPolicies.TransientErrors()
		);

		// Assert
		Assert.True(outcome.IsFailure);
		Assert.Equal(error, outcome.Error);
		Assert.Equal(0, callCount); // Should not execute function
	}

	[Fact]
	public async Task BindWithRetryAsync_TransientError_RetriesUntilSuccess()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var callCount = 0;

		// Act
		var outcome = await result.BindWithRetryAsync(
			async value =>
			{
				callCount++;
				await Task.Delay(1);

				// Fail first 2 times with transient error, succeed on 3rd
				if (callCount < 3)
					return Result<string>.Failure(Error.UnavailableError("Service temporarily down"));

				return Result<string>.Success(value.ToString());
			},
			RetryPolicies.TransientErrors(maxAttempts: 5)
		);

		// Assert
		Assert.True(outcome.IsSuccess);
		Assert.Equal("42", outcome.Value);
		Assert.Equal(3, callCount); // Should retry twice, succeed on 3rd
	}

	[Fact]
	public async Task BindWithRetryAsync_TransientError_RespectsMaxAttempts()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var callCount = 0;
		var expectedError = Error.TimeoutError("Operation timed out");

		// Act
		var outcome = await result.BindWithRetryAsync(
			async value =>
			{
				callCount++;
				await Task.Delay(1);
				return Result<string>.Failure(expectedError);
			},
			RetryPolicies.TransientErrors(maxAttempts: 3)
		);

		// Assert
		Assert.True(outcome.IsFailure);
		Assert.Equal(expectedError, outcome.Error); // Should preserve original error
		Assert.Equal(3, callCount); // Should attempt exactly 3 times
	}

	[Fact]
	public async Task BindWithRetryAsync_PermanentError_DoesNotRetry()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var callCount = 0;
		var expectedError = Error.NotFoundError("User not found");

		// Act
		var outcome = await result.BindWithRetryAsync(
			async value =>
			{
				callCount++;
				await Task.Delay(1);
				return Result<string>.Failure(expectedError);
			},
			RetryPolicies.TransientErrors(maxAttempts: 5)
		);

		// Assert
		Assert.True(outcome.IsFailure);
		Assert.Equal(expectedError, outcome.Error);
		Assert.Equal(1, callCount); // Should only try once for permanent error
	}

	[Fact]
	public async Task BindWithRetryAsync_PreservesOriginalError()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var originalError = Error.UnavailableError("Database connection failed");

		// Act
		var outcome = await result.BindWithRetryAsync(
			async value =>
			{
				await Task.Delay(1);
				return Result<string>.Failure(originalError);
			},
			RetryPolicies.TransientErrors(maxAttempts: 3)
		);

		// Assert
		Assert.True(outcome.IsFailure);
		Assert.Equal(originalError, outcome.Error); // CRITICAL: Must preserve exact error
		Assert.Equal(ErrorType.Unavailable, outcome.Error.Type);
		Assert.Equal("Database connection failed", outcome.Error.Message);
	}

	[Fact]
	public async Task BindWithRetryAsync_ExponentialBackoff_IncreasesDelay()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var callCount = 0;
		var delays = new List<TimeSpan>();
		var lastCallTime = DateTime.UtcNow;

		// Act
		var outcome = await result.BindWithRetryAsync(
			async value =>
			{
				callCount++;
				var now = DateTime.UtcNow;
				if (callCount > 1)
					delays.Add(now - lastCallTime);
				lastCallTime = now;

				await Task.Delay(1);

				if (callCount < 4)
					return Result<string>.Failure(Error.TimeoutError("Timeout"));

				return Result<string>.Success(value.ToString());
			},
			RetryPolicies.TransientErrors(maxAttempts: 5, baseDelayMs: 100)
		);

		// Assert
		Assert.True(outcome.IsSuccess);
		Assert.Equal(4, callCount);
		Assert.Equal(3, delays.Count); // 3 delays between 4 attempts

		// Verify exponential backoff (with some tolerance for timing)
		// Attempt 1->2: ~100ms, Attempt 2->3: ~200ms, Attempt 3->4: ~400ms
		Assert.True(delays[0].TotalMilliseconds >= 80); // ~100ms (allow 20% tolerance)
		Assert.True(delays[1].TotalMilliseconds >= 180); // ~200ms
		Assert.True(delays[2].TotalMilliseconds >= 380); // ~400ms
	}

	[Fact]
	public async Task BindWithRetryAsync_TaskOverload_WorksCorrectly()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(42));
		var callCount = 0;

		// Act
		var outcome = await resultTask.BindWithRetryAsync(
			async value =>
			{
				callCount++;
				await Task.Delay(1);

				if (callCount < 2)
					return Result<string>.Failure(Error.UnavailableError("Temporary failure"));

				return Result<string>.Success(value.ToString());
			},
			RetryPolicies.TransientErrors()
		);

		// Assert
		Assert.True(outcome.IsSuccess);
		Assert.Equal("42", outcome.Value);
		Assert.Equal(2, callCount);
	}

	[Fact]
	public async Task TransientErrors_OnlyRetriesUnavailableAndTimeout()
	{
		// Test all error types to ensure only Unavailable and Timeout are retried
		var errorTypes = new[]
		{
			(ErrorType.Validation, false, "Validation error"),
			(ErrorType.NotFound, false, "Not found"),
			(ErrorType.Unauthorized, false, "Unauthorized"),
			(ErrorType.Permission, false, "Permission denied"),
			(ErrorType.Conflict, false, "Conflict"),
			(ErrorType.Business, false, "Business rule"),
			(ErrorType.Database, false, "Database error"),
			(ErrorType.Unavailable, true, "Service unavailable"), // Should retry
			(ErrorType.Timeout, true, "Operation timeout"),        // Should retry
			(ErrorType.Cancelled, false, "Cancelled"),
			(ErrorType.Unexpected, false, "Unexpected error")
		};

		foreach (var (errorType, shouldRetry, message) in errorTypes)
		{
			// Arrange
			var result = Result<int>.Success(1);
			var callCount = 0;
			var error = new Error(errorType, "TEST", message);

			// Act
			var outcome = await result.BindWithRetryAsync(
				async value =>
				{
					callCount++;
					await Task.Delay(1);
					return Result<int>.Failure(error);
				},
				RetryPolicies.TransientErrors(maxAttempts: 3)
			);

			// Assert
			Assert.True(outcome.IsFailure);
			Assert.Equal(error, outcome.Error);

			if (shouldRetry)
				Assert.Equal(3, callCount); // Should retry max attempts
			else
				Assert.Equal(1, callCount); // Should not retry permanent errors
		}
	}

	[Fact]
	public async Task CustomPolicy_RespectsCustomLogic()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var callCount = 0;

		var customPolicy = RetryPolicies.Custom(
			maxAttempts: 5,
			shouldRetry: e => e.Code == "RATE_LIMIT",
			delayStrategy: attempt => 50 * attempt // Linear backoff: 50ms, 100ms, 150ms...
		);

		// Act
		var outcome = await result.BindWithRetryAsync(
			async value =>
			{
				callCount++;
				await Task.Delay(1);

				if (callCount < 3)
					return Result<string>.Failure(Error.ConflictError("RATE_LIMIT", "Rate limited"));

				return Result<string>.Success(value.ToString());
			},
			customPolicy
		);

		// Assert
		Assert.True(outcome.IsSuccess);
		Assert.Equal("42", outcome.Value);
		Assert.Equal(3, callCount);
	}

	[Fact]
	public async Task CustomPolicy_WithNonMatchingError_DoesNotRetry()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var callCount = 0;

		var customPolicy = RetryPolicies.Custom(
			maxAttempts: 5,
			shouldRetry: e => e.Code == "RATE_LIMIT",
			delayStrategy: attempt => 50
		);

		// Act
		var outcome = await result.BindWithRetryAsync(
			async value =>
			{
				callCount++;
				await Task.Delay(1);
				return Result<string>.Failure(Error.ValidationError("Invalid data"));
			},
			customPolicy
		);

		// Assert
		Assert.True(outcome.IsFailure);
		Assert.Equal(1, callCount); // Should not retry non-matching error
	}

	[Fact]
	public async Task DefaultPolicy_EquivalentToTransientErrors()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var callCount = 0;

		// Act
		var outcome = await result.BindWithRetryAsync(
			async value =>
			{
				callCount++;
				await Task.Delay(1);

				if (callCount < 2)
					return Result<string>.Failure(Error.UnavailableError("Temporary failure"));

				return Result<string>.Success(value.ToString());
			},
			RetryPolicies.Default()
		);

		// Assert
		Assert.True(outcome.IsSuccess);
		Assert.Equal("42", outcome.Value);
		Assert.Equal(2, callCount);
	}

	[Fact]
	public async Task BindWithRetryAsync_WithZeroDelay_WorksCorrectly()
	{
		// Arrange
		var result = Result<int>.Success(42);
		var callCount = 0;

		var policy = RetryPolicies.Custom(
			maxAttempts: 3,
			shouldRetry: e => e.Type == ErrorType.Unavailable,
			delayStrategy: _ => 0 // No delay
		);

		// Act
		var outcome = await result.BindWithRetryAsync(
			async value =>
			{
				callCount++;
				await Task.Delay(1);

				if (callCount < 3)
					return Result<string>.Failure(Error.UnavailableError("Temporary"));

				return Result<string>.Success(value.ToString());
			},
			policy
		);

		// Assert
		Assert.True(outcome.IsSuccess);
		Assert.Equal(3, callCount);
	}

	// ==================== OnRetryAttempt Callback Tests ====================

	[Fact]
	public async Task OnRetryAttempt_CalledForEachFailedAttempt()
	{
		// Arrange
		var attempts = new List<(int attempt, ErrorType errorType, int delay)>();
		var callCount = 0;

		var result = Result<int>.Success(1);

		// Act
		var outcome = await result.BindWithRetryAsync(
			async _ =>
			{
				callCount++;
				await Task.Delay(1);
				if (callCount < 3)
					return Result<string>.Failure(Error.UnavailableError("Service down"));
				return Result<string>.Success("OK");
			},
			RetryPolicies.TransientErrors(maxAttempts: 5, baseDelayMs: 10),
			onRetryAttempt: (attempt, error, delay) =>
			{
				attempts.Add((attempt, error.Type, delay));
			});

		// Assert
		Assert.True(outcome.IsSuccess);
		Assert.Equal(2, attempts.Count); // 2 failures before success
		Assert.Equal(1, attempts[0].attempt);
		Assert.Equal(ErrorType.Unavailable, attempts[0].errorType);
		Assert.Equal(10, attempts[0].delay); // First delay
		Assert.Equal(2, attempts[1].attempt);
		Assert.Equal(20, attempts[1].delay); // Exponential backoff
	}

	[Fact]
	public async Task OnRetryAttempt_CalledWithZeroDelay_WhenNoMoreRetries()
	{
		// Arrange
		int? lastDelay = null;
		int? lastAttempt = null;

		var result = Result<int>.Success(1);

		// Act
		var outcome = await result.BindWithRetryAsync(
			async _ =>
			{
				await Task.Delay(1);
				return Result<string>.Failure(Error.UnavailableError("Always fails"));
			},
			RetryPolicies.TransientErrors(maxAttempts: 2, baseDelayMs: 10),
			onRetryAttempt: (attempt, _, delay) =>
			{
				lastAttempt = attempt;
				lastDelay = delay;
			});

		// Assert
		Assert.True(outcome.IsFailure);
		Assert.Equal(2, lastAttempt); // Final attempt
		Assert.Equal(0, lastDelay); // No more retries
	}

	[Fact]
	public async Task OnRetryAttempt_NotCalled_WhenFirstAttemptSucceeds()
	{
		// Arrange
		var called = false;
		var result = Result<int>.Success(1);

		// Act
		var outcome = await result.BindWithRetryAsync(
			async _ =>
			{
				await Task.Delay(1);
				return Result<string>.Success("OK");
			},
			RetryPolicies.TransientErrors(),
			onRetryAttempt: (_, _, _) => called = true);

		// Assert
		Assert.True(outcome.IsSuccess);
		Assert.False(called);
	}

	[Fact]
	public async Task OnRetryAttempt_NotCalled_ForNonTransientErrors()
	{
		// Arrange
		var called = false;
		var result = Result<int>.Success(1);

		// Act
		var outcome = await result.BindWithRetryAsync(
			async _ =>
			{
				await Task.Delay(1);
				return Result<string>.Failure(Error.ValidationError("Invalid input"));
			},
			RetryPolicies.TransientErrors(),
			onRetryAttempt: (_, _, _) => called = true);

		// Assert
		Assert.True(outcome.IsFailure);
		Assert.Equal(ErrorType.Validation, outcome.Error.Type);
		Assert.False(called); // Non-transient = no retry = no callback
	}

	[Fact]
	public async Task OnRetryAttempt_NullCallback_NoException()
	{
		// Arrange
		var result = Result<int>.Success(1);
		var callCount = 0;

		// Act & Assert - should not throw
		var outcome = await result.BindWithRetryAsync(
			async _ =>
			{
				callCount++;
				await Task.Delay(1);
				if (callCount < 2)
					return Result<string>.Failure(Error.UnavailableError("Fail"));
				return Result<string>.Success("OK");
			},
			RetryPolicies.TransientErrors(maxAttempts: 3, baseDelayMs: 10),
			onRetryAttempt: null);

		Assert.True(outcome.IsSuccess);
		Assert.Equal(2, callCount);
	}

	[Fact]
	public async Task OnRetryAttempt_TaskOverload_CalledCorrectly()
	{
		// Arrange
		var attempts = new List<int>();
		var callCount = 0;
		var resultTask = Task.FromResult(Result<int>.Success(1));

		// Act
		var outcome = await resultTask.BindWithRetryAsync(
			async _ =>
			{
				callCount++;
				await Task.Delay(1);
				if (callCount < 3)
					return Result<string>.Failure(Error.TimeoutError("Timeout"));
				return Result<string>.Success("OK");
			},
			RetryPolicies.TransientErrors(maxAttempts: 5, baseDelayMs: 10),
			onRetryAttempt: (attempt, _, _) => attempts.Add(attempt));

		// Assert
		Assert.True(outcome.IsSuccess);
		Assert.Equal(new[] { 1, 2 }, attempts);
	}

	[Fact]
	public async Task OnRetryAttempt_ReceivesCorrectErrorInfo()
	{
		// Arrange
		Error? capturedError = null;
		var result = Result<int>.Success(1);

		// Act
		await result.BindWithRetryAsync(
			async _ =>
			{
				await Task.Delay(1);
				return Result<string>.Failure(Error.TimeoutError("Request.Timeout", "Connection timed out after 30s"));
			},
			RetryPolicies.TransientErrors(maxAttempts: 3, baseDelayMs: 10),
			onRetryAttempt: (_, error, _) => capturedError = error);

		// Assert
		Assert.NotNull(capturedError);
		Assert.Equal(ErrorType.Timeout, capturedError!.Type);
		Assert.Equal("Request.Timeout", capturedError.Code);
		Assert.Equal("Connection timed out after 30s", capturedError.Message);
	}
}
