# Problem Statement

Event-driven financial transaction systems often split work across handlers for orders, payments, refunds, commissions, shipping fees, projections, notifications, and reporting. Each handler can process its own message successfully while the combined financial state becomes inconsistent.

## Failure Modes

Duplicate delivery can cause the same business event to be processed more than once. If processing is not idempotent, duplicated events can create duplicated financial effects.

Missing delivery can leave a workflow with incomplete financial records. For example, an order event may exist while the corresponding payment or refund event never reaches a downstream projection.

Retries can recover from transient failures, but they can also expose non-idempotent behavior when a side effect succeeds before the retry is triggered.

Partial failure can leave one part of a workflow committed while another part fails. A payment record, commission record, or refund projection may be persisted without all related records being updated.

Out-of-order delivery can make projections observe events before their prerequisites. A refund may be seen before the original payment, or a fee adjustment may arrive before the order state it depends on.

Inconsistent monetary values can appear when related events disagree about amount, currency, fee, commission, tax, or refund totals. Even small differences need explicit handling in financial records.

Projection drift can occur when derived read models, ledgers, reports, or caches no longer match the event stream or expected state because of missed updates, faulty replay, schema changes, or historical bugs.

## Why Logs Are Not Enough

Logs are important for investigation, but logs alone are not equivalent to deterministic financial reconciliation. Logs may be incomplete, unstructured, sampled, duplicated, or tied to implementation details instead of domain invariants. They often describe what a component observed, not what the complete financial state should be.

A deterministic reconciliation process needs explicit inputs, stable configuration, defined invariants, source-event traceability, repeatable classification, and comparable output across repeated runs.

## Current Project Position

FinReconLab does not yet solve the full set of problems described here. The current repository implements a narrow deterministic `PaymentCaptured` core with versioned scenario generation, duplicate, missing, delayed, out-of-order, and inconsistent-amount delivery faults, expected and observed projections, logical cutoff handling, traceable findings, and deterministic `reconciliation-report.v1` JSON. This tested synthetic foundation is reusable core behavior, not an operational service.

## Planned Operational Problem

The planned operational reference path addresses projection integrity outside the production transaction hot path. It is intended to incrementally derive expected payment state from an authoritative source, compare it with an observed payment projection, persist explainable findings and versioned reports, and resume safely from explicit checkpoints.

The authoritative source may be a database, retained event source, approved export, replay interface, or another source-specific implementation. It is not assumed to be an Event Store or broker. The observed projection also requires a domain-specific read adapter. FinReconLab cannot automatically understand arbitrary projections without explicit identity, ordering, money, and state-mapping semantics.

A planned source high-watermark bounds authoritative input, while a separate projection observation boundary establishes whether the observed projection is comparable to that source range. Final findings require evidence that the projection processed through a comparable source position, can be read at an equivalent immutable boundary, or is covered by an explicitly configured stabilization policy. Without comparability, a future tested contract must reject or defer the run, or explicitly mark it provisional rather than publish a conclusive discrepancy.

The planned worker maintains FinReconLab-owned incremental expected state separately from the external observed projection so deterministic aggregates survive bounded batches. A versioned reconciliation definition identifies compatible adapter mappings, reducer rules, partition semantics, expected-state namespaces, and checkpoints. Expected-state updates, findings, versioned reports, deterministic batch metadata, and checkpoint advancement complete atomically or through a documented replay-safe idempotent protocol.

The planned toolkit does not replace either source, intercept transaction processing, infer injected fault categories from operational evidence, or automatically repair production state. Source and projection access should normally be read-only. Planned FinReconLab-owned persistence includes versioned reconciliation definitions, incremental expected state, checkpoints, findings, versioned reports, deterministic batch metadata, and other operational metadata.

No source or projection comparability contract, versioned operational reconciliation definition, incremental expected-state persistence, atomic or replay-safe batch completion, runnable worker, operational adapter, checkpoint persistence, partitioning, PostgreSQL integration, API, CLI, Docker environment, telemetry integration, benchmark result, external evaluation, or production-ready behavior exists yet.
