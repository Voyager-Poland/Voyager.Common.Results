using Xunit;

namespace Voyager.Common.Results.Tests;

/// <summary>
/// Tests verifying that Result&lt;T&gt; satisfies Monad Laws:
/// 1. Left Identity: return a >>= f ≡ f a
/// 2. Right Identity: m >>= return ≡ m
/// 3. Associativity: (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
/// </summary>
public class MonadLawsTests
{
    #region Test Functions

    private static Result<int> AddOne(int x) => Result<int>.Success(x + 1);
    private static Result<int> MultiplyByTwo(int x) => Result<int>.Success(x * 2);
    private static Result<string> IntToString(int x) => Result<string>.Success(x.ToString());

    #endregion

    #region Left Identity Law: return a >>= f ≡ f a

    [Fact]
    public void MonadLaw_LeftIdentity_Success()
    {
        // Arrange
        int value = 5;

        // Act
        var left = Result<int>.Success(value).Bind(AddOne);
        var right = AddOne(value);

        // Assert
        Assert.True(left.IsSuccess);
        Assert.True(right.IsSuccess);
        Assert.Equal(right.Value, left.Value);
    }

    [Fact]
    public void MonadLaw_LeftIdentity_Failure()
    {
        // Arrange
        int value = 5;
        static Result<int> FailingFunc(int x) => Error.ValidationError("Always fails");

        // Act
        var left = Result<int>.Success(value).Bind(FailingFunc);
        var right = FailingFunc(value);

        // Assert
        Assert.True(left.IsFailure);
        Assert.True(right.IsFailure);
        Assert.Equal(right.Error.Code, left.Error.Code);
        Assert.Equal(right.Error.Message, left.Error.Message);
    }

    #endregion

    #region Right Identity Law: m >>= return ≡ m

    [Fact]
    public void MonadLaw_RightIdentity_Success()
    {
        // Arrange
        var original = Result<int>.Success(42);

        // Act
        var bound = original.Bind(x => Result<int>.Success(x));

        // Assert
        Assert.True(bound.IsSuccess);
        Assert.Equal(original.Value, bound.Value);
    }

    [Fact]
    public void MonadLaw_RightIdentity_Failure()
    {
        // Arrange
        var original = Result<int>.Failure(Error.ValidationError("Original error"));

        // Act
        var bound = original.Bind(x => Result<int>.Success(x));

        // Assert
        Assert.True(bound.IsFailure);
        Assert.Equal(original.Error.Code, bound.Error.Code);
        Assert.Equal(original.Error.Message, bound.Error.Message);
    }

    #endregion

    #region Associativity Law: (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)

    [Fact]
    public void MonadLaw_Associativity_Success()
    {
        // Arrange
        var m = Result<int>.Success(5);

        // Act - Left side: (m >>= f) >>= g
        var leftSide = m.Bind(AddOne).Bind(MultiplyByTwo);

        // Act - Right side: m >>= (\x -> f x >>= g)
        var rightSide = m.Bind(x => AddOne(x).Bind(MultiplyByTwo));

        // Assert
        Assert.True(leftSide.IsSuccess);
        Assert.True(rightSide.IsSuccess);
        Assert.Equal(rightSide.Value, leftSide.Value);
        Assert.Equal(12, leftSide.Value); // (5 + 1) * 2 = 12
    }

    [Fact]
    public void MonadLaw_Associativity_FirstFailure()
    {
        // Arrange
        var m = Result<int>.Success(5);
        static Result<int> FailingAddOne(int x) => Error.ValidationError("Add failed");

        // Act - Left side: (m >>= f) >>= g
        var leftSide = m.Bind(FailingAddOne).Bind(MultiplyByTwo);

        // Act - Right side: m >>= (\x -> f x >>= g)
        var rightSide = m.Bind(x => FailingAddOne(x).Bind(MultiplyByTwo));

        // Assert
        Assert.True(leftSide.IsFailure);
        Assert.True(rightSide.IsFailure);
        Assert.Equal(rightSide.Error.Code, leftSide.Error.Code);
    }

