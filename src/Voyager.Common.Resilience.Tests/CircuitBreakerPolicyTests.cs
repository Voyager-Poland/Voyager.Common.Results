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
	/// Tests for CircuitBreakerPolicy core functionality
	/// </summary>
	public sealed class CircuitBreakerPolicyTests
	{
		[Fact]
		public async Task Constructor_WithDefaultParameters_CreatesClosedCircuit()
		{
			// Arrange & Act
			var policy = new CircuitBreakerPolicy();

			// Assert
			Assert.Equal(CircuitState.Closed, policy.State);
			Assert.Equal(0, policy.FailureCount);
			Assert.Null(policy.LastError);
		}

		[Fact]
		public async Task Constructor_WithCustomParameters_UsesProvidedValues()
		{
			// Arrange & Act
			var policy = new CircuitBreakerPolicy(
				failureThreshold: 10,
				openTimeout: TimeSpan.FromMinutes(5),
				halfOpenMaxAttempts: 5);

			// Assert
			Assert.Equal(CircuitState.Closed, policy.State);
		}

		[Fact]
		public void Constructor_WithZeroFailureThreshold_ThrowsArgumentException()
		{
			// Act & Assert
			var ex = Assert.Throws<ArgumentException>(() => new CircuitBreakerPolicy(failureThreshold: 0));
			Assert.Contains("Failure threshold must be greater than zero", ex.Message);
		}

		[Fact]
		public void Constructor_WithNegativeFailureThreshold_ThrowsArgumentException()
		{
			// Act & Assert
			var ex = Assert.Throws<ArgumentException>(() => new CircuitBreakerPolicy(failureThreshold: -1));
			Assert.Contains("Failure threshold must be greater than zero", ex.Message);
		}

		[Fact]
		public void Constructor_WithZeroHalfOpenMaxAttempts_ThrowsArgumentException()
		{
			// Act & Assert
			var ex = Assert.Throws<ArgumentException>(() => new CircuitBreakerPolicy(halfOpenMaxAttempts: 0));
			Assert.Contains("Half-open max attempts must be greater than zero", ex.Message);
		}

		[Fact]
		public async Task ShouldAllowRequestAsync_ClosedCircuit_AllowsRequest()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy();

			// Act
			var result = await policy.ShouldAllowRequestAsync();

			// Assert
			Assert.True(result.IsSuccess);
			Assert.True(result.Value);
		}

		[Fact]
		public async Task RecordFailureAsync_BelowThreshold_KeepsCircuitClosed()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 3);
			var error = Error.ValidationError("Test error");

			// Act
			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);

			// Assert
			Assert.Equal(CircuitState.Closed, policy.State);
			Assert.Equal(2, policy.FailureCount);
			Assert.Equal(error, policy.LastError);
		}

		[Fact]
		public async Task RecordFailureAsync_AtThreshold_OpensCircuit()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 3);
			var error = Error.ValidationError("Test error");

			// Act
			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);

			// Assert
			Assert.Equal(CircuitState.Open, policy.State);
			Assert.Equal(3, policy.FailureCount);
			Assert.Equal(error, policy.LastError);
		}

		[Fact]
		public async Task ShouldAllowRequestAsync_OpenCircuit_BlocksRequest()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 2);
			var error = Error.ValidationError("Test error");

			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);

			// Act
			var result = await policy.ShouldAllowRequestAsync();

			// Assert
			Assert.False(result.IsSuccess);
			Assert.Equal(ErrorType.CircuitBreakerOpen, result.Error.Type);
			Assert.Contains(error.Type.ToString(), result.Error.Message);
			Assert.Contains(error.Message, result.Error.Message);
		}

		[Fact]
		public async Task ShouldAllowRequestAsync_OpenCircuitAfterTimeout_TransitionsToHalfOpen()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(
				failureThreshold: 2,
				openTimeout: TimeSpan.FromMilliseconds(100));
			var error = Error.ValidationError("Test error");

			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);

			// Act - Wait for timeout
			await Task.Delay(150);
			var result = await policy.ShouldAllowRequestAsync();

			// Assert
			Assert.True(result.IsSuccess);
			Assert.Equal(CircuitState.HalfOpen, policy.State);
		}

		[Fact]
		public async Task RecordSuccessAsync_HalfOpenCircuit_ClosesCircuit()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(
				failureThreshold: 2,
				openTimeout: TimeSpan.FromMilliseconds(100));
			var error = Error.ValidationError("Test error");

			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);
			await Task.Delay(150);
			await policy.ShouldAllowRequestAsync(); // Transition to HalfOpen

			// Act
			await policy.RecordSuccessAsync();

			// Assert
			Assert.Equal(CircuitState.Closed, policy.State);
			Assert.Equal(0, policy.FailureCount);
			Assert.Null(policy.LastError);
		}

		[Fact]
		public async Task RecordSuccessAsync_ClosedCircuit_ResetsFailureCount()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 5);
			var error = Error.ValidationError("Test error");

			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);

			// Act
			await policy.RecordSuccessAsync();

			// Assert
			Assert.Equal(CircuitState.Closed, policy.State);
			Assert.Equal(0, policy.FailureCount);
			Assert.Null(policy.LastError);
		}

		[Fact]
		public async Task RecordFailureAsync_HalfOpenCircuit_ReopensCircuit()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(
				failureThreshold: 2,
				openTimeout: TimeSpan.FromMilliseconds(100));
			var error = Error.ValidationError("Test error");

			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);
			await Task.Delay(150);
			await policy.ShouldAllowRequestAsync(); // Transition to HalfOpen

			// Act
			await policy.RecordFailureAsync(error);

			// Assert
			Assert.Equal(CircuitState.Open, policy.State);
		}

		[Fact]
		public async Task ResetAsync_OpensCircuit_ResetsToClosedState()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 2);
			var error = Error.ValidationError("Test error");

			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);

			// Act
			await policy.ResetAsync();

			// Assert
			Assert.Equal(CircuitState.Closed, policy.State);
			Assert.Equal(0, policy.FailureCount);
			Assert.Null(policy.LastError);
		}

		[Fact]
		public async Task ResetAsync_HalfOpenCircuit_ResetsToClosedState()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(
				failureThreshold: 2,
				openTimeout: TimeSpan.FromMilliseconds(100));
			var error = Error.ValidationError("Test error");

			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);
			await Task.Delay(150);
			await policy.ShouldAllowRequestAsync(); // Transition to HalfOpen

			// Act
			await policy.ResetAsync();

			// Assert
			Assert.Equal(CircuitState.Closed, policy.State);
		}
	}
}
