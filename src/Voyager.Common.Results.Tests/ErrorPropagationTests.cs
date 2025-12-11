using Xunit;

namespace Voyager.Common.Results.Tests;

/// <summary>
/// Tests verifying that errors are correctly propagated through all Result operators.
/// Once a Result is in failure state, it should remain in failure state through most operations.
/// </summary>
public class ErrorPropagationTests
{
    #region Map Error Propagation

    [Fact]
    public void Map_WithFailure_PropagatesOriginalError()
    {
        // Arrange
        var originalError = Error.ValidationError("VAL_001", "Original error");
        var result = Result<int>.Failure(originalError);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        Assert.True(mapped.IsFailure);
        Assert.Equal(originalError.Type, mapped.Error.Type);
        Assert.Equal(originalError.Code, mapped.Error.Code);
        Assert.Equal(originalError.Message, mapped.Error.Message);
    }

    [Fact]
    public void Map_ChainedOnFailure_PropagatesFirstError()
    {
        // Arrange
        var originalError = Error.NotFoundError("NF_001", "Not found");
        var result = Result<int>.Failure(originalError);

        // Act - Multiple Maps should all skip and keep original error
        var mapped = result
            .Map(x => x * 2)
            .Map(x => x + 10)
            .Map(x => x.ToString());

        // Assert
        Assert.True(mapped.IsFailure);
        Assert.Equal(originalError.Type, mapped.Error.Type);
        Assert.Equal(originalError.Code, mapped.Error.Code);
        Assert.Equal(originalError.Message, mapped.Error.Message);
    }

    [Fact]
    public void NonGenericMap_WithFailure_PropagatesOriginalError()
    {
        // Arrange
        var originalError = Error.DatabaseError("DB_001", "Database error");
        var result = Result.Failure(originalError);

        // Act
        var mapped = result.Map(() => 42);

        // Assert
        Assert.True(mapped.IsFailure);
        Assert.Equal(originalError.Type, mapped.Error.Type);
        Assert.Equal(originalError.Code, mapped.Error.Code);
    }

    #endregion

    #region Bind Error Propagation

    [Fact]
    public void Bind_WithFailure_PropagatesOriginalError()
    {
        // Arrange
        var originalError = Error.BusinessError("BIZ_001", "Business rule failed");
        var result = Result<int>.Failure(originalError);

        // Act - Bind should not execute, error propagates
        var bound = result.Bind(x => Result<string>.Success(x.ToString()));

        // Assert
        Assert.True(bound.IsFailure);
        Assert.Equal(originalError.Type, bound.Error.Type);
        Assert.Equal(originalError.Code, bound.Error.Code);
        Assert.Equal(originalError.Message, bound.Error.Message);
    }

    [Fact]
    public void Bind_ChainedOnFailure_PropagatesFirstError()
    {
        // Arrange
        var originalError = Error.PermissionError("PERM_001", "Access denied");
        var result = Result<int>.Failure(originalError);

        // Act - Chain multiple Binds
        var bound = result
            .Bind(x => Result<int>.Success(x * 2))
            .Bind(x => Result<string>.Success(x.ToString()))
            .Bind(x => Result<int>.Success(int.Parse(x)));

        // Assert - Original error preserved
        Assert.True(bound.IsFailure);
        Assert.Equal(originalError.Type, bound.Error.Type);
        Assert.Equal(originalError.Code, bound.Error.Code);
    }

    [Fact]
    public void Bind_ReturnsNewError_NewErrorPropagates()
    {
        // Arrange
        var result = Result<int>.Success(5);
        var newError = Error.ValidationError("VAL_002", "New error");

        // Act - Bind returns failure
        var bound = result.Bind(x => Result<string>.Failure(newError));

        // Assert - New error from Bind propagates
        Assert.True(bound.IsFailure);
        Assert.Equal(newError.Type, bound.Error.Type);
        Assert.Equal(newError.Code, bound.Error.Code);
        Assert.Equal(newError.Message, bound.Error.Message);
    }

    #endregion

    #region Tap Error Propagation

    [Fact]
    public void Tap_WithFailure_PropagatesOriginalError()
    {
        // Arrange
        var originalError = Error.ConflictError("CONF_001", "Conflict");
        var result = Result<int>.Failure(originalError);
        var actionExecuted = false;

        // Act
        var tapped = result.Tap(x => actionExecuted = true);

        // Assert - Action not executed, error propagated
        Assert.False(actionExecuted);
        Assert.True(tapped.IsFailure);
        Assert.Equal(originalError.Code, tapped.Error.Code);
    }

