using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Voyager.Common.Results.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class NullableSuccessAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "VCR0070";

		private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "Success should not receive null",
			messageFormat: "Result<{0}>.Success() should not receive null. A successful result must carry a value. Use Failure() or remove nullable from the type parameter.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description:
				"Passing null to Result<T>.Success() defeats the purpose of the Result pattern. " +
				"A successful result should always carry a meaningful value. " +
				"Use Result<T>.Failure() for absent values or remove the nullable annotation from the type parameter.",
			helpLinkUri: ResultTypeHelper.HelpLinkBase + "VCR0070.md");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
			ImmutableArray.Create(Rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
			context.RegisterOperationAction(AnalyzeConversion, OperationKind.Conversion);
		}

		private static void AnalyzeInvocation(OperationAnalysisContext context)
		{
			var invocation = (IInvocationOperation)context.Operation;
			var method = invocation.TargetMethod;

			if (method.Name != "Success")
				return;

			if (!ResultTypeHelper.IsResultType(method.ContainingType))
				return;

			// Only generic Result<T>, not void Result
			if (method.ContainingType is not INamedTypeSymbol { IsGenericType: true } containingType)
				return;

			if (invocation.Arguments.Length != 1)
				return;

			var argValue = invocation.Arguments[0].Value;

			if (!IsNullOrDefault(argValue))
				return;

			var typeArg = containingType.TypeArguments[0]
				.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

			context.ReportDiagnostic(
				Diagnostic.Create(Rule, invocation.Syntax.GetLocation(), typeArg));
		}

		private static void AnalyzeConversion(OperationAnalysisContext context)
		{
			var conversion = (IConversionOperation)context.Operation;

			// Only implicit user-defined conversions (implicit operator Result<T>(T value))
			if (!conversion.IsImplicit)
				return;

			// Must be a user-defined conversion with an operator method (T -> Result<T>)
			if (conversion.OperatorMethod == null)
				return;

			// The operator parameter must be the T type (not Error -> Result<T>)
			if (conversion.OperatorMethod.Parameters.Length != 1)
				return;

			var paramType = conversion.OperatorMethod.Parameters[0].Type;
			if (ResultTypeHelper.IsResultType(paramType as INamedTypeSymbol))
				return;

			// Check that param is not Error type
			if (paramType.Name == "Error")
				return;

			// Target must be Result<T>
			if (conversion.Type is not INamedTypeSymbol { IsGenericType: true } resultType)
				return;

			if (!ResultTypeHelper.IsResultType(resultType))
				return;

			// Operand must be null/default
			if (!IsNullOrDefault(conversion.Operand))
				return;

			var typeArg = resultType.TypeArguments[0]
				.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

			context.ReportDiagnostic(
				Diagnostic.Create(Rule, conversion.Syntax.GetLocation(), typeArg));
		}

		private static bool IsNullOrDefault(IOperation operation)
		{
			// Unwrap conversions (e.g. (Order?)null, implicit casts, null! forgiving operator)
			while (operation is IConversionOperation conversion)
				operation = conversion.Operand;

			return operation.ConstantValue.HasValue && operation.ConstantValue.Value is null;
		}
	}
}
