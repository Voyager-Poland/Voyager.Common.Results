# ADR-0002: Add net6 Target and Disable Implicit Usings

## Status

Accepted - December 2025

## Context

- Several consumer applications are still on .NET 6 and cannot move to .NET 8 yet.
- Library previously targeted net8.0 and net48 only, relying on implicit usings for net8.
- Enabling net6 surfaced missing namespace issues when implicit usings were disabled or absent.
- We need consistent compilation across all target frameworks without relying on SDK defaults that differ by TFM.

## Decision

- Add `net6.0` to the target frameworks list alongside `net8.0` and `net48`.
- Disable `ImplicitUsings` globally and provide explicit global usings for net6+/net8 builds.
- Keep `Nullable` enabled across all targets.

## Rationale

- Compatibility: Support teams running .NET 6 while we keep .NET 8 as current LTS and .NET 4.8 for legacy.
- Predictability: Explicit global usings avoid TFM-specific defaults and reduce hidden dependencies.
- Maintainability: A single global usings file per project keeps the namespace surface clear and consistent.
- Build Reliability: Prevents missing `System`, `System.Linq`, `System.Threading.Tasks`, etc., when implicit usings differ between SDK versions.

## Consequences

- Build matrix expands (net48, net6.0, net8.0) increasing CI time and package size.
- Must maintain explicit global usings; new files should rely on them instead of implicit usings.
- Consumers on .NET 6 remain supported; future drop of net6 will require a follow-up ADR.
- Stronger guarantees that code compiles identically across TFMs without silent namespace differences.
