using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Voyager.Common.Results.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class GetValueOrThrowAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "VCR0040";

		private const string ResultNamespace = "Voyager.Common.Results";

		private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "GetValueOrThrow defeats Result pattern",
			messageFormat: "Consider using 'Match', 'Bind', or 'Map' instead of 'GetValueOrThrow' to preserve railway-oriented error handling",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Info,
			isEnabledByDefault: true,
			description:
				"GetValueOrThrow converts a Result failure back into an exception, " +
				"which defeats the purpose of using the Result pattern. " +
				"Prefer Match, Bind, or Map for composable error handling.");

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

			if (method.Name != "GetValueOrThrow")
				return;

			if (!IsResultType(method.ContainingType))
				return;

			context.ReportDiagnostic(
				Diagnostic.Create(Rule, invocation.Syntax.GetLocation()));
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
