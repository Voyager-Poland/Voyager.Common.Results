#if NET48
using System;
using System.Threading.Tasks;
#endif

namespace Voyager.Common.Results.Extensions
{
	/// <summary>
	/// Extension methods for error chaining in distributed systems.
	/// </summary>
	public static class ResultErrorChainExtensions
	{
		/// <summary>
		/// Wraps the error with a new error, preserving the original as InnerError.
		/// </summary>
		public static Result<T> WrapError<T>(
			this Result<T> result,
			Func<Error, Error> wrapperFactory)
		{
			if (result.IsSuccess)
				return result;

			var wrapper = wrapperFactory(result.Error);
			return Result<T>.Failure(wrapper.WithInner(result.Error));
		}

		/// <summary>
		/// Wraps the error with a new error, preserving the original as InnerError.
		/// </summary>
		public static Result WrapError(
			this Result result,
			Func<Error, Error> wrapperFactory)
		{
			if (result.IsSuccess)
				return result;

			var wrapper = wrapperFactory(result.Error);
			return Result.Failure(wrapper.WithInner(result.Error));
		}

		/// <summary>
		/// Wraps the error with context about the calling service.
		/// Preserves the original error type.
		/// </summary>
		public static Result<T> AddErrorContext<T>(
			this Result<T> result,
			string serviceName,
			string operation)
		{
			if (result.IsSuccess)
				return result;

			var contextError = new Error(
				result.Error.Type,
				$"{serviceName}.{operation}.Failed",
				$"{serviceName}.{operation} failed: {result.Error.Message}"
			) { InnerError = result.Error };

			return Result<T>.Failure(contextError);
		}

		/// <summary>
		/// Wraps the error with context about the calling service.
		/// Preserves the original error type.
		/// </summary>
		public static Result AddErrorContext(
			this Result result,
			string serviceName,
			string operation)
		{
			if (result.IsSuccess)
				return result;

			var contextError = new Error(
				result.Error.Type,
				$"{serviceName}.{operation}.Failed",
				$"{serviceName}.{operation} failed: {result.Error.Message}"
			) { InnerError = result.Error };

			return Result.Failure(contextError);
		}

		/// <summary>
		/// Wraps the error with a new error, preserving the original as InnerError. Async version.
		/// </summary>
		public static async Task<Result<T>> WrapErrorAsync<T>(
			this Task<Result<T>> resultTask,
			Func<Error, Error> wrapperFactory)
		{
			var result = await resultTask.ConfigureAwait(false);
			return result.WrapError(wrapperFactory);
		}

		/// <summary>
		/// Wraps the error with a new error, preserving the original as InnerError. Async version.
		/// </summary>
		public static async Task<Result> WrapErrorAsync(
			this Task<Result> resultTask,
			Func<Error, Error> wrapperFactory)
		{
			var result = await resultTask.ConfigureAwait(false);
			return result.WrapError(wrapperFactory);
		}

		/// <summary>
		/// Wraps the error with context about the calling service. Async version.
		/// </summary>
		public static async Task<Result<T>> AddErrorContextAsync<T>(
			this Task<Result<T>> resultTask,
			string serviceName,
			string operation)
		{
			var result = await resultTask.ConfigureAwait(false);
			return result.AddErrorContext(serviceName, operation);
		}

		/// <summary>
		/// Wraps the error with context about the calling service. Async version.
		/// </summary>
		public static async Task<Result> AddErrorContextAsync(
			this Task<Result> resultTask,
			string serviceName,
			string operation)
		{
			var result = await resultTask.ConfigureAwait(false);
			return result.AddErrorContext(serviceName, operation);
		}
	}
}
