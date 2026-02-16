using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Voyager.Common.Results.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class ResultValueAccessedWithoutCheckAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "VCR0020";

		private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "Result value accessed without success check",
			messageFormat: "Access to 'Value' on '{0}' without checking 'IsSuccess'. The result may be a failure.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description:
				"Accessing Result<T>.Value without first checking IsSuccess may lead to " +
				"using a default/null value when the operation failed. " +
				"Use Match, Switch, or check IsSuccess before accessing Value.",
			helpLinkUri: ResultTypeHelper.HelpLinkBase + "VCR0020.md");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
			ImmutableArray.Create(Rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
		}

		private static void AnalyzePropertyReference(OperationAnalysisContext context)
		{
			var propertyRef = (IPropertyReferenceOperation)context.Operation;

			if (propertyRef.Property.Name != "Value")
				return;

			// Check that the property is declared on Result<T>
			var containingType = propertyRef.Property.ContainingType;
			if (containingType == null || !containingType.IsGenericType ||
				containingType.Name != "Result" ||
				containingType.ContainingNamespace?.ToDisplayString() != ResultTypeHelper.ResultNamespace)
				return;

			// Get the receiver symbol to match against guards
			var receiverSymbol = GetReceiverSymbol(propertyRef);

			// If receiver is not a trackable symbol (e.g., method call like GetUser().Value), always warn
			if (receiverSymbol == null)
			{
				ReportDiagnostic(context, propertyRef);
				return;
			}

			// Check if inside a guarding if/ternary block
			if (IsInsideSuccessGuard(propertyRef, receiverSymbol))
				return;

			// Check for early-return guard pattern: if (result.IsFailure) return;
			if (HasPrecedingGuard(propertyRef, receiverSymbol))
				return;

			ReportDiagnostic(context, propertyRef);
		}

		private static void ReportDiagnostic(
			OperationAnalysisContext context, IPropertyReferenceOperation propertyRef)
		{
			var receiverName = propertyRef.Instance?.Syntax.ToString() ?? "result";
			context.ReportDiagnostic(
				Diagnostic.Create(Rule, propertyRef.Syntax.GetLocation(), receiverName));
		}

		private static ISymbol? GetReceiverSymbol(IPropertyReferenceOperation propertyRef)
		{
			return propertyRef.Instance switch
			{
				ILocalReferenceOperation local => local.Local,
				IParameterReferenceOperation param => param.Parameter,
				IFieldReferenceOperation field => field.Field,
				_ => null
			};
		}

		private static bool IsInsideSuccessGuard(IOperation valueAccess, ISymbol receiverSymbol)
		{
			var current = valueAccess;
			while (current.Parent != null)
			{
				var parent = current.Parent;

				// Check if/ternary: if (result.IsSuccess) { ...Value... }
				if (parent is IConditionalOperation conditional)
				{
					if (IsSuccessCheckOnSymbol(conditional.Condition, receiverSymbol,
							out var checksForSuccess))
					{
						if (ReferenceEquals(current, conditional.WhenTrue) && checksForSuccess)
							return true;
						if (ReferenceEquals(current, conditional.WhenFalse) && !checksForSuccess)
							return true;
					}
				}

				// Check && short-circuit: result.IsSuccess && result.Value...
				if (parent is IBinaryOperation binary &&
					binary.OperatorKind == BinaryOperatorKind.ConditionalAnd)
				{
					if (ReferenceEquals(current, binary.RightOperand) &&
						IsSuccessCheckOnSymbol(binary.LeftOperand, receiverSymbol,
							out var checksForSuccess) &&
						checksForSuccess)
					{
						return true;
					}
				}

				current = parent;
			}

			return false;
		}

		private static bool IsSuccessCheckOnSymbol(
			IOperation condition, ISymbol symbol, out bool checksForSuccess)
		{
			checksForSuccess = false;

			if (condition is IPropertyReferenceOperation propRef)
			{
				if (propRef.Property.Name == "IsSuccess" && ReceiverMatchesSymbol(propRef, symbol))
				{
					checksForSuccess = true;
					return true;
				}

				if (propRef.Property.Name == "IsFailure" && ReceiverMatchesSymbol(propRef, symbol))
				{
					checksForSuccess = false;
					return true;
				}
			}

			// !result.IsSuccess or !result.IsFailure
			if (condition is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } unary &&
				IsSuccessCheckOnSymbol(unary.Operand, symbol, out var inner))
			{
				checksForSuccess = !inner;
				return true;
			}

			return false;
		}

		private static bool ReceiverMatchesSymbol(IPropertyReferenceOperation propRef, ISymbol symbol)
		{
			var receiverSymbol = propRef.Instance switch
			{
				ILocalReferenceOperation local => (ISymbol)local.Local,
				IParameterReferenceOperation param => param.Parameter,
				IFieldReferenceOperation field => field.Field,
				_ => null
			};

			return receiverSymbol != null &&
				   SymbolEqualityComparer.Default.Equals(receiverSymbol, symbol);
		}

		private static bool HasPrecedingGuard(IPropertyReferenceOperation valueAccess, ISymbol receiverSymbol)
		{
			// Traverse current and parent blocks looking for a failure guard
			var currentOp = (IOperation)valueAccess;
			while (true)
			{
				var statement = FindEnclosingStatement(currentOp);
				if (statement == null)
					return false;

				var block = statement.Parent as IBlockOperation;
				if (block == null)
					return false;

				int statementIndex = -1;
				for (int i = 0; i < block.Operations.Length; i++)
				{
					if (ReferenceEquals(block.Operations[i], statement))
					{
						statementIndex = i;
						break;
					}
				}

				if (statementIndex > 0)
				{
					// Scan backwards for guard: if (result.IsFailure) return/throw/reassign;
					for (int i = statementIndex - 1; i >= 0; i--)
					{
						if (block.Operations[i] is IConditionalOperation guard &&
							guard.WhenFalse == null &&
							GuardEnsuresSuccess(guard.WhenTrue, receiverSymbol) &&
							IsFailureGuardOnSymbol(guard.Condition, receiverSymbol))
						{
							return true;
						}
					}
				}

				// Continue searching in parent blocks
				currentOp = block;
			}
		}

		private static bool IsFailureGuardOnSymbol(IOperation condition, ISymbol symbol)
		{
			return IsSuccessCheckOnSymbol(condition, symbol, out var checksForSuccess) &&
				   !checksForSuccess;
		}

		private static IOperation? FindEnclosingStatement(IOperation operation)
		{
			var current = operation;
			while (current != null)
			{
				if (current.Parent is IBlockOperation)
					return current;
				current = current.Parent;
			}

			return null;
		}

		/// <summary>
		/// Checks if the guard body guarantees the receiver is success after execution.
		/// This is true when:
		/// 1. The body unconditionally exits (return, throw, continue, break), OR
		/// 2. The last statement reassigns the receiver to Result.Success(...)
		/// </summary>
		private static bool GuardEnsuresSuccess(IOperation? body, ISymbol receiverSymbol)
		{
			if (body == null)
				return false;

			// Simple case: direct exit statement
			if (IsExitStatement(body))
				return true;

			if (body is IBlockOperation block)
			{
				// Case 1: any direct child is unconditional exit
				foreach (var op in block.Operations)
				{
					if (IsExitStatement(op))
						return true;
				}

				// Case 2: last statement reassigns the variable to Result<T>.Success(...)
				if (block.Operations.Length > 0)
				{
					var lastOp = block.Operations[block.Operations.Length - 1];
					if (IsSuccessReassignment(lastOp, receiverSymbol))
						return true;
				}
			}

			return false;
		}

		private static bool IsExitStatement(IOperation operation) =>
			operation is IReturnOperation or IThrowOperation or IBranchOperation;

		/// <summary>
		/// Checks if the operation is an assignment of the form:
		/// receiverSymbol = Result&lt;T&gt;.Success(...);
		/// </summary>
		private static bool IsSuccessReassignment(IOperation operation, ISymbol receiverSymbol)
		{
			if (operation is not IExpressionStatementOperation exprStatement)
				return false;

			if (exprStatement.Operation is not ISimpleAssignmentOperation assignment)
				return false;

			// Check left side matches the receiver symbol
			var targetSymbol = assignment.Target switch
			{
				ILocalReferenceOperation local => (ISymbol)local.Local,
				IParameterReferenceOperation param => param.Parameter,
				IFieldReferenceOperation field => field.Field,
				_ => null
			};

			if (targetSymbol == null ||
				!SymbolEqualityComparer.Default.Equals(targetSymbol, receiverSymbol))
				return false;

			// Check right side is Result<T>.Success(...)
			if (assignment.Value is IInvocationOperation invocation)
			{
				return invocation.TargetMethod.Name == "Success" &&
					   ResultTypeHelper.IsResultType(invocation.TargetMethod.ContainingType);
			}

			return false;
		}

	}
}
