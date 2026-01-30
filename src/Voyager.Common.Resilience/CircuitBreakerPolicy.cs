#if NET48
using System;
using System.Threading;
using System.Threading.Tasks;
#endif

using Voyager.Common.Results;

namespace Voyager.Common.Resilience
{
	/// <summary>
	/// Represents the state of a circuit breaker
	/// </summary>
	public enum CircuitState
	{
		/// <summary>
		/// Normal operation - requests are allowed through
		/// </summary>
		Closed,

		/// <summary>
		/// Circuit is broken - requests fail immediately without attempting operation
		/// </summary>
		Open,

		/// <summary>
		/// Testing if the circuit can be closed - allows limited requests through
		/// </summary>
		HalfOpen
	}

	/// <summary>
	/// Thread-safe circuit breaker policy for managing fault tolerance
	/// </summary>
	public sealed class CircuitBreakerPolicy
	{
		private readonly int _failureThreshold;
		private readonly TimeSpan _openTimeout;
		private readonly int _halfOpenMaxAttempts;
		private readonly SemaphoreSlim _lock = new(1, 1);

		private CircuitState _state = CircuitState.Closed;
		private int _failureCount = 0;
		private int _halfOpenAttempts = 0;
		private DateTime _openedAt = DateTime.MinValue;
		private Error? _lastError;

		/// <summary>
		/// Creates a new circuit breaker policy
		/// </summary>
		/// <param name="failureThreshold">Number of consecutive failures before opening circuit (default: 5)</param>
		/// <param name="openTimeout">Duration to keep circuit open before attempting half-open (default: 30 seconds)</param>
		/// <param name="halfOpenMaxAttempts">Maximum attempts in half-open state before re-opening (default: 3)</param>
		public CircuitBreakerPolicy(
			int failureThreshold = 5,
			TimeSpan? openTimeout = null,
			int halfOpenMaxAttempts = 3)
		{
			if (failureThreshold <= 0)
				throw new ArgumentException("Failure threshold must be greater than zero", nameof(failureThreshold));

			if (halfOpenMaxAttempts <= 0)
				throw new ArgumentException("Half-open max attempts must be greater than zero", nameof(halfOpenMaxAttempts));

			_failureThreshold = failureThreshold;
			_openTimeout = openTimeout ?? TimeSpan.FromSeconds(30);
			_halfOpenMaxAttempts = halfOpenMaxAttempts;
		}

		/// <summary>
		/// Gets the current state of the circuit breaker
		/// </summary>
		public CircuitState State => _state;

		/// <summary>
		/// Gets the number of failures in the current window
		/// </summary>
		public int FailureCount => _failureCount;

		/// <summary>
		/// Gets the last error that occurred
		/// </summary>
		public Error? LastError => _lastError;

		/// <summary>
		/// Checks if a request should be allowed through the circuit breaker
		/// </summary>
		/// <returns>Success if request allowed, Failure with CircuitBreakerOpenError if blocked</returns>
		public async Task<Result<bool>> ShouldAllowRequestAsync()
		{
			await _lock.WaitAsync().ConfigureAwait(false);
			try
			{
				switch (_state)
				{
					case CircuitState.Closed:
						return Result<bool>.Success(true);

					case CircuitState.Open:
						// Check if timeout has elapsed to transition to half-open
						if (DateTime.UtcNow - _openedAt >= _openTimeout)
						{
							_state = CircuitState.HalfOpen;
							_halfOpenAttempts = 0;
							return Result<bool>.Success(true);
						}

						// Circuit still open - block request
						return _lastError != null
							? Result<bool>.Failure(Error.CircuitBreakerOpenError(_lastError))
							: Result<bool>.Failure(Error.CircuitBreakerOpenError("Circuit breaker is open"));

					case CircuitState.HalfOpen:
						if (_halfOpenAttempts < _halfOpenMaxAttempts)
						{
							_halfOpenAttempts++;
							return Result<bool>.Success(true);
						}

						// Exceeded half-open attempts - reopen circuit
						_state = CircuitState.Open;
						_openedAt = DateTime.UtcNow;
						return _lastError != null
							? Result<bool>.Failure(Error.CircuitBreakerOpenError(_lastError))
							: Result<bool>.Failure(Error.CircuitBreakerOpenError("Circuit breaker reopened after half-open failures"));

					default:
						return Result<bool>.Failure(Error.UnexpectedError($"Unknown circuit state: {_state}"));
				}
			}
			finally
			{
				_lock.Release();
			}
		}

		/// <summary>
		/// Records a successful operation
		/// </summary>
		public async Task RecordSuccessAsync()
		{
			await _lock.WaitAsync().ConfigureAwait(false);
			try
			{
				_failureCount = 0;
				_lastError = null;

				if (_state == CircuitState.HalfOpen)
				{
					// Success in half-open state - close circuit
					_state = CircuitState.Closed;
					_halfOpenAttempts = 0;
				}
			}
			finally
			{
				_lock.Release();
			}
		}

		/// <summary>
		/// Records a failed operation. Only infrastructure errors (Unavailable, Timeout, Database, Unexpected)
		/// are counted towards circuit breaker threshold. Business/validation errors are ignored.
		/// </summary>
		/// <param name="error">The error that occurred</param>
		public async Task RecordFailureAsync(Error error)
		{
			// Only count infrastructure failures - ignore business/validation errors
			if (!IsInfrastructureError(error))
			{
				// Business errors don't affect circuit state
				return;
			}

			await _lock.WaitAsync().ConfigureAwait(false);
			try
			{
				_failureCount++;
				_lastError = error;

				if (_state == CircuitState.HalfOpen)
				{
					// Failure in half-open state - reopen circuit
					_state = CircuitState.Open;
					_openedAt = DateTime.UtcNow;
					_halfOpenAttempts = 0;
				}
				else if (_state == CircuitState.Closed && _failureCount >= _failureThreshold)
				{
					// Exceeded threshold - open circuit
					_state = CircuitState.Open;
					_openedAt = DateTime.UtcNow;
				}
			}
			finally
			{
				_lock.Release();
			}
		}

		/// <summary>
		/// Determines if an error represents an infrastructure failure that should affect circuit breaker state
		/// </summary>
		private static bool IsInfrastructureError(Error error)
		{
			return error.Type == ErrorType.Unavailable
				|| error.Type == ErrorType.Timeout
				|| error.Type == ErrorType.Database
				|| error.Type == ErrorType.Unexpected;
		}

		/// <summary>
		/// Resets the circuit breaker to closed state
		/// </summary>
		public async Task ResetAsync()
		{
			await _lock.WaitAsync().ConfigureAwait(false);
			try
			{
				_state = CircuitState.Closed;
				_failureCount = 0;
				_halfOpenAttempts = 0;
				_lastError = null;
				_openedAt = DateTime.MinValue;
			}
			finally
			{
				_lock.Release();
			}
		}

		/// <summary>
		/// Disposes resources used by the circuit breaker
		/// </summary>
		public void Dispose()
		{
			_lock?.Dispose();
		}
	}
}
