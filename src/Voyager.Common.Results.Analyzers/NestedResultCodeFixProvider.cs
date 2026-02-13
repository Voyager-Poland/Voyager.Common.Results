using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Voyager.Common.Results.Analyzers
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NestedResultCodeFixProvider))]
	[Shared]
	public sealed class NestedResultCodeFixProvider : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(NestedResultAnalyzer.DiagnosticId);

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

			// Find the invocation: result.Map(...) or result.MapAsync(...)
			var invocation = node as InvocationExpressionSyntax
				?? node.Parent as InvocationExpressionSyntax;

			if (invocation?.Expression is not MemberAccessExpressionSyntax memberAccess)
				return;

			var methodName = memberAccess.Name.Identifier.Text;
			string replacementName;

			if (methodName == "Map")
				replacementName = "Bind";
			else if (methodName == "MapAsync")
				replacementName = "BindAsync";
			else
				return;

			context.RegisterCodeFix(
				CodeAction.Create(
					title: $"Replace '{methodName}' with '{replacementName}'",
					createChangedDocument: ct => ReplaceMapWithBindAsync(
						context.Document, memberAccess, replacementName, ct),
					equivalenceKey: "ReplaceMapWithBind"),
				diagnostic);
		}

		private static async Task<Document> ReplaceMapWithBindAsync(
			Document document, MemberAccessExpressionSyntax memberAccess,
			string replacementName, CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (root == null)
				return document;

			var newMemberAccess = memberAccess.WithName(
				memberAccess.Name.WithIdentifier(
					Microsoft.CodeAnalysis.CSharp.SyntaxFactory.Identifier(replacementName)
						.WithTriviaFrom(memberAccess.Name.Identifier)));

			var newRoot = root.ReplaceNode(memberAccess, newMemberAccess);
			return document.WithSyntaxRoot(newRoot);
		}
	}
}
