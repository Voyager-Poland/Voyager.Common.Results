#if NET48
using System;
#endif

namespace Voyager.Common.Results
{
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
		/// <param name="value">Value to wrap in a successful Result.</param>
		public static Result<TValue> Success(TValue value) => new(value);

		/// <summary>
		/// Creates a failure result
		/// </summary>
		/// <param name="error">Error describing the failure.</param>
		public static new Result<TValue> Failure(Error error) => new(error);

		/// <summary>
		/// Executes a function and wraps any exceptions in a Result
		/// </summary>
		/// <example>
		/// <code>
		/// var result = Result&lt;int&gt;.Try(() => int.Parse("123"));
		/// var config = Result&lt;Config&gt;.Try(() => JsonSerializer.Deserialize&lt;Config&gt;(json));
		/// </code>
		/// </example>
		/// <param name="func">Function to execute.</param>
		/// <returns>Success with function result if completes without exception, otherwise Failure with error from exception.</returns>
		public static Result<TValue> Try(Func<TValue> func)
		{
			try
			{
				return Success(func());
			}
			catch (Exception ex)
			{
				return Failure(Error.FromException(ex));
			}
		}

		/// <summary>
		/// Executes a function and wraps any exceptions in a Result with custom error mapping
		/// </summary>
		/// <example>
		/// <code>
		/// var result = Result&lt;Config&gt;.Try(
		///     () => JsonSerializer.Deserialize&lt;Config&gt;(json),
		///     ex => ex is JsonException 
		///         ? Error.ValidationError("Invalid JSON")
		///         : Error.UnexpectedError(ex.Message));
		/// </code>
		/// </example>
		/// <param name="func">Function to execute.</param>
		/// <param name="errorMapper">Function to convert exception to custom error.</param>
		/// <returns>Success with function result if completes without exception, otherwise Failure with mapped error.</returns>
		public static Result<TValue> Try(Func<TValue> func, Func<Exception, Error> errorMapper)
		{
			try
			{
				return Success(func());
			}
			catch (Exception ex)
			{
				return Failure(errorMapper(ex));
			}
		}

		// ========== PATTERN MATCHING ==========

		/// <summary>
		/// Returns a value depending on the result
		/// </summary>
		/// <typeparam name="TResult">Type of the returned value.</typeparam>
		/// <param name="onSuccess">Function to execute on success.</param>
		/// <param name="onFailure">Function to execute on failure.</param>
		/// <returns>Result of executed function.</returns>
		public TResult Match<TResult>(
				Func<TValue, TResult> onSuccess,
				Func<Error, TResult> onFailure)
		{
			return IsSuccess ? onSuccess(Value!) : onFailure(Error!);
		}

		/// <summary>
		/// Executes the appropriate action depending on the result
		/// </summary>
		/// <param name="onSuccess">Action to execute on success with the value.</param>
		/// <param name="onFailure">Action to execute on failure with the error.</param>
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
		/// <param name="mapper">Mapping function applied to the success value.</param>
		/// <typeparam name="TOut">Target type.</typeparam>
		/// <returns>Mapped Result of type Result&lt;TOut&gt;.</returns>
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
		/// <param name="binder">Binder function returning Result&lt;TOut&gt;.</param>
		/// <typeparam name="TOut">Target result value type.</typeparam>
		/// <returns>Result&lt;TOut&gt; produced by binder or propagates error.</returns>
		public Result<TOut> Bind<TOut>(Func<TValue, Result<TOut>> binder)
		{
			return IsSuccess
					? binder(Value!)
					: Result<TOut>.Failure(Error!);
		}

		/// <summary>
		/// Tap - performs an action on the value without changing the Result (side effect)
		/// </summary>
		/// <param name="action">Action to perform on the success value.</param>
		/// <returns>The same Result instance.</returns>
		public Result<TValue> Tap(Action<TValue> action)
		{
			if (IsSuccess)
				action(Value!);
			return this;
		}

		/// <summary>
		/// TapError - performs an action on the error without changing the Result
		/// </summary>
		/// <param name="action">Action to perform on the error.</param>
		/// <returns>The same Result instance.</returns>
		public Result<TValue> TapError(Action<Error> action)
		{
			if (IsFailure)
				action(Error!);
			return this;
		}

		/// <summary>
		/// Executes an action regardless of whether the result is success or failure (like finally block)
		/// </summary>
		/// <example>
		/// <code>
		/// var userData = LoadFromFile(path)
		///     .Finally(() => fileStream.Dispose());
		/// </code>
		/// </example>
		/// <param name="action">Action to execute always.</param>
		/// <returns>The original Result&lt;TValue&gt; unchanged.</returns>
		public new Result<TValue> Finally(Action action)
		{
			action();
			return this;
		}

		/// <summary>
		/// Ensure - validates the success value, may convert to Failure
		/// </summary>
		/// <param name="predicate">Predicate to validate the success value.</param>
		/// <param name="error">Error to return if predicate fails.</param>
		/// <returns>Original result if predicate passes, otherwise a Failure result.</returns>
		public Result<TValue> Ensure(Func<TValue, bool> predicate, Error error)
		{
			if (IsFailure)
				return this;

			return predicate(Value!) ? this : Failure(error);
		}

		/// <summary>
		/// MapError - transforms the error
		/// </summary>
		/// <param name="mapper">Function to transform the error.</param>
		/// <returns>Transformed failure or original success.</returns>
		public Result<TValue> MapError(Func<Error, Error> mapper)
		{
			return IsFailure
					? Failure(mapper(Error!))
					: this;
		}

		/// <summary>
		/// GetValueOrDefault - returns the value or the default value
		/// </summary>
		/// <param name="defaultValue">Default value to return when result is failure.</param>
		/// <returns>Success value or provided default.</returns>
		public TValue? GetValueOrDefault(TValue? defaultValue = default)
		{
			return IsSuccess ? Value : defaultValue;
		}

		/// <summary>
		/// GetValueOrThrow - returns the value or throws an exception
		/// </summary>
		/// <returns>Success value.</returns>
		/// <exception cref="InvalidOperationException">Thrown when result is a failure.</exception>
		public TValue GetValueOrThrow()
		{
			if (IsFailure)
				throw new InvalidOperationException($"Result is a failure: {Error!.Message}");
			return Value!;
		}


		/// <summary>
		/// OrElse - returns the current result if successful, otherwise returns the alternative result
		/// </summary>
		/// <param name="alternative">Alternative result to return when current result is failure.</param>
		/// <returns>Current result if success, otherwise the alternative result.</returns>
		public Result<TValue> OrElse(Result<TValue> alternative)
		{
			return IsSuccess ? this : alternative;
		}

		/// <summary>
		/// OrElse - returns the current result if successful, otherwise invokes the alternative function
		/// </summary>
		/// <param name="alternativeFunc">Function producing an alternative result when current result is failure.</param>
		/// <returns>Current result if success, otherwise the result from alternative function.</returns>
		public Result<TValue> OrElse(Func<Result<TValue>> alternativeFunc)
		{
			return IsSuccess ? this : alternativeFunc();
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
		/// <param name="error">The error to convert to a failed result.</param>
		public static implicit operator Result<TValue>(Error error) => Failure(error);
	}
}
