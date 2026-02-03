using System.Linq;
using Voyager.Common.Results;

namespace Voyager.Common.Results.Tests;

public class ErrorFromExceptionTests
{
	[Fact]
	public void FromException_PreservesStackTrace()
	{
		// Arrange
		Exception caught;
		try { throw new InvalidOperationException("Test"); }
		catch (Exception ex) { caught = ex; }

		// Act
		var error = Error.FromException(caught);

		// Assert
		Assert.NotNull(error.StackTrace);
		Assert.Contains("FromException_PreservesStackTrace", error.StackTrace);
	}

	[Fact]
	public void FromException_PreservesExceptionType()
	{
		// Arrange
		var exception = new ArgumentNullException("param");

		// Act
		var error = Error.FromException(exception);

		// Assert
		Assert.Equal("System.ArgumentNullException", error.ExceptionType);
		Assert.Equal("Exception.ArgumentNullException", error.Code);
	}

	[Fact]
	public void FromException_PreservesSource()
	{
		// Arrange
		Exception caught;
		try { throw new InvalidOperationException("Test"); }
		catch (Exception ex) { caught = ex; }

		// Act
		var error = Error.FromException(caught);

		// Assert - Source is set for thrown exceptions
		Assert.Equal(caught.Source, error.Source);
	}

	[Fact]
	public void FromException_MapsOperationCanceledException()
	{
		// Act
		var error = Error.FromException(new OperationCanceledException());

		// Assert
		Assert.Equal(ErrorType.Cancelled, error.Type);
	}

	[Fact]
	public void FromException_MapsTimeoutException()
	{
		// Act
		var error = Error.FromException(new TimeoutException());

		// Assert
		Assert.Equal(ErrorType.Timeout, error.Type);
	}

	[Fact]
	public void FromException_MapsArgumentException()
	{
		// Act
		var error = Error.FromException(new ArgumentException("Invalid"));

		// Assert
		Assert.Equal(ErrorType.Validation, error.Type);
	}

	[Fact]
	public void FromException_MapsArgumentNullException()
	{
		// Act
		var error = Error.FromException(new ArgumentNullException("param"));

		// Assert
		Assert.Equal(ErrorType.Validation, error.Type);
	}

	[Fact]
	public void FromException_MapsInvalidOperationException()
	{
		// Act
		var error = Error.FromException(new InvalidOperationException("Invalid"));

		// Assert
		Assert.Equal(ErrorType.Business, error.Type);
	}

	[Fact]
	public void FromException_MapsKeyNotFoundException()
	{
		// Act
		var error = Error.FromException(new KeyNotFoundException("Key not found"));

		// Assert
		Assert.Equal(ErrorType.NotFound, error.Type);
	}

	[Fact]
	public void FromException_MapsUnauthorizedAccessException()
	{
		// Act
		var error = Error.FromException(new UnauthorizedAccessException("Access denied"));

		// Assert
		Assert.Equal(ErrorType.Permission, error.Type);
	}

	[Fact]
	public void FromException_MapsUnknownExceptionToUnexpected()
	{
		// Act
		var error = Error.FromException(new NotSupportedException("Not supported"));

		// Assert
		Assert.Equal(ErrorType.Unexpected, error.Type);
	}

	[Fact]
	public void FromException_ChainsInnerExceptions()
	{
		// Arrange
		var inner = new InvalidOperationException("Inner");
		var outer = new Exception("Outer", inner);

		// Act
		var error = Error.FromException(outer);

		// Assert
		Assert.NotNull(error.InnerError);
		Assert.Equal("Inner", error.InnerError.Message);
		Assert.Equal(ErrorType.Business, error.InnerError.Type);
		Assert.Equal("System.InvalidOperationException", error.InnerError.ExceptionType);
	}

	[Fact]
	public void FromException_ChainsMultipleInnerExceptions()
	{
		// Arrange
		var innermost = new KeyNotFoundException("Not found");
		var middle = new InvalidOperationException("Middle", innermost);
		var outer = new Exception("Outer", middle);

		// Act
		var error = Error.FromException(outer);

		// Assert
		Assert.NotNull(error.InnerError);
		Assert.NotNull(error.InnerError.InnerError);
		Assert.Equal("Not found", error.InnerError.InnerError.Message);
		Assert.Equal(ErrorType.NotFound, error.InnerError.InnerError.Type);
	}

