using Voyager.Common.Results;
using Voyager.Common.Results.Extensions;

namespace Voyager.Common.Results.Tests;

public class ResultErrorChainExtensionsTests
{
	[Fact]
	public void WrapError_OnSuccess_ReturnsOriginalResult()
	{
		// Arrange
		var result = Result<int>.Success(42);

		// Act
		var wrapped = result.WrapError(e => Error.UnavailableError("Wrapper", "Wrapped"));

		// Assert
		Assert.True(wrapped.IsSuccess);
		Assert.Equal(42, wrapped.Value);
	}

	[Fact]
	public void WrapError_OnFailure_WrapsErrorWithInner()
	{
		// Arrange
		var originalError = Error.NotFoundError("Original", "Original error");
		var result = Result<int>.Failure(originalError);

		// Act
		var wrapped = result.WrapError(e => Error.UnavailableError("Wrapper", $"Wrapped: {e.Message}"));

		// Assert
		Assert.True(wrapped.IsFailure);
		Assert.Equal(ErrorType.Unavailable, wrapped.Error.Type);
		Assert.Equal("Wrapper", wrapped.Error.Code);
		Assert.NotNull(wrapped.Error.InnerError);
		Assert.Equal(originalError, wrapped.Error.InnerError);
	}

	[Fact]
	public void WrapError_NonGeneric_OnSuccess_ReturnsOriginalResult()
	{
		// Arrange
		var result = Result.Success();

		// Act
		var wrapped = result.WrapError(e => Error.UnavailableError("Wrapper", "Wrapped"));

		// Assert
		Assert.True(wrapped.IsSuccess);
	}

	[Fact]
	public void WrapError_NonGeneric_OnFailure_WrapsErrorWithInner()
	{
		// Arrange
		var originalError = Error.NotFoundError("Original", "Original error");
		var result = Result.Failure(originalError);

		// Act
		var wrapped = result.WrapError(e => Error.UnavailableError("Wrapper", $"Wrapped: {e.Message}"));

		// Assert
		Assert.True(wrapped.IsFailure);
		Assert.Equal(ErrorType.Unavailable, wrapped.Error.Type);
		Assert.NotNull(wrapped.Error.InnerError);
		Assert.Equal(originalError, wrapped.Error.InnerError);
	}

	[Fact]
	public void AddErrorContext_OnSuccess_ReturnsOriginalResult()
	{
		// Arrange
		var result = Result<int>.Success(42);

		// Act
		var contextual = result.AddErrorContext("ProductService", "GetProduct");

		// Assert
		Assert.True(contextual.IsSuccess);
		Assert.Equal(42, contextual.Value);
	}

	[Fact]
	public void AddErrorContext_OnFailure_AddsContext()
	{
		// Arrange
		var originalError = Error.NotFoundError("Product.NotFound", "Product 123 not found");
		var result = Result<int>.Failure(originalError);

		// Act
		var contextual = result.AddErrorContext("ProductService", "GetProduct");

		// Assert
		Assert.True(contextual.IsFailure);
		Assert.Equal(ErrorType.NotFound, contextual.Error.Type); // Preserves original type
		Assert.Equal("ProductService.GetProduct.Failed", contextual.Error.Code);
		Assert.Contains("ProductService.GetProduct failed", contextual.Error.Message);
		Assert.NotNull(contextual.Error.InnerError);
		Assert.Equal(originalError, contextual.Error.InnerError);
	}

	[Fact]
	public void AddErrorContext_NonGeneric_OnFailure_AddsContext()
	{
		// Arrange
		var originalError = Error.ValidationError("Invalid", "Invalid input");
		var result = Result.Failure(originalError);

		// Act
		var contextual = result.AddErrorContext("UserService", "Validate");

		// Assert
		Assert.True(contextual.IsFailure);
		Assert.Equal(ErrorType.Validation, contextual.Error.Type);
		Assert.Equal("UserService.Validate.Failed", contextual.Error.Code);
		Assert.NotNull(contextual.Error.InnerError);
	}

	[Fact]
	public async Task WrapErrorAsync_OnSuccess_ReturnsOriginalResult()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(42));

		// Act
		var wrapped = await resultTask.WrapErrorAsync(e => Error.UnavailableError("Wrapper", "Wrapped"));

		// Assert
		Assert.True(wrapped.IsSuccess);
		Assert.Equal(42, wrapped.Value);
	}

	[Fact]
	public async Task WrapErrorAsync_OnFailure_WrapsErrorWithInner()
	{
		// Arrange
		var originalError = Error.NotFoundError("Original", "Original error");
		var resultTask = Task.FromResult(Result<int>.Failure(originalError));

		// Act
		var wrapped = await resultTask.WrapErrorAsync(e => Error.UnavailableError("Wrapper", "Wrapped"));

		// Assert
		Assert.True(wrapped.IsFailure);
		Assert.Equal(ErrorType.Unavailable, wrapped.Error.Type);
		Assert.NotNull(wrapped.Error.InnerError);
		Assert.Equal(originalError, wrapped.Error.InnerError);
	}

	[Fact]
	public async Task AddErrorContextAsync_OnSuccess_ReturnsOriginalResult()
	{
		// Arrange
		var resultTask = Task.FromResult(Result<int>.Success(42));

		// Act
		var contextual = await resultTask.AddErrorContextAsync("ProductService", "GetProduct");

		// Assert
		Assert.True(contextual.IsSuccess);
	}

	[Fact]
	public async Task AddErrorContextAsync_OnFailure_AddsContext()
	{
		// Arrange
		var originalError = Error.TimeoutError("Timeout", "Request timed out");
		var resultTask = Task.FromResult(Result<int>.Failure(originalError));

		// Act
		var contextual = await resultTask.AddErrorContextAsync("ExternalApi", "FetchData");

		// Assert
		Assert.True(contextual.IsFailure);
		Assert.Equal(ErrorType.Timeout, contextual.Error.Type);
		Assert.Equal("ExternalApi.FetchData.Failed", contextual.Error.Code);
		Assert.NotNull(contextual.Error.InnerError);
	}

	[Fact]
	public void ChainedContext_PreservesFullChain()
	{
		// Arrange - simulate A -> B -> C chain
		var errorC = Error.NotFoundError("C.NotFound", "Resource not found in C");
		var resultC = Result<int>.Failure(errorC);

		// Act - simulate error propagation through services
		var resultB = resultC.AddErrorContext("ServiceB", "CallC");
		var resultA = resultB.AddErrorContext("ServiceA", "CallB");

		// Assert
		Assert.True(resultA.IsFailure);
		Assert.Equal("ServiceA.CallB.Failed", resultA.Error.Code);

		var innerB = resultA.Error.InnerError;
		Assert.NotNull(innerB);
		Assert.Equal("ServiceB.CallC.Failed", innerB.Code);

		var innerC = innerB.InnerError;
		Assert.NotNull(innerC);
		Assert.Equal("C.NotFound", innerC.Code);

		// Root cause should be C's error
		Assert.Equal(errorC, resultA.Error.GetRootCause());
	}
}
