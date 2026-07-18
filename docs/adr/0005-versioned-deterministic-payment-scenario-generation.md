# ADR-0005: Versioned Deterministic Payment Scenario Generation

## Status

Accepted

## Context

FinReconLab needs repeatable synthetic truth streams before broader reconciliation scenarios can be implemented. The first generator should be narrow enough to validate deterministic `PaymentCaptured` event creation without introducing a generic scenario framework, statistical distributions, or additional event types.

## Decision

The project defines `ScenarioDefinition` schema version `payment-captured.v1` for deterministic `PaymentCaptured` truth-stream generation.

The v1 definition includes:

- Exact schema version.
- Scenario identifier.
- Unsigned seed.
- Payment count with an upper bound of 10,000 events.
- Payment amount.
- Starting timestamp supplied by the caller.
- Positive event interval supplied by the caller.

The generator creates event ids and order ids from the scenario identifier, seed, and one-based ordinal using invariant formatting. Logical sequences are contiguous and one-based. Timestamps are derived only from the supplied starting timestamp and interval.

The complete generated timestamp range is validated when the scenario definition is constructed.

In v1, the seed acts only as a deterministic identity namespace. It does not implement statistical randomness, sampling, or distributions.

## Consequences

Identical definitions produce structurally identical truth streams. The generated events can be used by the existing expected projection, duplicate-delivery fault injector, observed projection, and reconciliation engine without giving the reconciliation engine access to oracle data.

Future scenario versions can add additional event types or distribution semantics only after explicit design and tests.
