namespace Voyager.Common.Results;

/// <summary>
/// Result pattern - represents the outcome of an operation that may succeed or fail
/// </summary>
/// <typeparam name="TValue">Type of value returned in case of success</typeparam>
public class Result<TValue> : Result
{
    /// <summary>
    /// Value returned in case of success
    /// </summary>
    public TValue? Value { get; }

    private Result(TValue value) : base(true, Error.None)
    {
        Value = value;
    }

    private Result(Error error) : base(false, error)
    {
        Value = default;
    }

    // ========== FACTORY METHODS ==========

    /// <summary>
    /// Creates a success result with a value
    /// </summary>
    public static Result<TValue> Success(TValue value) => new(value);

    /// <summary>
    /// Creates a failure result
    /// </summary>
    public new static Result<TValue> Failure(Error error) => new(error);

    // ========== PATTERN MATCHING ==========

    /// <summary>
    /// Returns a value depending on the result
    /// </summary>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<Error, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Value!) : onFailure(Error!);
    }

    /// <summary>
    /// Executes the appropriate action depending on the result
    /// </summary>
    public void Switch(
        Action<TValue> onSuccess,
        Action<Error> onFailure)
    {
        if (IsSuccess)
            onSuccess(Value!);
        else
            onFailure(Error!);
    }

    // ========== FUNCTIONAL METHODS ==========

    /// <summary>
    /// Maps the success value to another type (functor map)
    /// </summary>
    /// <example>
    /// var result = Result&lt;int&gt;.Success(5);
    /// var mapped = result.Map(x => x * 2); // Result&lt;int&gt; with value 10
    /// </example>
    public Result<TOut> Map<TOut>(Func<TValue, TOut> mapper)
    {
        return IsSuccess
            ? Result<TOut>.Success(mapper(Value!))
            : Result<TOut>.Failure(Error!);
    }

    /// <summary>
    /// Bind (FlatMap) - composes operations returning Result (monad bind)
    /// </summary>
    /// <example>
    /// var result = Result&lt;int&gt;.Success(5);
    /// var bound = result.Bind(x => x > 0 
    ///     ? Result&lt;string&gt;.Success(x.ToString()) 
    ///     : Result&lt;string&gt;.Failure(Error.ValidationError("Must be positive")));
    /// </example>
    public Result<TOut> Bind<TOut>(Func<TValue, Result<TOut>> binder)
    {
        return IsSuccess
            ? binder(Value!)
            : Result<TOut>.Failure(Error!);
    }

    /// <summary>
    /// Tap - performs an action on the value without changing the Result (side effect)
    /// </summary>
    /// <example>
    /// var result = Result&lt;int&gt;.Success(5);
    /// result.Tap(x => Console.WriteLine($"Value: {x}")); // Logs but returns same result
    /// </example>
    public Result<TValue> Tap(Action<TValue> action)
    {
        if (IsSuccess)
            action(Value!);
        return this;
    }

    /// <summary>
    /// TapError - performs an action on the error without changing the Result
    /// </summary>
    public Result<TValue> TapError(Action<Error> action)
    {
        if (IsFailure)
            action(Error!);
        return this;
    }

    /// <summary>
    /// Ensure - validates the success value, may convert to Failure
    /// </summary>
    public Result<TValue> Ensure(Func<TValue, bool> predicate, Error error)
    {
        if (IsFailure)
            return this;

        return predicate(Value!) ? this : Failure(error);
    }

    /// <summary>
    /// MapError - transforms the error
    /// </summary>
    public Result<TValue> MapError(Func<Error, Error> mapper)
    {
        return IsFailure
            ? Failure(mapper(Error!))
            : this;
    }

    /// <summary>
    /// GetValueOrDefault - returns the value or the default value
    /// </summary>
    public TValue? GetValueOrDefault(TValue? defaultValue = default)
    {
        return IsSuccess ? Value : defaultValue;
    }

    /// <summary>
    /// GetValueOrThrow - returns the value or throws an exception
    /// </summary>
    public TValue GetValueOrThrow()
    {
        if (IsFailure)
            throw new InvalidOperationException($"Result is a failure: {Error!.Message}");
        return Value!;
    }

    // ========== IMPLICIT CONVERSIONS ==========
/// <summary>
/// Implicitly converts a value of type <typeparamref name="TValue"/> to a successful <see cref="Result{TValue}"/>
/// instance.
/// </summary>
/// <remarks>This conversion allows direct assignment of a <typeparamref name="TValue"/> value to a <see
/// cref="Result{TValue}"/> variable, treating the value as a successful result.</remarks>
/// <param name="value">The value to wrap in a successful <see cref="Result{TValue}"/>.</param>
    public static implicit operator Result<TValue>(TValue value) => Success(value);
    /// <summary>
    /// Implicitly converts an <see cref="Error"/> to a failed <see cref="Result{TValue}"/> instance.
    /// </summary>
    /// <param name="error"></param>
    public static implicit operator Result<TValue>(Error error) => Failure(error);
}
