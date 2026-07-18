# ADR-0002: Use .NET 10 And Initial Project Boundaries

## Status

Accepted

## Context

FinReconLab now includes the first deterministic duplicate-payment reconciliation vertical slice. The implementation needs a small project structure that keeps domain logic separate from application orchestration while avoiding infrastructure projects before they are needed.

## Decision

The initial implementation targets .NET 10 and uses four projects:

- `FinReconLab.Domain` for deterministic domain values, events, snapshots, and findings.
- `FinReconLab.Application` for projections, deterministic fault injection, and reconciliation use cases.
- `FinReconLab.Domain.Tests` for domain behavior tests.
- `FinReconLab.Application.Tests` for vertical-slice behavior tests.

The first slice does not include API, database, broker, Docker, telemetry, benchmark, or infrastructure projects.

## Consequences

The deterministic core can be tested without external infrastructure. Later infrastructure integration can depend on the application/domain boundaries instead of changing the core reconciliation model.
