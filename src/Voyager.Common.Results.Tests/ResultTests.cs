using Voyager.Common.Results;

namespace Voyager.Common.Results.Tests;

public class ResultTests
{
	[Fact]
	public void Success_CreatesSuccessfulResult()
	{
		// Act
		var result = Result.Success();

		// Assert
		Assert.True(result.IsSuccess);
		Assert.False(result.IsFailure);
		Assert.True (result.Error.Type == ErrorType.None);
	}

	[Fact]
	public void Failure_CreatesFailedResult()
	{
		// Arrange
		var error = Error.ValidationError("Test error");

		// Act
		var result = Result.Failure(error);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);
	}

	[Fact]
	public void Match_WithSuccess_CallsOnSuccess()
	{
		// Arrange
		var result = Result.Success();
		var successCalled = false;
		var failureCalled = false;

		// Act
		result.Match(
			onSuccess: () => { successCalled = true; return 1; },
			onFailure: _ => { failureCalled = true; return 0; }
		);

		// Assert
		Assert.True(successCalled);
		Assert.False(failureCalled);
	}

	[Fact]
	public void Match_WithFailure_CallsOnFailure()
	{
		// Arrange
		var error = Error.ValidationError("Test error");
		var result = Result.Failure(error);
		var successCalled = false;
		var failureCalled = false;

		// Act
		result.Match(
			onSuccess: () => { successCalled = true; return 1; },
			onFailure: _ => { failureCalled = true; return 0; }
		);

		// Assert
		Assert.False(successCalled);
		Assert.True(failureCalled);
	}

	[Fact]
	public void ImplicitConversion_FromError_CreatesFailure()
	{
		// Arrange
		var error = Error.ValidationError("Test");

		// Act
		Result result = error;

		// Assert
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);
	}
}
