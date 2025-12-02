#if NET48
using System;
using System.Threading.Tasks;
#endif

namespace Voyager.Common.Results.Extensions
{
	/// <summary>
	/// Extension methods for async Result operations
	/// </summary>
	public static class TaskResultExtensions
	{
		// ========== TRY ASYNC ==========

		/// <summary>
		/// Executes an asynchronous action and wraps any exceptions in a Result
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await Result.TryAsync(async () => await File.WriteAllTextAsync("log.txt", message));
		/// </code>
		/// </example>
		/// <param name="action">Asynchronous action to execute.</param>
		/// <returns>Success if action completes without exception, otherwise Failure with error from exception.</returns>
		public static async Task<Result> TryAsync(Func<Task> action)
		{
			try
			{
				await action().ConfigureAwait(false);
				return Result.Success();
			}
			catch (Exception ex)
			{
				return Result.Failure(Error.FromException(ex));
			}
		}

		/// <summary>
		/// Executes an asynchronous action and wraps any exceptions in a Result with custom error mapping
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await Result.TryAsync(
		///     async () => await File.WriteAllTextAsync("log.txt", message),
		///     ex => ex is IOException 
		///         ? Error.UnavailableError("File system unavailable")
		///         : Error.UnexpectedError(ex.Message));
		/// </code>
		/// </example>
		/// <param name="action">Asynchronous action to execute.</param>
		/// <param name="errorMapper">Function to convert exception to custom error.</param>
		/// <returns>Success if action completes without exception, otherwise Failure with mapped error.</returns>
		public static async Task<Result> TryAsync(Func<Task> action, Func<Exception, Error> errorMapper)
		{
			try
			{
				await action().ConfigureAwait(false);
				return Result.Success();
			}
			catch (Exception ex)
			{
				return Result.Failure(errorMapper(ex));
			}
		}

		/// <summary>
		/// Executes an asynchronous function and wraps any exceptions in a Result
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await Result.TryAsync(async () => await LoadDataAsync());
		/// var config = await Result.TryAsync(async () => await JsonSerializer.DeserializeAsync&lt;Config&gt;(stream));
		/// </code>
		/// </example>
		/// <param name="func">Asynchronous function to execute.</param>
		/// <typeparam name="TValue">Type of value returned by the function.</typeparam>
		/// <returns>Success with function result if completes without exception, otherwise Failure with error from exception.</returns>
		public static async Task<Result<TValue>> TryAsync<TValue>(Func<Task<TValue>> func)
		{
			try
			{
				var value = await func().ConfigureAwait(false);
				return Result<TValue>.Success(value);
			}
			catch (Exception ex)
			{
				return Result<TValue>.Failure(Error.FromException(ex));
			}
		}

		/// <summary>
		/// Executes an asynchronous function and wraps any exceptions in a Result with custom error mapping
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await Result.TryAsync(
		///     async () => await JsonSerializer.DeserializeAsync&lt;Config&gt;(stream),
		///     ex => ex is JsonException 
		///         ? Error.ValidationError("Invalid JSON")
		///         : Error.UnexpectedError(ex.Message));
		/// </code>
		/// </example>
		/// <param name="func">Asynchronous function to execute.</param>
		/// <param name="errorMapper">Function to convert exception to custom error.</param>
		/// <typeparam name="TValue">Type of value returned by the function.</typeparam>
		/// <returns>Success with function result if completes without exception, otherwise Failure with mapped error.</returns>
		public static async Task<Result<TValue>> TryAsync<TValue>(Func<Task<TValue>> func, Func<Exception, Error> errorMapper)
		{
			try
			{
				var value = await func().ConfigureAwait(false);
				return Result<TValue>.Success(value);
			}
			catch (Exception ex)
			{
				return Result<TValue>.Failure(errorMapper(ex));
			}
		}

		// ========== MAP ASYNC ==========

		/// <summary>
		/// Maps a Task&lt;Result&lt;TIn&gt;&gt; to another Result type using a synchronous mapper
		/// </summary>
		public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
			this Task<Result<TIn>> resultTask,
			Func<TIn, TOut> mapper)
		{
			var result = await resultTask.ConfigureAwait(false);
			return result.Map(mapper);
		}

		/// <summary>
		/// Maps a Result&lt;TIn&gt; using an asynchronous mapper function
		/// </summary>
		public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
			this Result<TIn> result,
			Func<TIn, Task<TOut>> mapper)
		{
			if (result.IsFailure)
				return Result<TOut>.Failure(result.Error!);

			var value = await mapper(result.Value!).ConfigureAwait(false);
			return Result<TOut>.Success(value);
		}

