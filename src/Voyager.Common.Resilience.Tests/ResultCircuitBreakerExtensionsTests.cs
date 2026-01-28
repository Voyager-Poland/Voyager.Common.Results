#if NET48
using System;
using System.Threading;
using System.Threading.Tasks;
#endif

#if NET6_0_OR_GREATER
using System;
using System.Threading.Tasks;
#endif

namespace Voyager.Common.Resilience.Tests
{
	/// <summary>
	/// Tests for ResultCircuitBreakerExtensions
	/// </summary>
	public sealed class ResultCircuitBreakerExtensionsTests
	{
		[Fact]
		public async Task BindWithCircuitBreakerAsync_ClosedCircuit_ExecutesFunction()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy();
			var input = Result<int>.Success(42);

			// Act
			var result = await input.BindWithCircuitBreakerAsync(
				async x => Result<string>.Success($"Value: {x}"),
				policy);

			// Assert
			Assert.True(result.IsSuccess);
			Assert.Equal("Value: 42", result.Value);
			Assert.Equal(CircuitState.Closed, policy.State);
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_SuccessfulOperation_RecordsSuccess()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 3);
			var error = Error.ValidationError("Test error");

			// Fail once
			await policy.RecordFailureAsync(error);
			Assert.Equal(1, policy.FailureCount);

			var input = Result<int>.Success(42);

			// Act
			var result = await input.BindWithCircuitBreakerAsync(
				async x => Result<string>.Success($"Value: {x}"),
				policy);

			// Assert
			Assert.True(result.IsSuccess);
			Assert.Equal(0, policy.FailureCount); // Reset after success
			Assert.Null(policy.LastError);
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_FailedOperation_RecordsFailure()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 3);
			var input = Result<int>.Success(42);
			var error = Error.ValidationError("Operation failed");

			// Act
			var result = await input.BindWithCircuitBreakerAsync(
				async x => Result<string>.Failure(error),
				policy);

			// Assert
			Assert.False(result.IsSuccess);
			Assert.Equal(error, result.Error);
			Assert.Equal(1, policy.FailureCount);
			Assert.Equal(error, policy.LastError);
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_InputFailure_PropagatesError()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy();
			var error = Error.ValidationError("Input error");
			var input = Result<int>.Failure(error);

			// Act
			var result = await input.BindWithCircuitBreakerAsync(
				async x => Result<string>.Success($"Value: {x}"),
				policy);

			// Assert
			Assert.False(result.IsSuccess);
			Assert.Equal(error, result.Error);
			Assert.Equal(0, policy.FailureCount); // No failure recorded since operation didn't run
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_OpenCircuit_ReturnsCircuitBreakerOpenError()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 2);
			var opError = Error.ValidationError("Operation failed");

			// Open the circuit
			await policy.RecordFailureAsync(opError);
			await policy.RecordFailureAsync(opError);

			var input = Result<int>.Success(42);

			// Act
			var result = await input.BindWithCircuitBreakerAsync(
				async x => Result<string>.Success($"Value: {x}"),
				policy);

			// Assert
			Assert.False(result.IsSuccess);
			Assert.Equal(ErrorType.CircuitBreakerOpen, result.Error.Type);
			Assert.Contains(opError.Type.ToString(), result.Error.Message);
			Assert.Contains(opError.Message, result.Error.Message);
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_MultipleFailures_OpensCircuit()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 3);
			var error = Error.ValidationError("Operation failed");

			// Act - Execute multiple failing operations
			for (int i = 0; i < 3; i++)
			{
				var input = Result<int>.Success(i);
				await input.BindWithCircuitBreakerAsync(
					async x => Result<string>.Failure(error),
					policy);
			}

			// Assert
			Assert.Equal(CircuitState.Open, policy.State);
			Assert.Equal(3, policy.FailureCount);
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_HalfOpenSuccess_ClosesCircuit()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(
				failureThreshold: 2,
				openTimeout: TimeSpan.FromMilliseconds(100));
			var error = Error.ValidationError("Operation failed");

			// Open circuit
			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);

			// Wait for timeout to transition to half-open
			await Task.Delay(150);

			var input = Result<int>.Success(42);

			// Act
			var result = await input.BindWithCircuitBreakerAsync(
				async x => Result<string>.Success($"Value: {x}"),
				policy);

			// Assert
			Assert.True(result.IsSuccess);
			Assert.Equal(CircuitState.Closed, policy.State); // Circuit closed after success
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_HalfOpenFailure_ReopensCircuit()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(
				failureThreshold: 2,
				openTimeout: TimeSpan.FromMilliseconds(100));
			var error = Error.ValidationError("Operation failed");

			// Open circuit
			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);

			// Wait for timeout to transition to half-open
			await Task.Delay(150);

			var input = Result<int>.Success(42);

			// Act
			var result = await input.BindWithCircuitBreakerAsync(
				async x => Result<string>.Failure(error),
				policy);

			// Assert
			Assert.False(result.IsSuccess);
			Assert.Equal(CircuitState.Open, policy.State); // Circuit reopened after failure
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_TaskOverload_WorksCorrectly()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy();
			var inputTask = Task.FromResult(Result<int>.Success(42));

			// Act
			var result = await inputTask.BindWithCircuitBreakerAsync(
				async x => Result<string>.Success($"Value: {x}"),
				policy);

			// Assert
			Assert.True(result.IsSuccess);
			Assert.Equal("Value: 42", result.Value);
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_SyncFunction_WorksCorrectly()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy();
			var input = Result<int>.Success(42);

			// Act
			var result = await input.BindWithCircuitBreakerAsync(
				x => Result<string>.Success($"Value: {x}"),
				policy);

			// Assert
			Assert.True(result.IsSuccess);
			Assert.Equal("Value: 42", result.Value);
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_SyncFunctionTaskOverload_WorksCorrectly()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy();
			var inputTask = Task.FromResult(Result<int>.Success(42));

			// Act
			var result = await inputTask.BindWithCircuitBreakerAsync(
				x => Result<string>.Success($"Value: {x}"),
				policy);

			// Assert
			Assert.True(result.IsSuccess);
			Assert.Equal("Value: 42", result.Value);
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_PreservesErrorContext_InCircuitBreakerOpenError()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 2);
			var originalError = Error.DatabaseError("Database connection failed");

			// Open circuit with specific error
			await policy.RecordFailureAsync(originalError);
			await policy.RecordFailureAsync(originalError);

			var input = Result<int>.Success(42);

			// Act
			var result = await input.BindWithCircuitBreakerAsync(
				async x => Result<string>.Success($"Value: {x}"),
				policy);

			// Assert
			Assert.False(result.IsSuccess);
			Assert.Equal(ErrorType.CircuitBreakerOpen, result.Error.Type);

			// Verify original error context is preserved
			Assert.Contains(originalError.Type.ToString(), result.Error.Message);
			Assert.Contains(originalError.Message, result.Error.Message);
		}
	}
}
