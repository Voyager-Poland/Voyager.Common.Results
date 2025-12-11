using Xunit;

namespace Voyager.Common.Results.Tests;

/// <summary>
/// Tests verifying that Result&lt;T&gt; and Result maintain critical invariants.
/// These properties must ALWAYS be true regardless of operations performed.
/// </summary>
public class InvariantTests
{
    #region XOR Property: IsSuccess XOR IsFailure

    [Fact]
    public void Success_MustHaveIsSuccessTrueAndIsFailureFalse()
    {
        // Arrange & Act
        var result = Result<int>.Success(42);

        // Assert - XOR: exactly one must be true
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.True(result.IsSuccess ^ result.IsFailure); // XOR check
    }

    [Fact]
    public void Failure_MustHaveIsSuccessFalseAndIsFailureTrue()
    {
        // Arrange & Act
        var result = Result<int>.Failure(Error.ValidationError("Error"));

        // Assert - XOR: exactly one must be true
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.True(result.IsSuccess ^ result.IsFailure); // XOR check
    }

    [Fact]
    public void NonGenericSuccess_MustHaveIsSuccessTrueAndIsFailureFalse()
    {
        // Arrange & Act
        var result = Result.Success();

        // Assert - XOR: exactly one must be true
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.True(result.IsSuccess ^ result.IsFailure); // XOR check
    }

    [Fact]
    public void NonGenericFailure_MustHaveIsSuccessFalseAndIsFailureTrue()
    {
        // Arrange & Act
        var result = Result.Failure(Error.NotFoundError("Not found"));

        // Assert - XOR: exactly one must be true
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.True(result.IsSuccess ^ result.IsFailure); // XOR check
    }

    #endregion

    #region Error Invariants

