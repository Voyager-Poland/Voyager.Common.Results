using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Voyager.Common.Results.Analyzers;

namespace Voyager.Common.Results.Analyzers.Tests;

public class NullableResultTypeAnalyzerTests
{
	private const string ResultStubs = @"
#nullable enable
using System.Threading.Tasks;
using Voyager.Common.Results;

namespace Voyager.Common.Results
{
	public enum ErrorType { None, Validation, NotFound, Unexpected }
	public sealed record Error(ErrorType Type, string Code, string Message)
	{
		public static readonly Error None = new(ErrorType.None, string.Empty, string.Empty);
		public static Error NotFoundError(string message) => new(ErrorType.NotFound, """", message);
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

	#region Info cases (disabled by default — must enable via CompilerDiagnostics)

	[Fact]
	public async Task ReportsInfo_WhenMethodReturnsResultWithNullableReferenceType()
	{
		var test = ResultStubs + @"
class C
{
	Result<string?> {|#0:GetValue|}() => Result<string?>.Success(""hello"");
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(NullableResultTypeAnalyzer.DiagnosticId,
					DiagnosticSeverity.Info)
				.WithLocation(0));
	}

	[Fact]
	public async Task ReportsInfo_WhenMethodReturnsResultWithNullableValueType()
	{
		var test = ResultStubs + @"
class C
{
	Result<int?> {|#0:GetValue|}() => Result<int?>.Success(42);
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(NullableResultTypeAnalyzer.DiagnosticId,
					DiagnosticSeverity.Info)
				.WithLocation(0));
	}

	[Fact]
	public async Task ReportsInfo_WhenMethodReturnsTaskResultWithNullableType()
	{
		var test = ResultStubs + @"
class C
{
	Task<Result<string?>> {|#0:GetValueAsync|}() => Task.FromResult(Result<string?>.Success(""hello""));
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(NullableResultTypeAnalyzer.DiagnosticId,
					DiagnosticSeverity.Info)
				.WithLocation(0));
	}

	[Fact]
	public async Task ReportsInfo_WhenPropertyReturnsResultWithNullableType()
	{
		var test = ResultStubs + @"
class C
{
	Result<string?> {|#0:Value|} => Result<string?>.Success(""hello"");
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(NullableResultTypeAnalyzer.DiagnosticId,
					DiagnosticSeverity.Info)
				.WithLocation(0));
	}

	[Fact]
	public async Task ReportsInfo_WhenFieldIsResultWithNullableType()
	{
		var test = ResultStubs + @"
class C
{
	Result<string?> {|#0:_value|} = Result<string?>.Success(""hello"");
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(NullableResultTypeAnalyzer.DiagnosticId,
					DiagnosticSeverity.Info)
				.WithLocation(0));
	}

	#endregion

	#region No-info cases

	[Fact]
	public async Task NoInfo_WhenMethodReturnsResultWithNonNullableType()
	{
		var test = ResultStubs + @"
class C
{
	Result<string> GetValue() => Result<string>.Success(""hello"");
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoInfo_WhenMethodReturnsNonGenericResult()
	{
		var test = ResultStubs + @"
class C
{
	Result GetValue() => Result.Success();
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoInfo_WhenMethodReturnsNonResultType()
	{
		var test = ResultStubs + @"
class C
{
	string? GetValue() => null;
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoInfo_WhenPropertyReturnsResultWithNonNullableType()
	{
		var test = ResultStubs + @"
class C
{
	Result<int> Value => Result<int>.Success(42);
}
";
		await RunAnalyzerTest(test);
	}

	#endregion

	#region Helpers

	private static async Task RunAnalyzerTest(string source, params DiagnosticResult[] expected)
	{
		var test = new CSharpAnalyzerTest<NullableResultTypeAnalyzer, DefaultVerifier>
		{
			TestCode = source,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
		};
		// VCR0071 is disabled by default — enable it for tests
		test.SolutionTransforms.Add((solution, projectId) =>
		{
			var project = solution.GetProject(projectId);
			if (project == null)
				return solution;
			var options = project.CompilationOptions;
			if (options == null)
				return solution;
			var diagnosticOptions = options.SpecificDiagnosticOptions.SetItem(
				NullableResultTypeAnalyzer.DiagnosticId, ReportDiagnostic.Info);
			return solution.WithProjectCompilationOptions(projectId,
				options.WithSpecificDiagnosticOptions(diagnosticOptions));
		});
		test.ExpectedDiagnostics.AddRange(expected);
		await test.RunAsync();
	}

	#endregion
}
