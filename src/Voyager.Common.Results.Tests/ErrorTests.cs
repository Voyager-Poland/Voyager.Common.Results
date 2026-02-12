using Voyager.Common.Results;

namespace Voyager.Common.Results.Tests;

public class ErrorTests
{
	[Fact]
	public void ValidationError_CreatesValidationError()
	{
		// Act
		var error = Error.ValidationError("Test message");

		// Assert
		Assert.Equal(ErrorType.Validation, error.Type);
		Assert.Equal("Validation.Failed", error.Code);
		Assert.Equal("Test message", error.Message);
	}

	[Fact]
	public void ValidationError_WithCode_CreatesValidationError()
	{
		// Act
		var error = Error.ValidationError("Custom.Code", "Test message");

		// Assert
		Assert.Equal(ErrorType.Validation, error.Type);
		Assert.Equal("Custom.Code", error.Code);
		Assert.Equal("Test message", error.Message);
	}

	[Fact]
	public void PermissionError_CreatesPermissionError()
	{
		// Act
		var error = Error.PermissionError("Access denied");

		// Assert
		Assert.Equal(ErrorType.Permission, error.Type);
		Assert.Equal("Permission.Denied", error.Code);
		Assert.Equal("Access denied", error.Message);
	}

	[Fact]
	public void UnauthorizedError_CreatesUnauthorizedError()
	{
		// Act
		var error = Error.UnauthorizedError("User not logged in");

		// Assert
		Assert.Equal(ErrorType.Unauthorized, error.Type);
		Assert.Equal("Unauthorized", error.Code);
		Assert.Equal("User not logged in", error.Message);
	}

	[Fact]
	public void UnauthorizedError_WithCode_CreatesUnauthorizedError()
	{
		// Act
		var error = Error.UnauthorizedError("Auth.TokenExpired", "Session expired");

		// Assert
		Assert.Equal(ErrorType.Unauthorized, error.Type);
		Assert.Equal("Auth.TokenExpired", error.Code);
		Assert.Equal("Session expired", error.Message);
	}

	[Fact]
	public void DatabaseError_CreatesDatabaseError()
	{
		// Act
		var error = Error.DatabaseError("Connection failed");

		// Assert
		Assert.Equal(ErrorType.Database, error.Type);
		Assert.Equal("Database.Error", error.Code);
		Assert.Equal("Connection failed", error.Message);
	}

	[Fact]
	public void BusinessError_CreatesBusinessError()
	{
		// Act
		var error = Error.BusinessError("Invalid operation");

		// Assert
		Assert.Equal(ErrorType.Business, error.Type);
		Assert.Equal("Business.RuleViolation", error.Code);
		Assert.Equal("Invalid operation", error.Message);
	}

	[Fact]
	public void NotFoundError_CreatesNotFoundError()
	{
		// Act
		var error = Error.NotFoundError("Resource not found");

		// Assert
		Assert.Equal(ErrorType.NotFound, error.Type);
		Assert.Equal("NotFound", error.Code);
		Assert.Equal("Resource not found", error.Message);
	}

	[Fact]
	public void ConflictError_CreatesConflictError()
	{
		// Act
		var error = Error.ConflictError("Duplicate entry");

		// Assert
		Assert.Equal(ErrorType.Conflict, error.Type);
		Assert.Equal("Conflict", error.Code);
		Assert.Equal("Duplicate entry", error.Message);
	}

	[Fact]
	public void UnavailableError_CreatesUnavailableError()
	{
		// Act
		var error = Error.UnavailableError("Service temporarily down");

		// Assert
		Assert.Equal(ErrorType.Unavailable, error.Type);
		Assert.Equal("Service.Unavailable", error.Code);
		Assert.Equal("Service temporarily down", error.Message);
	}

	[Fact]
	public void UnavailableError_WithCode_CreatesUnavailableError()
	{
		// Act
		var error = Error.UnavailableError("RateLimit.Exceeded", "Too many requests");

		// Assert
		Assert.Equal(ErrorType.Unavailable, error.Type);
		Assert.Equal("RateLimit.Exceeded", error.Code);
		Assert.Equal("Too many requests", error.Message);
	}

	[Fact]
	public void TimeoutError_CreatesTimeoutError()
	{
		// Act
		var error = Error.TimeoutError("Operation exceeded time limit");

		// Assert
		Assert.Equal(ErrorType.Timeout, error.Type);
		Assert.Equal("Operation.Timeout", error.Code);
		Assert.Equal("Operation exceeded time limit", error.Message);
	}

	[Fact]
	public void TimeoutError_WithCode_CreatesTimeoutError()
	{
		// Act
		var error = Error.TimeoutError("Database.Timeout", "Query timeout after 30 seconds");

		// Assert
		Assert.Equal(ErrorType.Timeout, error.Type);
		Assert.Equal("Database.Timeout", error.Code);
		Assert.Equal("Query timeout after 30 seconds", error.Message);
	}

	[Fact]
	public void UnexpectedError_CreatesUnexpectedError()
	{
		// Act
		var error = Error.UnexpectedError("Something went wrong");

		// Assert
		Assert.Equal(ErrorType.Unexpected, error.Type);
		Assert.Equal("Unexpected.Error", error.Code);
		Assert.Equal("Something went wrong", error.Message);
	}

	[Fact]
	public void FromException_MapsExceptionTypeAndPreservesDetails()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");

		// Act
		var error = Error.FromException(exception);

		// Assert - InvalidOperationException maps to Business
		Assert.Equal(ErrorType.Business, error.Type);
		Assert.Equal("Exception.InvalidOperationException", error.Code);
		Assert.Equal("Test exception", error.Message);
		Assert.Equal("System.InvalidOperationException", error.ExceptionType);
	}

	[Fact]
	public void FromException_UnknownExceptionMapsToUnexpected()
	{
		// Arrange
		var exception = new NotSupportedException("Test exception");

		// Act
		var error = Error.FromException(exception);

		// Assert
		Assert.Equal(ErrorType.Unexpected, error.Type);
		Assert.Equal("Exception.NotSupportedException", error.Code);
		Assert.Equal("Test exception", error.Message);
	}

	[Fact]
	public void None_IsEmptyError()
	{
		// Act
		var error = Error.None;

		// Assert
		Assert.Equal(ErrorType.None, error.Type);
		Assert.Equal(string.Empty, error.Code);
		Assert.Equal(string.Empty, error.Message);
	}
}
