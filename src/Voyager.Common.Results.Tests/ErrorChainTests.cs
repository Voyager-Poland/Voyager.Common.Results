using Voyager.Common.Results;
using Voyager.Common.Results.Extensions;

namespace Voyager.Common.Results.Tests;

public class ErrorChainTests
{
	[Fact]
	public void WithInner_CreatesChain()
	{
		// Arrange
		var inner = Error.NotFoundError("Product.NotFound", "Product not found");

		// Act
		var outer = Error.UnavailableError("Order.Failed", "Order processing failed")
			.WithInner(inner);

		// Assert
		Assert.NotNull(outer.InnerError);
		Assert.Equal(inner, outer.InnerError);
		Assert.Equal(ErrorType.Unavailable, outer.Type);
		Assert.Equal(ErrorType.NotFound, outer.InnerError.Type);
	}

	[Fact]
	public void WithInner_PreservesOriginalError()
	{
		// Arrange
		var inner = Error.NotFoundError("Product.NotFound", "Product not found");
		var original = Error.UnavailableError("Order.Failed", "Order processing failed");

		// Act
		var chained = original.WithInner(inner);

		// Assert - original is unchanged (immutability)
		Assert.Null(original.InnerError);
		Assert.NotNull(chained.InnerError);
	}

	[Fact]
	public void GetRootCause_ReturnsSelf_WhenNoInnerError()
	{
		// Arrange
		var error = Error.NotFoundError("Test", "Test message");

		// Act
		var rootCause = error.GetRootCause();

		// Assert
		Assert.Equal(error, rootCause);
	}

	[Fact]
	public void GetRootCause_ReturnsDeepestError()
	{
		// Arrange
		var root = Error.NotFoundError("Deep.Error", "Root cause");
		var middle = Error.DatabaseError("Middle.Error", "Middle").WithInner(root);
		var top = Error.UnavailableError("Top.Error", "Top").WithInner(middle);

		// Act
		var rootCause = top.GetRootCause();

		// Assert
		Assert.Equal(root, rootCause);
		Assert.Equal(ErrorType.NotFound, rootCause.Type);
	}

	[Fact]
	public void HasInChain_ReturnsTrue_WhenPredicateMatchesTop()
	{
		// Arrange
		var inner = Error.NotFoundError("Inner", "Inner error");
		var outer = Error.UnavailableError("Outer", "Outer error").WithInner(inner);

		// Act
		var result = outer.HasInChain(e => e.Type == ErrorType.Unavailable);

		// Assert
		Assert.True(result);
	}

	[Fact]
	public void HasInChain_ReturnsTrue_WhenPredicateMatchesInner()
	{
		// Arrange
		var inner = Error.NotFoundError("Inner", "Inner error");
		var outer = Error.UnavailableError("Outer", "Outer error").WithInner(inner);

		// Act
		var result = outer.HasInChain(e => e.Type == ErrorType.NotFound);

		// Assert
		Assert.True(result);
	}

	[Fact]
	public void HasInChain_ReturnsFalse_WhenNoMatch()
	{
		// Arrange
		var inner = Error.NotFoundError("Inner", "Inner error");
		var outer = Error.UnavailableError("Outer", "Outer error").WithInner(inner);

		// Act
		var result = outer.HasInChain(e => e.Type == ErrorType.Validation);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void HasInChain_FindsErrorByCode()
	{
		// Arrange
		var inner = Error.NotFoundError("Product.NotFound", "Product not found");
		var outer = Error.UnavailableError("Service.Failed", "Service failed").WithInner(inner);

		// Act
		var result = outer.HasInChain(e => e.Code == "Product.NotFound");

		// Assert
		Assert.True(result);
	}

	[Fact]
	public void TooManyRequestsError_CreatesCorrectError()
	{
		// Act
		var error = Error.TooManyRequestsError("Rate limit exceeded");

		// Assert
		Assert.Equal(ErrorType.TooManyRequests, error.Type);
		Assert.Equal("RateLimit.Exceeded", error.Code);
		Assert.Equal("Rate limit exceeded", error.Message);
	}

	[Fact]
	public void TooManyRequestsError_WithCode_CreatesCorrectError()
	{
		// Act
		var error = Error.TooManyRequestsError("Api.RateLimit", "Too many API calls");

		// Assert
		Assert.Equal(ErrorType.TooManyRequests, error.Type);
		Assert.Equal("Api.RateLimit", error.Code);
		Assert.Equal("Too many API calls", error.Message);
	}

	[Fact]
	public void InnerError_IsNull_ByDefault()
	{
		// Act
		var error = Error.ValidationError("Test", "Test message");

		// Assert
		Assert.Null(error.InnerError);
	}

	[Fact]
	public void ChainOfThree_GetRootCause_ReturnsDeepest()
	{
		// Arrange - simulate A -> B -> C chain
		var errorC = Error.NotFoundError("C.NotFound", "Resource C not found");
		var errorB = Error.DatabaseError("B.Failed", "Service B failed").WithInner(errorC);
		var errorA = Error.UnavailableError("A.Failed", "Service A failed").WithInner(errorB);

		// Act
		var rootCause = errorA.GetRootCause();

		// Assert
		Assert.Equal(errorC, rootCause);
		Assert.Equal("C.NotFound", rootCause.Code);
	}
}
