using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Voyager.Common.Results.Analyzers;

namespace Voyager.Common.Results.Analyzers.Tests;

public class NestedResultAnalyzerTests
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
		public Result<TOut> Map<TOut>(Func<T, TOut> mapper) => default;
		public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder) => default;
		public static Result<T> Success(T value) => default;
	}
}
";

	#region Warning cases

	[Fact]
	public async Task ReportsWarning_WhenMapProducesNestedResult()
	{
		var test = ResultStubs + @"
class C
{
	Result<string> GetName(int id) => Result<string>.Success(""test"");
	void Test()
	{
		var result = Result<int>.Success(1);
		var nested = {|#0:result.Map(x => GetName(x))|};
	}
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(NestedResultAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
				.WithLocation(0)
				.WithArguments("Map", "string"));
	}

	#endregion

	#region No-warning cases

	[Fact]
	public async Task NoWarning_WhenMapProducesFlatResult()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(1);
		var mapped = result.Map(x => x.ToString());
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
	Result<string> GetName(int id) => Result<string>.Success(""test"");
	void Test()
	{
		var result = Result<int>.Success(1);
		var bound = result.Bind(x => GetName(x));
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_ForNonResultMap()
	{
		var test = @"
using System.Linq;
using System.Collections.Generic;
class C
{
	void Test()
	{
		var list = new List<int> { 1, 2, 3 };
		var mapped = list.Select(x => x * 2);
	}
}
";
		await RunAnalyzerTest(test);
	}

	#endregion

	#region Code fix tests

	[Fact]
	public async Task CodeFix_ReplacesMapWithBind()
	{
		var test = ResultStubs + @"
class C
{
	Result<string> GetName(int id) => Result<string>.Success(""test"");
	void Test()
	{
		var result = Result<int>.Success(1);
		var nested = {|#0:result.Map(x => GetName(x))|};
	}
}
";
		var fixedCode = ResultStubs + @"
class C
{
	Result<string> GetName(int id) => Result<string>.Success(""test"");
	void Test()
	{
		var result = Result<int>.Success(1);
		var nested = result.Bind(x => GetName(x));
	}
}
";
		await RunCodeFixTest(test, fixedCode,
			new DiagnosticResult(NestedResultAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
				.WithLocation(0)
				.WithArguments("Map", "string"));
	}

	#endregion

	#region Helpers

	private static async Task RunAnalyzerTest(string source, params DiagnosticResult[] expected)
	{
		var test = new CSharpAnalyzerTest<NestedResultAnalyzer, DefaultVerifier>
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
		var test = new CSharpCodeFixTest<NestedResultAnalyzer, NestedResultCodeFixProvider, DefaultVerifier>
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
