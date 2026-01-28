#if NET48
using System;
using System.Threading.Tasks;
#endif

namespace Voyager.Common.Results.Extensions
{
	/// <summary>
	/// Delegate that determines retry behavior based on attempt number and error
	/// </summary>
	/// <param name="attemptNumber">Current attempt number (1-based)</param>
	/// <param name="error">Error from the previous attempt</param>
	/// <returns>Success with delay in milliseconds if retry should continue, Failure to stop retrying</returns>
	public delegate Result<int> RetryPolicy(int attemptNumber, Error error);

	/// <summary>
	/// Extension methods for retry logic with Result pattern
	/// </summary>
	/// <remarks>
	/// For circuit breaker patterns or advanced resilience features, consider using Polly or a separate resilience library.
	/// These extensions provide lightweight retry functionality for simple transient failure scenarios.
	/// </remarks>
	public static class ResultRetryExtensions
	{
		/// <summary>
		/// Executes an operation with retry logic for transient failures
		/// </summary>
		/// <typeparam name="TIn">Type of the input value</typeparam>
		/// <typeparam name="TOut">Type of the output value</typeparam>
		/// <param name="result">Input result to bind</param>
		/// <param name="func">Function to execute that may fail transiently</param>
		/// <param name="policy">Retry policy that determines when and how to retry</param>
		/// <returns>Result from the operation, preserving the original error from the last attempt if all retries fail</returns>
		/// <remarks>
		/// CRITICAL: This method ALWAYS preserves the original error from the last failed attempt.
		/// It never replaces errors with generic "max retries exceeded" messages.
		/// Use <see cref="RetryPolicies.TransientErrors"/> for most scenarios.
		/// </remarks>
		/// <example>
		/// <code>
		/// var result = await GetDatabaseConnection()
		///     .BindWithRetryAsync(
		///         conn => ExecuteQuery(conn),
		///         RetryPolicies.TransientErrors(maxAttempts: 5)
		///     );
		/// </code>
		/// </example>
		public static async Task<Result<TOut>> BindWithRetryAsync<TIn, TOut>(
			this Result<TIn> result,
			Func<TIn, Task<Result<TOut>>> func,
			RetryPolicy policy)
		{
			if (result.IsFailure) return Result<TOut>.Failure(result.Error);

			int attempt = 1;
			Result<TOut> lastOutcome = default!;

			while (true)
			{
				lastOutcome = await func(result.Value!).ConfigureAwait(false);

				if (lastOutcome.IsSuccess) return lastOutcome;

				var retryDecision = policy(attempt, lastOutcome.Error);

				// Stop retrying - return the ORIGINAL error, never replace it
				if (retryDecision.IsFailure) return lastOutcome;

				// Wait before next attempt
				await Task.Delay(retryDecision.Value).ConfigureAwait(false);
				attempt++;
			}
		}

		/// <summary>
		/// Executes an operation with retry logic for transient failures (Task&lt;Result&gt; overload)
		/// </summary>
		/// <typeparam name="TIn">Type of the input value</typeparam>
		/// <typeparam name="TOut">Type of the output value</typeparam>
		/// <param name="resultTask">Task returning the input result to bind</param>
		/// <param name="func">Function to execute that may fail transiently</param>
		/// <param name="policy">Retry policy that determines when and how to retry</param>
		/// <returns>Result from the operation, preserving the original error from the last attempt if all retries fail</returns>
		/// <example>
		/// <code>
		/// var result = await GetDatabaseConnectionAsync()
		///     .BindWithRetryAsync(
		///         conn => ExecuteQuery(conn),
		///         RetryPolicies.TransientErrors()
		///     );
		/// </code>
		/// </example>
		public static async Task<Result<TOut>> BindWithRetryAsync<TIn, TOut>(
			this Task<Result<TIn>> resultTask,
			Func<TIn, Task<Result<TOut>>> func,
			RetryPolicy policy)
		{
			var result = await resultTask.ConfigureAwait(false);
			return await result.BindWithRetryAsync(func, policy).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Factory methods for common retry policies
	/// </summary>
	public static class RetryPolicies
	{
		/// <summary>
		/// Creates a retry policy that retries only transient errors (Unavailable, Timeout) with exponential backoff
		/// </summary>
		/// <param name="maxAttempts">Maximum number of attempts (default: 3)</param>
		/// <param name="baseDelayMs">Base delay in milliseconds, doubled with each attempt (default: 1000ms)</param>
		/// <returns>Retry policy with exponential backoff for transient errors</returns>
		/// <remarks>
		/// This is the recommended default for most scenarios. It retries:
		/// - <see cref="ErrorType.Unavailable"/> - Service temporarily down, network issues, deadlocks
		/// - <see cref="ErrorType.Timeout"/> - Operation exceeded time limit
		/// 
		/// Delay strategy: baseDelay * 2^(attempt-1)
		/// - Attempt 1: 1000ms (1s)
		/// - Attempt 2: 2000ms (2s)
		/// - Attempt 3: 4000ms (4s)
		/// </remarks>
		/// <example>
		/// <code>
		/// var result = await FetchDataAsync()
		///     .BindWithRetryAsync(
		///         data => ProcessData(data),
		///         RetryPolicies.TransientErrors(maxAttempts: 5, baseDelayMs: 500)
		///     );
		/// </code>
		/// </example>
		public static RetryPolicy TransientErrors(int maxAttempts = 3, int baseDelayMs = 1000)
		{
			return (attempt, error) =>
			{
				// Only retry transient errors - permanent failures should not be retried
				bool isTransient = error.Type == ErrorType.Unavailable
								|| error.Type == ErrorType.Timeout;

				if (attempt >= maxAttempts || !isTransient)
					return Result<int>.Failure(error); // Stop retrying, preserve error

				// Exponential backoff: baseDelay * 2^(attempt-1)
				int delayMs = baseDelayMs * (int)Math.Pow(2, attempt - 1);
				return Result<int>.Success(delayMs);
			};
		}

		/// <summary>
		/// Creates a retry policy with custom logic
		/// </summary>
		/// <param name="maxAttempts">Maximum number of attempts</param>
		/// <param name="shouldRetry">Predicate to determine if an error should be retried</param>
		/// <param name="delayStrategy">Function that calculates delay in milliseconds based on attempt number (1-based)</param>
		/// <returns>Custom retry policy</returns>
		/// <example>
		/// <code>
		/// // Custom policy with fixed delay and specific error types
		/// var policy = RetryPolicies.Custom(
		///     maxAttempts: 10,
		///     shouldRetry: e => e.Type == ErrorType.Unavailable || e.Code == "RATE_LIMIT",
		///     delayStrategy: attempt => 500 // Fixed 500ms delay
		/// );
		/// </code>
		/// </example>
		public static RetryPolicy Custom(
			int maxAttempts,
			Func<Error, bool> shouldRetry,
			Func<int, int> delayStrategy)
		{
			return (attempt, error) =>
			{
				if (attempt >= maxAttempts || !shouldRetry(error))
					return Result<int>.Failure(error);

				int delayMs = delayStrategy(attempt);
				return Result<int>.Success(delayMs);
			};
		}

		/// <summary>
		/// Creates the default retry policy (equivalent to <see cref="TransientErrors(int, int)"/>)
		/// </summary>
		/// <returns>Default retry policy with 3 attempts and 1000ms base delay</returns>
		public static RetryPolicy Default() => TransientErrors();
	}
}
