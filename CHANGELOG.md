# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2025-01-XX

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
- Comprehensive unit tests

### Supported Frameworks
- .NET Framework 4.8
- .NET 8.0

[Unreleased]: https://github.com/Voyager-Poland/Voyager.Common.Results/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/Voyager-Poland/Voyager.Common.Results/releases/tag/v1.0.0