    [Fact]
    public void MonadLaw_Associativity_SecondFailure()
    {
        // Arrange
        var m = Result<int>.Success(5);
        static Result<int> FailingMultiply(int x) => Error.ValidationError("Multiply failed");

        // Act - Left side: (m >>= f) >>= g
        var leftSide = m.Bind(AddOne).Bind(FailingMultiply);

        // Act - Right side: m >>= (\x -> f x >>= g)
        var rightSide = m.Bind(x => AddOne(x).Bind(FailingMultiply));

        // Assert
        Assert.True(leftSide.IsFailure);
        Assert.True(rightSide.IsFailure);
        Assert.Equal(rightSide.Error.Code, leftSide.Error.Code);
    }

    [Fact]
    public void MonadLaw_Associativity_InitialFailure()
    {
        // Arrange
        var m = Result<int>.Failure(Error.NotFoundError("Initial error"));

        // Act - Left side: (m >>= f) >>= g
        var leftSide = m.Bind(AddOne).Bind(MultiplyByTwo);

        // Act - Right side: m >>= (\x -> f x >>= g)
        var rightSide = m.Bind(x => AddOne(x).Bind(MultiplyByTwo));

        // Assert
        Assert.True(leftSide.IsFailure);
        Assert.True(rightSide.IsFailure);
        Assert.Equal(rightSide.Error.Code, leftSide.Error.Code);
        Assert.Equal("Initial error", leftSide.Error.Message);
    }

    [Fact]
    public void MonadLaw_Associativity_DifferentTypes()
    {
        // Arrange
        var m = Result<int>.Success(42);

        // Act - Left side: (m >>= f) >>= g
        var leftSide = m.Bind(AddOne).Bind(IntToString);

        // Act - Right side: m >>= (\x -> f x >>= g)
        var rightSide = m.Bind(x => AddOne(x).Bind(IntToString));

        // Assert
        Assert.True(leftSide.IsSuccess);
        Assert.True(rightSide.IsSuccess);
        Assert.Equal(rightSide.Value, leftSide.Value);
        Assert.Equal("43", leftSide.Value); // 42 + 1 = 43
    }

    #endregion

    #region Functor Law: Map preserves composition

    [Fact]
    public void FunctorLaw_MapPreservesComposition_Success()
    {
        // Functor law: fmap (g . f) ≡ fmap g . fmap f
        // Arrange
        var m = Result<int>.Success(5);
        Func<int, int> f = x => x + 1;
        Func<int, int> g = x => x * 2;

        // Act - Left side: Map(g ∘ f)
        var leftSide = m.Map(x => g(f(x)));

        // Act - Right side: Map(g) ∘ Map(f)
        var rightSide = m.Map(f).Map(g);

        // Assert
        Assert.True(leftSide.IsSuccess);
        Assert.True(rightSide.IsSuccess);
        Assert.Equal(rightSide.Value, leftSide.Value);
        Assert.Equal(12, leftSide.Value); // (5 + 1) * 2 = 12
    }

    [Fact]
    public void FunctorLaw_MapPreservesIdentity_Success()
    {
        // Functor law: fmap id ≡ id
        // Arrange
        var m = Result<int>.Success(42);

        // Act
        var mapped = m.Map(x => x); // Identity function

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(m.Value, mapped.Value);
    }

    [Fact]
    public void FunctorLaw_MapPreservesFailure()
    {
        // Arrange
        var m = Result<int>.Failure(Error.ValidationError("Error"));

        // Act
        var mapped = m.Map(x => x * 2);

        // Assert
        Assert.True(mapped.IsFailure);
        Assert.Equal(m.Error.Code, mapped.Error.Code);
    }

    #endregion
}
