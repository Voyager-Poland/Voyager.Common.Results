#if NET48
using System;
using System.Collections.Generic;
#endif
using System.Text;

namespace Voyager.Common.Results
{
	/// <summary>
	/// Represents an error with a type, code and message
	/// </summary>
	public sealed record Error(ErrorType Type, string Code, string Message)
	{
		/// <summary>
		/// No error - used for representing success
		/// </summary>
		public static readonly Error None = new(ErrorType.None, string.Empty, string.Empty);

		/// <summary>
		/// Inner error that caused this error (similar to Exception.InnerException).
		/// Used for error chaining in distributed systems.
		/// </summary>
		public Error? InnerError { get; init; }

		/// <summary>
		/// Stack trace from the original exception.
		/// Copied as string - original Exception can be garbage collected.
		/// </summary>
		public string? StackTrace { get; init; }

		/// <summary>
		/// Full type name of the original exception (e.g., "System.Data.SqlClient.SqlException").
		/// </summary>
		public string? ExceptionType { get; init; }

		/// <summary>
		/// Source of the exception (assembly/module name).
		/// </summary>
		public string? Source { get; init; }

		/// <summary>
		/// Creates a copy of this error with an inner error attached.
		/// </summary>
		public Error WithInner(Error inner) => this with { InnerError = inner };

		/// <summary>
		/// Gets the root cause error by traversing the InnerError chain.
		/// </summary>
		public Error GetRootCause()
		{
			var current = this;
			while (current.InnerError is not null)
			{
				current = current.InnerError;
			}
			return current;
		}

		/// <summary>
		/// Returns true if any error in the chain matches the predicate.
		/// </summary>
		public bool HasInChain(Func<Error, bool> predicate)
		{
			var current = this;
			while (current is not null)
			{
				if (predicate(current))
					return true;
				current = current.InnerError;
			}
			return false;
		}

		/// <summary>
		/// Returns detailed error information including stack trace and inner errors.
		/// Useful for logging and debugging.
		/// </summary>
		public string ToDetailedString()
		{
			var sb = new StringBuilder();
			AppendErrorDetails(sb, this, depth: 0);
			return sb.ToString();
		}

		private static void AppendErrorDetails(StringBuilder sb, Error error, int depth)
		{
			var indent = new string(' ', depth * 2);

			sb.AppendLine($"{indent}[{error.Type}] {error.Code}: {error.Message}");

			if (error.ExceptionType is not null)
				sb.AppendLine($"{indent}  Exception: {error.ExceptionType}");

			if (error.Source is not null)
				sb.AppendLine($"{indent}  Source: {error.Source}");

			if (error.StackTrace is not null)
			{
				sb.AppendLine($"{indent}  Stack Trace:");
				var lines = error.StackTrace.Split('\n');
				var lineCount = 0;
				foreach (var line in lines)
				{
					if (lineCount >= 10)
					{
						sb.AppendLine($"{indent}    ... ({lines.Length - 10} more lines)");
						break;
					}
					var trimmed = line.Trim();
					if (!string.IsNullOrEmpty(trimmed))
					{
						sb.AppendLine($"{indent}    {trimmed}");
						lineCount++;
					}
				}
			}

			if (error.InnerError is not null)
			{
				sb.AppendLine($"{indent}  Caused by:");
				AppendErrorDetails(sb, error.InnerError, depth + 1);
			}
		}

		// ========== FACTORY METHODS ==========

		/// <summary>
		/// Creates a validation error
		/// </summary>
		public static Error ValidationError(string code, string message) =>
				new(ErrorType.Validation, code, message);

		/// <summary>
		/// Creates a validation error with a default code
		/// </summary>
		public static Error ValidationError(string message) =>
				new(ErrorType.Validation, "Validation.Failed", message);

		/// <summary>
		/// Creates a permission error
		/// </summary>
		public static Error PermissionError(string code, string message) =>
				new(ErrorType.Permission, code, message);

		/// <summary>
		/// Creates a permission error with a default code
		/// </summary>
		public static Error PermissionError(string message) =>
				new(ErrorType.Permission, "Permission.Denied", message);

		/// <summary>
		/// Creates an unauthorized error (user not authenticated)
		/// </summary>
		public static Error UnauthorizedError(string code, string message) =>
				new(ErrorType.Unauthorized, code, message);

		/// <summary>
		/// Creates an unauthorized error with a default code
		/// </summary>
		public static Error UnauthorizedError(string message) =>
				new(ErrorType.Unauthorized, "Unauthorized", message);

		/// <summary>
		/// Creates a database error
		/// </summary>
		public static Error DatabaseError(string code, string message) =>
				new(ErrorType.Database, code, message);     /// <summary>
																										/// Creates a database error with a default code
																										/// </summary>
		public static Error DatabaseError(string message) =>
				new(ErrorType.Database, "Database.Error", message);

		/// <summary>
		/// Creates a business logic error
		/// </summary>
		public static Error BusinessError(string code, string message) =>
				new(ErrorType.Business, code, message);

		/// <summary>
		/// Creates a business logic error with a default code
		/// </summary>
		public static Error BusinessError(string message) =>
				new(ErrorType.Business, "Business.RuleViolation", message);

		/// <summary>
		/// Creates a not found error
		/// </summary>
		public static Error NotFoundError(string code, string message) =>
				new(ErrorType.NotFound, code, message);

		/// <summary>
		/// Creates a not found error with a default code
		/// </summary>
		public static Error NotFoundError(string message) =>
				new(ErrorType.NotFound, "NotFound", message);

