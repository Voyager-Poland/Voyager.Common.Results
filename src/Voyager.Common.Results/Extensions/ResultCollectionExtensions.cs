namespace Voyager.Common.Results.Extensions;

/// <summary>
/// Extension methods dla operacji na kolekcjach Result
/// </summary>
public static class ResultCollectionExtensions
{
	/// <summary>
	/// Łączy kolekcję Result w jeden Result zawierający listę wartości.
	/// Jeśli którykolwiek Result jest Failure, zwraca pierwszy napotkany błąd.
	/// </summary>
	/// <example>
	/// var results = new[] { 
	///     Result&lt;int&gt;.Success(1), 
	///     Result&lt;int&gt;.Success(2) 
	/// };
	/// var combined = results.Combine(); // Result&lt;List&lt;int&gt;&gt; with [1, 2]
	/// </example>
	public static Result<List<TValue>> Combine<TValue>(
		this IEnumerable<Result<TValue>> results)
	{
		var resultsList = results.ToList();
		var values = new List<TValue>(resultsList.Count);

		foreach (var result in resultsList)
		{
			if (result.IsFailure)
				return Result<List<TValue>>.Failure(result.Error!);

			values.Add(result.Value!);
		}

		return Result<List<TValue>>.Success(values);
	}

	/// <summary>
	/// Łączy kolekcję Result w jeden Result.
	/// Jeśli którykolwiek Result jest Failure, zwraca pierwszy napotkany błąd.
	/// </summary>
	public static Result Combine(this IEnumerable<Result> results)
	{
		foreach (var result in results)
		{
			if (result.IsFailure)
				return Result.Failure(result.Error!);
		}

		return Result.Success();
	}

	/// <summary>
	/// Zbiera wszystkie błędy z kolekcji Results
	/// </summary>
	public static List<Error> GetErrors<TValue>(
		this IEnumerable<Result<TValue>> results)
	{
		return results
			.Where(r => r.IsFailure)
			.Select(r => r.Error!)
			.ToList();
	}

	/// <summary>
	/// Zbiera wszystkie wartości z pomyślnych Results
	/// </summary>
	public static List<TValue> GetSuccessValues<TValue>(
		this IEnumerable<Result<TValue>> results)
	{
		return results
			.Where(r => r.IsSuccess)
			.Select(r => r.Value!)
			.ToList();
	}

	/// <summary>
	/// Sprawdza czy wszystkie Results są sukcesami
	/// </summary>
	public static bool AllSuccess<TValue>(
		this IEnumerable<Result<TValue>> results)
	{
		return results.All(r => r.IsSuccess);
	}

	/// <summary>
	/// Sprawdza czy którykolwiek Result jest sukcesem
	/// </summary>
	public static bool AnySuccess<TValue>(
		this IEnumerable<Result<TValue>> results)
	{
		return results.Any(r => r.IsSuccess);
	}

	/// <summary>
	/// Partycjonuje Results na sukcesy i niepowodzenia
	/// </summary>
	public static (List<TValue> Successes, List<Error> Failures) Partition<TValue>(
		this IEnumerable<Result<TValue>> results)
	{
		var successes = new List<TValue>();
		var failures = new List<Error>();

		foreach (var result in results)
		{
			if (result.IsSuccess)
				successes.Add(result.Value!);
			else
				failures.Add(result.Error!);
		}

		return (successes, failures);
	}
}
