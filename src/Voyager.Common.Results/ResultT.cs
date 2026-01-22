#if NET48
using System;
using System.Threading;
using System.Threading.Tasks;
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

		// ========== TRY ASYNC (PROXY METHODS) ==========

		/// <summary>
		/// Executes an asynchronous function and wraps any exceptions in a Result
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await Result&lt;Config&gt;.TryAsync(async () => 
		///     await JsonSerializer.DeserializeAsync&lt;Config&gt;(stream));
		/// </code>
		/// </example>
		/// <param name="func">Asynchronous function to execute.</param>
		/// <returns>Success with function result if completes without exception, otherwise Failure with error from exception.</returns>
		public static Task<Result<TValue>> TryAsync(Func<Task<TValue>> func) =>
			Extensions.TaskResultExtensions.TryAsync(func);

		/// <summary>
		/// Executes an asynchronous function and wraps any exceptions in a Result with custom error mapping
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await Result&lt;Config&gt;.TryAsync(
		///     async () => await JsonSerializer.DeserializeAsync&lt;Config&gt;(stream),
		///     ex => ex is JsonException 
		///         ? Error.ValidationError("Invalid JSON")
		///         : Error.UnexpectedError(ex.Message));
		/// </code>
		/// </example>
		/// <param name="func">Asynchronous function to execute.</param>
		/// <param name="errorMapper">Function to convert exception to custom error.</param>
		/// <returns>Success with function result if completes without exception, otherwise Failure with mapped error.</returns>
		public static Task<Result<TValue>> TryAsync(Func<Task<TValue>> func, Func<Exception, Error> errorMapper) =>
			Extensions.TaskResultExtensions.TryAsync(func, errorMapper);

		/// <summary>
		/// Executes an asynchronous function with cancellation support
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await Result&lt;string&gt;.TryAsync(
		///     async ct => await httpClient.GetStringAsync(url, ct),
		///     cancellationToken);
		/// </code>
		/// </example>
		/// <param name="func">Asynchronous function that accepts a CancellationToken.</param>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		/// <returns>Success with value if completes, Failure with CancelledError if cancelled, or error from exception.</returns>
		public static Task<Result<TValue>> TryAsync(Func<CancellationToken, Task<TValue>> func, CancellationToken cancellationToken) =>
			Extensions.TaskResultExtensions.TryAsync(func, cancellationToken);

		/// <summary>
		/// Executes an asynchronous function with cancellation support and custom error mapping
		/// </summary>
		/// <param name="func">Asynchronous function that accepts a CancellationToken.</param>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		/// <param name="errorMapper">Function to convert exception to custom error.</param>
		/// <returns>Success with value if completes, Failure with CancelledError if cancelled, or mapped error from exception.</returns>
		public static Task<Result<TValue>> TryAsync(Func<CancellationToken, Task<TValue>> func, CancellationToken cancellationToken, Func<Exception, Error> errorMapper) =>
			Extensions.TaskResultExtensions.TryAsync(func, cancellationToken, errorMapper);

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
		/// Bind - chains the result with a Result-returning function that produces no value (void Result)
		/// </summary>
		/// <remarks>
		/// Useful for composing operations where the current value is used to perform an operation
		/// that can fail but doesn't produce a new value. Unlike Tap, Bind propagates errors from the operation.
		/// </remarks>
		/// <example>
		/// GetUser(userId)
		///     .Bind(user => ValidateAge(user))        // Result&lt;User&gt; → Result&lt;User&gt;
		///     .Bind(user => SendNotification(user))   // Result&lt;User&gt; → Result (void)
		///     .Map(() => "Success");
		/// </example>
		/// <param name="binder">Binder function returning Result (void operation).</param>
		/// <returns>Result produced by binder or propagates error.</returns>
		public Result Bind(Func<TValue, Result> binder)
		{
			return IsSuccess
					? binder(Value!)
					: Result.Failure(Error!);
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
		public new Result<TValue> TapError(Action<Error> action)
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
		/// Ensure - validates the success value with contextual error, may convert to Failure
		/// </summary>
		/// <example>
		/// <code>
		/// var result = GetUser(id)
		///     .Ensure(
		///         user => user.Age >= 18,
		///         user => Error.ValidationError($"User {user.Name} is {user.Age} years old, must be 18+"));
		/// </code>
		/// </example>
		/// <param name="predicate">Predicate to validate the success value.</param>
		/// <param name="errorFactory">Function to create error from the value if predicate fails.</param>
		/// <returns>Original result if predicate passes, otherwise a Failure result with contextual error.</returns>
		public Result<TValue> Ensure(Func<TValue, bool> predicate, Func<TValue, Error> errorFactory)
		{
			if (IsFailure)
				return this;

			return predicate(Value!) ? this : Failure(errorFactory(Value!));
		}

		/// <summary>
		/// EnsureAsync - validates the success value with an async predicate
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await GetUser(id)
		///     .EnsureAsync(
		///         async user => await _repo.IsActiveAsync(user.Id),
		///         Error.ValidationError("User is inactive"));
		/// </code>
		/// </example>
		/// <param name="predicate">Async predicate to validate the success value.</param>
		/// <param name="error">Error to return if predicate fails.</param>
		/// <returns>Original result if predicate passes, otherwise a Failure result.</returns>
		public Task<Result<TValue>> EnsureAsync(Func<TValue, Task<bool>> predicate, Error error) =>
			Extensions.TaskResultExtensions.EnsureAsync(this, predicate, error);

		/// <summary>
		/// EnsureAsync - validates the success value with an async predicate and contextual error
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await GetUser(id)
		///     .EnsureAsync(
		///         async user => await _repo.IsActiveAsync(user.Id),
		///         user => Error.ValidationError($"User {user.Name} is inactive"));
		/// </code>
		/// </example>
		/// <param name="predicate">Async predicate to validate the success value.</param>
		/// <param name="errorFactory">Function to create error from the value if predicate fails.</param>
		/// <returns>Original result if predicate passes, otherwise a Failure result with contextual error.</returns>
		public Task<Result<TValue>> EnsureAsync(Func<TValue, Task<bool>> predicate, Func<TValue, Error> errorFactory) =>
			Extensions.TaskResultExtensions.EnsureAsync(this, predicate, errorFactory);

		/// <summary>
		/// TapAsync - executes an async side effect if the result is a success
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await GetUser(id)
		///     .TapAsync(async user => await _auditLog.LogAsync($"User {user.Name} accessed"));
		/// </code>
		/// </example>
		/// <param name="action">Async action to execute on success value.</param>
		/// <returns>Original result unchanged.</returns>
		public Task<Result<TValue>> TapAsync(Func<TValue, Task> action) =>
			Extensions.TaskResultExtensions.TapAsync(this, action);

		/// <summary>
		/// OrElseAsync - provides an async fallback if the result is a failure
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await GetUserFromCache(id)
		///     .OrElseAsync(async () => await GetUserFromDatabaseAsync(id));
		/// </code>
		/// </example>
		/// <param name="alternativeFunc">Async function returning alternative result.</param>
		/// <returns>Original result if success, otherwise the alternative result.</returns>
		public Task<Result<TValue>> OrElseAsync(Func<Task<Result<TValue>>> alternativeFunc) =>
			Extensions.TaskResultExtensions.OrElseAsync(this, alternativeFunc);

		/// <summary>
		/// MapError - transforms the error
		/// </summary>
		/// <param name="mapper">Function to transform the error.</param>
		/// <returns>Transformed failure or original success.</returns>
		public new Result<TValue> MapError(Func<Error, Error> mapper)
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
