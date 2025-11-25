using Xunit;

namespace Voyager.Common.Results.Tests;

/// <summary>
/// Tests verifying that Result operators compose correctly.
/// Composition means combining multiple operations in different ways should produce consistent results.
/// </summary>
public class CompositionTests
{
    #region Map Composition

    [Fact]
    public void Map_Composition_MapsCanBeChained()
    {
        // Arrange
        var result = Result<int>.Success(5);
        Func<int, int> addOne = x => x + 1;
        Func<int, int> multiplyByTwo = x => x * 2;

        // Act - Separate Maps
        var separateMaps = result.Map(addOne).Map(multiplyByTwo);

        // Act - Combined function
        var combinedFunction = result.Map(x => multiplyByTwo(addOne(x)));

        // Assert - Both produce same result
        Assert.True(separateMaps.IsSuccess);
        Assert.True(combinedFunction.IsSuccess);
        Assert.Equal(separateMaps.Value, combinedFunction.Value);
        Assert.Equal(12, separateMaps.Value); // (5 + 1) * 2 = 12
    }

    [Fact]
    public void Map_Composition_OrderMatters()
    {
        // Arrange
        var result = Result<int>.Success(5);
        Func<int, int> addOne = x => x + 1;
        Func<int, int> multiplyByTwo = x => x * 2;

        // Act - Different orders
        var order1 = result.Map(addOne).Map(multiplyByTwo);      // (5 + 1) * 2 = 12
        var order2 = result.Map(multiplyByTwo).Map(addOne);      // (5 * 2) + 1 = 11

        // Assert - Different results prove order matters
        Assert.NotEqual(order1.Value, order2.Value);
        Assert.Equal(12, order1.Value);
        Assert.Equal(11, order2.Value);
    }

    [Fact]
    public void Map_WithFailure_CompositionShortCircuits()
    {
        // Arrange
        var result = Result<int>.Failure(Error.ValidationError("VAL_001", "Error"));
        var firstMapCalled = false;
        var secondMapCalled = false;

        // Act
        var composed = result
            .Map(x => { firstMapCalled = true; return x + 1; })
            .Map(x => { secondMapCalled = true; return x * 2; });

        // Assert - No Maps executed
        Assert.False(firstMapCalled);
        Assert.False(secondMapCalled);
        Assert.True(composed.IsFailure);
    }

    #endregion

    #region Bind Composition

    [Fact]
    public void Bind_Composition_BindsCanBeChained()
    {
        // Arrange
        var result = Result<int>.Success(5);
        Result<int> AddOne(int x) => Result<int>.Success(x + 1);
        Result<int> MultiplyByTwo(int x) => Result<int>.Success(x * 2);

        // Act
        var chained = result
            .Bind(AddOne)
            .Bind(MultiplyByTwo);

        // Assert
        Assert.True(chained.IsSuccess);
        Assert.Equal(12, chained.Value); // (5 + 1) * 2 = 12
    }

    [Fact]
    public void Bind_Composition_FirstFailureStopsChain()
    {
        // Arrange
        var result = Result<int>.Success(5);
        Result<int> FailingOperation(int x) => Error.ValidationError("VAL_002", "Failed");
        Result<int> NeverCalled(int x) => Result<int>.Success(x * 2);
        var secondBindCalled = false;

        // Act
        var chained = result
            .Bind(FailingOperation)
            .Bind(x => { secondBindCalled = true; return NeverCalled(x); });

        // Assert
        Assert.True(chained.IsFailure);
        Assert.False(secondBindCalled);
        Assert.Equal("VAL_002", chained.Error.Code);
    }

    [Fact]
    public void Bind_Composition_NestedVsFlat()
    {
        // Arrange
        var result = Result<int>.Success(5);
        Result<int> AddOne(int x) => Result<int>.Success(x + 1);
        Result<int> MultiplyByTwo(int x) => Result<int>.Success(x * 2);

        // Act - Nested Binds
        var nested = result.Bind(x => AddOne(x).Bind(y => MultiplyByTwo(y)));

        // Act - Flat chain
        var flat = result.Bind(AddOne).Bind(MultiplyByTwo);

        // Assert - Both produce same result (Monad Associativity Law)
        Assert.Equal(flat.Value, nested.Value);
        Assert.Equal(12, flat.Value);
    }

    #endregion

    #region Map + Bind Composition

    [Fact]
    public void MapBind_Composition_CanBeInterleaved()
    {
        // Arrange
        var result = Result<int>.Success(5);
        static Result<int> MultiplyByTwo(int x) => Result<int>.Success(x * 2);

        // Act - Map then Bind
        var mapThenBind = result
            .Map(x => x + 1)           // 6
            .Bind(MultiplyByTwo);      // 12

        // Act - Bind then Map
        var bindThenMap = result
            .Bind(MultiplyByTwo)       // 10
            .Map(x => x + 1);          // 11

        // Assert - Different results based on order
        Assert.Equal(12, mapThenBind.Value);
        Assert.Equal(11, bindThenMap.Value);
    }

