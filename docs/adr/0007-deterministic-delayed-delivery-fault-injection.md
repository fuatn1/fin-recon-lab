# ADR-0007: Deterministic Delayed Delivery Fault Injection

## Status

Accepted

## Context

FinReconLab now has deterministic `PaymentCaptured` truth-stream generation plus duplicate and missing delivery fault injection. The next v0.1 slice needs to represent delayed delivery without timers, sleeps, retries, wall-clock waiting, transport infrastructure, or a new event vocabulary.

## Decision

The project adds a deterministic delayed-delivery fault injector for `PaymentCaptured` events.

The injector accepts a caller-supplied fault id, source event id, and delayed delivery sequence. It validates all inputs before producing a result, sorts truth events by logical sequence and event id using ordinal comparison, rejects duplicate source event identities, rejects unknown source event ids, and rejects delayed delivery sequences that are negative, at or before the source event baseline delivery sequence, or colliding with another event's baseline delivery sequence.

Non-selected events are converted to baseline delivered events with `DeliverySequence` equal to the source event `LogicalSequence` and `DeliveryAttempt` equal to `1`. The selected event is delivered exactly once with the requested delayed delivery sequence and `DeliveryAttempt` equal to `1`. No event is silently renumbered.

The Fault Manifest includes a strongly typed `DelayedDeliveryFaultManifestEntry` with:

- `FaultId`
- `SourceEventId`
- `OriginalDeliverySequence`
- `DelayedDeliverySequence`

The delayed manifest entry validates that the delayed delivery sequence is strictly greater than the original delivery sequence. It does not use nullable placeholder fields or catch-all metadata.

The Reconciliation Engine continues to receive only expected and observed snapshots. It does not receive the Fault Manifest or any fault-injection result type.

## Cutoff Semantics

The current v0.1 payment slices use a synthetic shared logical sequence axis. Baseline delivered events use the source event `LogicalSequence` as `DeliverySequence`. A delayed delivery moves the observed delivery position forward on that same axis.

For a `PaymentCaptured` event with `LogicalSequence` `2` delayed to `DeliverySequence` `4`, `ReconciliationCutoff(3)` includes the event in Expected State and excludes it from Observed State, producing a captured-amount mismatch. `ReconciliationCutoff(4)` includes the delayed delivery in Observed State and removes that mismatch.

ADR-0004 records the original cutoff and manifest-isolation decision. Independent truth and transport timelines may require separate cutoff semantics in a future version.

## Consequences

Duplicate, missing, and delayed `PaymentCaptured` delivery faults now share a typed manifest abstraction while preserving oracle isolation from reconciliation.

Out-of-order delivery, inconsistent-amount delivery, explicit missing-record classification, broader event types, infrastructure integration, benchmarks, and production readiness remain planned.