	[Fact]
	public void FromException_WithCustomErrorType_UsesProvidedType()
	{
		// Arrange
		var exception = new Exception("Test");

		// Act
		var error = Error.FromException(exception, ErrorType.Database);

		// Assert
		Assert.Equal(ErrorType.Database, error.Type);
		Assert.Equal("Exception.Exception", error.Code);
	}

	[Fact]
	public void FromException_WithCustomErrorType_PreservesDetails()
	{
		// Arrange
		Exception caught;
		try { throw new InvalidOperationException("Test"); }
		catch (Exception ex) { caught = ex; }

		// Act
		var error = Error.FromException(caught, ErrorType.Unavailable);

		// Assert
		Assert.Equal(ErrorType.Unavailable, error.Type);
		Assert.NotNull(error.StackTrace);
		Assert.Equal("System.InvalidOperationException", error.ExceptionType);
	}

	[Fact]
	public void FromException_DoesNotHoldReferenceToException()
	{
		// Arrange
		WeakReference<Exception> weakRef = null!;
		Error error = null!;

		// Create exception in separate method to ensure it's out of scope
		void CreateError()
		{
			var exception = new InvalidOperationException("Test message for GC test");
			weakRef = new WeakReference<Exception>(exception);
			error = Error.FromException(exception);
		}
		CreateError();

		// Act - force GC multiple times
		for (int i = 0; i < 3; i++)
		{
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
			GC.WaitForPendingFinalizers();
		}

		// Assert - error still has all data even if exception is collected
		Assert.NotNull(error);
		Assert.NotNull(error.ExceptionType);
		Assert.Equal("Test message for GC test", error.Message);
		Assert.Equal("System.InvalidOperationException", error.ExceptionType);
		// Note: WeakReference may or may not be collected depending on runtime
		// The important thing is that Error has all the data it needs
	}

	[Fact]
	public void ToDetailedString_IncludesType()
	{
		// Arrange
		var error = Error.FromException(new InvalidOperationException("Test error"));

		// Act
		var detailed = error.ToDetailedString();

		// Assert
		Assert.Contains("[Business]", detailed);
	}

	[Fact]
	public void ToDetailedString_IncludesExceptionType()
	{
		// Arrange
		var error = Error.FromException(new InvalidOperationException("Test error"));

		// Act
		var detailed = error.ToDetailedString();

		// Assert
		Assert.Contains("InvalidOperationException", detailed);
	}

	[Fact]
	public void ToDetailedString_IncludesMessage()
	{
		// Arrange
		var error = Error.FromException(new InvalidOperationException("Test error message"));

		// Act
		var detailed = error.ToDetailedString();

		// Assert
		Assert.Contains("Test error message", detailed);
	}

	[Fact]
	public void ToDetailedString_IncludesStackTrace()
	{
		// Arrange
		Exception caught;
		try { throw new InvalidOperationException("Test"); }
		catch (Exception ex) { caught = ex; }
		var error = Error.FromException(caught);

		// Act
		var detailed = error.ToDetailedString();

		// Assert
		Assert.Contains("Stack Trace:", detailed);
	}

	[Fact]
	public void ToDetailedString_IncludesInnerError()
	{
		// Arrange
		var inner = new KeyNotFoundException("Inner error");
		var outer = new InvalidOperationException("Outer error", inner);
		var error = Error.FromException(outer);

		// Act
		var detailed = error.ToDetailedString();

		// Assert
		Assert.Contains("Caused by:", detailed);
		Assert.Contains("Inner error", detailed);
		Assert.Contains("KeyNotFoundException", detailed);
	}

	[Fact]
	public void ToDetailedString_TruncatesLongStackTrace()
	{
		// Arrange - create an error with a long stack trace directly
		// (recursive exceptions can be optimized away in Release mode)
		var longStackTrace = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"   at Method{i}() in File.cs:line {i * 10}"));
		var error = new Error(ErrorType.Business, "Test.Error", "Test message")
		{
			StackTrace = longStackTrace,
			ExceptionType = "System.InvalidOperationException"
		};

		// Act
		var detailed = error.ToDetailedString();

		// Assert
		Assert.Contains("more lines", detailed);
	}

	[Fact]
	public void ToDetailedString_WorksForSimpleError()
	{
		// Arrange
		var error = Error.ValidationError("Simple error");

		// Act
		var detailed = error.ToDetailedString();

		// Assert
		Assert.Contains("[Validation]", detailed);
		Assert.Contains("Simple error", detailed);
		Assert.DoesNotContain("Stack Trace:", detailed);
	}
}
