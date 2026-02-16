using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Voyager.Common.Results.Analyzers;

namespace Voyager.Common.Results.Analyzers.Tests;

public class ResultValueAccessedWithoutCheckAnalyzerTests
{
	private const string ResultStubs = @"
using Voyager.Common.Results;

namespace Voyager.Common.Results
{
	public class Result
	{
		public bool IsSuccess { get; }
		public bool IsFailure => !IsSuccess;
	}
	public class Result<T> : Result
	{
		public T Value { get; }
		public static Result<T> Success(T value) => new Result<T>();
	}
}
";

	#region Warning cases

	[Fact]
	public async Task ReportsWarning_WhenValueAccessedWithoutCheck()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		var x = {|#0:result.Value|};
	}
}
";
		await RunAnalyzerTest(test, Expect("result"));
	}

	[Fact]
	public async Task ReportsWarning_WhenValueAccessedOnParameter()
	{
		var test = ResultStubs + @"
class C
{
	void Test(Result<int> result)
	{
		var x = {|#0:result.Value|};
	}
}
";
		await RunAnalyzerTest(test, Expect("result"));
	}

	[Fact]
	public async Task ReportsWarning_WhenValueAccessedInWrongBranch()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		if (result.IsSuccess) { }
		else
		{
			var x = {|#0:result.Value|};
		}
	}
}
";
		await RunAnalyzerTest(test, Expect("result"));
	}

	[Fact]
	public async Task ReportsWarning_WhenValueAccessedInFailureBranch()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		if (result.IsFailure)
		{
			var x = {|#0:result.Value|};
		}
	}
}
";
		await RunAnalyzerTest(test, Expect("result"));
	}

	[Fact]
	public async Task ReportsWarning_WhenValueAccessedOnMethodCallWithoutVariable()
	{
		var test = ResultStubs + @"
class C
{
	Result<int> GetResult() => Result<int>.Success(42);
	void Test()
	{
		var x = {|#0:GetResult().Value|};
	}
}
";
		await RunAnalyzerTest(test,
			new DiagnosticResult(ResultValueAccessedWithoutCheckAnalyzer.DiagnosticId,
					Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
				.WithLocation(0)
				.WithArguments("GetResult()"));
	}

	#endregion

	#region No-warning cases

	[Fact]
	public async Task NoWarning_WhenInsideIsSuccessCheck()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		if (result.IsSuccess)
		{
			var x = result.Value;
		}
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenInsideNegatedIsFailureCheck()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		if (!result.IsFailure)
		{
			var x = result.Value;
		}
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenInsideElseOfIsFailure()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		if (result.IsFailure) { }
		else
		{
			var x = result.Value;
		}
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenTernaryGuardedByIsSuccess()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		var x = result.IsSuccess ? result.Value : 0;
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenGuardedByAndOperator()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		if (result.IsSuccess && result.Value > 0) { }
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenPrecededByEarlyReturn()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		if (result.IsFailure) return;
		var x = result.Value;
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenPrecededByNegatedIsSuccessReturn()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		if (!result.IsSuccess) return;
		var x = result.Value;
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_ForNonResultTypes()
	{
		var test = ResultStubs + @"
class Wrapper<T>
{
	public T Value { get; }
}
class C
{
	void Test()
	{
		var w = new Wrapper<int>();
		var x = w.Value;
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenGuardInParentBlock()
	{
		var test = ResultStubs + @"
class C
{
	int Test()
	{
		var result = Result<int>.Success(42);
		if (result.IsFailure) return -1;
		if (result.Value > 0)
		{
			return result.Value;
		}
		return 0;
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenGuardInParentBlockWithEarlyReturnBlock()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var list = new System.Collections.Generic.List<int>();
		var result = Result<int>.Success(42);
		if (result.IsFailure)
		{
			return;
		}
		if (result.Value > 0)
		{
			list.Add(result.Value);
		}
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenFailureGuardReassignsToSuccess()
	{
		var test = ResultStubs + @"
class C
{
	int Test()
	{
		var result = Result<int>.Success(42);
		if (result.IsFailure)
		{
			result = Result<int>.Success(0);
		}
		var x = result.Value;
		return x;
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenFailureGuardWithNestedReturnAndReassignment()
	{
		var test = ResultStubs + @"
class C
{
	int Test(bool fallback)
	{
		var result = Result<int>.Success(42);
		if (result.IsFailure)
		{
			if (!fallback)
			{
				return -1;
			}
			result = Result<int>.Success(0);
		}
		var x = result.Value;
		return x;
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenFailureGuardWithContinueInLoop()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var items = new[] { 1, 2, 3 };
		var list = new System.Collections.Generic.List<int>();
		foreach (var item in items)
		{
			var result = Result<int>.Success(item);
			if (result.IsFailure)
			{
				continue;
			}
			list.Add(result.Value);
		}
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenFailureGuardWithContinueAndNestedAccess()
	{
		var test = ResultStubs + @"
class C
{
	void Test()
	{
		var items = new[] { 1, 2, 3 };
		var list = new System.Collections.Generic.List<int>();
		foreach (var item in items)
		{
			var result = Result<int>.Success(item);
			if (result.IsFailure)
			{
				continue;
			}
			if (result.Value > 0)
			{
				list.Add(result.Value);
			}
		}
	}
}
";
		await RunAnalyzerTest(test);
	}

	#endregion

	#region Assert guard cases

	private const string ResultStubsNoUsing = @"
namespace Voyager.Common.Results
{
	public class Result
	{
		public bool IsSuccess { get; }
		public bool IsFailure => !IsSuccess;
	}
	public class Result<T> : Result
	{
		public T Value { get; }
		public static Result<T> Success(T value) => new Result<T>();
	}
}
";

	private const string AssertStubs = @"
namespace Xunit
{
	public static class Assert
	{
		public static void True(bool condition) { }
		public static void False(bool condition) { }
	}
}
namespace NUnit.Framework
{
	public static class Assert
	{
		public static void That(bool condition, object constraint) { }
		public static void IsTrue(bool condition) { }
		public static void IsFalse(bool condition) { }
	}
	public static class Is
	{
		public static object True => null;
	}
}
namespace FluentAssertions
{
	public static class AssertionExtensions
	{
		public static BooleanAssertions Should(this bool value) => new();
	}
	public class BooleanAssertions
	{
		public void BeTrue() { }
		public void BeFalse() { }
	}
}
";

	[Fact]
	public async Task NoWarning_WhenPrecededByXunitAssertTrue()
	{
		var test = @"
using Xunit;
using Voyager.Common.Results;
" + AssertStubs + ResultStubsNoUsing + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		Assert.True(result.IsSuccess);
		var x = result.Value;
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenPrecededByXunitAssertFalseIsFailure()
	{
		var test = @"
using Xunit;
using Voyager.Common.Results;
" + AssertStubs + ResultStubsNoUsing + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		Assert.False(result.IsFailure);
		var x = result.Value;
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenPrecededByNunitAssertThat()
	{
		var test = @"
using NUnit.Framework;
using Voyager.Common.Results;
" + AssertStubs + ResultStubsNoUsing + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		Assert.That(result.IsSuccess, Is.True);
		var x = result.Value;
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenPrecededByNunitAssertIsTrue()
	{
		var test = @"
using NUnit.Framework;
using Voyager.Common.Results;
" + AssertStubs + ResultStubsNoUsing + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		Assert.IsTrue(result.IsSuccess);
		var x = result.Value;
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenPrecededByFluentAssertionsBeTrue()
	{
		var test = @"
using FluentAssertions;
using Voyager.Common.Results;
" + AssertStubs + ResultStubsNoUsing + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		result.IsSuccess.Should().BeTrue();
		var x = result.Value;
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task NoWarning_WhenPrecededByFluentAssertionsBeFalseOnIsFailure()
	{
		var test = @"
using FluentAssertions;
using Voyager.Common.Results;
" + AssertStubs + ResultStubsNoUsing + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		result.IsFailure.Should().BeFalse();
		var x = result.Value;
	}
}
";
		await RunAnalyzerTest(test);
	}

	[Fact]
	public async Task ReportsWarning_WhenAssertChecksDifferentVariable()
	{
		var test = @"
using Xunit;
using Voyager.Common.Results;
" + AssertStubs + ResultStubsNoUsing + @"
class C
{
	void Test()
	{
		var result1 = Result<int>.Success(42);
		var result2 = Result<int>.Success(99);
		Assert.True(result1.IsSuccess);
		var x = {|#0:result2.Value|};
	}
}
";
		await RunAnalyzerTest(test, Expect("result2"));
	}

	[Fact]
	public async Task ReportsWarning_WhenAssertFalseOnIsSuccess()
	{
		var test = @"
using Xunit;
using Voyager.Common.Results;
" + AssertStubs + ResultStubsNoUsing + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		Assert.False(result.IsSuccess);
		var x = {|#0:result.Value|};
	}
}
";
		await RunAnalyzerTest(test, Expect("result"));
	}

	#endregion

	#region Code fix tests

	[Fact]
	public async Task CodeFix_ReplacesValueWithGetValueOrThrow()
	{
		var test = ResultStubsWithGetValueOrThrow + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		var x = {|#0:result.Value|};
	}
}
";
		var fixedCode = ResultStubsWithGetValueOrThrow + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		var x = result.GetValueOrThrow();
	}
}
";
		await RunCodeFixTest(test, fixedCode, Expect("result"));
	}

	[Fact]
	public async Task CodeFix_ReplacesChainedValueWithGetValueOrThrow()
	{
		var test = ResultStubsWithGetValueOrThrow + @"
class C
{
	void Test()
	{
		var result = Result<string>.Success(""hello"");
		var len = {|#0:result.Value|}.Length;
	}
}
";
		var fixedCode = ResultStubsWithGetValueOrThrow + @"
class C
{
	void Test()
	{
		var result = Result<string>.Success(""hello"");
		var len = result.GetValueOrThrow().Length;
	}
}
";
		await RunCodeFixTest(test, fixedCode, Expect("result"));
	}

	[Fact]
	public async Task CodeFix_AddsIsSuccessGuard()
	{
		var test = ResultStubsWithGetValueOrThrow + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		var x = {|#0:result.Value|};
	}
}
";
		var fixedCode = ResultStubsWithGetValueOrThrow + @"
class C
{
	void Test()
	{
		var result = Result<int>.Success(42);
		if (result.IsSuccess)
		{
			var x = result.Value;
		}
	}
}
";
		await RunCodeFixTest(test, fixedCode, Expect("result"), codeActionIndex: 1);
	}

	#endregion

	#region Helpers

	private const string ResultStubsWithGetValueOrThrow = @"
using System;
using Voyager.Common.Results;

namespace Voyager.Common.Results
{
	public class Result
	{
		public bool IsSuccess { get; }
		public bool IsFailure => !IsSuccess;
	}
	public class Result<T> : Result
	{
		public T Value { get; }
		public T GetValueOrThrow() => default;
		public static Result<T> Success(T value) => new Result<T>();
	}
}
";

	private static DiagnosticResult Expect(string receiverName) =>
		new DiagnosticResult(ResultValueAccessedWithoutCheckAnalyzer.DiagnosticId,
				Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
			.WithLocation(0)
			.WithArguments(receiverName);

	private static async Task RunAnalyzerTest(string source, params DiagnosticResult[] expected)
	{
		var test = new CSharpAnalyzerTest<ResultValueAccessedWithoutCheckAnalyzer, DefaultVerifier>
		{
			TestCode = source,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
		};
		test.ExpectedDiagnostics.AddRange(expected);
		await test.RunAsync();
	}

	private static async Task RunCodeFixTest(
		string source, string fixedSource, DiagnosticResult expected, int codeActionIndex = 0)
	{
		var test = new CSharpCodeFixTest<ResultValueAccessedWithoutCheckAnalyzer, ResultValueAccessedWithoutCheckCodeFixProvider, DefaultVerifier>
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
