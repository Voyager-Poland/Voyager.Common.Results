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
				"Prefer Match, Bind, or Map for composable error handling.",
			helpLinkUri: ResultTypeHelper.HelpLinkBase + "VCR0040.md");

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

			if (!ResultTypeHelper.IsResultType(method.ContainingType))
				return;

			context.ReportDiagnostic(
				Diagnostic.Create(Rule, invocation.Syntax.GetLocation()));
		}

	}
}