    [Fact]
    public void Success_MustHaveErrorNone()
    {
        // Arrange & Act
        var result = Result<string>.Success("test");

        // Assert
        Assert.Equal(ErrorType.None, result.Error.Type);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_MustHaveNonNoneError()
    {
        // Arrange
        var error = Error.ValidationError("VAL_001", "Validation failed");

        // Act
        var result = Result<string>.Failure(error);

        // Assert
        Assert.NotEqual(ErrorType.None, result.Error.Type);
        Assert.NotEqual(Error.None, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void NonGenericSuccess_MustHaveErrorNone()
    {
        // Arrange & Act
        var result = Result.Success();

        // Assert
        Assert.Equal(ErrorType.None, result.Error.Type);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void NonGenericFailure_MustHaveNonNoneError()
    {
        // Arrange
        var error = Error.DatabaseError("DB_001", "Database error");

        // Act
        var result = Result.Failure(error);

        // Assert
        Assert.NotEqual(ErrorType.None, result.Error.Type);
        Assert.NotEqual(Error.None, result.Error);
        Assert.Equal(ErrorType.Database, result.Error.Type);
    }

    #endregion

    #region Null Safety

    [Fact]
    public void Success_WithNullValue_ShouldStoreNull()
    {
        // Arrange & Act
        var result = Result<string?>.Success(null);

        // Assert - This is valid: nullable reference type can be null
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Success_WithNonNullValue_ShouldNeverBeNull()
    {
        // Arrange & Act
        var result = Result<string>.Success("test");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public void Failure_ValueShouldBeDefault()
    {
        // Arrange & Act
        var result = Result<int>.Failure(Error.ValidationError("Error"));

        // Assert - Failure should have default value
        Assert.True(result.IsFailure);
        Assert.Equal(default(int), result.Value);
    }

    [Fact]
    public void Failure_ReferenceType_ValueShouldBeNull()
    {
        // Arrange & Act
        var result = Result<string>.Failure(Error.NotFoundError("Not found"));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
    }

    #endregion

    #region Operation Invariants

    [Fact]
    public void Map_PreservesIsSuccessIsFailureInvariant()
    {
        // Arrange
        var success = Result<int>.Success(5);
        var failure = Result<int>.Failure(Error.ValidationError("Error"));

        // Act
        var mappedSuccess = success.Map(x => x * 2);
        var mappedFailure = failure.Map(x => x * 2);

        // Assert - XOR maintained after Map
        Assert.True(mappedSuccess.IsSuccess ^ mappedSuccess.IsFailure);
        Assert.True(mappedFailure.IsSuccess ^ mappedFailure.IsFailure);
    }

    [Fact]
    public void Bind_PreservesIsSuccessIsFailureInvariant()
    {
        // Arrange
        var success = Result<int>.Success(5);
        var failure = Result<int>.Failure(Error.ValidationError("Error"));

        // Act
        var boundSuccess = success.Bind(x => Result<int>.Success(x * 2));
        var boundFailure = failure.Bind(x => Result<int>.Success(x * 2));

        // Assert - XOR maintained after Bind
        Assert.True(boundSuccess.IsSuccess ^ boundSuccess.IsFailure);
        Assert.True(boundFailure.IsSuccess ^ boundFailure.IsFailure);
    }

    [Fact]
    public void Tap_PreservesIsSuccessIsFailureInvariant()
    {
        // Arrange
        var success = Result<int>.Success(5);
        var failure = Result<int>.Failure(Error.ValidationError("Error"));

        // Act
        var tappedSuccess = success.Tap(x => { });
        var tappedFailure = failure.Tap(x => { });

        // Assert - XOR maintained after Tap
        Assert.True(tappedSuccess.IsSuccess ^ tappedSuccess.IsFailure);
        Assert.True(tappedFailure.IsSuccess ^ tappedFailure.IsFailure);
    }

    [Fact]
    public void Ensure_PreservesIsSuccessIsFailureInvariant()
    {
        // Arrange
        var result = Result<int>.Success(5);

        // Act
        var ensuredTrue = result.Ensure(x => x > 0, Error.ValidationError("Error"));
        var ensuredFalse = result.Ensure(x => x < 0, Error.ValidationError("Error"));

        // Assert - XOR maintained after Ensure
        Assert.True(ensuredTrue.IsSuccess ^ ensuredTrue.IsFailure);
        Assert.True(ensuredFalse.IsSuccess ^ ensuredFalse.IsFailure);
    }

    [Fact]
    public void OrElse_PreservesIsSuccessIsFailureInvariant()
    {
        // Arrange
        var success = Result<int>.Success(5);
        var failure = Result<int>.Failure(Error.ValidationError("Error"));

        // Act
        var successOrElse = success.OrElse(() => Result<int>.Success(10));
        var failureOrElse = failure.OrElse(() => Result<int>.Success(10));

        // Assert - XOR maintained after OrElse
        Assert.True(successOrElse.IsSuccess ^ successOrElse.IsFailure);
        Assert.True(failureOrElse.IsSuccess ^ failureOrElse.IsFailure);
    }

    #endregion

    #region Immutability Invariants

    [Fact]
    public void Map_DoesNotMutateOriginalResult()
    {
        // Arrange
        var original = Result<int>.Success(5);
        var originalValue = original.Value;

        // Act
        var mapped = original.Map(x => x * 2);

        // Assert - Original unchanged
        Assert.Equal(originalValue, original.Value);
        Assert.True(original.IsSuccess);
        Assert.NotEqual(original.Value, mapped.Value);
    }

    [Fact]
    public void Bind_DoesNotMutateOriginalResult()
    {
        // Arrange
        var original = Result<int>.Success(5);
        var originalValue = original.Value;

        // Act
        var bound = original.Bind(x => Result<int>.Success(x * 2));

        // Assert - Original unchanged
        Assert.Equal(originalValue, original.Value);
        Assert.True(original.IsSuccess);
    }

    [Fact]
    public void Tap_DoesNotMutateOriginalResult()
    {
        // Arrange
        var original = Result<int>.Success(5);
        var capturedValue = 0;

        // Act
        var tapped = original.Tap(x => capturedValue = x);

        // Assert - Original unchanged
        Assert.Equal(5, original.Value);
        Assert.Equal(5, capturedValue);
        Assert.Same(original, tapped); // Tap returns same instance
    }

    [Fact]
    public void Error_ImmutabilityAfterCreation()
    {
        // Arrange
        var error = Error.ValidationError("VAL_001", "Original message");
        var result = Result<int>.Failure(error);

        // Assert - Error properties cannot be changed (record type)
        Assert.Equal("VAL_001", result.Error.Code);
        Assert.Equal("Original message", result.Error.Message);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    #endregion

    #region Match Invariants

    [Fact]
    public void Match_AlwaysExecutesExactlyOneBranch_Success()
    {
        // Arrange
        var result = Result<int>.Success(42);
        var successCalled = false;
        var failureCalled = false;

        // Act
        result.Match(
            onSuccess: x => { successCalled = true; return x; },
            onFailure: e => { failureCalled = true; return 0; }
        );

        // Assert - Exactly one branch executed
        Assert.True(successCalled);
        Assert.False(failureCalled);
    }

    [Fact]
    public void Match_AlwaysExecutesExactlyOneBranch_Failure()
    {
        // Arrange
        var result = Result<int>.Failure(Error.ValidationError("Error"));
        var successCalled = false;
        var failureCalled = false;

        // Act
        result.Match(
            onSuccess: x => { successCalled = true; return x; },
            onFailure: e => { failureCalled = true; return 0; }
        );

        // Assert - Exactly one branch executed
        Assert.False(successCalled);
        Assert.True(failureCalled);
    }

    [Fact]
    public void Switch_AlwaysExecutesExactlyOneBranch_Success()
    {
        // Arrange
        var result = Result<int>.Success(42);
        var successCalled = false;
        var failureCalled = false;

        // Act
        result.Switch(
            onSuccess: x => successCalled = true,
            onFailure: e => failureCalled = true
        );

        // Assert - Exactly one branch executed
        Assert.True(successCalled);
        Assert.False(failureCalled);
    }

    [Fact]
    public void Switch_AlwaysExecutesExactlyOneBranch_Failure()
    {
        // Arrange
        var result = Result<int>.Failure(Error.NotFoundError("Not found"));
        var successCalled = false;
        var failureCalled = false;

        // Act
        result.Switch(
            onSuccess: x => successCalled = true,
            onFailure: e => failureCalled = true
        );

        // Assert - Exactly one branch executed
        Assert.False(successCalled);
        Assert.True(failureCalled);
    }

    #endregion
}