    [Fact]
    public void TapError_WithFailure_PropagatesSameError()
    {
        // Arrange
        var originalError = Error.TimeoutError("TIMEOUT_001", "Request timed out");
        var result = Result<int>.Failure(originalError);
        Error? capturedError = null;

        // Act
        var tapped = result.TapError(e => capturedError = e);

        // Assert - Error action executed, same error propagated
        Assert.NotNull(capturedError);
        Assert.Equal(originalError.Code, capturedError.Code);
        Assert.True(tapped.IsFailure);
        Assert.Equal(originalError.Code, tapped.Error.Code);
    }

    [Fact]
    public void NonGenericTap_WithFailure_PropagatesOriginalError()
    {
        // Arrange
        var originalError = Error.UnavailableError("UNAVAIL_001", "Service unavailable");
        var result = Result.Failure(originalError);
        var actionExecuted = false;

        // Act
        var tapped = result.Tap(() => actionExecuted = true);

        // Assert
        Assert.False(actionExecuted);
        Assert.True(tapped.IsFailure);
        Assert.Equal(originalError.Code, tapped.Error.Code);
    }

    #endregion

    #region Ensure Error Propagation

    [Fact]
    public void Ensure_WithFailure_PropagatesOriginalError()
    {
        // Arrange
        var originalError = Error.ValidationError("VAL_003", "Original validation error");
        var result = Result<int>.Failure(originalError);

        // Act - Ensure predicate should not execute
        var ensured = result.Ensure(
            x => x > 0,
            Error.ValidationError("VAL_004", "This should not appear")
        );

        // Assert - Original error preserved
        Assert.True(ensured.IsFailure);
        Assert.Equal(originalError.Code, ensured.Error.Code);
        Assert.Equal(originalError.Message, ensured.Error.Message);
    }

    [Fact]
    public void Ensure_PredicateFails_CreatesNewError()
    {
        // Arrange
        var result = Result<int>.Success(5);
        var newError = Error.ValidationError("VAL_005", "Predicate failed");

        // Act
        var ensured = result.Ensure(x => x > 10, newError);

        // Assert - New error from failed predicate
        Assert.True(ensured.IsFailure);
        Assert.Equal(newError.Code, ensured.Error.Code);
        Assert.Equal(newError.Message, ensured.Error.Message);
    }

    #endregion

    #region OrElse Error Recovery

    [Fact]
    public void OrElse_WithFailure_ExecutesFallbackAndReplacesError()
    {
        // Arrange
        var originalError = Error.NotFoundError("NF_002", "Not found");
        var result = Result<int>.Failure(originalError);

        // Act - OrElse should execute and replace error
        var recovered = result.OrElse(() => Result<int>.Success(42));

        // Assert - Recovered with success
        Assert.True(recovered.IsSuccess);
        Assert.Equal(42, recovered.Value);
    }

    [Fact]
    public void OrElse_WithFailure_FallbackAlsoFails_PropagatesFallbackError()
    {
        // Arrange
        var originalError = Error.NotFoundError("NF_003", "Original not found");
        var fallbackError = Error.UnavailableError("UNAVAIL_002", "Fallback unavailable");
        var result = Result<int>.Failure(originalError);

        // Act
        var recovered = result.OrElse(() => Result<int>.Failure(fallbackError));

        // Assert - Fallback error propagates
        Assert.True(recovered.IsFailure);
        Assert.Equal(fallbackError.Code, recovered.Error.Code);
        Assert.Equal(fallbackError.Message, recovered.Error.Message);
    }

    [Fact]
    public void OrElse_WithSuccess_DoesNotExecuteFallback()
    {
        // Arrange
        var result = Result<int>.Success(10);
        var fallbackExecuted = false;

        // Act
        var recovered = result.OrElse(() =>
        {
            fallbackExecuted = true;
            return Result<int>.Success(42);
        });

        // Assert - Fallback not executed, original success preserved
        Assert.False(fallbackExecuted);
        Assert.True(recovered.IsSuccess);
        Assert.Equal(10, recovered.Value);
    }

    #endregion

    #region MapError Error Transformation

    [Fact]
    public void MapError_WithFailure_TransformsError()
    {
        // Arrange
        var originalError = Error.ValidationError("VAL_006", "Original validation");
        var result = Result<int>.Failure(originalError);

        // Act
        var mapped = result.MapError(e =>
            Error.BusinessError("BIZ_" + e.Code, "Transformed: " + e.Message)
        );

        // Assert - Error transformed
        Assert.True(mapped.IsFailure);
        Assert.Equal(ErrorType.Business, mapped.Error.Type);
        Assert.Equal("BIZ_VAL_006", mapped.Error.Code);
        Assert.Equal("Transformed: Original validation", mapped.Error.Message);
    }

    [Fact]
    public void MapError_WithSuccess_DoesNotTransform()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var mapped = result.MapError(e =>
            Error.BusinessError("BIZ_999", "Should not appear")
        );

