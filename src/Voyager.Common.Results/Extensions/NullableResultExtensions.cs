namespace Voyager.Common.Results.Extensions
{
	/// <summary>
	/// Extension methods for converting nullable values to Result.
	/// Provides ergonomic bridge between T? (repository pattern) and Result&lt;T&gt; (Railway).
	/// </summary>
	public static class NullableResultExtensions
	{
		// ========== REFERENCE TYPES (T? where T : class) ==========

		/// <summary>
		/// Converts a nullable reference to Result — Success if non-null,
		/// Failure with NotFoundError if null.
		/// </summary>
		/// <example>
		/// <code>
		/// Result&lt;User&gt; result = _repo.Find(id).NullToResult($"User {id} not found");
		/// </code>
		/// </example>
		/// <param name="value">Nullable value to convert.</param>
		/// <param name="notFoundMessage">Message for the NotFoundError if value is null.</param>
		/// <typeparam name="T">Type of the value.</typeparam>
		/// <returns>Success with value if non-null, Failure with NotFoundError if null.</returns>
		public static Result<T> NullToResult<T>(this T? value, string notFoundMessage) where T : class
			=> value is not null ? value : Error.NotFoundError(notFoundMessage);

		/// <summary>
		/// Converts a nullable reference to Result with a custom error.
		/// Use when NotFoundError is not the right error type.
		/// </summary>
		/// <example>
		/// <code>
		/// Result&lt;Config&gt; result = _provider.Get(key)
		///     .NullToResult(Error.ValidationError($"Required config '{key}' is missing"));
		/// </code>
		/// </example>
		/// <param name="value">Nullable value to convert.</param>
		/// <param name="error">Error to use if value is null.</param>
		/// <typeparam name="T">Type of the value.</typeparam>
		/// <returns>Success with value if non-null, Failure with given error if null.</returns>
		public static Result<T> NullToResult<T>(this T? value, Error error) where T : class
			=> value is not null ? value : error;

		/// <summary>
		/// Converts a nullable reference to Result with lazy error creation.
		/// </summary>
		/// <param name="value">Nullable value to convert.</param>
		/// <param name="errorFactory">Factory to create error if value is null.</param>
		/// <typeparam name="T">Type of the value.</typeparam>
		/// <returns>Success with value if non-null, Failure with error from factory if null.</returns>
		public static Result<T> NullToResult<T>(this T? value, Func<Error> errorFactory) where T : class
			=> value is not null ? value : errorFactory();

		// ========== VALUE TYPES (T? where T : struct) ==========

		/// <summary>
		/// Converts a nullable value type to Result — Success if HasValue,
		/// Failure with NotFoundError if null.
		/// </summary>
		/// <example>
		/// <code>
		/// Result&lt;DateTime&gt; result = _repo.GetLastLoginDate(userId)
		///     .NullToResult($"No login record for user {userId}");
		/// </code>
		/// </example>
		/// <param name="value">Nullable value to convert.</param>
		/// <param name="notFoundMessage">Message for the NotFoundError if value is null.</param>
		/// <typeparam name="T">Type of the value.</typeparam>
		/// <returns>Success with value if HasValue, Failure with NotFoundError otherwise.</returns>
		public static Result<T> NullToResult<T>(this T? value, string notFoundMessage) where T : struct
			=> value.HasValue ? value.Value : Error.NotFoundError(notFoundMessage);

		/// <summary>
		/// Converts a nullable value type to Result with a custom error.
		/// </summary>
		/// <param name="value">Nullable value to convert.</param>
		/// <param name="error">Error to use if value is null.</param>
		/// <typeparam name="T">Type of the value.</typeparam>
		/// <returns>Success with value if HasValue, Failure with given error otherwise.</returns>
		public static Result<T> NullToResult<T>(this T? value, Error error) where T : struct
			=> value.HasValue ? value.Value : error;

		/// <summary>
		/// Converts a nullable value type to Result with lazy error creation.
		/// </summary>
		/// <param name="value">Nullable value to convert.</param>
		/// <param name="errorFactory">Factory to create error if value is null.</param>
		/// <typeparam name="T">Type of the value.</typeparam>
		/// <returns>Success with value if HasValue, Failure with error from factory otherwise.</returns>
		public static Result<T> NullToResult<T>(this T? value, Func<Error> errorFactory) where T : struct
			=> value.HasValue ? value.Value : errorFactory();
	}
}
