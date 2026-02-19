using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Voyager.Common.Results.Extensions
{
	/// <summary>
	/// Extension methods for operations on collections of Result
	/// </summary>
	public static class ResultCollectionExtensions
	{
		// ========== COMBINE (COLLECTION) ==========

		/// <summary>
		/// Combines a collection of Result&lt;TValue&gt; into a single Result containing a list of values.
		/// If any Result is a failure, returns the first encountered error.
		/// </summary>
		/// <example>
		/// var results = new[] {
		///     Result&lt;int&gt;.Success(1),
		///     Result&lt;int&gt;.Success(2)
		/// };
		/// var combined = results.Combine(); // Result&lt;List&lt;int&gt;&gt; with [1, 2]
		/// </example>
		public static Result<List<TValue>> Combine<TValue>(
			this IEnumerable<Result<TValue>> results)
		{
			var resultsList = results.ToList();
			var values = new List<TValue>(resultsList.Count);

			foreach (var result in resultsList)
			{
				if (result.IsFailure)
					return Result<List<TValue>>.Failure(result.Error!);

				values.Add(result.Value!);
			}

			return Result<List<TValue>>.Success(values);
		}

		/// <summary>
		/// Combines a collection of non-generic Result into a single Result.
		/// If any Result is a failure, returns the first encountered error.
		/// </summary>
		public static Result Combine(this IEnumerable<Result> results)
		{
			foreach (var result in results)
			{
				if (result.IsFailure)
					return Result.Failure(result.Error!);
			}

			return Result.Success();
		}

		// ========== COMBINE (TUPLE) ==========

		/// <summary>
		/// Combines two Result&lt;T&gt; instances into a single Result containing a tuple of values.
		/// If any Result is a failure, returns the first encountered error.
		/// </summary>
		/// <example>
		/// <code>
		/// var name = Result&lt;string&gt;.Success("Alice");
		/// var age = Result&lt;int&gt;.Success(30);
		/// var combined = name.Combine(age); // Result&lt;(string, int)&gt; with ("Alice", 30)
		/// </code>
		/// </example>
		public static Result<(T1, T2)> Combine<T1, T2>(
			this Result<T1> first,
			Result<T2> second)
		{
			if (first.IsFailure)
				return Result<(T1, T2)>.Failure(first.Error!);
			if (second.IsFailure)
				return Result<(T1, T2)>.Failure(second.Error!);

			return Result<(T1, T2)>.Success((first.Value!, second.Value!));
		}

		/// <summary>
		/// Combines three Result&lt;T&gt; instances into a single Result containing a tuple of values.
		/// If any Result is a failure, returns the first encountered error.
		/// </summary>
		public static Result<(T1, T2, T3)> Combine<T1, T2, T3>(
			this Result<T1> first,
			Result<T2> second,
			Result<T3> third)
		{
			if (first.IsFailure)
				return Result<(T1, T2, T3)>.Failure(first.Error!);
			if (second.IsFailure)
				return Result<(T1, T2, T3)>.Failure(second.Error!);
			if (third.IsFailure)
				return Result<(T1, T2, T3)>.Failure(third.Error!);

			return Result<(T1, T2, T3)>.Success((first.Value!, second.Value!, third.Value!));
		}

		/// <summary>
		/// Combines four Result&lt;T&gt; instances into a single Result containing a tuple of values.
		/// If any Result is a failure, returns the first encountered error.
		/// </summary>
		public static Result<(T1, T2, T3, T4)> Combine<T1, T2, T3, T4>(
			this Result<T1> first,
			Result<T2> second,
			Result<T3> third,
			Result<T4> fourth)
		{
			if (first.IsFailure)
				return Result<(T1, T2, T3, T4)>.Failure(first.Error!);
			if (second.IsFailure)
				return Result<(T1, T2, T3, T4)>.Failure(second.Error!);
			if (third.IsFailure)
				return Result<(T1, T2, T3, T4)>.Failure(third.Error!);
			if (fourth.IsFailure)
				return Result<(T1, T2, T3, T4)>.Failure(fourth.Error!);

			return Result<(T1, T2, T3, T4)>.Success((first.Value!, second.Value!, third.Value!, fourth.Value!));
		}

		// ========== TRAVERSE ASYNC (FAIL-FAST) ==========

		/// <summary>
		/// Sequentially applies an async Result-returning function to each element.
		/// Stops on the first failure (fail-fast).
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await operations.TraverseAsync(
		///     x => OperationUpdateResultAsync(ctx, x.Op, x.Data));
		/// // Result&lt;List&lt;TOut&gt;&gt; — all values if all succeeded, or first error
		/// </code>
		/// </example>
		/// <typeparam name="T">Type of input elements.</typeparam>
		/// <typeparam name="TOut">Type of output values produced by the function.</typeparam>
		/// <param name="source">Input collection to traverse.</param>
		/// <param name="func">Async function that returns Result&lt;TOut&gt; for each element.</param>
		/// <returns>Success with list of all values, or first encountered error.</returns>
		public static async Task<Result<List<TOut>>> TraverseAsync<T, TOut>(
			this IEnumerable<T> source,
			Func<T, Task<Result<TOut>>> func)
		{
			var values = new List<TOut>();

			foreach (var item in source)
			{
				var result = await func(item).ConfigureAwait(false);
				if (result.IsFailure)
					return Result<List<TOut>>.Failure(result.Error!);

				values.Add(result.Value!);
			}

			return Result<List<TOut>>.Success(values);
		}

		/// <summary>
		/// Sequentially applies an async Result-returning function to each element (non-generic variant).
		/// Stops on the first failure (fail-fast).
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await operations.TraverseAsync(
		///     x => SendNotificationAsync(x));
		/// // Result — success if all succeeded, or first error
		/// </code>
		/// </example>
		/// <typeparam name="T">Type of input elements.</typeparam>
		/// <param name="source">Input collection to traverse.</param>
		/// <param name="func">Async function that returns Result for each element.</param>
		/// <returns>Success if all operations succeeded, or first encountered error.</returns>
		public static async Task<Result> TraverseAsync<T>(
			this IEnumerable<T> source,
			Func<T, Task<Result>> func)
		{
			foreach (var item in source)
			{
				var result = await func(item).ConfigureAwait(false);
				if (result.IsFailure)
					return Result.Failure(result.Error!);
			}

			return Result.Success();
		}

		// ========== TRAVERSE ALL ASYNC (COLLECT ALL ERRORS) ==========

		/// <summary>
		/// Sequentially applies an async Result-returning function to each element.
		/// Continues on failure and collects ALL errors (not fail-fast).
		/// Errors are aggregated using InnerError chain: first error → second error → ...
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await items.TraverseAllAsync(
		///     x => ValidateAndProcessAsync(x));
		/// // On failure, result.Error has InnerError chain with all errors
		/// </code>
		/// </example>
		/// <typeparam name="T">Type of input elements.</typeparam>
		/// <typeparam name="TOut">Type of output values produced by the function.</typeparam>
		/// <param name="source">Input collection to traverse.</param>
		/// <param name="func">Async function that returns Result&lt;TOut&gt; for each element.</param>
		/// <returns>Success with list of all values, or aggregated error with InnerError chain.</returns>
		public static async Task<Result<List<TOut>>> TraverseAllAsync<T, TOut>(
			this IEnumerable<T> source,
			Func<T, Task<Result<TOut>>> func)
		{
			var values = new List<TOut>();
			var errors = new List<Error>();

			foreach (var item in source)
			{
				var result = await func(item).ConfigureAwait(false);
				if (result.IsFailure)
					errors.Add(result.Error!);
				else
					values.Add(result.Value!);
			}

			if (errors.Count == 0)
				return Result<List<TOut>>.Success(values);

			return Result<List<TOut>>.Failure(AggregateErrors(errors));
		}

		/// <summary>
		/// Sequentially applies an async Result-returning function to each element (non-generic variant).
		/// Continues on failure and collects ALL errors (not fail-fast).
		/// Errors are aggregated using InnerError chain: first error → second error → ...
		/// </summary>
		/// <typeparam name="T">Type of input elements.</typeparam>
		/// <param name="source">Input collection to traverse.</param>
		/// <param name="func">Async function that returns Result for each element.</param>
		/// <returns>Success if all operations succeeded, or aggregated error with InnerError chain.</returns>
		public static async Task<Result> TraverseAllAsync<T>(
			this IEnumerable<T> source,
			Func<T, Task<Result>> func)
		{
			var errors = new List<Error>();

			foreach (var item in source)
			{
				var result = await func(item).ConfigureAwait(false);
				if (result.IsFailure)
					errors.Add(result.Error!);
			}

			if (errors.Count == 0)
				return Result.Success();

			return Result.Failure(AggregateErrors(errors));
		}

		// ========== COMBINE ASYNC ==========

		/// <summary>
		/// Awaits all tasks and combines their results into a single Result containing a list of values.
		/// If any Result is a failure, returns the first encountered error.
		/// </summary>
		/// <example>
		/// <code>
		/// var tasks = items.Select(x => ProcessAsync(x));
		/// var result = await tasks.CombineAsync();
		/// </code>
		/// </example>
		/// <typeparam name="TValue">Type of values in the Results.</typeparam>
		/// <param name="tasks">Collection of tasks producing Results.</param>
		/// <returns>Success with list of all values, or first encountered error.</returns>
		public static async Task<Result<List<TValue>>> CombineAsync<TValue>(
			this IEnumerable<Task<Result<TValue>>> tasks)
		{
			var taskList = new List<Task<Result<TValue>>>();
			foreach (var task in tasks)
				taskList.Add(task);

			await Task.WhenAll(taskList).ConfigureAwait(false);

			var values = new List<TValue>(taskList.Count);
			foreach (var task in taskList)
			{
				var result = task.Result;
				if (result.IsFailure)
					return Result<List<TValue>>.Failure(result.Error!);

				values.Add(result.Value!);
			}

			return Result<List<TValue>>.Success(values);
		}

		/// <summary>
		/// Awaits all tasks and combines their non-generic Results into a single Result.
		/// If any Result is a failure, returns the first encountered error.
		/// </summary>
		/// <param name="tasks">Collection of tasks producing Results.</param>
		/// <returns>Success if all Results succeeded, or first encountered error.</returns>
		public static async Task<Result> CombineAsync(
			this IEnumerable<Task<Result>> tasks)
		{
			var taskList = new List<Task<Result>>();
			foreach (var task in tasks)
				taskList.Add(task);

			await Task.WhenAll(taskList).ConfigureAwait(false);

			foreach (var task in taskList)
			{
				var result = task.Result;
				if (result.IsFailure)
					return Result.Failure(result.Error!);
			}

			return Result.Success();
		}

		// ========== PARTITION ASYNC ==========

		/// <summary>
		/// Awaits all tasks and partitions the results into successes and failures.
		/// </summary>
		/// <typeparam name="TValue">Type of values in the Results.</typeparam>
		/// <param name="tasks">Collection of tasks producing Results.</param>
		/// <returns>Tuple of success values and failure errors.</returns>
		public static async Task<(List<TValue> Successes, List<Error> Failures)> PartitionAsync<TValue>(
			this IEnumerable<Task<Result<TValue>>> tasks)
		{
			var taskList = new List<Task<Result<TValue>>>();
			foreach (var task in tasks)
				taskList.Add(task);

			await Task.WhenAll(taskList).ConfigureAwait(false);

			var successes = new List<TValue>();
			var failures = new List<Error>();

			foreach (var task in taskList)
			{
				var result = task.Result;
				if (result.IsSuccess)
					successes.Add(result.Value!);
				else
					failures.Add(result.Error!);
			}

			return (successes, failures);
		}

		// ========== QUERY METHODS ==========

		/// <summary>
		/// Collects all errors from a sequence of Result&lt;TValue&gt; instances.
		/// </summary>
		public static List<Error> GetErrors<TValue>(
			this IEnumerable<Result<TValue>> results)
		{
			return results
				.Where(r => r.IsFailure)
				.Select(r => r.Error!)
				.ToList();
		}

		/// <summary>
		/// Collects all values from successful Results
		/// </summary>
		public static List<TValue> GetSuccessValues<TValue>(
			this IEnumerable<Result<TValue>> results)
		{
			return results
				.Where(r => r.IsSuccess)
				.Select(r => r.Value!)
				.ToList();
		}

		/// <summary>
		/// Checks whether all Results are successful
		/// </summary>
		public static bool AllSuccess<TValue>(
			this IEnumerable<Result<TValue>> results)
		{
			return results.All(r => r.IsSuccess);
		}

		/// <summary>
		/// Checks whether any Result is successful
		/// </summary>
		public static bool AnySuccess<TValue>(
			this IEnumerable<Result<TValue>> results)
		{
			return results.Any(r => r.IsSuccess);
		}

		/// <summary>
		/// Partitions a sequence of Results into successes and failures
		/// </summary>
		public static (List<TValue> Successes, List<Error> Failures) Partition<TValue>(
			this IEnumerable<Result<TValue>> results)
		{
			var successes = new List<TValue>();
			var failures = new List<Error>();

			foreach (var result in results)
			{
				if (result.IsSuccess)
					successes.Add(result.Value!);
				else
					failures.Add(result.Error!);
			}

			return (successes, failures);
		}

		// ========== PRIVATE HELPERS ==========

		/// <summary>
		/// Aggregates multiple errors into a single error chain using InnerError.
		/// The first error becomes the main error, subsequent errors are chained via InnerError.
		/// </summary>
		private static Error AggregateErrors(List<Error> errors)
		{
			var aggregate = errors[errors.Count - 1];
			for (var i = errors.Count - 2; i >= 0; i--)
				aggregate = errors[i].WithInner(aggregate);

			return aggregate;
		}
	}
}
