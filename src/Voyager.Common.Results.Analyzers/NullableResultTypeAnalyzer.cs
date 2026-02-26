using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Voyager.Common.Results.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class NullableResultTypeAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "VCR0071";

		private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
			id: DiagnosticId,
			title: "Result<T?> uses nullable type parameter",
			messageFormat: "Result<{0}> uses a nullable type parameter. Consider using Result<{1}> with Failure() for absent values instead.",
			category: "Usage",
			defaultSeverity: DiagnosticSeverity.Info,
			isEnabledByDefault: false,
			description:
				"Using a nullable type parameter in Result<T?> suggests that null is a valid success value, " +
				"which defeats the purpose of the Result pattern. Consider using a non-nullable type with " +
				"Result<T>.Failure() for absent values.",
			helpLinkUri: ResultTypeHelper.HelpLinkBase + "VCR0071.md");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
			ImmutableArray.Create(Rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
			context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
			context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
		}

		private static void AnalyzeMethod(SymbolAnalysisContext context)
		{
			var method = (IMethodSymbol)context.Symbol;

			// Skip property accessors — handled by AnalyzeProperty
			if (method.AssociatedSymbol is IPropertySymbol)
				return;

			var returnType = method.ReturnType;

			// Unwrap Task<T>/ValueTask<T>
			var unwrapped = ResultTypeHelper.UnwrapTaskType(returnType);
			if (unwrapped != null)
				returnType = unwrapped;

			CheckResultWithNullableTypeArg(context, returnType, method.Locations);
		}

		private static void AnalyzeProperty(SymbolAnalysisContext context)
		{
			var property = (IPropertySymbol)context.Symbol;
			CheckResultWithNullableTypeArg(context, property.Type, property.Locations);
		}

		private static void AnalyzeField(SymbolAnalysisContext context)
		{
			var field = (IFieldSymbol)context.Symbol;
			CheckResultWithNullableTypeArg(context, field.Type, field.Locations);
		}

		private static void CheckResultWithNullableTypeArg(
			SymbolAnalysisContext context, ITypeSymbol type, ImmutableArray<Location> locations)
		{
			if (type is not INamedTypeSymbol { IsGenericType: true } namedType)
				return;

			if (!ResultTypeHelper.IsResultType(namedType))
				return;

			var typeArg = namedType.TypeArguments[0];

			bool isNullable = false;

			// Nullable value type: int?, decimal?, etc.
			if (typeArg.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
				isNullable = true;

			// Nullable reference type annotation
			if (typeArg.NullableAnnotation == NullableAnnotation.Annotated &&
				typeArg.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
				isNullable = true;

			if (!isNullable)
				return;

			// Get the non-nullable name for the suggestion
			string nullableName = typeArg.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
			string nonNullableName = nullableName.TrimEnd('?');

			foreach (var location in locations)
			{
				if (location.IsInSource)
				{
					context.ReportDiagnostic(
						Diagnostic.Create(Rule, location, nullableName, nonNullableName));
				}
			}
		}
	}
}
