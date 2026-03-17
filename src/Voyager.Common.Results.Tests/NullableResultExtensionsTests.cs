using Voyager.Common.Results;
using Voyager.Common.Results.Extensions;

namespace Voyager.Common.Results.Tests;

public class NullableResultExtensionsTests
{
	// ========== REFERENCE TYPES: NullToResult(string) ==========

	[Fact]
	public void NullToResult_String_NonNullReference_ReturnsSuccess()
	{
		// Arrange
		string? value = "hello";

		// Act
		var result = value.NullToResult("not found");

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal("hello", result.Value);
	}

	[Fact]
	public void NullToResult_String_NullReference_ReturnsNotFoundFailure()
	{
		// Arrange
		string? value = null;

		// Act
		var result = value.NullToResult("item not found");

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.NotFound, result.Error.Type);
		Assert.Equal("item not found", result.Error.Message);
	}

	// ========== REFERENCE TYPES: NullToResult(Error) ==========

	[Fact]
	public void NullToResult_Error_NonNullReference_ReturnsSuccess()
	{
		// Arrange
		string? value = "hello";

		// Act
		var result = value.NullToResult(Error.ValidationError("custom error"));

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal("hello", result.Value);
	}

	[Fact]
	public void NullToResult_Error_NullReference_ReturnsFailureWithGivenError()
	{
		// Arrange
		string? value = null;
		var error = Error.ValidationError("required field missing");

		// Act
		var result = value.NullToResult(error);

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error.Type);
		Assert.Equal("required field missing", result.Error.Message);
	}

	// ========== REFERENCE TYPES: NullToResult(Func<Error>) ==========

	[Fact]
	public void NullToResult_ErrorFactory_NonNullReference_DoesNotInvokeFactory()
	{
		// Arrange
		string? value = "hello";
		var factoryInvoked = false;

		// Act
		var result = value.NullToResult(() =>
		{
			factoryInvoked = true;
			return Error.NotFoundError("should not be called");
		});

		// Assert
		Assert.True(result.IsSuccess);
		Assert.False(factoryInvoked);
	}

	[Fact]
	public void NullToResult_ErrorFactory_NullReference_InvokesFactory()
	{
		// Arrange
		string? value = null;

		// Act
		var result = value.NullToResult(() => Error.BusinessError("lazy error"));

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Business, result.Error.Type);
		Assert.Equal("lazy error", result.Error.Message);
	}

	// ========== VALUE TYPES: NullToResult(string) ==========

	[Fact]
	public void NullToResult_String_NonNullValueType_ReturnsSuccess()
	{
		// Arrange
		int? value = 42;

		// Act
		var result = value.NullToResult("value not found");

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(42, result.Value);
	}

	[Fact]
	public void NullToResult_String_NullValueType_ReturnsNotFoundFailure()
	{
		// Arrange
		int? value = null;

		// Act
		var result = value.NullToResult("value not found");

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.NotFound, result.Error.Type);
		Assert.Equal("value not found", result.Error.Message);
	}

	// ========== VALUE TYPES: NullToResult(Error) ==========

	[Fact]
	public void NullToResult_Error_NonNullValueType_ReturnsSuccess()
	{
		// Arrange
		DateTime? value = new DateTime(2026, 1, 1);

		// Act
		var result = value.NullToResult(Error.ValidationError("date required"));

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(new DateTime(2026, 1, 1), result.Value);
	}

	[Fact]
	public void NullToResult_Error_NullValueType_ReturnsFailureWithGivenError()
	{
		// Arrange
		DateTime? value = null;
		var error = Error.ValidationError("date required");

		// Act
		var result = value.NullToResult(error);

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Validation, result.Error.Type);
	}

	// ========== VALUE TYPES: NullToResult(Func<Error>) ==========

	[Fact]
	public void NullToResult_ErrorFactory_NonNullValueType_DoesNotInvokeFactory()
	{
		// Arrange
		int? value = 7;
		var factoryInvoked = false;

		// Act
		var result = value.NullToResult(() =>
		{
			factoryInvoked = true;
			return Error.NotFoundError("should not be called");
		});

		// Assert
		Assert.True(result.IsSuccess);
		Assert.False(factoryInvoked);
	}

	[Fact]
	public void NullToResult_ErrorFactory_NullValueType_InvokesFactory()
	{
		// Arrange
		int? value = null;

		// Act
		var result = value.NullToResult(() => Error.DatabaseError("lazy error"));

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.Database, result.Error.Type);
	}

	// ========== CHAINING ==========

	[Fact]
	public void NullToResult_CanChainWithBind()
	{
		// Arrange
		string? value = "hello";

		// Act
		var result = value
			.NullToResult("not found")
			.Bind(v => Result<int>.Success(v.Length));

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(5, result.Value);
	}

	[Fact]
	public void NullToResult_NullPropagatesThroughChain()
	{
		// Arrange
		string? value = null;

		// Act
		var result = value
			.NullToResult("not found")
			.Map(v => v.Length)
			.Map(len => len * 2);

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(ErrorType.NotFound, result.Error.Type);
		Assert.Equal("not found", result.Error.Message);
	}

	[Fact]
	public void NullToResult_CanChainWithOrElse()
	{
		// Arrange
		string? primary = null;
		string? fallback = "fallback value";

		// Act
		var result = primary
			.NullToResult("primary not found")
			.OrElse(() => fallback.NullToResult("fallback not found"));

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal("fallback value", result.Value);
	}

	[Fact]
	public void NullToResult_BothNullsChainedWithOrElse_ReturnsLastError()
	{
		// Arrange
		string? primary = null;
		string? fallback = null;

		// Act
		var result = primary
			.NullToResult("primary not found")
			.OrElse(() => fallback.NullToResult("fallback not found"));

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal("fallback not found", result.Error.Message);
	}

	// ========== DEFAULT ERROR CODE ==========

	[Fact]
	public void NullToResult_String_UsesNotFoundDefaultCode()
	{
		// Arrange
		string? value = null;

		// Act
		var result = value.NullToResult("test message");

		// Assert
		Assert.Equal("NotFound", result.Error.Code);
	}
}
