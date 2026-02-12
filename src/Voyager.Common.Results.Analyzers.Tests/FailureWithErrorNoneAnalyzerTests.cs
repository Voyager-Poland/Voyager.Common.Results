using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Voyager.Common.Results.Analyzers;

namespace Voyager.Common.Results.Analyzers.Tests;

public class FailureWithErrorNoneAnalyzerTests
{
	private const string ResultStubs = @"
using Voyager.Common.Results;

namespace Voyager.Common.Results
{
	public enum ErrorType { None, Validation }
	public sealed record Error(ErrorType Type, string Code, string Message)
	{
		public static readonly Error None = new(ErrorType.None, string.Empty, string.Empty);
		public static Error ValidationError(string message) => new(ErrorType.Validation, """", message);
	}
	public class Result
	{
		public static Result Success() => new();
		public static Result Failure(Error error) => new();
	}
	public class Result<T> : Result
	{
		public static new Result<T> Failure(Error error) => new();
		public static Result<T> Success(T value) => new();
	}
}
";

	#region Warning cases

	[Fact]
	public async Task ReportsError_WhenResultFailureWithErrorNone()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = {|#0:Result.Failure(Error.None)|};
	}
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(FailureWithErrorNoneAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
				.WithLocation(0));
	}

	[Fact]
	public async Task ReportsError_WhenGenericResultFailureWithErrorNone()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = {|#0:Result<int>.Failure(Error.None)|};
	}
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(FailureWithErrorNoneAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
				.WithLocation(0));
	}

	#endregion

	#region No-warning cases

	[Fact]
	public async Task NoWarning_WhenFailureWithRealError()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result.Failure(Error.ValidationError(""bad input""));
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenFailureWithVariable()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var error = Error.ValidationError(""bad input"");
		var result = Result.Failure(error);
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenSuccess()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result.Success();
	}
}
";
		await RunAnalyzerTest(test);
	}

	#endregion

	#region Helpers

	private static async Task RunAnalyzerTest(string source, params DiagnosticResult[] expected)
	{
		var test = new CSharpAnalyzerTest<FailureWithErrorNoneAnalyzer, DefaultVerifier>
		{
			TestCode = source,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
		};
		test.ExpectedDiagnostics.AddRange(expected);
		await test.RunAsync();
	}

	#endregion
}
