# ADR-0009: Deterministic Inconsistent Payment Amount Delivery

## Status

Accepted

## Context

FinReconLab has deterministic `PaymentCaptured` truth-stream generation plus duplicate, missing, delayed, and out-of-order delivery fault injection. The next v0.1 slice needs to represent a delivered payment amount that differs from clean scenario truth without mutating the source event, introducing random corruption, or adding infrastructure.

## Decision

The project adds a deterministic inconsistent-amount delivery fault injector for one selected `PaymentCaptured` source event.

The caller supplies the fault id, source event id, and exact corrupted `Money` value. The injector does not derive or randomize the amount. It materializes and deterministically orders the truth stream, rejects duplicate source event identities and an unknown selected identity, and preserves every source event exactly once with its baseline delivery sequence and delivery attempt `1`.

`DeliveredPaymentCaptured` preserves the original immutable `PaymentCaptured` as `SourceEvent` and separately exposes `DeliveredCapturedAmount`. Existing three-argument construction represents normal delivery and sets the delivered amount to `SourceEvent.CapturedAmount`. The explicit inconsistent-amount path changes only the selected event's delivered amount; the clean truth event and all unaffected delivered amounts remain unchanged.

The supplied delivered amount must use the same currency as the selected source amount and must differ from it. Currency comparison uses ordinal semantics. No rounding is introduced.

Expected State continues to use `PaymentCaptured.CapturedAmount` from the clean truth stream. Observed State uses `DeliveredPaymentCaptured.DeliveredCapturedAmount`. A cutoff before the selected event excludes both values and creates no premature mismatch. A cutoff including the selected event exposes the exact signed difference between the delivered and expected amounts.

The Fault Manifest contains one strongly typed `InconsistentAmountDeliveryFaultManifestEntry` with:

- `FaultId`
- `SourceEventId`
- `Kind = InconsistentAmountDelivery`
- `OriginalCapturedAmount`
- `DeliveredCapturedAmount`

The manifest entry directly rejects null amounts, mismatched currencies, and unchanged amounts. It does not use catch-all metadata or string-encoded monetary values.

The Reconciliation Engine continues to receive only expected and observed snapshots. It does not receive the Fault Manifest, fault request, injector, or fault-injection result. ADR-0004 records this oracle-isolation constraint.

## Consequences

Synthetic tests can now model deterministic higher or lower delivered payment amounts while retaining clean source-event traceability. Structurally identical truth streams, requests, cutoffs, and configuration produce structurally identical delivered streams, manifests, and findings.

Random corruption, statistical distributions, external infrastructure, additional event types, explicit missing-record classification, automated repair, benchmarks, deployment support, and production readiness remain outside this slice.