    [Fact]
    public void MapBind_Composition_ComplexChain()
    {
        // Arrange
        var result = Result<int>.Success(10);
        static Result<int> Divide(int x) => x > 0
            ? Result<int>.Success(100 / x)
            : Error.ValidationError("VAL_003", "Cannot divide by zero");

        // Act
        var complex = result
            .Map(x => x - 5)          // 5
            .Bind(Divide)             // 100 / 5 = 20
            .Map(x => x * 2)          // 40
            .Ensure(x => x > 30, Error.ValidationError("VAL_004", "Too small"))  // Pass
            .Map(x => x + 10);        // 50

        // Assert
        Assert.True(complex.IsSuccess);
        Assert.Equal(50, complex.Value);
    }

    #endregion

    #region Tap Composition

    [Fact]
    public void Tap_Composition_TapsExecuteInOrder()
    {
        // Arrange
        var result = Result<int>.Success(5);
        var executionOrder = new List<int>();

        // Act
        var tapped = result
            .Tap(x => executionOrder.Add(1))
            .Map(x => x * 2)
            .Tap(x => executionOrder.Add(2))
            .Map(x => x + 1)
            .Tap(x => executionOrder.Add(3));

        // Assert - Taps executed in order
        Assert.Equal(new[] { 1, 2, 3 }, executionOrder);
        Assert.Equal(11, tapped.Value); // (5 * 2) + 1 = 11
    }

    [Fact]
    public void Tap_Composition_DoesNotAffectDataFlow()
    {
        // Arrange
        var result = Result<int>.Success(5);
        var sideEffectValue = 0;

        // Act
        var withTap = result
            .Map(x => x * 2)
            .Tap(x => sideEffectValue = x * 1000)  // Side effect only
            .Map(x => x + 1);

        // Assert - Side effect executed, but doesn't change result
        Assert.Equal(10000, sideEffectValue);  // Tap captured value
        Assert.Equal(11, withTap.Value);       // Data flow unaffected
    }

    [Fact]
    public void TapError_Composition_OnlyExecutesOnFailure()
    {
        // Arrange
        var result = Result<int>.Success(5);
        var errorTapCalled = false;
        var successTapCalled = false;

        // Act
        var tapped = result
            .Tap(x => successTapCalled = true)
            .TapError(e => errorTapCalled = true);

        // Assert
        Assert.True(successTapCalled);
        Assert.False(errorTapCalled);
    }

    #endregion

    #region Ensure Composition

    [Fact]
    public void Ensure_Composition_MultipleEnsuresActAsAndCondition()
    {
        // Arrange
        var result = Result<int>.Success(15);

        // Act
        var ensured = result
            .Ensure(x => x > 10, Error.ValidationError("VAL_005", "Too small"))
            .Ensure(x => x < 20, Error.ValidationError("VAL_006", "Too large"))
            .Ensure(x => x % 5 == 0, Error.ValidationError("VAL_007", "Not divisible by 5"));

        // Assert - All conditions pass
        Assert.True(ensured.IsSuccess);
        Assert.Equal(15, ensured.Value);
    }

    [Fact]
    public void Ensure_Composition_FirstFailureStopsChain()
    {
        // Arrange
        var result = Result<int>.Success(25);
        var thirdEnsureCalled = false;

        // Act
        var ensured = result
            .Ensure(x => x > 10, Error.ValidationError("VAL_008", "Too small"))      // Pass
            .Ensure(x => x < 20, Error.ValidationError("VAL_009", "Too large"))      // Fail
            .Ensure(x => { thirdEnsureCalled = true; return true; },
                Error.ValidationError("VAL_010", "Never checked"));                   // Skipped

        // Assert
        Assert.True(ensured.IsFailure);
        Assert.Equal("VAL_009", ensured.Error.Code);
        Assert.False(thirdEnsureCalled);
    }

    #endregion

    #region OrElse Composition

    [Fact]
    public void OrElse_Composition_FirstSuccessWins()
    {
        // Arrange
        var result = Result<int>.Failure(Error.NotFoundError("NF_001", "Not found"));

        // Act
        var recovered = result
            .OrElse(() => Result<int>.Success(10))      // This succeeds
            .OrElse(() => Result<int>.Success(20));     // Never called

        // Assert
        Assert.True(recovered.IsSuccess);
        Assert.Equal(10, recovered.Value);
    }

