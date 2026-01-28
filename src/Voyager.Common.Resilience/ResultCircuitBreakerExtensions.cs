#if NET48
using System;
using System.Threading;
using System.Threading.Tasks;
#endif

using Voyager.Common.Results;

namespace Voyager.Common.Resilience
{
	/// <summary>
	/// Extension methods for integrating circuit breaker pattern with Result types
	/// </summary>
	public static class ResultCircuitBreakerExtensions
	{
		/// <summary>
		/// Applies a circuit breaker to an operation, failing fast when the circuit is open
		/// </summary>
		/// <typeparam name="TIn">Input value type</typeparam>
		/// <typeparam name="TOut">Output value type</typeparam>
		/// <param name="result">The input result</param>
		/// <param name="func">Function to execute if circuit allows</param>
		/// <param name="policy">Circuit breaker policy to use</param>
		/// <returns>Result of operation or CircuitBreakerOpenError if blocked</returns>
		public static async Task<Result<TOut>> BindWithCircuitBreakerAsync<TIn, TOut>(
			this Result<TIn> result,
			Func<TIn, Task<Result<TOut>>> func,
			CircuitBreakerPolicy policy)
		{
			if (!result.IsSuccess)
				return Result<TOut>.Failure(result.Error);

			// Check if circuit breaker allows the request
			var allowResult = await policy.ShouldAllowRequestAsync().ConfigureAwait(false);
			if (!allowResult.IsSuccess)
				return Result<TOut>.Failure(allowResult.Error);

			var inputValue = result.Value!; // Safe: IsSuccess guarantees non-null value

			// Execute the operation
			var opResult = await func(inputValue).ConfigureAwait(false);

			// Record success or failure
			if (opResult.IsSuccess)
				await policy.RecordSuccessAsync().ConfigureAwait(false);
			else
				await policy.RecordFailureAsync(opResult.Error).ConfigureAwait(false);

			return opResult;
		}

		/// <summary>
		/// Applies a circuit breaker to an async operation, failing fast when the circuit is open
		/// </summary>
		/// <typeparam name="TIn">Input value type</typeparam>
		/// <typeparam name="TOut">Output value type</typeparam>
		/// <param name="resultTask">The async input result</param>
		/// <param name="func">Function to execute if circuit allows</param>
		/// <param name="policy">Circuit breaker policy to use</param>
		/// <returns>Result of operation or CircuitBreakerOpenError if blocked</returns>
		public static async Task<Result<TOut>> BindWithCircuitBreakerAsync<TIn, TOut>(
				this Task<Result<TIn>> resultTask,
				Func<TIn, Task<Result<TOut>>> func,
				CircuitBreakerPolicy policy)
		{
			var result = await resultTask.ConfigureAwait(false);
			return await result.BindWithCircuitBreakerAsync(func, policy).ConfigureAwait(false);
		}

		/// <summary>
		/// Applies a circuit breaker to a synchronous function, converting it to async execution
		/// </summary>
		/// <typeparam name="TIn">Input value type</typeparam>
		/// <typeparam name="TOut">Output value type</typeparam>
		/// <param name="result">The input result</param>
		/// <param name="func">Synchronous function to execute if circuit allows</param>
		/// <param name="policy">Circuit breaker policy to use</param>
		/// <returns>Result of operation or CircuitBreakerOpenError if blocked</returns>
		public static Task<Result<TOut>> BindWithCircuitBreakerAsync<TIn, TOut>(
			this Result<TIn> result,
			Func<TIn, Result<TOut>> func,
			CircuitBreakerPolicy policy)
		{
			return result.BindWithCircuitBreakerAsync(
				input => Task.FromResult(func(input)),
				policy);
		}

		/// <summary>
		/// Applies a circuit breaker to a synchronous function on an async result
		/// </summary>
		/// <typeparam name="TIn">Input value type</typeparam>
		/// <typeparam name="TOut">Output value type</typeparam>
		/// <param name="resultTask">The async input result</param>
		/// <param name="func">Synchronous function to execute if circuit allows</param>
		/// <param name="policy">Circuit breaker policy to use</param>
		/// <returns>Result of operation or CircuitBreakerOpenError if blocked</returns>
		public static async Task<Result<TOut>> BindWithCircuitBreakerAsync<TIn, TOut>(
			this Task<Result<TIn>> resultTask,
			Func<TIn, Result<TOut>> func,
			CircuitBreakerPolicy policy)
		{
			var result = await resultTask.ConfigureAwait(false);
			return await result.BindWithCircuitBreakerAsync(func, policy).ConfigureAwait(false);
		}
	}
}
