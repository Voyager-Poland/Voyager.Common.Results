using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Voyager.Common.Results.Analyzers;

namespace Voyager.Common.Results.Analyzers.Tests;

public class GetValueOrThrowAnalyzerTests
{
	private const string ResultStubs = @"
using System;
using Voyager.Common.Results;

namespace Voyager.Common.Results
{
	public class Result
	{
		public bool IsSuccess { get; }
	}
	public class Result<T> : Result
	{
		public T Value { get; }
		public T GetValueOrThrow() => Value;
		public T GetValueOrDefault(T defaultValue = default) => IsSuccess ? Value : defaultValue;
		public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder) => default;
		public static Result<T> Success(T value) => new Result<T>();
	}
}
";

	#region Warning cases

	[Fact]
	public async Task ReportsInfo_WhenGetValueOrThrowCalled()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		var value = {|#0:result.GetValueOrThrow()|};
	}
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(GetValueOrThrowAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
				.WithLocation(0));
	}

	[Fact]
	public async Task ReportsInfo_WhenChainedGetValueOrThrow()
	{
		var test = ResultStubs + @"
class C
{
	Result<int> GetValue() => Result<int>.Success(42);
	void Test()
	{
		var value = {|#0:GetValue().GetValueOrThrow()|};
	}
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(GetValueOrThrowAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
				.WithLocation(0));
	}

	#endregion

	#region No-warning cases

	[Fact]
	public async Task NoWarning_WhenGetValueOrDefaultCalled()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		var value = result.GetValueOrDefault(0);
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenBindIsUsed()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		var bound = result.Bind(x => Result<string>.Success(x.ToString()));
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_ForNonResultGetValueOrThrow()
	{
		var test = ResultStubs + @"
class MyType
{
	public int GetValueOrThrow() => 42;
}
class C
{
	void Test()
	{
		var x = new MyType();
		var value = x.GetValueOrThrow();
	}
}
";
		await RunAnalyzerTest(test);
	}

	#endregion

	#region Helpers

	private static async Task RunAnalyzerTest(string source, params DiagnosticResult[] expected)
	{
		var test = new CSharpAnalyzerTest<GetValueOrThrowAnalyzer, DefaultVerifier>
		{
			TestCode = source,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
		};
		test.ExpectedDiagnostics.AddRange(expected);
		await test.RunAsync();
	}

	#endregion
}
