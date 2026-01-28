namespace Voyager.Common.Results
{
	/// <summary>
	/// Error types in the system
	/// </summary>
	public enum ErrorType
	{
		/// <summary>
		/// No error
		/// </summary>
		None,

		/// <summary>
		/// Input validation error
		/// </summary>
		Validation,

		/// <summary>
		/// Permission/authorization error
		/// </summary>
		Permission,

		/// <summary>
		/// User not authenticated (not logged in)
		/// </summary>
		Unauthorized,

		/// <summary>
		/// Database error
		/// </summary>
		Database,       /// <summary>
										/// Business logic error
										/// </summary>
		Business,

		/// <summary>
		/// Resource not found
		/// </summary>
		NotFound,

		/// <summary>
		/// Conflict (e.g. duplicate)
		/// </summary>
		Conflict,

		/// <summary>
		/// Temporary unavailability (e.g. service down, rate limit)
		/// </summary>
		Unavailable,

		/// <summary>
		/// Operation timeout
		/// </summary>
		Timeout,

		/// <summary>
		/// Operation was cancelled
		/// </summary>
		Cancelled,

		/// <summary>
		/// Unexpected system error
		/// </summary>
		Unexpected,

		/// <summary>
		/// Circuit breaker is open (protection mechanism active)
		/// </summary>
		CircuitBreakerOpen
	}
}
