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
			var error = Error.UnavailableError("Service unavailable");

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
			var error = Error.UnavailableError("Service unavailable");

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
		public async Task RecordFailureAsync_BusinessErrors_DoNotOpenCircuit()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 2);

			// Act - Record business/validation errors (should be ignored)
			await policy.RecordFailureAsync(Error.ValidationError("Invalid input"));
			await policy.RecordFailureAsync(Error.NotFoundError("User not found"));
			await policy.RecordFailureAsync(Error.BusinessError("Business rule violation"));
			await policy.RecordFailureAsync(Error.PermissionError("Access denied"));
			await policy.RecordFailureAsync(Error.ConflictError("Duplicate entry"));
			await policy.RecordFailureAsync(Error.UnauthorizedError("Not authenticated"));

			// Assert - Circuit should remain closed
			Assert.Equal(CircuitState.Closed, policy.State);
			Assert.Equal(0, policy.FailureCount);
			Assert.Null(policy.LastError);
		}

		[Fact]
		public async Task RecordFailureAsync_InfrastructureErrors_OpenCircuit()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 3);

			// Act - Only infrastructure errors should count
			await policy.RecordFailureAsync(Error.ValidationError("Ignored"));
			await policy.RecordFailureAsync(Error.UnavailableError("Service down"));
			await policy.RecordFailureAsync(Error.BusinessError("Ignored"));
			await policy.RecordFailureAsync(Error.TimeoutError("Request timeout"));
			await policy.RecordFailureAsync(Error.NotFoundError("Ignored"));
			await policy.RecordFailureAsync(Error.DatabaseError("Connection failed"));

			// Assert - Should open after 3 infrastructure failures
			Assert.Equal(CircuitState.Open, policy.State);
			Assert.Equal(3, policy.FailureCount);
			Assert.Equal(ErrorType.Database, policy.LastError!.Type);
		}

		[Fact]
		public async Task ShouldAllowRequestAsync_OpenCircuit_BlocksRequest()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 2);
			var error = Error.TimeoutError("Request timeout");

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
			var error = Error.DatabaseError("Connection failed");

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
			var error = Error.UnavailableError("Service unavailable");

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
			var error = Error.UnavailableError("Service unavailable");

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
			var error = Error.UnavailableError("Service unavailable");

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
			var error = Error.UnavailableError("Service unavailable");

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
			var error = Error.UnavailableError("Service unavailable");

			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);
			await Task.Delay(150);
			await policy.ShouldAllowRequestAsync(); // Transition to HalfOpen

			// Act
			await policy.ResetAsync();

			// Assert
			Assert.Equal(CircuitState.Closed, policy.State);
		}

		// ==================== OnStateChanged Callback Tests ====================

		[Fact]
		public async Task OnStateChanged_CalledWhenCircuitOpens()
		{
			// Arrange
			CircuitState? capturedOldState = null;
			CircuitState? capturedNewState = null;
			int? capturedFailureCount = null;
			Error? capturedError = null;

			var policy = new CircuitBreakerPolicy(failureThreshold: 2);
			policy.OnStateChanged = (oldState, newState, failures, error) =>
			{
				capturedOldState = oldState;
				capturedNewState = newState;
				capturedFailureCount = failures;
				capturedError = error;
			};

			var testError = Error.UnavailableError("Service unavailable");

			// Act
			await policy.RecordFailureAsync(testError);
			await policy.RecordFailureAsync(testError);

			// Assert
			Assert.Equal(CircuitState.Closed, capturedOldState);
			Assert.Equal(CircuitState.Open, capturedNewState);
			Assert.Equal(2, capturedFailureCount);
			Assert.Equal(testError, capturedError);
		}

		[Fact]
		public async Task OnStateChanged_CalledWhenCircuitClosesFromHalfOpen()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(
				failureThreshold: 2,
				openTimeout: TimeSpan.FromMilliseconds(50));
			var error = Error.UnavailableError("Service unavailable");

			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);
			await Task.Delay(100);
			await policy.ShouldAllowRequestAsync(); // Transition to HalfOpen

			CircuitState? capturedOldState = null;
			CircuitState? capturedNewState = null;
			policy.OnStateChanged = (oldState, newState, _, _) =>
			{
				capturedOldState = oldState;
				capturedNewState = newState;
			};

			// Act
			await policy.RecordSuccessAsync();

			// Assert
			Assert.Equal(CircuitState.HalfOpen, capturedOldState);
			Assert.Equal(CircuitState.Closed, capturedNewState);
		}

		[Fact]
		public async Task OnStateChanged_CalledWhenTransitioningToHalfOpen()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(
				failureThreshold: 2,
				openTimeout: TimeSpan.FromMilliseconds(50));
			var error = Error.UnavailableError("Service unavailable");

			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);
			await Task.Delay(100);

			CircuitState? capturedOldState = null;
			CircuitState? capturedNewState = null;
			policy.OnStateChanged = (oldState, newState, _, _) =>
			{
				capturedOldState = oldState;
				capturedNewState = newState;
			};

			// Act
			await policy.ShouldAllowRequestAsync();

			// Assert
			Assert.Equal(CircuitState.Open, capturedOldState);
			Assert.Equal(CircuitState.HalfOpen, capturedNewState);
		}

		[Fact]
		public async Task OnStateChanged_CalledWhenHalfOpenReopens()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(
				failureThreshold: 2,
				openTimeout: TimeSpan.FromMilliseconds(50));
			var error = Error.UnavailableError("Service unavailable");

			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);
			await Task.Delay(100);
			await policy.ShouldAllowRequestAsync(); // Transition to HalfOpen

			CircuitState? capturedOldState = null;
			CircuitState? capturedNewState = null;
			policy.OnStateChanged = (oldState, newState, _, _) =>
			{
				capturedOldState = oldState;
				capturedNewState = newState;
			};

			// Act
			await policy.RecordFailureAsync(error);

			// Assert
			Assert.Equal(CircuitState.HalfOpen, capturedOldState);
			Assert.Equal(CircuitState.Open, capturedNewState);
		}

		[Fact]
		public async Task OnStateChanged_NotCalledForBusinessErrors()
		{
			// Arrange
			var callCount = 0;
			var policy = new CircuitBreakerPolicy(failureThreshold: 1);
			policy.OnStateChanged = (_, _, _, _) => callCount++;

			// Act - business errors should not affect state
			await policy.RecordFailureAsync(Error.ValidationError("Invalid input"));
			await policy.RecordFailureAsync(Error.NotFoundError("Not found"));
			await policy.RecordFailureAsync(Error.BusinessError("Business error"));

			// Assert
			Assert.Equal(0, callCount);
			Assert.Equal(CircuitState.Closed, policy.State);
		}

		[Fact]
		public async Task OnStateChanged_NotCalledWhenStateDoesNotChange()
		{
			// Arrange
			var callCount = 0;
			var policy = new CircuitBreakerPolicy(failureThreshold: 5);
			policy.OnStateChanged = (_, _, _, _) => callCount++;

			// Act - failures below threshold don't change state
			await policy.RecordFailureAsync(Error.UnavailableError("Error 1"));
			await policy.RecordFailureAsync(Error.UnavailableError("Error 2"));

			// Assert
			Assert.Equal(0, callCount);
			Assert.Equal(CircuitState.Closed, policy.State);
		}

		[Fact]
		public async Task OnStateChanged_CalledOnReset()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 2);
			var error = Error.UnavailableError("Service unavailable");

			await policy.RecordFailureAsync(error);
			await policy.RecordFailureAsync(error);

			CircuitState? capturedOldState = null;
			CircuitState? capturedNewState = null;
			policy.OnStateChanged = (oldState, newState, _, _) =>
			{
				capturedOldState = oldState;
				capturedNewState = newState;
			};

			// Act
			await policy.ResetAsync();

			// Assert
			Assert.Equal(CircuitState.Open, capturedOldState);
			Assert.Equal(CircuitState.Closed, capturedNewState);
		}

		[Fact]
		public async Task OnStateChanged_NotCalledOnResetWhenAlreadyClosed()
		{
			// Arrange
			var callCount = 0;
			var policy = new CircuitBreakerPolicy(failureThreshold: 5);
			policy.OnStateChanged = (_, _, _, _) => callCount++;

			// Act
			await policy.ResetAsync();

			// Assert
			Assert.Equal(0, callCount);
		}

		[Fact]
		public async Task OnStateChanged_NullCallback_NoException()
		{
			// Arrange
			var policy = new CircuitBreakerPolicy(failureThreshold: 2);
			policy.OnStateChanged = null;

			// Act & Assert - should not throw
			await policy.RecordFailureAsync(Error.UnavailableError("Error 1"));
			await policy.RecordFailureAsync(Error.UnavailableError("Error 2"));
			Assert.Equal(CircuitState.Open, policy.State);
		}
	}
}
