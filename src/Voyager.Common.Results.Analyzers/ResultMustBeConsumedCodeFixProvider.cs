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
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ResultMustBeConsumedCodeFixProvider))]
	[Shared]
	public sealed class ResultMustBeConsumedCodeFixProvider : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(ResultMustBeConsumedAnalyzer.DiagnosticId);

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

			var expressionStatement = node as ExpressionStatementSyntax
				?? node.Parent as ExpressionStatementSyntax;

			if (expressionStatement == null)
				return;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Discard result",
					createChangedDocument: ct => AddDiscardAsync(context.Document, expressionStatement, ct),
					equivalenceKey: "DiscardResult"),
				diagnostic);

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Assign to variable",
					createChangedDocument: ct => AssignToVariableAsync(context.Document, expressionStatement, ct),
					equivalenceKey: "AssignToVariable"),
				diagnostic);
		}

		private static async Task<Document> AddDiscardAsync(
			Document document, ExpressionStatementSyntax expressionStatement, CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document;

			var expression = expressionStatement.Expression;

			var discardAssignment = SyntaxFactory.ExpressionStatement(
				SyntaxFactory.AssignmentExpression(
					SyntaxKind.SimpleAssignmentExpression,
					SyntaxFactory.IdentifierName(
						SyntaxFactory.Identifier(
							SyntaxFactory.TriviaList(),
							"_",
							SyntaxFactory.TriviaList(SyntaxFactory.Space))),
					expression.WithoutLeadingTrivia()),
				expressionStatement.SemicolonToken)
				.WithLeadingTrivia(expressionStatement.GetLeadingTrivia());

			var newRoot = root.ReplaceNode(expressionStatement, discardAssignment);
			return document.WithSyntaxRoot(newRoot);
		}

		private static async Task<Document> AssignToVariableAsync(
			Document document, ExpressionStatementSyntax expressionStatement, CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document;

			var expression = expressionStatement.Expression;

			var variableDeclaration = SyntaxFactory.LocalDeclarationStatement(
				SyntaxFactory.VariableDeclaration(
					SyntaxFactory.IdentifierName(
						SyntaxFactory.Identifier(
							SyntaxFactory.TriviaList(),
							"var",
							SyntaxFactory.TriviaList(SyntaxFactory.Space))),
					SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.VariableDeclarator(
							SyntaxFactory.Identifier(
								SyntaxFactory.TriviaList(),
								"result",
								SyntaxFactory.TriviaList(SyntaxFactory.Space)))
						.WithInitializer(
							SyntaxFactory.EqualsValueClause(
								expression.WithoutLeadingTrivia())))))
				.WithSemicolonToken(expressionStatement.SemicolonToken)
				.WithLeadingTrivia(expressionStatement.GetLeadingTrivia());

			var newRoot = root.ReplaceNode(expressionStatement, variableDeclaration);
			return document.WithSyntaxRoot(newRoot);
		}
	}
}
