# ADR-0010: Deterministic Reconciliation Traceability

## Status

Accepted

## Context

FinReconLab can deterministically generate clean `PaymentCaptured` truth events, inject the implemented delivery faults, project expected and observed captured totals, and emit captured-amount mismatch findings. Aggregate amounts alone do not provide enough evidence to trace which included truth events formed Expected State or which included deliveries formed Observed State.

The Fault Manifest cannot supply this evidence to reconciliation. It is oracle data that describes injected faults, while trace evidence must describe the actual inputs applied by each projection without revealing or guessing the injected fault.

## Decision

The payment projections produce role-specific immutable snapshots:

- `ExpectedPaymentSnapshot` carries `ExpectedPaymentContribution` entries containing source event id, source logical sequence, and applied clean captured amount.
- `ObservedPaymentSnapshot` carries `ObservedPaymentContribution` entries containing source event id, source logical sequence, delivery sequence, delivery attempt, and applied delivered captured amount.

Expected contributions are built only from truth events included by the reconciliation cutoff and are ordered by source logical sequence followed by source event id using ordinal comparison. Observed contributions are built only from delivered events included by the cutoff and are ordered by delivery sequence, source event id using ordinal comparison, and delivery attempt.

Snapshot constructors defensively copy contribution inputs, expose read-only collections, reject contributions outside their role-specific cutoff sequence, require every contribution to use the snapshot currency, and require the contribution total to equal the aggregate captured amount. Empty contribution collections are valid only with a zero aggregate. Contribution records reject blank identities, negative sequences, non-positive delivery attempts, and null amounts.

Contribution collections use immutable structural value semantics. Snapshot and finding record equality compares every contribution in order, and their hash codes include the same ordered contribution values.

The Reconciliation Engine accepts `ExpectedPaymentSnapshot` and `ObservedPaymentSnapshot` explicitly. Every captured-amount mismatch finding carries independent read-only copies of both contribution collections. The observed evidence records `DeliveredCapturedAmount`, including caller-supplied inconsistent amounts, while expected evidence retains the clean truth-event amount.

Duplicate deliveries therefore appear as multiple observed contributions with distinct attempts. Missing deliveries are absent from observed contributions. Delayed and out-of-order deliveries retain their deterministic delivery positions. Inconsistent-amount delivery exposes the corrupted observed contribution beside the clean expected contribution.

The Reconciliation Engine does not accept or access the Fault Manifest, fault requests, injectors, or fault-injection results. Trace evidence does not identify a fault type and does not infer why expected and observed values differ. ADR-0004 records the original Fault Manifest isolation decision.

## Consequences

Captured-amount mismatch findings now contain deterministic evidence for the exact truth and delivered inputs included by their cutoff. Structurally identical inputs produce structurally identical contribution evidence and findings.

Explicit fault attribution, missing-record classification, broader discrepancy categories, additional event types, reports, infrastructure integration, benchmarks, deployment support, and production readiness remain planned.
