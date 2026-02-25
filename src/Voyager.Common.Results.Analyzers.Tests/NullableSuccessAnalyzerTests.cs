using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Voyager.Common.Results.Analyzers;

namespace Voyager.Common.Results.Analyzers.Tests;

public class NullableSuccessAnalyzerTests
{
	private const string ResultStubs = @"
#nullable enable
using Voyager.Common.Results;

namespace Voyager.Common.Results
{
	public enum ErrorType { None, Validation, NotFound, Unexpected }
	public sealed record Error(ErrorType Type, string Code, string Message)
	{
		public static readonly Error None = new(ErrorType.None, string.Empty, string.Empty);
		public static Error ValidationError(string message) => new(ErrorType.Validation, """", message);
		public static Error NotFoundError(string message) => new(ErrorType.NotFound, """", message);
		public static Error UnexpectedError(string message) => new(ErrorType.Unexpected, """", message);
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
	public async Task ReportsWarning_WhenSuccessWithLiteralNull()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = {|#0:Result<string?>.Success(null)|};
	}
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(NullableSuccessAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
				.WithLocation(0));
	}

	[Fact]
	public async Task ReportsWarning_WhenSuccessWithDefault()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = {|#0:Result<string?>.Success(default)|};
	}
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(NullableSuccessAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
				.WithLocation(0));
	}

	[Fact]
	public async Task ReportsWarning_WhenGenericResultSuccessWithNull()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = {|#0:Result<int?>.Success(null)|};
	}
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(NullableSuccessAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
				.WithLocation(0));
	}

	[Fact]
	public async Task ReportsWarning_WhenSuccessWithCastNull()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = {|#0:Result<string?>.Success((string?)null)|};
	}
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(NullableSuccessAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
				.WithLocation(0));
	}

	[Fact]
	public async Task ReportsWarning_WhenSuccessWithDefaultOnReferenceType()
	{
		var test = ResultStubs + @"
class Order { }
class C
{
	void Test()
	{
		var result = {|#0:Result<Order?>.Success(default)|};
	}
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(NullableSuccessAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
				.WithLocation(0));
	}

	#endregion

	#region No-warning cases

	[Fact]
	public async Task NoWarning_WhenSuccessWithValue()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<string>.Success(""hello"");
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenSuccessWithNonNullValueInNullableResult()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<string?>.Success(""hello"");
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenSuccessWithVariable()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		string? value = ""hello"";
		var result = Result<string?>.Success(value);
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenFailureWithError()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<string>.Failure(Error.ValidationError(""bad input""));
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenNonGenericResultSuccess()
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

	[Fact]
	public async Task NoWarning_WhenNonResultType()
	{
		var test = ResultStubs + @"
class OtherType<T>
{
	public static OtherType<T> Success(T value) => new();
}
class C
{
	void Test()
	{
		var result = OtherType<string?>.Success(null);
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenSuccessWithNonNullInt()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int?>.Success(42);
	}
}
";
		await RunAnalyzerTest(test);
	}

	#endregion

	#region Code fix tests

	[Fact]
	public async Task CodeFix_ReplacesSuccessNullWithFailure()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = {|#0:Result<string?>.Success(null)|};
	}
}
";
		var fixedSource = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<string?>.Failure(Error.NotFoundError(""TODO: provide meaningful error""));
	}
}
";
		await RunCodeFixTest(test, fixedSource,
			new DiagnosticResult(NullableSuccessAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
				.WithLocation(0));
	}

	[Fact]
	public async Task CodeFix_ReplacesSuccessNullInNullableValueType()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = {|#0:Result<int?>.Success(null)|};
	}
}
";
		var fixedSource = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int?>.Failure(Error.NotFoundError(""TODO: provide meaningful error""));
	}
}
";
		await RunCodeFixTest(test, fixedSource,
			new DiagnosticResult(NullableSuccessAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
				.WithLocation(0));
	}

	#endregion

	#region Helpers

	private static async Task RunAnalyzerTest(string source, params DiagnosticResult[] expected)
	{
		var test = new CSharpAnalyzerTest<NullableSuccessAnalyzer, DefaultVerifier>
		{
			TestCode = source,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
		};
		test.ExpectedDiagnostics.AddRange(expected);
		await test.RunAsync();
	}

	private static async Task RunCodeFixTest(
		string source, string fixedSource, DiagnosticResult expected)
	{
		var test = new CSharpCodeFixTest<NullableSuccessAnalyzer, NullableSuccessCodeFixProvider, DefaultVerifier>
		{
			TestCode = source,
			FixedCode = fixedSource,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
		};
		test.ExpectedDiagnostics.Add(expected);
		await test.RunAsync();
	}

	#endregion
}
