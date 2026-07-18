# ADR-0008: Deterministic Out-Of-Order Delivery Fault Injection

## Status

Accepted

## Context

FinReconLab has deterministic `PaymentCaptured` truth-stream generation plus duplicate, missing, and delayed delivery fault injection. The next v0.1 slice needs to represent out-of-order delivery without timers, retries, transport infrastructure, new event types, or a new cutoff model.

## Decision

The project adds a deterministic out-of-order delivery fault injector for `PaymentCaptured` events.

The injector accepts a caller-supplied fault id, earlier source event id, and later source event id. It validates all inputs before producing a result, materializes and sorts truth events by logical sequence and event id using ordinal comparison, rejects duplicate source event identities, rejects duplicate logical sequences, rejects unknown selected source event ids, rejects selecting the same source event twice, and verifies that the earlier source event has a lower logical sequence than the later source event.

Out-of-order delivery is represented by swapping two baseline delivery positions on the shared synthetic sequence axis:

- The earlier source event receives the later source event's baseline delivery sequence.
- The later source event receives the earlier source event's baseline delivery sequence.
- Every unaffected event keeps its baseline delivery sequence.
- Every delivered event keeps `DeliveryAttempt` equal to `1`.

No event is added, removed, duplicated, or silently renumbered.

The Fault Manifest hierarchy now distinguishes single-source fault entries from pairwise entries. Duplicate, missing, and delayed entries derive from `SingleSourceFaultManifestEntry`. `OutOfOrderDeliveryFaultManifestEntry` derives directly from `FaultManifestEntry` because it records a pair of source events.

The out-of-order manifest entry records:

- `FaultId`
- `EarlierSourceEventId`
- `LaterSourceEventId`
- `EarlierOriginalDeliverySequence`
- `EarlierDeliveredSequence`
- `LaterOriginalDeliverySequence`
- `LaterDeliveredSequence`

The manifest entry validates that source ids are nonblank and distinct, sequences are non-negative, the earlier original sequence is lower than the later original sequence, and the delivered sequences represent the exact swap.

The Reconciliation Engine continues to receive only expected and observed snapshots. It does not receive the Fault Manifest or any fault-injection result type.

## Cutoff Semantics

The current v0.1 payment slices use a synthetic shared logical sequence axis. Baseline delivered events use the source event `LogicalSequence` as `DeliverySequence`; out-of-order delivery swaps two positions on that same axis.

At an intermediate cutoff between the swapped source events, the earlier order can be expected but not yet observed, while the later order can be observed before it is expected. At a cutoff that includes both swapped positions, both order projections reconcile again.

ADR-0004 records the original cutoff and manifest-isolation decision. ADR-0007 records delayed delivery on the same synthetic sequence axis. Independent truth and transport timelines may require separate cutoff semantics in a future version.

## Consequences

Duplicate, missing, delayed, and out-of-order `PaymentCaptured` delivery faults now share explicit typed manifest entries while preserving oracle isolation from reconciliation.

Inconsistent-amount delivery, explicit missing-record classification, broader event types, infrastructure integration, benchmarks, deployment support, and production readiness remain planned.
