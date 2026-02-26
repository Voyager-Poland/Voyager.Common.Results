using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Voyager.Common.Results.Analyzers
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullableSuccessCodeFixProvider))]
	[Shared]
	public sealed class NullableSuccessCodeFixProvider : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(NullableSuccessAnalyzer.DiagnosticId);

		public override FixAllProvider GetFixAllProvider() =>
			WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null)
				return;

			var diagnostic = context.Diagnostics[0];
			var diagnosticSpan = diagnostic.Location.SourceSpan;
			var node = root.FindNode(diagnosticSpan);

			// Find the Success(...) invocation
			var invocation = node as InvocationExpressionSyntax
				?? node.Parent as InvocationExpressionSyntax;

			if (invocation?.Expression is not MemberAccessExpressionSyntax memberAccess)
				return;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Replace Success(null) with Failure(Error.NotFoundError(...))",
					createChangedDocument: ct => ReplaceSuccessNullWithFailureAsync(
						context.Document, invocation, memberAccess, ct),
					equivalenceKey: "ReplaceSuccessNullWithFailure"),
				diagnostic);
		}

		private static async Task<Document> ReplaceSuccessNullWithFailureAsync(
			Document document, InvocationExpressionSyntax invocation,
			MemberAccessExpressionSyntax memberAccess, CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document;

			// Replace .Success with .Failure
			var newMemberAccess = memberAccess.WithName(
				SyntaxFactory.IdentifierName("Failure")
					.WithTriviaFrom(memberAccess.Name));

			// Build: Error.NotFoundError("TODO: provide meaningful error")
			var newArgument = SyntaxFactory.Argument(
				SyntaxFactory.InvocationExpression(
					SyntaxFactory.MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression,
						SyntaxFactory.IdentifierName("Error"),
						SyntaxFactory.IdentifierName("NotFoundError")))
				.WithArgumentList(
					SyntaxFactory.ArgumentList(
						SyntaxFactory.SingletonSeparatedList(
							SyntaxFactory.Argument(
								SyntaxFactory.LiteralExpression(
									SyntaxKind.StringLiteralExpression,
									SyntaxFactory.Literal("TODO: provide meaningful error")))))));

			var newArgumentList = SyntaxFactory.ArgumentList(
				SyntaxFactory.SingletonSeparatedList(newArgument));

			var newInvocation = invocation
				.WithExpression(newMemberAccess)
				.WithArgumentList(newArgumentList.WithTriviaFrom(invocation.ArgumentList));

			var newRoot = root.ReplaceNode(invocation, newInvocation);
			return document.WithSyntaxRoot(newRoot);
		}
	}
}
