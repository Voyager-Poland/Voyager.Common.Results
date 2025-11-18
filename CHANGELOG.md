# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- Fixed CI/CD pack command to target only the main project (`src/Voyager.Common.Results/Voyager.Common.Results.csproj`)
- Fixed IDE0036 code analyzer error: Corrected modifier ordering from `public new static` to `public static new` in `ResultT.cs`
- Fixed duplicate `IsExternalInit` package reference issue

### Changed
- Updated GitHub Actions workflow to pack only the library project, not test projects
- Enhanced MinVer versioning documentation in `docs/QUICK-START-VERSIONING.md`
- Updated build documentation in `BUILD.md` with improved MinVer guidance
- Improved AI coding instructions in `.github/copilot-instructions.md` with latest patterns

### Technical
- Migrated to MinVer-based Git tag versioning system
- Added ADR-001 documenting MinVer Git-based versioning strategy with Major-only AssemblyVersion approach
- Updated CI workflow to use artifacts for better package handling between jobs

## [1.2.0] - 2025-01-15

### Added
- New error types for better error categorization:
  - `UnavailableError` - for temporary service unavailability (rate limiting, maintenance, circuit breaker)
  - `TimeoutError` - for operation timeouts (HTTP, database, gateway timeouts)
- Factory methods for new error types:
  - `Error.UnavailableError(string message)`
  - `Error.UnavailableError(string code, string message)`
  - `Error.TimeoutError(string message)`
  - `Error.TimeoutError(string code, string message)`
- Comprehensive documentation for new error types in `docs\error-types.md`
- Real-world examples for timeout and unavailability scenarios
- 4 new unit tests for `UnavailableError` and `TimeoutError`
- HTTP status code mapping guidance (503, 429, 408, 504)
- Exception to error type mapping table
- Circuit breaker pattern example with `UnavailableError`
- Retry logic pattern for transient errors

### Changed
- Updated `ErrorType` enum with `Unavailable` and `Timeout` values
- Enhanced error type decision tree in documentation
- Improved best practices for error type selection

### Technical
- Upgraded C# language version to 10.0 for .NET Framework 4.8 projects
- Added `IsExternalInit` package for C# 10 record support on .NET Framework 4.8
- Fixed CS8630 error (nullable reference types require C# 8.0+)
- Fixed CS8773 error (file-scoped namespaces and global usings require C# 10.0+)

## [1.1.0] - 2025-01-13

### Added
- `OrElse` methods for fallback pattern with lazy evaluation
- `OrElseAsync` methods for async fallback operations (4 overloads)
- Comprehensive documentation for OrElse pattern in `docs\orelse-pattern.md`
- Real-world examples for multi-tier caching, resilient APIs, and configuration hierarchies
- 16 new unit tests for OrElse/OrElseAsync patterns

## [1.0.0] - 2025-01-10

### Added
- Initial release of Voyager.Common.Results
- `Result<T>` type for functional error handling
- `Result` (non-generic) type for void operations
- `Error` type with multiple error categories:
  - ValidationError
  - NotFoundError
  - PermissionError
  - ConflictError
  - DatabaseError
  - BusinessError
  - UnexpectedError
- Railway Oriented Programming support:
  - `Map` - transform success values
  - `Bind` - chain operations returning Result
  - `Tap` - side effects without changing Result
  - `Ensure` - validation with predicates
  - `Match` - pattern matching
  - `Switch` - void pattern matching
- Async extensions:
  - `MapAsync`
  - `BindAsync`
  - `TapAsync`
  - `EnsureAsync`
- Collection extensions:
  - `Combine` - merge multiple Results
  - `Partition` - split into successes and failures
  - `GetSuccessValues` - extract all success values
  - `GetErrors` - extract all errors
  - `AllSuccess` / `AnySuccess` - check collection state
- Implicit conversions for ergonomic API
- Multi-targeting support for .NET Framework 4.8 and .NET 8
- Full XML documentation
- Source Link support for debugging
- Comprehensive unit tests (212 tests)

### Supported Frameworks
- .NET Framework 4.8
- .NET 8.0

[Unreleased]: https://github.com/Voyager-Poland/Voyager.Common.Results/compare/v1.2.7...HEAD
[1.2.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.2.0
[1.1.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.1.0
[1.0.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.0.0
