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
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FailureWithErrorNoneCodeFixProvider))]
	[Shared]
	public sealed class FailureWithErrorNoneCodeFixProvider : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(FailureWithErrorNoneAnalyzer.DiagnosticId);

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

			// Find the Failure(...) invocation
			var invocation = node as InvocationExpressionSyntax
				?? node.Parent as InvocationExpressionSyntax;

			if (invocation == null)
				return;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Replace Error.None with Error.UnexpectedError(...)",
					createChangedDocument: ct => ReplaceErrorNoneAsync(
						context.Document, invocation, ct),
					equivalenceKey: "ReplaceErrorNoneWithUnexpectedError"),
				diagnostic);
		}

		private static async Task<Document> ReplaceErrorNoneAsync(
			Document document, InvocationExpressionSyntax invocation,
			CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document;

			// Build: Error.UnexpectedError("TODO: provide error message")
			var newArgument = SyntaxFactory.Argument(
				SyntaxFactory.InvocationExpression(
					SyntaxFactory.MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression,
						SyntaxFactory.IdentifierName("Error"),
						SyntaxFactory.IdentifierName("UnexpectedError")))
				.WithArgumentList(
					SyntaxFactory.ArgumentList(
						SyntaxFactory.SingletonSeparatedList(
							SyntaxFactory.Argument(
								SyntaxFactory.LiteralExpression(
									SyntaxKind.StringLiteralExpression,
									SyntaxFactory.Literal("TODO: provide error message")))))));

			var newArgumentList = SyntaxFactory.ArgumentList(
				SyntaxFactory.SingletonSeparatedList(newArgument));

			var newInvocation = invocation
				.WithArgumentList(newArgumentList.WithTriviaFrom(invocation.ArgumentList));

			var newRoot = root.ReplaceNode(invocation, newInvocation);
			return document.WithSyntaxRoot(newRoot);
		}
	}
}
