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

FinReconLab does not yet solve the full set of problems described here. The current repository includes project documentation and a narrow deterministic duplicate-payment reconciliation slice that demonstrates expected-state versus observed-state comparison for one synthetic failure mode.