		/// <summary>
		/// Creates a conflict error
		/// </summary>
		public static Error ConflictError(string code, string message) =>
				new(ErrorType.Conflict, code, message);

		/// <summary>
		/// Creates a conflict error with a default code
		/// </summary>
		public static Error ConflictError(string message) =>
				new(ErrorType.Conflict, "Conflict", message);



		/// <summary>
		/// Creates an unavailable error (temporary unavailability)
		/// </summary>
		public static Error UnavailableError(string code, string message) =>
				new(ErrorType.Unavailable, code, message);

		/// <summary>
		/// Creates an unavailable error with a default code
		/// </summary>
		public static Error UnavailableError(string message) =>
				new(ErrorType.Unavailable, "Service.Unavailable", message);

		/// <summary>
		/// Creates a timeout error
		/// </summary>
		public static Error TimeoutError(string code, string message) =>
				new(ErrorType.Timeout, code, message);

		/// <summary>
		/// Creates a timeout error with a default code
		/// </summary>
		public static Error TimeoutError(string message) =>
				new(ErrorType.Timeout, "Operation.Timeout", message);
		/// <summary>
		/// Creates a cancelled error
		/// </summary>
		public static Error CancelledError(string code, string message) =>
				new(ErrorType.Cancelled, code, message);

		/// <summary>
		/// Creates a cancelled error with a default code
		/// </summary>
		public static Error CancelledError(string message) =>
				new(ErrorType.Cancelled, "Operation.Cancelled", message);

		/// <summary>
		/// Creates a circuit breaker open error
		/// </summary>
		public static Error CircuitBreakerOpenError(string code, string message) =>
				new(ErrorType.CircuitBreakerOpen, code, message);

		/// <summary>
		/// Creates a circuit breaker open error with a default code
		/// </summary>
		public static Error CircuitBreakerOpenError(string message) =>
				new(ErrorType.CircuitBreakerOpen, "CircuitBreaker.Open", message);

		/// <summary>
		/// Creates a circuit breaker open error with context from the last failure
		/// </summary>
		/// <param name="lastError">The error that caused the circuit breaker to open</param>
		/// <returns>Circuit breaker error with context from the last failure</returns>
		public static Error CircuitBreakerOpenError(Error lastError) =>
				new(ErrorType.CircuitBreakerOpen,
					"CircuitBreaker.Open",
					$"Circuit breaker open. Last error: [{lastError.Type}] {lastError.Message}");

		/// <summary>
		/// Creates an unexpected error
		/// </summary>
		public static Error UnexpectedError(string code, string message) =>
				new(ErrorType.Unexpected, code, message);

		/// <summary>
		/// Creates an unexpected error with a default code
		/// </summary>
		public static Error UnexpectedError(string message) =>
				new(ErrorType.Unexpected, "Unexpected.Error", message);

		/// <summary>
		/// Creates an error from an exception, preserving diagnostic information as strings.
		/// The original Exception is NOT stored - only string copies are kept,
		/// allowing GC to collect the Exception immediately.
		/// </summary>
		public static Error FromException(Exception exception)
		{
			var errorType = MapExceptionToErrorType(exception);
			var code = $"Exception.{exception.GetType().Name}";

			return new Error(errorType, code, exception.Message)
			{
				StackTrace = exception.StackTrace,
				ExceptionType = exception.GetType().FullName,
				Source = exception.Source,
				InnerError = exception.InnerException is not null
					? FromException(exception.InnerException)
					: null
			};
		}

		/// <summary>
		/// Creates an error from an exception with a custom error type.
		/// </summary>
		public static Error FromException(Exception exception, ErrorType errorType)
		{
			var code = $"Exception.{exception.GetType().Name}";

			return new Error(errorType, code, exception.Message)
			{
				StackTrace = exception.StackTrace,
				ExceptionType = exception.GetType().FullName,
				Source = exception.Source,
				InnerError = exception.InnerException is not null
					? FromException(exception.InnerException)
					: null
			};
		}

		/// <summary>
		/// Maps common exception types to appropriate ErrorType.
		/// </summary>
		private static ErrorType MapExceptionToErrorType(Exception exception)
		{
			return exception switch
			{
				OperationCanceledException => ErrorType.Cancelled,
				TimeoutException => ErrorType.Timeout,
				UnauthorizedAccessException => ErrorType.Permission,
				ArgumentException => ErrorType.Validation,
				InvalidOperationException => ErrorType.Business,
				KeyNotFoundException => ErrorType.NotFound,
				// Check by name to avoid hard dependencies on specific assemblies
				_ when exception.GetType().Name.Contains("Sql") => ErrorType.Database,
				_ when exception.GetType().Name.Contains("Db") => ErrorType.Database,
				_ when exception.GetType().Name == "HttpRequestException" => ErrorType.Unavailable,
				_ when exception.GetType().Name.Contains("Socket") => ErrorType.Unavailable,
				_ when exception.GetType().Name.Contains("WebException") => ErrorType.Unavailable,
				_ => ErrorType.Unexpected
			};
		}

		/// <summary>
		/// Creates a too many requests error (rate limiting)
		/// </summary>
		public static Error TooManyRequestsError(string code, string message) =>
				new(ErrorType.TooManyRequests, code, message);

		/// <summary>
		/// Creates a too many requests error with a default code
		/// </summary>
		public static Error TooManyRequestsError(string message) =>
				new(ErrorType.TooManyRequests, "RateLimit.Exceeded", message);
	}
}