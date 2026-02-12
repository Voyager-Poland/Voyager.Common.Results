using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Voyager.Common.Results.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class ResultMustBeConsumedAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "VCR0010";

		private const string ResultTypeName = "Result";
		private const string ResultNamespace = "Voyager.Common.Results";

		private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "Result must be consumed",
			messageFormat: "Result of '{0}' must be checked. Ignoring a Result silently discards potential errors.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description:
				"Methods returning Result or Result<T> must have their return value checked. " +
				"Ignoring the result means errors are silently lost.");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
			ImmutableArray.Create(Rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterOperationAction(AnalyzeExpressionStatement, OperationKind.ExpressionStatement);
		}

		private static void AnalyzeExpressionStatement(OperationAnalysisContext context)
		{
			var expressionStatement = (IExpressionStatementOperation)context.Operation;
			var operation = expressionStatement.Operation;

			// Assignments (e.g. _ = M()) mean the result is consumed
			if (operation is ISimpleAssignmentOperation)
				return;

			// Handle await — the Task is consumed but the inner Result may not be
			if (operation is IAwaitOperation awaitOp)
				operation = awaitOp.Operation;

			var returnType = GetReturnType(operation);
			if (returnType == null)
				return;

			// Unwrap Task<T> → T (for non-awaited scenarios)
			var unwrapped = UnwrapTaskType(returnType);
			if (unwrapped != null)
				returnType = unwrapped;

			if (!IsResultType(returnType))
				return;

			var methodName = GetMethodName(operation, returnType);

			context.ReportDiagnostic(
				Diagnostic.Create(Rule, expressionStatement.Operation.Syntax.GetLocation(), methodName));
		}

		private static ITypeSymbol? GetReturnType(IOperation operation)
		{
			switch (operation)
			{
				case IInvocationOperation invocation:
					return invocation.TargetMethod.ReturnType;
				case IPropertyReferenceOperation propertyRef:
					return propertyRef.Type;
				case IConversionOperation conversion:
					return GetReturnType(conversion.Operand);
				default:
					return operation.Type;
			}
		}

		private static string GetMethodName(IOperation operation, ITypeSymbol returnType)
		{
			switch (operation)
			{
				case IInvocationOperation invocation:
					return invocation.TargetMethod.Name;
				case IPropertyReferenceOperation propertyRef:
					return propertyRef.Property.Name;
				default:
					return returnType.Name;
			}
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
			if (type == null)
				return false;

			// Check the type and all its base types
			var current = type;
			while (current != null)
			{
				if (current.Name == ResultTypeName &&
					current.ContainingNamespace?.ToDisplayString() == ResultNamespace)
					return true;

				current = current.BaseType;
			}

			return false;
		}
	}
}
