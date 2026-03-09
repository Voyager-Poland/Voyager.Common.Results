namespace Voyager.Common.Results.Extensions
{
	/// <summary>
	/// Extension methods for querying Result state by ErrorType.
	/// </summary>
	public static class ResultQueryExtensions
	{
		/// <summary>
		/// Returns true if the result is a failure with ErrorType.NotFound.
		/// </summary>
		/// <example>
		/// <code>
		/// var result = _repo.Find(id).NullToResult($"User {id}");
		/// if (result.IsNotFound())
		///     return NotFound();
		/// </code>
		/// </example>
		/// <param name="result">Result to check.</param>
		/// <returns>True if the result is a failure with ErrorType.NotFound.</returns>
		public static bool IsNotFound(this Result result)
			=> result.IsFailure && result.Error.Type == ErrorType.NotFound;

		/// <summary>
		/// Returns true if the result is a failure with ErrorType.NotFound.
		/// </summary>
		/// <example>
		/// <code>
		/// Result&lt;User&gt; result = _repo.Find(id).NullToResult($"User {id}");
		/// if (result.IsNotFound())
		///     return NotFound();
		/// </code>
		/// </example>
		/// <param name="result">Result to check.</param>
		/// <typeparam name="T">Type of the result value.</typeparam>
		/// <returns>True if the result is a failure with ErrorType.NotFound.</returns>
		public static bool IsNotFound<T>(this Result<T> result)
			=> result.IsFailure && result.Error.Type == ErrorType.NotFound;
	}
}
