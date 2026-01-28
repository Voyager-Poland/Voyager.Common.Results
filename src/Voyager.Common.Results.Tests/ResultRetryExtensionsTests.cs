using Voyager.Common.Results;
using Voyager.Common.Results.Extensions;

namespace Voyager.Common.Results.Tests;

public class ResultRetryExtensionsTests
{
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
}
