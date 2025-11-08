namespace Voyager.Common.Results;

/// <summary>
/// Result pattern - represents the outcome of an operation that does not return a value (void operations)
/// </summary>
public class Result
{
    /// <summary>
    /// Indicates whether the operation completed successfully
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the result represents a failure state.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Error in case the operation failed
    /// </summary>
    public Error Error { get; }


    /// <summary>
    /// Protected constructor - use factory methods Success and Failure
    /// </summary>
    /// <param name="isSuccess"></param>
    /// <param name="error"></param>
    /// <exception cref="InvalidOperationException"></exception>
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None && error is not null)
            throw new InvalidOperationException("Successful result cannot have an error.");
        if (!isSuccess && error is null)
            throw new InvalidOperationException("Failed result must have an error.");

        IsSuccess = isSuccess;
        Error = error!;
    }

    // ========== FACTORY METHODS ==========

    /// <summary>
    /// Creates a success result
    /// </summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>
    /// Creates a failure result
    /// </summary>
    public static Result Failure(Error error) => new(false, error);

    // ========== PATTERN MATCHING ==========

    /// <summary>
    /// Executes the appropriate action depending on the result
    /// </summary>
    public void Switch(Action onSuccess, Action<Error> onFailure)
    {
        if (IsSuccess)
            onSuccess();
        else
            onFailure(Error!);
    }

    /// <summary>
    /// Returns a value depending on the result
    /// </summary>
    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<Error, TResult> onFailure)
    {
        return IsSuccess ? onSuccess() : onFailure(Error!);
    }

    // ========== IMPLICIT CONVERSIONS ==========
    /// <summary>
    /// Implicit conversion from Error to Result (creates a failure result)
    /// </summary>
    /// <param name="error"></param>
    public static implicit operator Result(Error error) => Failure(error);
}
