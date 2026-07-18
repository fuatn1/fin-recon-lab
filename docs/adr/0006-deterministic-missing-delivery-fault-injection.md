# ADR-0006: Deterministic Missing Delivery Fault Injection

## Status

Accepted

## Context

FinReconLab already has deterministic `PaymentCaptured` truth-stream generation and duplicate-delivery fault injection. The next v0.1 slice needs to model a missing delivered payment without adding new event types, infrastructure, statistical randomness, or a dedicated missing-record finding category.

## Decision

The project adds a deterministic missing-delivery fault injector for `PaymentCaptured` events.

The injector accepts a caller-supplied fault id and source event id, validates them before injection, sorts the truth stream by logical sequence and event id using ordinal comparison, and removes exactly the selected source event from the delivered stream. Remaining events are converted to delivered events with their original logical sequence as the delivery sequence and delivery attempt `1`. Delivery sequence gaps are preserved and never renumbered.

The Fault Manifest model is refactored into explicit entry types:

- `DuplicateDeliveryFaultManifestEntry` includes the duplicate delivery sequence.
- `MissingDeliveryFaultManifestEntry` has no delivery sequence, nullable sentinel, or catch-all field.

The Reconciliation Engine continues to receive only expected and observed snapshots. It does not receive the Fault Manifest or fault-injection result types.

The current missing-delivery slice uses the existing `CapturedAmountMismatch` finding when expected and observed captured totals differ. It does not implement a separate missing-payment or missing-record classification.

In the current v0.1 payment slices, baseline delivered events use the source event `LogicalSequence` as `DeliverySequence`. The same numeric sequence boundary can therefore align Expected State and Observed State, while missing delivery preserves the sequence gap. Independent truth and delivery timelines may require separate cutoff semantics in a future version. ADR-0004 records the original cutoff and manifest-isolation decision.

## Consequences

Duplicate and missing `PaymentCaptured` delivery faults now share a typed manifest abstraction while preserving oracle isolation from reconciliation. Repeated runs with structurally identical truth streams, missing fault requests, cutoffs, and configuration produce structurally identical delivered streams, manifests, and findings.

Future fault types can add their own manifest entry records without introducing nullable placeholder fields into existing entries.
