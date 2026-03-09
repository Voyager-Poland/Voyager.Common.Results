using Voyager.Common.Results;
using Voyager.Common.Results.Extensions;

namespace Voyager.Common.Results.Tests;

public class ResultQueryExtensionsTests
{
	// ========== IsNotFound — Result<T> ==========

	[Fact]
	public void IsNotFound_GenericResult_WithNotFoundError_ReturnsTrue()
	{
		// Arrange
		var result = Result<string>.Failure(Error.NotFoundError("not found"));

		// Act & Assert
		Assert.True(result.IsNotFound());
	}

	[Fact]
	public void IsNotFound_GenericResult_WithOtherError_ReturnsFalse()
	{
		// Arrange
		var result = Result<string>.Failure(Error.ValidationError("invalid"));

		// Act & Assert
		Assert.False(result.IsNotFound());
	}

	[Fact]
	public void IsNotFound_GenericResult_WithSuccess_ReturnsFalse()
	{
		// Arrange
		var result = Result<string>.Success("hello");

		// Act & Assert
		Assert.False(result.IsNotFound());
	}

	// ========== IsNotFound — Result (non-generic) ==========

	[Fact]
	public void IsNotFound_Result_WithNotFoundError_ReturnsTrue()
	{
		// Arrange
		var result = Result.Failure(Error.NotFoundError("not found"));

		// Act & Assert
		Assert.True(result.IsNotFound());
	}

	[Fact]
	public void IsNotFound_Result_WithOtherError_ReturnsFalse()
	{
		// Arrange
		var result = Result.Failure(Error.DatabaseError("db error"));

		// Act & Assert
		Assert.False(result.IsNotFound());
	}

	[Fact]
	public void IsNotFound_Result_WithSuccess_ReturnsFalse()
	{
		// Arrange
		var result = Result.Success();

		// Act & Assert
		Assert.False(result.IsNotFound());
	}

	// ========== IsNotFound — all other ErrorTypes return false ==========

	[Theory]
	[InlineData(ErrorType.Validation)]
	[InlineData(ErrorType.Permission)]
	[InlineData(ErrorType.Unauthorized)]
	[InlineData(ErrorType.Database)]
	[InlineData(ErrorType.Business)]
	[InlineData(ErrorType.Conflict)]
	[InlineData(ErrorType.Unavailable)]
	[InlineData(ErrorType.Timeout)]
	[InlineData(ErrorType.Cancelled)]
	[InlineData(ErrorType.Unexpected)]
	[InlineData(ErrorType.CircuitBreakerOpen)]
	[InlineData(ErrorType.TooManyRequests)]
	public void IsNotFound_GenericResult_OtherErrorTypes_ReturnsFalse(ErrorType errorType)
	{
		// Arrange
		var error = new Error(errorType, "test", "test message");
		var result = Result<string>.Failure(error);

		// Act & Assert
		Assert.False(result.IsNotFound());
	}

	// ========== Integration with NullToResult ==========

	[Fact]
	public void IsNotFound_WorksWithNullToResult()
	{
		// Arrange
		string? value = null;

		// Act
		var result = value.NullToResult("item not found");

		// Assert
		Assert.True(result.IsNotFound());
	}

	[Fact]
	public void IsNotFound_NullToResultWithCustomError_ReturnsFalse()
	{
		// Arrange
		string? value = null;

		// Act
		var result = value.NullToResult(Error.ValidationError("required"));

		// Assert
		Assert.False(result.IsNotFound());
	}
}
