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
			var result = await input.BindWithCircuitBreakerAsync(async x => Result<string>.Success($"Value: {x}"),
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
			var error = Error.UnavailableError("Test error");

			// Fail once
			await policy.RecordFailureAsync(error);
			Assert.Equal(1, policy.FailureCount);

			var input = Result<int>.Success(42);

			// Act
			var result = await input.BindWithCircuitBreakerAsync(async x => Result<string>.Success($"Value: {x}"),
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
			var error = Error.UnavailableError("Operation failed");

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
			var error = Error.UnavailableError("Input error");
			var input = Result<int>.Failure(error);

			// Act
			var result = await input.BindWithCircuitBreakerAsync(async x => Result<string>.Success($"Value: {x}"),
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
			var opError = Error.UnavailableError("Operation failed");

			// Open the circuit
			await policy.RecordFailureAsync(opError);
			await policy.RecordFailureAsync(opError);

			var input = Result<int>.Success(42);

			// Act
			var result = await input.BindWithCircuitBreakerAsync(async x => Result<string>.Success($"Value: {x}"),
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
			var error = Error.UnavailableError("Operation failed");

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
			var error = Error.UnavailableError("Operation failed");

			// Open circuit
			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);

			// Wait for timeout to transition to half-open
			await Task.Delay(150);

			var input = Result<int>.Success(42);

			// Act
			var result = await input.BindWithCircuitBreakerAsync(async x => Result<string>.Success($"Value: {x}"),
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
			var error = Error.UnavailableError("Operation failed");

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
			var result = await inputTask.BindWithCircuitBreakerAsync(async x => Result<string>.Success($"Value: {x}"),
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
			var result = await inputTask.BindWithCircuitBreakerAsync(x => Result<string>.Success($"Value: {x}"),
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
			var result = await input.BindWithCircuitBreakerAsync(async x => Result<string>.Success($"Value: {x}"),
				policy);

			// Assert
			Assert.False(result.IsSuccess);
			Assert.Equal(ErrorType.CircuitBreakerOpen, result.Error.Type);

			// Verify original error context is preserved
			Assert.Contains(originalError.Type.ToString(), result.Error.Message);
			Assert.Contains(originalError.Message, result.Error.Message);
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_DirectValue_AsyncFunc_NoNeedForResultSuccess()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy();
			var serviceDate = "2025-01-28";

			// Act - Direct value via policy.ExecuteAsync, no Result.Success() wrapper needed
			var result = await policy.ExecuteAsync(serviceDate,
				async sd => Result<string>.Success($"Processed: {sd}"));

			// Assert
			Assert.True(result.IsSuccess);
			Assert.Equal("Processed: 2025-01-28", result.Value);
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_DirectValue_SyncFunc_NoNeedForResultSuccess()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy();
			var userId = 42;

			// Act - Direct value with sync function
			var result = await policy.ExecuteAsync(userId,
				id => Result<string>.Success($"User: {id}"));

			// Assert
			Assert.True(result.IsSuccess);
			Assert.Equal("User: 42", result.Value);
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_OperationIndependentOfInput_AsyncFunc_ExecutesCorrectly()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy();
			var previousResult = Result.Success(); // Non-generic Result
			var executeCount = 0;

			// Act - Operation independent of input value
			var result = await previousResult.BindWithCircuitBreakerAsync(
				async () =>
				{
					executeCount++;
					return await Task.FromResult(Result<string>.Success("Operation executed"));
				},
				policy);

			// Assert
			Assert.True(result.IsSuccess);
			Assert.Equal("Operation executed", result.Value);
			Assert.Equal(1, executeCount);
			Assert.Equal(CircuitState.Closed, policy.State);
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_OperationIndependentOfInput_PropagatesPreviousError()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy();
			var previousError = Error.ValidationError("Previous operation failed");
			var previousResult = Result.Failure(previousError);

			// Act - Should not execute if previous result was failure
			var result = await previousResult.BindWithCircuitBreakerAsync(
				async () => Result<string>.Success("Operation executed"),
				policy);

			// Assert
			Assert.False(result.IsSuccess);
			Assert.Equal(previousError, result.Error);
			Assert.Equal(0, policy.FailureCount); // Operation didn't run, so no failure recorded
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_OperationIndependentOfInput_SyncFunc_ExecutesCorrectly()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy();
			var previousResult = Result.Success();
			var executeCount = 0;

			// Act - Synchronous operation independent of input
			var result = await previousResult.BindWithCircuitBreakerAsync(
				() =>
				{
					executeCount++;
					return Result<int>.Success(42);
				},
				policy);

			// Assert
			Assert.True(result.IsSuccess);
			Assert.Equal(42, result.Value);
			Assert.Equal(1, executeCount);
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_OperationIndependentOfInput_RecordsFailure()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 2);
			var previousResult = Result.Success();
			var operationError = Error.UnavailableError("Operation failed");

			// Act - Operation fails
			var result = await previousResult.BindWithCircuitBreakerAsync(
				async () => Result<string>.Failure(operationError),
				policy);

			// Assert
			Assert.False(result.IsSuccess);
			Assert.Equal(operationError, result.Error);
			Assert.Equal(1, policy.FailureCount);
			Assert.Equal(operationError, policy.LastError);
		}

		[Fact]
		public async Task BindWithCircuitBreakerAsync_OperationIndependentOfInput_OpenCircuit_FailsFast()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 1);
			var previousResult = Result.Success();
			var operationError = Error.UnavailableError("Operation failed");

			// Open the circuit
			await policy.RecordFailureAsync(operationError);

			// Act - Circuit is open, operation shouldn't execute
			var result = await previousResult.BindWithCircuitBreakerAsync(
				async () => Result<string>.Success("Should not execute"),
				policy);

			// Assert
			Assert.False(result.IsSuccess);
			Assert.Equal(ErrorType.CircuitBreakerOpen, result.Error.Type);
			Assert.Equal(CircuitState.Open, policy.State);
		}

		[Fact]
		public async Task ExecuteAsync_InputIndependent_AsyncFunc_ExecutesCorrectly()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy();
			var executeCount = 0;

			// Act - Execute async operation without input parameter via policy.ExecuteAsync
			var result = await policy.ExecuteAsync(
				async () =>
				{
					executeCount++;
					return await Task.FromResult(Result<string>.Success("Executed"));
				});

			// Assert
			Assert.True(result.IsSuccess);
			Assert.Equal("Executed", result.Value);
			Assert.Equal(1, executeCount);
			Assert.Equal(CircuitState.Closed, policy.State);
		}

		[Fact]
		public async Task ExecuteAsync_InputIndependent_SyncFunc_ExecutesCorrectly()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy();
			var executeCount = 0;

			// Act - Execute sync operation without input parameter
			var result = await policy.ExecuteAsync(
				() =>
				{
					executeCount++;
					return Result<int>.Success(100);
				});

			// Assert
			Assert.True(result.IsSuccess);
			Assert.Equal(100, result.Value);
			Assert.Equal(1, executeCount);
		}

		[Fact]
		public async Task ExecuteAsync_InputIndependent_RecordsFailure()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 2);
			var operationError = Error.DatabaseError("Database unavailable");

			// Act - Operation fails
			var result = await policy.ExecuteAsync(
				async () => Result<string>.Failure(operationError));

			// Assert
			Assert.False(result.IsSuccess);
			Assert.Equal(operationError, result.Error);
			Assert.Equal(1, policy.FailureCount);
			Assert.Equal(operationError, policy.LastError);
		}

		[Fact]
		public async Task ExecuteAsync_InputIndependent_OpenCircuit_FailsFast()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 1);
			var operationError = Error.TimeoutError("Timeout");

			// Open the circuit
			await policy.RecordFailureAsync(operationError);

			// Act - Circuit is open, should fail fast
			var result = await policy.ExecuteAsync(
				async () => Result<string>.Success("Should not execute"));

			// Assert
			Assert.False(result.IsSuccess);
			Assert.Equal(ErrorType.CircuitBreakerOpen, result.Error.Type);
			Assert.Equal(CircuitState.Open, policy.State);
		}
	}
}