        // Assert - Still success, error not transformed
        Assert.True(mapped.IsSuccess);
        Assert.Equal(42, mapped.Value);
        Assert.Equal(ErrorType.None, mapped.Error.Type);
    }

    [Fact]
    public void NonGenericMapError_WithFailure_TransformsError()
    {
        // Arrange
        var originalError = Error.DatabaseError("DB_002", "Database failure");
        var result = Result.Failure(originalError);

        // Act
        var mapped = result.MapError(e =>
            Error.UnavailableError("SERVICE_" + e.Code, "Service unavailable: " + e.Message)
        );

        // Assert
        Assert.True(mapped.IsFailure);
        Assert.Equal(ErrorType.Unavailable, mapped.Error.Type);
        Assert.Equal("SERVICE_DB_002", mapped.Error.Code);
    }

    #endregion

    #region Try Error Creation

    [Fact]
    public void Try_WithException_CreatesUnexpectedError()
    {
        // Arrange & Act
        var result = Result<int>.Try(() => throw new InvalidOperationException("Test exception"));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unexpected, result.Error.Type);
        Assert.Contains("Test exception", result.Error.Message);
    }

    [Fact]
    public void Try_WithCustomErrorMapper_CreatesCustomError()
    {
        // Arrange
        var customError = Error.DatabaseError("DB_003", "Custom mapped error");

        // Act
        var result = Result<int>.Try(
            () => throw new InvalidOperationException("Test"),
            ex => customError
        );

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Database, result.Error.Type);
        Assert.Equal("DB_003", result.Error.Code);
        Assert.Equal("Custom mapped error", result.Error.Message);
    }

    #endregion

    #region Finally Cleanup with Error Propagation

    [Fact]
    public void Finally_WithFailure_ExecutesCleanupAndPropagatesError()
    {
        // Arrange
        var originalError = Error.ValidationError("VAL_007", "Validation failed");
        var result = Result<int>.Failure(originalError);
        var cleanupExecuted = false;

        // Act
        var final = result.Finally(() => cleanupExecuted = true);

        // Assert - Cleanup executed, error propagated
        Assert.True(cleanupExecuted);
        Assert.True(final.IsFailure);
        Assert.Equal(originalError.Code, final.Error.Code);
    }

    [Fact]
    public void Finally_WithSuccess_ExecutesCleanupAndPropagatesSuccess()
    {
        // Arrange
        var result = Result<int>.Success(42);
        var cleanupExecuted = false;

        // Act
        var final = result.Finally(() => cleanupExecuted = true);

        // Assert - Cleanup executed, success preserved
        Assert.True(cleanupExecuted);
        Assert.True(final.IsSuccess);
        Assert.Equal(42, final.Value);
    }

    #endregion

    #region Complex Error Propagation Chains

    [Fact]
    public void ComplexChain_FirstOperationFails_ErrorPropagatesThroughEntireChain()
    {
        // Arrange
        var originalError = Error.NotFoundError("NF_004", "User not found");
        var result = Result<int>.Failure(originalError);

        // Act - Complex chain with multiple operators
        var final = result
            .Map(x => x * 2)                                    // Skipped
            .Bind(x => Result<string>.Success(x.ToString()))    // Skipped
            .Tap(x => { })                                      // Skipped
            .Ensure(x => x.Length > 0, Error.ValidationError("VAL_008", "Empty"))  // Skipped
            .Map(x => x.ToUpper());                             // Skipped

        // Assert - Original error preserved through entire chain
        Assert.True(final.IsFailure);
        Assert.Equal(originalError.Code, final.Error.Code);
        Assert.Equal(originalError.Message, final.Error.Message);
    }

    [Fact]
    public void ComplexChain_MiddleOperationFails_SubsequentOperationsSkipped()
    {
        // Arrange
        var result = Result<int>.Success(5);
        var ensureError = Error.ValidationError("VAL_009", "Value too small");

        // Act
        var final = result
            .Map(x => x * 2)                                    // Executes: 10
            .Ensure(x => x > 100, ensureError)                  // Fails here
            .Map(x => x * 3)                                    // Skipped
            .Bind(x => Result<string>.Success(x.ToString()));   // Skipped

        // Assert - Error from Ensure propagates
        Assert.True(final.IsFailure);
        Assert.Equal(ensureError.Code, final.Error.Code);
        Assert.Equal(ensureError.Message, final.Error.Message);
    }

    [Fact]
    public void ComplexChain_WithOrElse_RecoveryThenContinue()
    {
        // Arrange
        var result = Result<int>.Failure(Error.NotFoundError("NF_005", "Not found"));

        // Act - Fail, recover, continue
        var final = result
            .OrElse(() => Result<int>.Success(10))  // Recovery
            .Map(x => x * 2)                         // Executes on recovered value
            .Tap(x => { });                          // Executes

        // Assert - Recovered and continued successfully
        Assert.True(final.IsSuccess);
        Assert.Equal(20, final.Value);
    }

    #endregion
}
