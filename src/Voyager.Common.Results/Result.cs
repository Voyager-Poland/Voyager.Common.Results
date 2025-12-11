#if NET48
using System;
#endif

namespace Voyager.Common.Results
{
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
        /// <param name="isSuccess">Indicates whether the result represents success.</param>
        /// <param name="error">Error associated with the failure.</param>
        /// <exception cref="InvalidOperationException">Thrown when constructor invariants are violated.</exception>
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
        /// <returns>A successful Result instance.</returns>
        public static Result Success() => new(true, Error.None);

        /// <summary>
        /// Creates a failure result
        /// </summary>
        /// <param name="error">The error describing the failure.</param>
        /// <returns>A failed Result instance containing the specified error.</returns>
        public static Result Failure(Error error) => new(false, error);

        /// <summary>
        /// Executes an action and wraps any exceptions in a Result
        /// </summary>
        /// <example>
        /// <code>
        /// var result = Result.Try(() => File.WriteAllText("log.txt", message));
        /// </code>
        /// </example>
        /// <param name="action">Action to execute.</param>
        /// <returns>Success if action completes without exception, otherwise Failure with error from exception.</returns>
        public static Result Try(Action action)
        {
            try
            {
                action();
                return Success();
            }
            catch (Exception ex)
            {
                return Failure(Error.FromException(ex));
            }
        }

        /// <summary>
        /// Executes an action and wraps any exceptions in a Result with custom error mapping
        /// </summary>
        /// <example>
        /// <code>
        /// var result = Result.Try(
        ///     () => File.WriteAllText("log.txt", message),
        ///     ex => ex is IOException 
        ///         ? Error.UnavailableError("File system unavailable")
        ///         : Error.UnexpectedError(ex.Message));
        /// </code>
        /// </example>
        /// <param name="action">Action to execute.</param>
        /// <param name="errorMapper">Function to convert exception to custom error.</param>
        /// <returns>Success if action completes without exception, otherwise Failure with mapped error.</returns>
        public static Result Try(Action action, Func<Exception, Error> errorMapper)
        {
            try
            {
                action();
                return Success();
            }
            catch (Exception ex)
            {
                return Failure(errorMapper(ex));
            }
        }

        // ========== PATTERN MATCHING ==========

        /// <summary>
        /// Executes the appropriate action depending on the result
        /// </summary>
        /// <param name="onSuccess">Action to execute on success.</param>
        /// <param name="onFailure">Action to execute on failure.</param>
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
        /// <typeparam name="TResult">Type of the returned value.</typeparam>
        /// <param name="onSuccess">Function to execute on success.</param>
        /// <param name="onFailure">Function to execute on failure.</param>
        /// <returns>Result of executed function.</returns>
        public TResult Match<TResult>(
            Func<TResult> onSuccess,
            Func<Error, TResult> onFailure)
        {
            return IsSuccess ? onSuccess() : onFailure(Error!);
        }

        // ========== FUNCTIONAL METHODS ==========

        /// <summary>
        /// Map - transforms void result into a Result with a value (functor map)
        /// </summary>
        /// <example>
        /// <code>
        /// var result = ValidateInput()
        ///     .Map(() => DateTime.UtcNow); // Result → Result&lt;DateTime&gt;
        /// </code>
        /// </example>
        /// <param name="mapper">Function producing a value from success.</param>
        /// <typeparam name="TValue">Type of value to produce.</typeparam>
        /// <returns>Result&lt;TValue&gt; with mapped value or propagates failure.</returns>
        public Result<TValue> Map<TValue>(Func<TValue> mapper)
        {
            return IsSuccess
                ? Result<TValue>.Success(mapper())
                : Result<TValue>.Failure(Error);
        }

        /// <summary>
        /// Bind - chains void operations returning Result (monad bind for void operations)
        /// </summary>
        /// <example>
        /// <code>
        /// var result = ValidateInput()
        ///     .Bind(() => SaveToDatabase())
        ///     .Bind(() => SendNotification());
        /// </code>
        /// </example>
        /// <param name="binder">Function returning next Result in chain.</param>
        /// <returns>Result from binder if current is success, otherwise propagates failure.</returns>
        public Result Bind(Func<Result> binder)
        {
            return IsSuccess ? binder() : this;
        }

        /// <summary>
        /// Bind - chains void operation to an operation that returns a value
        /// </summary>
        /// <example>
        /// <code>
        /// var result = ValidateInput()
        ///     .Bind(() => GetUser(userId)); // Result → Result&lt;User&gt;
        /// </code>
        /// </example>
        /// <param name="binder">Function returning Result&lt;TValue&gt;.</param>
        /// <typeparam name="TValue">Type of value in the resulting Result.</typeparam>
        /// <returns>Result&lt;TValue&gt; from binder if current is success, otherwise propagates failure.</returns>
        public Result<TValue> Bind<TValue>(Func<Result<TValue>> binder)
        {
            return IsSuccess ? binder() : Result<TValue>.Failure(Error);
        }

        // ========== SIDE EFFECTS ==========

        /// <summary>
        /// Executes a side effect if the result is a success, without modifying the result
        /// </summary>
        /// <example>
        /// <code>
        /// var result = SaveToDatabase(data)
        ///     .Tap(() => _logger.LogInfo("Data saved"));
        /// </code>
        /// </example>
        /// <param name="action">Action to execute on success.</param>
        /// <returns>The original Result unchanged.</returns>
        public Result Tap(Action action)
        {
            if (IsSuccess)
                action();

            return this;
        }

        /// <summary>
        /// Executes a side effect if the result is a failure, without modifying the result
        /// </summary>
        /// <example>
        /// <code>
        /// var result = SaveToDatabase(data)
        ///     .TapError(error => _logger.LogError(error.Message));
        /// </code>
        /// </example>
        /// <param name="action">Action to execute on failure.</param>
        /// <returns>The original Result unchanged.</returns>
        public Result TapError(Action<Error> action)
        {
            if (IsFailure)
                action(Error);

            return this;
        }

        /// <summary>
        /// MapError - transforms the error without affecting success
        /// </summary>
        /// <example>
        /// <code>
        /// var result = Operation()
        ///     .MapError(error => Error.DatabaseError("DB_" + error.Code, error.Message));
        /// </code>
        /// </example>
        /// <param name="mapper">Function to transform the error.</param>
        /// <returns>Transformed failure or original success.</returns>
        public Result MapError(Func<Error, Error> mapper)
        {
            return IsFailure
                ? Failure(mapper(Error))
                : this;
        }

        /// <summary>
        /// Executes an action regardless of whether the result is success or failure (like finally block)
        /// </summary>
        /// <example>
        /// <code>
        /// var result = SaveToDatabase(data)
        ///     .Finally(() => connection.Close());
        /// </code>
        /// </example>
        /// <param name="action">Action to execute always.</param>
        /// <returns>The original Result unchanged.</returns>
        public Result Finally(Action action)
        {
            action();
            return this;
        }

        // ========== IMPLICIT CONVERSIONS ==========
        /// <summary>
        /// Implicit conversion from Error to Result (creates a failure result)
        /// </summary>
        /// <param name="error">Error to convert into Result.</param>
        public static implicit operator Result(Error error) => Failure(error);
    }
}
