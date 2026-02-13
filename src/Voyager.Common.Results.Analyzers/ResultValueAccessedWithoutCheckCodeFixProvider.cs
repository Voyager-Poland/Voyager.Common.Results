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
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ResultValueAccessedWithoutCheckCodeFixProvider))]
	[Shared]
	public sealed class ResultValueAccessedWithoutCheckCodeFixProvider : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(ResultValueAccessedWithoutCheckAnalyzer.DiagnosticId);

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

			// Find the .Value member access: result.Value
			var memberAccess = node as MemberAccessExpressionSyntax
				?? node.Parent as MemberAccessExpressionSyntax;

			if (memberAccess == null || memberAccess.Name.Identifier.Text != "Value")
				return;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Use GetValueOrThrow()",
					createChangedDocument: ct => ReplaceWithGetValueOrThrowAsync(context.Document, memberAccess, ct),
					equivalenceKey: "UseGetValueOrThrow"),
				diagnostic);

			// Find the receiver name for the IsSuccess guard
			var receiverName = memberAccess.Expression is IdentifierNameSyntax identifier
				? identifier.Identifier.Text
				: null;

			if (receiverName != null)
			{
				// Find the containing statement to wrap with if guard
				var containingStatement = memberAccess.FirstAncestorOrSelf<StatementSyntax>();
				if (containingStatement != null)
				{
					context.RegisterCodeFix(
						CodeAction.Create(
							title: "Add IsSuccess guard",
							createChangedDocument: ct => AddIsSuccessGuardAsync(
								context.Document, containingStatement, receiverName, ct),
							equivalenceKey: "AddIsSuccessGuard"),
						diagnostic);
				}
			}
		}

		private static async Task<Document> ReplaceWithGetValueOrThrowAsync(
			Document document, MemberAccessExpressionSyntax memberAccess, CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document;

			// Build: result.GetValueOrThrow()
			var invocation = SyntaxFactory.InvocationExpression(
				SyntaxFactory.MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					memberAccess.Expression,
					SyntaxFactory.IdentifierName("GetValueOrThrow")))
				.WithTriviaFrom(memberAccess);

			var newRoot = root.ReplaceNode(memberAccess, invocation);
			return document.WithSyntaxRoot(newRoot);
		}

		private static async Task<Document> AddIsSuccessGuardAsync(
			Document document, StatementSyntax containingStatement,
			string receiverName, CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document;

			// Extract indentation string from leading trivia
			var indent = "";
			foreach (var trivia in containingStatement.GetLeadingTrivia())
			{
				if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
					indent = trivia.ToString();
			}

			// Detect line ending from existing trivia (Linux CI uses \n, Windows uses \r\n)
			var eol = "\n";
			foreach (var trivia in containingStatement.GetTrailingTrivia())
			{
				if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
				{
					eol = trivia.ToString();
					break;
				}
			}

			var innerIndent = indent + "\t";

			// Build: if (receiver.IsSuccess)\r\n{indent}{\r\n{innerIndent}<statement>\r\n{indent}}
			var ifText = $"if ({receiverName}.IsSuccess)" + eol +
				$"{indent}{{" + eol +
				$"{innerIndent}{containingStatement.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()}" + eol +
				$"{indent}}}";

			var ifStatement = (IfStatementSyntax)SyntaxFactory.ParseStatement(ifText)
				.WithLeadingTrivia(containingStatement.GetLeadingTrivia())
				.WithTrailingTrivia(containingStatement.GetTrailingTrivia());

			var newRoot = root.ReplaceNode(containingStatement, ifStatement);
			return document.WithSyntaxRoot(newRoot);
		}
	}
}
