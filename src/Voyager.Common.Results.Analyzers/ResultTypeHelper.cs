using Microsoft.CodeAnalysis;

namespace Voyager.Common.Results.Analyzers
{
	internal static class ResultTypeHelper
	{
		internal const string ResultNamespace = "Voyager.Common.Results";

		/// <summary>
		/// Checks if the type is Result or Result&lt;T&gt; from Voyager.Common.Results,
		/// including inherited types (traverses base type hierarchy).
		/// </summary>
		internal static bool IsResultType(ITypeSymbol? type)
		{
			var current = type;
			while (current != null)
			{
				if (current.Name == "Result" &&
					current.ContainingNamespace?.ToDisplayString() == ResultNamespace)
					return true;
				current = current.BaseType;
			}

			return false;
		}

		/// <summary>
		/// Checks if the method belongs to a Result type or is an extension method
		/// in the Voyager.Common.Results namespace.
		/// </summary>
		internal static bool IsResultMethod(IMethodSymbol method)
		{
			if (IsResultType(method.ContainingType))
				return true;

			if (method.IsExtensionMethod &&
				method.ContainingNamespace?.ToDisplayString()?.StartsWith(ResultNamespace) == true)
				return true;

			return false;
		}

		/// <summary>
		/// Unwraps Task&lt;T&gt; or ValueTask&lt;T&gt; to T. Returns null if not a task type.
		/// </summary>
		internal static ITypeSymbol? UnwrapTaskType(ITypeSymbol type)
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
	}
}
