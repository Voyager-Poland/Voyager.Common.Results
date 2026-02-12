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

		private const string ResultNamespace = "Voyager.Common.Results";

		private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "Nested Result<Result<T>> detected",
			messageFormat: "'{0}' produces nested Result<Result<{1}>>. Use 'Bind' instead of 'Map' to flatten the result.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description:
				"Using Map with a function that returns Result produces a nested Result<Result<T>>. " +
				"Use Bind (or BindAsync) instead to flatten the result.");

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
			if (!IsResultMethod(method))
				return;

			var returnType = (ITypeSymbol)method.ReturnType;

			// Unwrap Task<T> / ValueTask<T>
			var unwrapped = UnwrapTaskType(returnType);
			if (unwrapped != null)
				returnType = unwrapped;

			// Check for Result<Result<T>>
			if (returnType is INamedTypeSymbol outerResult &&
				outerResult.IsGenericType &&
				outerResult.Name == "Result" &&
				outerResult.ContainingNamespace?.ToDisplayString() == ResultNamespace &&
				outerResult.TypeArguments.Length == 1 &&
				IsResultType(outerResult.TypeArguments[0]))
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

		private static bool IsResultMethod(IMethodSymbol method)
		{
			// Instance method on Result/Result<T>
			if (IsResultType(method.ContainingType))
				return true;

			// Extension method in Voyager.Common.Results namespace
			if (method.IsExtensionMethod &&
				method.ContainingNamespace?.ToDisplayString()?.StartsWith(ResultNamespace) == true)
				return true;

			return false;
		}

		private static ITypeSymbol? UnwrapTaskType(ITypeSymbol type)
		{
			if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
			{
				var originalDef = namedType.OriginalDefinition.ToDisplayString();
				if (originalDef == "System.Threading.Tasks.Task<TResult>" ||
					originalDef == "System.Threading.Tasks.ValueTask<TResult>")
				{
					return namedType.TypeArguments[0];
				}
			}

			return null;
		}

		private static bool IsResultType(ITypeSymbol? type)
		{
			var current = type;
			while (current != null)
			{
				if (current.Name == "Result" &&
					current.ContainingNamespace?.ToDisplayString() == ResultNamespace)
					return true;
				current = current.BaseType;
			}

			return false;
		}
	}
}
