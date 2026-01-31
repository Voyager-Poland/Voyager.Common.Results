namespace Voyager.Common.Results.Extensions
{
	/// <summary>
	/// Extension methods for ErrorType classification.
	/// </summary>
	public static class ErrorTypeExtensions
	{
		/// <summary>
		/// Transient errors - client MAY retry with exponential backoff.
		/// </summary>
		public static bool IsTransient(this ErrorType errorType)
		{
			return errorType is
				ErrorType.Timeout or
				ErrorType.Unavailable or
				ErrorType.CircuitBreakerOpen or
				ErrorType.TooManyRequests;
		}

		/// <summary>
		/// Business errors - client should NOT retry.
		/// Circuit breaker should IGNORE these.
		/// </summary>
		public static bool IsBusinessError(this ErrorType errorType)
		{
			return errorType is
				ErrorType.Validation or
				ErrorType.Business or
				ErrorType.NotFound or
				ErrorType.Unauthorized or
				ErrorType.Permission or
				ErrorType.Conflict or
				ErrorType.Cancelled;
		}

		/// <summary>
		/// Infrastructure errors - client should NOT retry.
		/// Circuit breaker SHOULD count these toward failure threshold.
		/// </summary>
		public static bool IsInfrastructureError(this ErrorType errorType)
		{
			return errorType is
				ErrorType.Database or
				ErrorType.Unexpected;
		}

		/// <summary>
		/// Should circuit breaker count this error toward failure threshold?
		/// CircuitBreakerOpen is excluded - it's a protection mechanism, not a real failure.
		/// </summary>
		public static bool ShouldCountForCircuitBreaker(this ErrorType errorType)
		{
			if (errorType == ErrorType.CircuitBreakerOpen)
				return false;

			return IsTransient(errorType) || IsInfrastructureError(errorType);
		}

		/// <summary>
		/// Should the operation be retried?
		/// </summary>
		public static bool ShouldRetry(this ErrorType errorType)
		{
			return IsTransient(errorType);
		}

		/// <summary>
		/// Maps ErrorType to HTTP status code.
		/// </summary>
		public static int ToHttpStatusCode(this ErrorType errorType)
		{
			return errorType switch
			{
				ErrorType.None => 200,
				ErrorType.Validation => 400,
				ErrorType.Business => 400,
				ErrorType.Unauthorized => 401,
				ErrorType.Permission => 403,
				ErrorType.NotFound => 404,
				ErrorType.Conflict => 409,
				ErrorType.Cancelled => 499,
				ErrorType.TooManyRequests => 429,
				ErrorType.Timeout => 504,
				ErrorType.Unavailable => 503,
				ErrorType.CircuitBreakerOpen => 503,
				ErrorType.Database => 500,
				ErrorType.Unexpected => 500,
				_ => 500
			};
		}
	}
}
