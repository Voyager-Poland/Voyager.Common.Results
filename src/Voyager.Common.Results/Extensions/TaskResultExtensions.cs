namespace Voyager.Common.Results.Extensions;

/// <summary>
/// Extension methods dla async Result operations
/// </summary>
public static class TaskResultExtensions
{
	// ========== MAP ASYNC ==========

	/// <summary>
	/// Mapuje wynik Task&lt;Result&lt;T&gt;&gt; na inny typ
	/// </summary>
	public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
		this Task<Result<TIn>> resultTask,
		Func<TIn, TOut> mapper)
	{
		var result = await resultTask.ConfigureAwait(false);
		return result.Map(mapper);
	}

	/// <summary>
	/// Mapuje wynik Result&lt;T&gt; używając async funkcji
	/// </summary>
	public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
		this Result<TIn> result,
		Func<TIn, Task<TOut>> mapper)
	{
		if (result.IsFailure)
			return Result<TOut>.Failure(result.Error!);

		var value = await mapper(result.Value!).ConfigureAwait(false);
		return Result<TOut>.Success(value);
	}

	/// <summary>
	/// Mapuje wynik Task&lt;Result&lt;T&gt;&gt; używając async funkcji
	/// </summary>
	public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
		this Task<Result<TIn>> resultTask,
		Func<TIn, Task<TOut>> mapper)
	{
		var result = await resultTask.ConfigureAwait(false);
		return await result.MapAsync(mapper).ConfigureAwait(false);
	}

	// ========== BIND ASYNC ==========

	/// <summary>
	/// Bind dla Task&lt;Result&lt;T&gt;&gt; z synchronicznym binder
	/// </summary>
	public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
		this Task<Result<TIn>> resultTask,
		Func<TIn, Result<TOut>> binder)
	{
		var result = await resultTask.ConfigureAwait(false);
		return result.Bind(binder);
	}

	/// <summary>
	/// Bind dla Result&lt;T&gt; z async binder
	/// </summary>
	public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
		this Result<TIn> result,
		Func<TIn, Task<Result<TOut>>> binder)
	{
		if (result.IsFailure)
			return Result<TOut>.Failure(result.Error!);

		return await binder(result.Value!).ConfigureAwait(false);
	}

	/// <summary>
	/// Bind dla Task&lt;Result&lt;T&gt;&gt; z async binder
	/// </summary>
	public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
		this Task<Result<TIn>> resultTask,
		Func<TIn, Task<Result<TOut>>> binder)
	{
		var result = await resultTask.ConfigureAwait(false);
		return await result.BindAsync(binder).ConfigureAwait(false);
	}

	// ========== TAP ASYNC ==========

	/// <summary>
	/// Tap dla Task&lt;Result&lt;T&gt;&gt; z synchroniczną akcją
	/// </summary>
	public static async Task<Result<TValue>> TapAsync<TValue>(
		this Task<Result<TValue>> resultTask,
		Action<TValue> action)
	{
		var result = await resultTask.ConfigureAwait(false);
		return result.Tap(action);
	}

	/// <summary>
	/// Tap dla Result&lt;T&gt; z async akcją
	/// </summary>
	public static async Task<Result<TValue>> TapAsync<TValue>(
		this Result<TValue> result,
		Func<TValue, Task> action)
	{
		if (result.IsSuccess)
			await action(result.Value!).ConfigureAwait(false);
		return result;
	}

	/// <summary>
	/// Tap dla Task&lt;Result&lt;T&gt;&gt; z async akcją
	/// </summary>
	public static async Task<Result<TValue>> TapAsync<TValue>(
		this Task<Result<TValue>> resultTask,
		Func<TValue, Task> action)
	{
		var result = await resultTask.ConfigureAwait(false);
		return await result.TapAsync(action).ConfigureAwait(false);
	}

	// ========== MATCH ASYNC ==========

	/// <summary>
	/// Match dla Task&lt;Result&lt;T&gt;&gt; z synchronicznymi funkcjami
	/// </summary>
	public static async Task<TResult> MatchAsync<TValue, TResult>(
		this Task<Result<TValue>> resultTask,
		Func<TValue, TResult> onSuccess,
		Func<Error, TResult> onFailure)
	{
		var result = await resultTask.ConfigureAwait(false);
		return result.Match(onSuccess, onFailure);
	}

	/// <summary>
	/// Match dla Result&lt;T&gt; z async funkcjami
	/// </summary>
	public static async Task<TResult> MatchAsync<TValue, TResult>(
		this Result<TValue> result,
		Func<TValue, Task<TResult>> onSuccess,
		Func<Error, Task<TResult>> onFailure)
	{
		return result.IsSuccess
			? await onSuccess(result.Value!).ConfigureAwait(false)
			: await onFailure(result.Error!).ConfigureAwait(false);
	}

	/// <summary>
	/// Match dla Task&lt;Result&lt;T&gt;&gt; z async funkcjami
	/// </summary>
	public static async Task<TResult> MatchAsync<TValue, TResult>(
		this Task<Result<TValue>> resultTask,
		Func<TValue, Task<TResult>> onSuccess,
		Func<Error, Task<TResult>> onFailure)
	{
		var result = await resultTask.ConfigureAwait(false);
		return await result.MatchAsync(onSuccess, onFailure).ConfigureAwait(false);
	}

	// ========== ENSURE ASYNC ==========

	/// <summary>
	/// Ensure dla Task&lt;Result&lt;T&gt;&gt; z synchronicznym predicate
	/// </summary>
	public static async Task<Result<TValue>> EnsureAsync<TValue>(
		this Task<Result<TValue>> resultTask,
		Func<TValue, bool> predicate,
		Error error)
	{
		var result = await resultTask.ConfigureAwait(false);
		return result.Ensure(predicate, error);
	}

	/// <summary>
	/// Ensure dla Result&lt;T&gt; z async predicate
	/// </summary>
	public static async Task<Result<TValue>> EnsureAsync<TValue>(
		this Result<TValue> result,
		Func<TValue, Task<bool>> predicate,
		Error error)
	{
		if (result.IsFailure)
			return result;

		var isValid = await predicate(result.Value!).ConfigureAwait(false);
		return isValid ? result : Result<TValue>.Failure(error);
	}

	/// <summary>
	/// Ensure dla Task&lt;Result&lt;T&gt;&gt; z async predicate
	/// </summary>
	public static async Task<Result<TValue>> EnsureAsync<TValue>(
		this Task<Result<TValue>> resultTask,
		Func<TValue, Task<bool>> predicate,
		Error error)
	{
		var result = await resultTask.ConfigureAwait(false);
		return await result.EnsureAsync(predicate, error).ConfigureAwait(false);
	}
}