		/// <summary>
		/// Maps a Task&lt;Result&lt;TIn&gt;&gt; using an asynchronous mapper function
		/// </summary>
		public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
			this Task<Result<TIn>> resultTask,
			Func<TIn, Task<TOut>> mapper)
		{
			var result = await resultTask.ConfigureAwait(false);
			return await result.MapAsync(mapper).ConfigureAwait(false);
		}

		// ========== BIND ASYNC ==========

		/// <summary>
		/// Binds a Task&lt;Result&lt;TIn&gt;&gt; using a synchronous binder
		/// </summary>
		public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
			this Task<Result<TIn>> resultTask,
			Func<TIn, Result<TOut>> binder)
		{
			var result = await resultTask.ConfigureAwait(false);
			return result.Bind(binder);
		}

		/// <summary>
		/// Binds a Result&lt;TIn&gt; using an asynchronous binder
		/// </summary>
		public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
			this Result<TIn> result,
			Func<TIn, Task<Result<TOut>>> binder)
		{
			if (result.IsFailure)
				return Result<TOut>.Failure(result.Error!);

			return await binder(result.Value!).ConfigureAwait(false);
		}

		/// <summary>
		/// Binds a Task&lt;Result&lt;TIn&gt;&gt; using an asynchronous binder
		/// </summary>
		public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
			this Task<Result<TIn>> resultTask,
			Func<TIn, Task<Result<TOut>>> binder)
		{
			var result = await resultTask.ConfigureAwait(false);
			return await result.BindAsync(binder).ConfigureAwait(false);
		}

		// ========== TAP ASYNC ==========

		/// <summary>
		/// Tap for Task&lt;Result&lt;TValue&gt;&gt; with a synchronous action
		/// </summary>
		public static async Task<Result<TValue>> TapAsync<TValue>(
			this Task<Result<TValue>> resultTask,
			Action<TValue> action)
		{
			var result = await resultTask.ConfigureAwait(false);
			return result.Tap(action);
		}

		/// <summary>
		/// Tap for Result&lt;TValue&gt; with an asynchronous action
		/// </summary>
		public static async Task<Result<TValue>> TapAsync<TValue>(
			this Result<TValue> result,
			Func<TValue, Task> action)
		{
			if (result.IsSuccess)
				await action(result.Value!).ConfigureAwait(false);
			return result;
		}

		/// <summary>
		/// Tap for Task&lt;Result&lt;TValue&gt;&gt; with an asynchronous action
		/// </summary>
		public static async Task<Result<TValue>> TapAsync<TValue>(
			this Task<Result<TValue>> resultTask,
			Func<TValue, Task> action)
		{
			var result = await resultTask.ConfigureAwait(false);
			return await result.TapAsync(action).ConfigureAwait(false);
		}

		// ========== MATCH ASYNC ==========

		/// <summary>
		/// Match for Task&lt;Result&lt;TValue&gt;&gt; with synchronous handlers
		/// </summary>
		public static async Task<TResult> MatchAsync<TValue, TResult>(
			this Task<Result<TValue>> resultTask,
			Func<TValue, TResult> onSuccess,
			Func<Error, TResult> onFailure)
		{
			var result = await resultTask.ConfigureAwait(false);
			return result.Match(onSuccess, onFailure);
		}

		/// <summary>
		/// Match for Result&lt;TValue&gt; with asynchronous handlers
		/// </summary>
		public static async Task<TResult> MatchAsync<TValue, TResult>(
			this Result<TValue> result,
			Func<TValue, Task<TResult>> onSuccess,
			Func<Error, Task<TResult>> onFailure)
		{
			return result.IsSuccess
				? await onSuccess(result.Value!).ConfigureAwait(false)
				: await onFailure(result.Error!).ConfigureAwait(false);
		}

		/// <summary>
		/// Match for Task&lt;Result&lt;TValue&gt;&gt; with asynchronous handlers
		/// </summary>
		public static async Task<TResult> MatchAsync<TValue, TResult>(
			this Task<Result<TValue>> resultTask,
			Func<TValue, Task<TResult>> onSuccess,
			Func<Error, Task<TResult>> onFailure)
		{
			var result = await resultTask.ConfigureAwait(false);
			return await result.MatchAsync(onSuccess, onFailure).ConfigureAwait(false);
		}

		// ========== ENSURE ASYNC ==========

		/// <summary>
		/// Ensure for Task&lt;Result&lt;TValue&gt;&gt; with a synchronous predicate
		/// </summary>
		public static async Task<Result<TValue>> EnsureAsync<TValue>(
			this Task<Result<TValue>> resultTask,
			Func<TValue, bool> predicate,
			Error error)
		{
			var result = await resultTask.ConfigureAwait(false);
			return result.Ensure(predicate, error);
		}

		/// <summary>
		/// Ensure for Result&lt;TValue&gt; with an asynchronous predicate
		/// </summary>
		public static async Task<Result<TValue>> EnsureAsync<TValue>(
			this Result<TValue> result,
			Func<TValue, Task<bool>> predicate,
			Error error)
		{
			if (result.IsFailure)
				return result;

			var isValid = await predicate(result.Value!).ConfigureAwait(false);
			return isValid ? result : Result<TValue>.Failure(error);
		}

		/// <summary>
		/// Ensure for Task&lt;Result&lt;TValue&gt;&gt; with an asynchronous predicate
		/// </summary>
		public static async Task<Result<TValue>> EnsureAsync<TValue>(
			this Task<Result<TValue>> resultTask,
			Func<TValue, Task<bool>> predicate,
			Error error)
		{
			var result = await resultTask.ConfigureAwait(false);
			return await result.EnsureAsync(predicate, error).ConfigureAwait(false);
		}

		// ========== ORELSE ASYNC ==========

		/// <summary>
		/// OrElse for Task&lt;Result&lt;TValue&gt;&gt; with a synchronous alternative result
		/// </summary>
		public static async Task<Result<TValue>> OrElseAsync<TValue>(
			this Task<Result<TValue>> resultTask,
			Result<TValue> alternative)
		{
			var result = await resultTask.ConfigureAwait(false);
			return result.OrElse(alternative);
		}

		/// <summary>
		/// OrElse for Task&lt;Result&lt;TValue&gt;&gt; with a synchronous alternative function
		/// </summary>
		public static async Task<Result<TValue>> OrElseAsync<TValue>(
			this Task<Result<TValue>> resultTask,
			Func<Result<TValue>> alternativeFunc)
		{
			var result = await resultTask.ConfigureAwait(false);
			return result.OrElse(alternativeFunc);
		}

		/// <summary>
		/// OrElse for Result&lt;TValue&gt; with an asynchronous alternative function
		/// </summary>
		public static async Task<Result<TValue>> OrElseAsync<TValue>(
			this Result<TValue> result,
			Func<Task<Result<TValue>>> alternativeFunc)
		{
			if (result.IsSuccess)
				return result;

			return await alternativeFunc().ConfigureAwait(false);
		}

		/// <summary>
		/// OrElse for Task&lt;Result&lt;TValue&gt;&gt; with an asynchronous alternative function
		/// </summary>
		public static async Task<Result<TValue>> OrElseAsync<TValue>(
			this Task<Result<TValue>> resultTask,
			Func<Task<Result<TValue>>> alternativeFunc)
		{
			var result = await resultTask.ConfigureAwait(false);
			return await result.OrElseAsync(alternativeFunc).ConfigureAwait(false);
		}
	}
}
