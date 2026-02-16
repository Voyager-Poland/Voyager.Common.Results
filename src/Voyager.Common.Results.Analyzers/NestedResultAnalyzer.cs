using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Voyager.Common.Results.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class NestedResultAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "VCR0030";

		private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "Nested Result<Result<T>> detected",
			messageFormat: "'{0}' produces nested Result<Result<{1}>>. Use 'Bind' instead of 'Map' to flatten the result.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description:
				"Using Map with a function that returns Result produces a nested Result<Result<T>>. " +
				"Use Bind (or BindAsync) instead to flatten the result.",
			helpLinkUri: ResultTypeHelper.HelpLinkBase + "VCR0030.md");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
			ImmutableArray.Create(Rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
		}

		private static void AnalyzeInvocation(OperationAnalysisContext context)
		{
			var invocation = (IInvocationOperation)context.Operation;
			var method = invocation.TargetMethod;

			if (method.Name != "Map" && method.Name != "MapAsync")
				return;

			// Verify the method is on a Result type or is an extension method in the Result namespace
			if (!ResultTypeHelper.IsResultMethod(method))
				return;

			var returnType = (ITypeSymbol)method.ReturnType;

			// Unwrap Task<T> / ValueTask<T>
			var unwrapped = ResultTypeHelper.UnwrapTaskType(returnType);
			if (unwrapped != null)
				returnType = unwrapped;

			// Check for Result<Result<T>>
			if (returnType is INamedTypeSymbol outerResult &&
				outerResult.IsGenericType &&
				outerResult.Name == "Result" &&
				outerResult.ContainingNamespace?.ToDisplayString() == ResultTypeHelper.ResultNamespace &&
				outerResult.TypeArguments.Length == 1 &&
				ResultTypeHelper.IsResultType(outerResult.TypeArguments[0]))
			{
				var innerTypeName = GetInnerTypeName(outerResult.TypeArguments[0]);
				context.ReportDiagnostic(
					Diagnostic.Create(Rule, invocation.Syntax.GetLocation(), method.Name, innerTypeName));
			}
		}

		private static string GetInnerTypeName(ITypeSymbol type)
		{
			if (type is INamedTypeSymbol namedType && namedType.IsGenericType &&
				namedType.TypeArguments.Length == 1)
			{
				return namedType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
			}

			return type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
		}

	}
}
