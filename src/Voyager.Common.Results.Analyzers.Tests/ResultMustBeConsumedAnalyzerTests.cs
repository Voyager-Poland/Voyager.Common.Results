using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Voyager.Common.Results.Analyzers;

namespace Voyager.Common.Results.Analyzers.Tests;

public class ResultMustBeConsumedAnalyzerTests
{
	private const string ResultStubs = @"
using System;
using System.Threading.Tasks;
using Voyager.Common.Results;

namespace Voyager.Common.Results
{
	public class Error { }
	public class Result
	{
		public bool IsSuccess { get; }
		public bool IsFailure => !IsSuccess;
		public static Result Success() => new Result();
		public static Result Failure() => new Result();
		public Result Tap(Action action) => this;
		public void Switch(Action onSuccess, Action<Error> onFailure) { }
		public TResult Match<TResult>(Func<TResult> onSuccess, Func<TResult> onFailure) => onSuccess();
	}
	public class Result<T> : Result
	{
		public T Value { get; }
		public static new Result<T> Success(T value) => new Result<T>();
		public static new Result<T> Failure() => new Result<T>();
		public Result<T> Ensure(Func<T, bool> predicate) => this;
		public void Switch(Action<T> onSuccess, Action<Error> onFailure) { }
	}
}
";

	#region Warning cases

	[Fact]
	public async Task ReportsWarning_WhenResultIgnored()
	{
		var test = ResultStubs + @"
class C
{
	Result M() => Result.Success();
	void Test()
	{
		{|#0:M()|};
	}
}
";
		await RunAnalyzerTest(test, Expect("M"));
	}

	[Fact]
	public async Task ReportsWarning_WhenGenericResultIgnored()
	{
		var test = ResultStubs + @"
class C
{
	Result<int> M() => Result<int>.Success(42);
	void Test()
	{
		{|#0:M()|};
	}
}
";
		await RunAnalyzerTest(test, Expect("M"));
	}

	[Fact]
	public async Task ReportsWarning_WhenAwaitedTaskResultIgnored()
	{
		var test = ResultStubs + @"
class C
{
	Task<Result> M() => Task.FromResult(Result.Success());
	async void Test()
	{
		{|#0:await M()|};
	}
}
";
		await RunAnalyzerTest(test, Expect("M"));
	}

	[Fact]
	public async Task ReportsWarning_WhenAwaitedTaskGenericResultIgnored()
	{
		var test = ResultStubs + @"
class C
{
	Task<Result<int>> M() => Task.FromResult(Result<int>.Success(42));
	async void Test()
	{
		{|#0:await M()|};
	}
}
";
		await RunAnalyzerTest(test, Expect("M"));
	}

	[Fact]
	public async Task ReportsWarning_WhenFactoryMethodIgnored()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		{|#0:Result.Success()|};
	}
}
";
		await RunAnalyzerTest(test, Expect("Success"));
	}

	#endregion

	#region No-warning cases

	[Fact]
	public async Task NoWarning_WhenAssignedToVariable()
	{
		var test = ResultStubs + @"
class C
{
	Result M() => Result.Success();
	void Test()
	{
		var result = M();
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenDiscarded()
	{
		var test = ResultStubs + @"
class C
{
	Result M() => Result.Success();
	void Test()
	{
		_ = M();
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenUsedInMethodChain()
	{
		var test = ResultStubs + @"
class C
{
	Result M() => Result.Success();
	void Test()
	{
		var x = M().Match(() => 1, () => 0);
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenPassedAsArgument()
	{
		var test = ResultStubs + @"
class C
{
	Result M() => Result.Success();
	void Log(Result r) { }
	void Test()
	{
		Log(M());
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenReturned()
	{
		var test = ResultStubs + @"
class C
{
	Result M() => Result.Success();
	Result Test()
	{
		return M();
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenUsedInCondition()
	{
		var test = ResultStubs + @"
class C
{
	Result M() => Result.Success();
	void Test()
	{
		if (M().IsSuccess) { }
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_ForNonResultTypes()
	{
		var test = ResultStubs + @"
class C
{
	int M() => 42;
	string N() => ""hello"";
	void Test()
	{
		M();
		N();
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_ForVoidMethods()
	{
		var test = ResultStubs + @"
class C
{
	void M() { }
	void Test()
	{
		M();
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenSwitchUsedAsStatement()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		result.Switch(v => { }, e => { });
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task ReportsWarning_WhenTapResultDiscarded()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result.Success();
		{|#0:result.Tap(() => { })|};
	}
}
";
		await RunAnalyzerTest(test, Expect("Tap"));
	}

	#endregion

	#region Code fix tests

	[Fact]
	public async Task CodeFix_AddsDiscard()
	{
		var test = ResultStubs + @"
class C
{
	Result M() => Result.Success();
	void Test()
	{
		{|#0:M()|};
	}
}
";
		var fixedCode = ResultStubs + @"
class C
{
	Result M() => Result.Success();
	void Test()
	{
		_ = M();
	}
}
";
		await RunCodeFixTest(test, fixedCode, Expect("M"), codeActionIndex: 0);
	}

	[Fact]
	public async Task CodeFix_AssignsToVariable()
	{
		var test = ResultStubs + @"
class C
{
	Result M() => Result.Success();
	void Test()
	{
		{|#0:M()|};
	}
}
";
		var fixedCode = ResultStubs + @"
class C
{
	Result M() => Result.Success();
	void Test()
	{
		var result = M();
	}
}
";
		await RunCodeFixTest(test, fixedCode, Expect("M"), codeActionIndex: 1);
	}

	#endregion

	#region Helpers

	private static DiagnosticResult Expect(string methodName) =>
		new DiagnosticResult(ResultMustBeConsumedAnalyzer.DiagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
			.WithLocation(0)
			.WithArguments(methodName);

	private static async Task RunAnalyzerTest(string source, params DiagnosticResult[] expected)
	{
		var test = new CSharpAnalyzerTest<ResultMustBeConsumedAnalyzer, DefaultVerifier>
		{
			TestCode = source,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
		};
		test.ExpectedDiagnostics.AddRange(expected);
		await test.RunAsync();
	}

	private static async Task RunCodeFixTest(
		string source, string fixedSource, DiagnosticResult expected, int codeActionIndex)
	{
		var test = new CSharpCodeFixTest<ResultMustBeConsumedAnalyzer, ResultMustBeConsumedCodeFixProvider, DefaultVerifier>
		{
			TestCode = source,
			FixedCode = fixedSource,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
			CodeActionIndex = codeActionIndex,
		};
		test.ExpectedDiagnostics.Add(expected);
		await test.RunAsync();
	}

	#endregion
}
