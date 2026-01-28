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
		/// Applies a circuit breaker to an operation independent of input value, failing fast when the circuit is open
		/// </summary>
		/// <typeparam name="TOut">Output value type</typeparam>
		/// <param name="result">The input result (only for error propagation, not used as parameter)</param>
		/// <param name="func">Function to execute if circuit allows (no parameters required)</param>
		/// <param name="policy">Circuit breaker policy to use</param>
		/// <returns>Result of operation or CircuitBreakerOpenError if blocked</returns>
		public static async Task<Result<TOut>> BindWithCircuitBreakerAsync<TOut>(
			this Result result,
			Func<Task<Result<TOut>>> func,
			CircuitBreakerPolicy policy)
		{
			if (!result.IsSuccess)
				return Result<TOut>.Failure(result.Error);

			// Check if circuit breaker allows the request
			var allowResult = await policy.ShouldAllowRequestAsync().ConfigureAwait(false);
			if (!allowResult.IsSuccess)
				return Result<TOut>.Failure(allowResult.Error);

			// Execute the operation (no input parameter)
			var opResult = await func().ConfigureAwait(false);

			// Record success or failure
			if (opResult.IsSuccess)
				await policy.RecordSuccessAsync().ConfigureAwait(false);
			else
				await policy.RecordFailureAsync(opResult.Error).ConfigureAwait(false);

			return opResult;
		}

		/// <summary>
		/// Applies a circuit breaker to a synchronous operation independent of input value
		/// </summary>
		/// <typeparam name="TOut">Output value type</typeparam>
		/// <param name="result">The input result (only for error propagation, not used as parameter)</param>
		/// <param name="func">Synchronous function to execute if circuit allows (no parameters required)</param>
		/// <param name="policy">Circuit breaker policy to use</param>
		/// <returns>Result of operation or CircuitBreakerOpenError if blocked</returns>
		public static Task<Result<TOut>> BindWithCircuitBreakerAsync<TOut>(
			this Result result,
			Func<Result<TOut>> func,
			CircuitBreakerPolicy policy)
		{
			return result.BindWithCircuitBreakerAsync(
				() => Task.FromResult(func()),
				policy);
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

		/// <summary>
		/// Applies a circuit breaker directly to a value with an async operation
		/// </summary>
		/// <typeparam name="TIn">Input value type</typeparam>
		/// <typeparam name="TOut">Output value type</typeparam>
		/// <param name="policy">Circuit breaker policy to use</param>
		/// <param name="value">The input value (not wrapped in Result)</param>
		/// <param name="func">Async function to execute if circuit allows</param>
		/// <returns>Result of operation or CircuitBreakerOpenError if blocked</returns>
		public static Task<Result<TOut>> ExecuteAsync<TIn, TOut>(
			this CircuitBreakerPolicy policy,
			TIn value,
			Func<TIn, Task<Result<TOut>>> func)
		{
			return Result<TIn>.Success(value)
				.BindWithCircuitBreakerAsync(func, policy);
		}

		/// <summary>
		/// Applies a circuit breaker directly to a value with a synchronous operation
		/// </summary>
		/// <typeparam name="TIn">Input value type</typeparam>
		/// <typeparam name="TOut">Output value type</typeparam>
		/// <param name="policy">Circuit breaker policy to use</param>
		/// <param name="value">The input value (not wrapped in Result)</param>
		/// <param name="func">Synchronous function to execute if circuit allows</param>
		/// <returns>Result of operation or CircuitBreakerOpenError if blocked</returns>
		public static Task<Result<TOut>> ExecuteAsync<TIn, TOut>(
			this CircuitBreakerPolicy policy,
			TIn value,
			Func<TIn, Result<TOut>> func)
		{
			return Result<TIn>.Success(value)
				.BindWithCircuitBreakerAsync(func, policy);
		}

		/// <summary>
		/// Applies a circuit breaker directly to an async value task with an async operation
		/// </summary>
		/// <typeparam name="TIn">Input value type</typeparam>
		/// <typeparam name="TOut">Output value type</typeparam>
		/// <param name="policy">Circuit breaker policy to use</param>
		/// <param name="valueTask">Async task returning the input value</param>
		/// <param name="func">Async function to execute if circuit allows</param>
		/// <returns>Result of operation or CircuitBreakerOpenError if blocked</returns>
		public static async Task<Result<TOut>> ExecuteAsync<TIn, TOut>(
			this CircuitBreakerPolicy policy,
			Task<TIn> valueTask,
			Func<TIn, Task<Result<TOut>>> func)
		{
			var value = await valueTask.ConfigureAwait(false);
			return await policy.ExecuteAsync(value, func).ConfigureAwait(false);
		}

		/// <summary>
		/// Applies a circuit breaker directly to an async value task with a synchronous operation
		/// </summary>
		/// <typeparam name="TIn">Input value type</typeparam>
		/// <typeparam name="TOut">Output value type</typeparam>
		/// <param name="policy">Circuit breaker policy to use</param>
		/// <param name="valueTask">Async task returning the input value</param>
		/// <param name="func">Synchronous function to execute if circuit allows</param>
		/// <returns>Result of operation or CircuitBreakerOpenError if blocked</returns>
		public static async Task<Result<TOut>> ExecuteAsync<TIn, TOut>(
			this CircuitBreakerPolicy policy,
			Task<TIn> valueTask,
			Func<TIn, Result<TOut>> func)
		{
			var value = await valueTask.ConfigureAwait(false);
			return await policy.ExecuteAsync(value, func).ConfigureAwait(false);
		}
	}
}
