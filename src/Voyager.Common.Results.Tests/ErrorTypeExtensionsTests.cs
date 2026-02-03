using Voyager.Common.Results;
using Voyager.Common.Results.Extensions;

namespace Voyager.Common.Results.Tests;

public class ErrorTypeExtensionsTests
{
	[Theory]
	[InlineData(ErrorType.Timeout, true)]
	[InlineData(ErrorType.Unavailable, true)]
	[InlineData(ErrorType.CircuitBreakerOpen, true)]
	[InlineData(ErrorType.TooManyRequests, true)]
	[InlineData(ErrorType.Validation, false)]
	[InlineData(ErrorType.Database, false)]
	[InlineData(ErrorType.NotFound, false)]
	[InlineData(ErrorType.Business, false)]
	public void IsTransient_ReturnsCorrectValue(ErrorType type, bool expected)
	{
		// Act
		var result = type.IsTransient();

		// Assert
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData(ErrorType.Validation, true)]
	[InlineData(ErrorType.Business, true)]
	[InlineData(ErrorType.NotFound, true)]
	[InlineData(ErrorType.Unauthorized, true)]
	[InlineData(ErrorType.Permission, true)]
	[InlineData(ErrorType.Conflict, true)]
	[InlineData(ErrorType.Cancelled, true)]
	[InlineData(ErrorType.Timeout, false)]
	[InlineData(ErrorType.Database, false)]
	[InlineData(ErrorType.Unavailable, false)]
	public void IsBusinessError_ReturnsCorrectValue(ErrorType type, bool expected)
	{
		// Act
		var result = type.IsBusinessError();

		// Assert
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData(ErrorType.Database, true)]
	[InlineData(ErrorType.Unexpected, true)]
	[InlineData(ErrorType.Timeout, false)]
	[InlineData(ErrorType.Validation, false)]
	[InlineData(ErrorType.NotFound, false)]
	public void IsInfrastructureError_ReturnsCorrectValue(ErrorType type, bool expected)
	{
		// Act
		var result = type.IsInfrastructureError();

		// Assert
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData(ErrorType.Timeout, true)]
	[InlineData(ErrorType.Unavailable, true)]
	[InlineData(ErrorType.TooManyRequests, true)]
	[InlineData(ErrorType.Database, true)]
	[InlineData(ErrorType.Unexpected, true)]
	[InlineData(ErrorType.CircuitBreakerOpen, false)] // Protection mechanism, not a real failure
	[InlineData(ErrorType.Validation, false)]
	[InlineData(ErrorType.NotFound, false)]
	[InlineData(ErrorType.Business, false)]
	public void ShouldCountForCircuitBreaker_ReturnsCorrectValue(ErrorType type, bool expected)
	{
		// Act
		var result = type.ShouldCountForCircuitBreaker();

		// Assert
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData(ErrorType.Timeout, true)]
	[InlineData(ErrorType.Unavailable, true)]
	[InlineData(ErrorType.CircuitBreakerOpen, true)]
	[InlineData(ErrorType.TooManyRequests, true)]
	[InlineData(ErrorType.Validation, false)]
	[InlineData(ErrorType.Database, false)]
	[InlineData(ErrorType.NotFound, false)]
	public void ShouldRetry_ReturnsCorrectValue(ErrorType type, bool expected)
	{
		// Act
		var result = type.ShouldRetry();

		// Assert
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData(ErrorType.None, 200)]
	[InlineData(ErrorType.Validation, 400)]
	[InlineData(ErrorType.Business, 400)]
	[InlineData(ErrorType.Unauthorized, 401)]
	[InlineData(ErrorType.Permission, 403)]
	[InlineData(ErrorType.NotFound, 404)]
	[InlineData(ErrorType.Conflict, 409)]
	[InlineData(ErrorType.Cancelled, 499)]
	[InlineData(ErrorType.TooManyRequests, 429)]
	[InlineData(ErrorType.Timeout, 504)]
	[InlineData(ErrorType.Unavailable, 503)]
	[InlineData(ErrorType.CircuitBreakerOpen, 503)]
	[InlineData(ErrorType.Database, 500)]
	[InlineData(ErrorType.Unexpected, 500)]
	public void ToHttpStatusCode_ReturnsCorrectCode(ErrorType type, int expected)
	{
		// Act
		var result = type.ToHttpStatusCode();

		// Assert
		Assert.Equal(expected, result);
	}
}