    [Fact]
    public void OrElse_Composition_FallbackChain()
    {
        // Arrange
        var result = Result<int>.Failure(Error.NotFoundError("NF_002", "Not found"));

        // Act
        var recovered = result
            .OrElse(() => Result<int>.Failure(Error.UnavailableError("UNAVAIL_001", "Cache unavailable")))
            .OrElse(() => Result<int>.Failure(Error.UnavailableError("UNAVAIL_002", "DB unavailable")))
            .OrElse(() => Result<int>.Success(42));  // Final fallback succeeds

        // Assert
        Assert.True(recovered.IsSuccess);
        Assert.Equal(42, recovered.Value);
    }

    [Fact]
    public void OrElse_Composition_AllFallbacksFail()
    {
        // Arrange
        var result = Result<int>.Failure(Error.NotFoundError("NF_003", "Not found"));
        var finalError = Error.UnavailableError("UNAVAIL_003", "All sources unavailable");

        // Act
        var recovered = result
            .OrElse(() => Result<int>.Failure(Error.UnavailableError("UNAVAIL_004", "Cache fail")))
            .OrElse(() => Result<int>.Failure(Error.UnavailableError("UNAVAIL_005", "DB fail")))
            .OrElse(() => Result<int>.Failure(finalError));

        // Assert - Last error propagates
        Assert.True(recovered.IsFailure);
        Assert.Equal(finalError.Code, recovered.Error.Code);
    }

    #endregion

    #region Complex Composition Scenarios

    [Fact]
    public void ComplexComposition_SuccessPath_AllOperatorsExecute()
    {
        // Arrange
        var result = Result<int>.Success(5);
        var tapExecuted = false;
        static Result<int> Double(int x) => Result<int>.Success(x * 2);

        // Act
        var final = result
            .Map(x => x + 5)                                          // 10
            .Bind(Double)                                             // 20
            .Tap(x => tapExecuted = true)                            // Side effect
            .Ensure(x => x > 15, Error.ValidationError("VAL_011", "Too small"))  // Pass
            .Map(x => x / 2)                                          // 10
            .OrElse(() => Result<int>.Success(999));                 // Not executed

        // Assert
        Assert.True(tapExecuted);
        Assert.True(final.IsSuccess);
        Assert.Equal(10, final.Value);
    }

    [Fact]
    public void ComplexComposition_FailurePath_WithRecovery()
    {
        // Arrange
        var result = Result<int>.Success(5);
        static Result<int> FailingOperation(int x) => Error.DatabaseError("DB_001", "DB failed");

        // Act
        var final = result
            .Map(x => x * 2)                              // 10
            .Bind(FailingOperation)                       // Fails here
            .Map(x => x * 3)                              // Skipped
            .OrElse(() => Result<int>.Success(100))      // Recovery
            .Map(x => x + 1);                             // 101

        // Assert
        Assert.True(final.IsSuccess);
        Assert.Equal(101, final.Value);
    }

    [Fact]
    public void ComplexComposition_MapError_TransformsThenContinues()
    {
        // Arrange
        var result = Result<int>.Failure(Error.DatabaseError("DB_002", "Database error"));

        // Act
        var final = result
            .MapError(e => Error.UnavailableError("SERVICE_" + e.Code, "Service: " + e.Message))
            .TapError(e => { /* Log transformed error */ })
            .OrElse(() => Result<int>.Success(42))
            .Map(x => x * 2);

        // Assert - Error transformed, then recovered
        Assert.True(final.IsSuccess);
        Assert.Equal(84, final.Value);
    }

    [Fact]
    public void ComplexComposition_Finally_ExecutesRegardlessOfPath()
    {
        // Arrange
        var successCleanup = false;
        var failureCleanup = false;

        // Act - Success path
        var success = Result<int>.Success(5)
            .Map(x => x * 2)
            .Finally(() => successCleanup = true);

        // Act - Failure path
        var failure = Result<int>.Failure(Error.ValidationError("VAL_012", "Error"))
            .Map(x => x * 2)
            .Finally(() => failureCleanup = true);

        // Assert - Finally executed in both paths
        Assert.True(successCleanup);
        Assert.True(failureCleanup);
        Assert.True(success.IsSuccess);
        Assert.True(failure.IsFailure);
    }

    #endregion

    #region Non-Generic Result Composition

    [Fact]
    public void NonGenericResult_Composition_MapToValue()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var mapped = result
            .Map(() => 42)
            .Map(x => x * 2)
            .Map(x => x.ToString());

        // Assert - Void → Value chain works
        Assert.True(mapped.IsSuccess);
        Assert.Equal("84", mapped.Value);
    }

    [Fact]
    public void NonGenericResult_Composition_WithFailure()
    {
        // Arrange
        var result = Result.Failure(Error.BusinessError("BIZ_001", "Business error"));
        var mapCalled = false;

        // Act
        var mapped = result
            .Map(() => { mapCalled = true; return 42; })
            .Map(x => x * 2);

        // Assert
        Assert.False(mapCalled);
        Assert.True(mapped.IsFailure);
        Assert.Equal("BIZ_001", mapped.Error.Code);
    }

    #endregion
}
