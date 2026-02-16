using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Voyager.Common.Results.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class PreferMatchSwitchAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "VCR0060";

		private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "Consider using Match or Switch instead of if/else on IsSuccess",
			messageFormat: "Consider using 'Match' or 'Switch' instead of branching on '{0}'. This ensures both success and failure paths are handled.",
			category: "Style",
			defaultSeverity: DiagnosticSeverity.Info,
			isEnabledByDefault: false,
			description:
				"When branching on Result.IsSuccess/IsFailure with both if and else, " +
				"consider using Match or Switch for exhaustive handling of both paths.",
			helpLinkUri: ResultTypeHelper.HelpLinkBase + "VCR0060.md");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
			ImmutableArray.Create(Rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterOperationAction(AnalyzeConditional, OperationKind.Conditional);
		}

		private static void AnalyzeConditional(OperationAnalysisContext context)
		{
			var conditional = (IConditionalOperation)context.Operation;

			// Only flag if-else statements, not ternary expressions
			if (conditional.Syntax is not IfStatementSyntax)
				return;

			// Both branches must exist
			if (conditional.WhenFalse == null)
				return;

			var propertyName = GetResultCheckProperty(conditional.Condition);
			if (propertyName == null)
				return;

			context.ReportDiagnostic(
				Diagnostic.Create(Rule, conditional.Syntax.GetLocation(), propertyName));
		}

		private static string? GetResultCheckProperty(IOperation condition)
		{
			// result.IsSuccess or result.IsFailure
			if (condition is IPropertyReferenceOperation propRef &&
				(propRef.Property.Name == "IsSuccess" || propRef.Property.Name == "IsFailure") &&
				ResultTypeHelper.IsResultType(propRef.Property.ContainingType))
			{
				return propRef.Property.Name;
			}

			// !result.IsSuccess or !result.IsFailure
			if (condition is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } unary)
				return GetResultCheckProperty(unary.Operand);

			return null;
		}

	}
}
