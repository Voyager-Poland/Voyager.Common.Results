using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Voyager.Common.Results.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class FailureWithErrorNoneAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "VCR0050";

		private const string ResultNamespace = "Voyager.Common.Results";

		private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "Failure created with Error.None",
			messageFormat: "Creating a Failure result with Error.None is a bug. A failure must have a meaningful error.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true,
			description:
				"Result.Failure(Error.None) creates a failure result without an actual error, " +
				"which is a semantic contradiction. Use a specific error or Result.Success() instead.");

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

			if (method.Name != "Failure")
				return;

			if (!IsResultType(method.ContainingType))
				return;

			if (invocation.Arguments.Length != 1)
				return;

			var argValue = invocation.Arguments[0].Value;

			// Check if the argument is Error.None
			if (argValue is IFieldReferenceOperation fieldRef &&
				fieldRef.Field.Name == "None" &&
				fieldRef.Field.ContainingType?.Name == "Error" &&
				fieldRef.Field.ContainingType?.ContainingNamespace?.ToDisplayString() == ResultNamespace)
			{
				context.ReportDiagnostic(
					Diagnostic.Create(Rule, invocation.Syntax.GetLocation()));
			}
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
