using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Voyager.Common.Results.Analyzers;

namespace Voyager.Common.Results.Analyzers.Tests;

public class PreferMatchSwitchAnalyzerTests
{
	private const string ResultStubs = @"
using System;
using Voyager.Common.Results;

namespace Voyager.Common.Results
{
	public sealed record Error(string Message);
	public class Result
	{
		public bool IsSuccess { get; }
		public bool IsFailure => !IsSuccess;
		public Error Error { get; }
		public void Switch(Action onSuccess, Action<Error> onFailure) { }
		public TResult Match<TResult>(Func<TResult> onSuccess, Func<Error, TResult> onFailure) => default;
	}
	public class Result<T> : Result
	{
		public T Value { get; }
		public void Switch(Action<T> onSuccess, Action<Error> onFailure) { }
		public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure) => default;
	}
}
";

	#region Warning cases

	[Fact]
	public async Task ReportsInfo_WhenIfElseOnIsSuccess()
	{
		var test = ResultStubs + @"
class C
{
	void Test(Result result)
	{
		{|#0:if (result.IsSuccess)
		{
			System.Console.WriteLine(""ok"");
		}
		else
		{
			System.Console.WriteLine(""fail"");
		}|}
	}
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(PreferMatchSwitchAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
				.WithLocation(0)
				.WithArguments("IsSuccess"));
	}

	[Fact]
	public async Task ReportsInfo_WhenIfElseOnIsFailure()
	{
		var test = ResultStubs + @"
class C
{
	void Test(Result result)
	{
		{|#0:if (result.IsFailure)
		{
			System.Console.WriteLine(""fail"");
		}
		else
		{
			System.Console.WriteLine(""ok"");
		}|}
	}
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(PreferMatchSwitchAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
				.WithLocation(0)
				.WithArguments("IsFailure"));
	}

	[Fact]
	public async Task ReportsInfo_WhenNegatedIsSuccess()
	{
		var test = ResultStubs + @"
class C
{
	void Test(Result result)
	{
		{|#0:if (!result.IsSuccess)
		{
			System.Console.WriteLine(""fail"");
		}
		else
		{
			System.Console.WriteLine(""ok"");
		}|}
	}
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(PreferMatchSwitchAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
				.WithLocation(0)
				.WithArguments("IsSuccess"));
	}

	#endregion

	#region No-warning cases

	[Fact]
	public async Task NoWarning_WhenIfWithoutElse()
	{
		var test = ResultStubs + @"
class C
{
	void Test(Result result)
	{
		if (result.IsSuccess)
		{
			System.Console.WriteLine(""ok"");
		}
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenNotResultType()
	{
		var test = ResultStubs + @"
class MyObj
{
	public bool IsSuccess { get; }
}
class C
{
	void Test(MyObj obj)
	{
		if (obj.IsSuccess) { }
		else { }
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenUsingMatch()
	{
		var test = ResultStubs + @"
class C
{
	void Test(Result result)
	{
		var x = result.Match(() => ""ok"", err => err.Message);
	}
}
";
		await RunAnalyzerTest(test);
	}

	#endregion

	#region Helpers

	private static async Task RunAnalyzerTest(string source, params DiagnosticResult[] expected)
	{
		var test = new CSharpAnalyzerTest<PreferMatchSwitchAnalyzer, DefaultVerifier>
		{
			TestCode = source,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
		};
		test.ExpectedDiagnostics.AddRange(expected);

		// VCR0060 is disabled by default — enable it via .editorconfig
		test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", @"
root = true
[*]
dotnet_diagnostic.VCR0060.severity = warning
"));

		await test.RunAsync();
	}

	#endregion
}
