#if NET48
using System;
using System.Collections.Generic;
using System.Linq;
#endif

namespace Voyager.Common.Results.Extensions
{
	/// <summary>
	/// Extension methods for operations on collections of Result
	/// </summary>
	public static class ResultCollectionExtensions
	{
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
	}
}
