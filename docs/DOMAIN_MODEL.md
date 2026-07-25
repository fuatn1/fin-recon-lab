# Domain Model

This document defines the initial conceptual domain for FinReconLab. It also notes the currently implemented subset without defining a storage schema.

## Reconciliation Concepts

### Scenario Definition

A versioned synthetic scenario configuration that defines transaction shape, event counts, currencies, seeds, fault rules, and reconciliation configuration.

The implemented schema version is `payment-captured.v1`. It supports deterministic `PaymentCaptured` truth-stream generation using a scenario identifier, unsigned seed, payment count, payment amount, starting timestamp, and positive event interval. The v1 seed is an identity namespace and does not implement statistical randomness or distributions.

### Truth Event Stream

The clean deterministic event stream generated from a Scenario Definition before fault injection. It is the source used to derive Expected State.

The implemented truth-stream generator currently emits ordered `PaymentCaptured` events only.

### Expected State

The financial state that should exist after applying configured rules and invariants to truth events included by the cutoff. The implemented `ExpectedPaymentSnapshot` carries ordered `ExpectedPaymentContribution` evidence containing source event id, source logical sequence, and applied clean captured amount.

### Fault Injector

The component that transforms the Truth Event Stream into a Delivered Event Stream by applying configured delivery faults. Duplicate, missing, delayed, out-of-order, and caller-supplied inconsistent-amount `PaymentCaptured` delivery faults are implemented. Broader event and fault types remain planned.

### Delivered Event Stream

The faulted event stream visible to the observed-side projection. Each delivered payment preserves its clean immutable `PaymentCaptured` source event and separately exposes `DeliveredCapturedAmount`. Normal delivery uses the source amount; inconsistent-amount delivery changes only the selected delivered amount. Delayed delivery moves the observed delivery position forward on the synthetic sequence axis, and out-of-order delivery swaps two baseline delivery positions on that axis. None of these behaviors uses real waiting or random corruption.

### Observed State

The financial state projected from each included delivered event's `DeliveredCapturedAmount` up to the configured Reconciliation Cutoff. The implemented `ObservedPaymentSnapshot` carries ordered `ObservedPaymentContribution` evidence containing source event id, source logical sequence, delivery sequence, delivery attempt, and applied delivered captured amount. Expected State continues to use the clean truth-event amount.

### Fault Manifest

Oracle data emitted by the Fault Injector describing injected faults. The Reconciliation Engine must never receive or access the Fault Manifest. Tests and the Benchmark Evaluator may use it after reconciliation completes. The implemented manifest uses explicit duplicate-delivery, missing-delivery, delayed-delivery, out-of-order delivery, and inconsistent-amount delivery entry records rather than nullable placeholder fields. The inconsistent-amount entry records exact original and delivered `Money` values in the same currency.

### Reconciliation Cutoff

A deterministic logical boundary for a reconciliation run. In the current v0.1 payment slices, baseline delivered events use the source event `LogicalSequence` as `DeliverySequence`, so the same numeric sequence boundary can align Expected State and Observed State. Missing delivery preserves the sequence gap, delayed delivery moves the observed delivery position forward, and out-of-order delivery swaps two baseline delivery positions on the same synthetic sequence axis. Independent truth and transport timelines may require separate cutoff semantics in a future version.

### Reconciliation Run

A deterministic execution that compares Expected State and Observed State for a defined Scenario Definition, seed, Reconciliation Cutoff, and configuration.

### Reconciliation Findings

Stable reconciliation output describing classified differences between Expected State and Observed State. The implemented payment slice reports captured-amount mismatches and carries immutable expected and observed contribution traces copied from the validated role-specific snapshots. Explicit fault attribution and missing-record classification remain planned. The Reconciliation Engine does not read the Fault Manifest to construct trace evidence.

### Reconciliation Report

An immutable, versioned collection of deterministic reconciliation evidence. The implemented `reconciliation-report.v1` contract records the `payment-captured.v1` Scenario Definition, shared Reconciliation Cutoff, ordered expected and observed snapshot collections, ordered findings, and their ordered contribution traces. Its JSON representation has explicit property order, uses JSON numbers for `decimal` money amounts, formats the scenario starting timestamp with invariant round-trip formatting, and represents the event interval as ticks.

The report has no automatically generated timestamp and cannot contain the Fault Manifest, fault-injection results, or an inferred fault type. It is evidence of the compared state, not an explanation of which fault was injected.

## Planned Operational Concepts

The concepts in this section are planned product-direction terms. They do not yet have implemented domain or application types.

### Authoritative Source Adapter

A domain-specific, normally read-only adapter that provides ordered authoritative payment events from a database, retained event source, approved export, replay interface, or another source-specific implementation. It must define identity, ordering, money, and high-watermark semantics explicitly. An Event Store or broker is not required.

### Observed Projection Adapter

A domain-specific, normally read-only adapter that provides the payment projection state to compare. It must define how projection identities, amounts, currencies, versions, and observation boundaries map into reconciliation inputs. FinReconLab does not automatically understand arbitrary projection schemas.

### Versioned Reconciliation Definition

A planned identity and compatibility boundary for an operational reconciliation use case. It identifies the adapter and mapping contract version, expected-state reducer or rule version, partitioning semantics, and compatible checkpoint and expected-state namespace. The exact API and persistence schema remain future decisions. Persisted operational state must not be silently reused after an incompatible definition change.

### Reconciliation Partition

A bounded, explicitly identified unit of operational work. Partition key semantics, ownership, ordering, and isolation remain to be designed and tested.

### Source High-Watermark

An immutable boundary selected from the authoritative source for a bounded operational run. It limits the source events considered by that run. It does not prove that the observed projection has processed the same source range. It is distinct from a projection observation boundary, persisted worker progress, and the implemented v0.1 synthetic shared sequence cutoff.

### Projection Observation Boundary

Planned evidence that an observed projection read is comparable to the selected source high-watermark. The observed-projection adapter must establish that the projection processed through a comparable source position, provide an equivalent immutable as-of read, or apply an explicitly configured stabilization policy with recorded limitations. Wall-clock timestamps alone are insufficient.

If comparability cannot be established, a future tested contract must reject or defer the run, or explicitly mark it provisional. It must not silently publish a conclusive discrepancy.

### Incremental Expected State

FinReconLab-owned planned reducer state persisted by versioned reconciliation definition, partition, and business identity. Authoritative events update it in stable order during bounded processing. It allows later batches to retain prior deterministic aggregates without replaying complete history and remains distinct from the external observed projection. Expected-state persistence failure prevents worker-checkpoint advancement.

### Reconciliation Checkpoint

FinReconLab-owned durable progress for a partition and compatible reconciliation-definition namespace. A checkpoint records only safely completed work so processing can resume. Planned progress is monotonic and cannot advance after partial persistence failure. It is distinct from the source high-watermark, projection observation boundary, and v0.1 synthetic Reconciliation Cutoff.

### Operational Batch Identity

A planned deterministic identity for a reconciliation definition, partition, source range, projection observation boundary, and execution attempt semantics. Findings and versioned reports use a deterministic batch or run identity so replaying the same completed input cannot duplicate them.

### Batch Completion

A planned consistency boundary across incremental expected-state updates, findings, versioned reports, required batch metadata, and checkpoint advancement. A batch is complete only when all required FinReconLab-owned state is committed atomically or through a documented replay-safe idempotent protocol. Failure before completion leaves the batch safely repeatable from its prior checkpoint.

### Incremental Reconciliation Worker

A planned out-of-band orchestrator that reads bounded authoritative events and comparable observed projection state through adapters, updates incremental expected state, invokes the deterministic core, persists findings and reports, and advances checkpoints according to the batch-completion contract. It does not participate in production transaction completion.

### Operational Reconciliation State

FinReconLab-owned versioned reconciliation definitions, incremental expected state, checkpoints, findings, versioned reports, deterministic batch metadata, and required execution metadata. This state remains distinct from the external observed projection. Writes to authoritative sources or observed projections are outside the planned boundary unless a future accepted ADR states otherwise.

### Benchmark Evaluator

The evaluation component that may compare Reconciliation Findings with the Fault Manifest after reconciliation completes. It is separate from the Reconciliation Engine.

### Recovery Recommendation

A non-mutating recommendation describing a possible recovery action for a discrepancy. In v0.1, recommendations must not automatically modify data.

## Financial Concepts

### Merchant Transaction

The complete financial lifecycle for a merchant-facing transaction, including order, payment, refund, commission, shipping-fee, and derived financial records.

### Order

A synthetic commercial intent representing items or services purchased by a buyer from a merchant.

### Payment

A synthetic monetary event representing captured funds associated with an order. Payment authorization is outside the initial v0.1 scope.

### Refund

A synthetic monetary event representing funds returned or expected to be returned for a captured payment or transaction.

### Commission

A synthetic monetary amount representing a platform commission charged to the merchant.

### Shipping Fee

A synthetic monetary amount representing a delivery-related charge.

### Financial Record

A derived record representing an expected or observed financial effect, such as a payment, refund, commission, or fee entry.

## Initial Event Vocabulary

### OrderPlaced

Establishes transaction context. It does not by itself prove that money was captured.

### PaymentCaptured

Creates a financial effect for captured funds. Payment authorization is outside the initial v0.1 scope.

### RefundIssued

Creates a financial effect for returned funds and must reference the relevant captured payment or transaction.

### CommissionAssessed

Represents a platform commission charged to the merchant.

### ShippingFeeAssessed

Represents a delivery-related charge.

## Money Semantics

- Every monetary value must have an amount and explicit ISO 4217-style three-letter uppercase currency code.
- Complete ISO 4217 registry membership validation is not implemented in the current payment slice.
- Amounts in different currencies must never be added or compared as equivalent.
- Exchange-rate conversion is outside v0.1.
- Rounding and precision rules require a dedicated ADR before implementation.
- The initial implementation must not silently round monetary values.
- Commission, shipping fee, payment, and refund signs or debit/credit semantics must be explicitly defined before implementation.

## Initial Invariants

- Duplicate events must not produce duplicate financial effects.
- The same Scenario Definition, seed, Reconciliation Cutoff, and configuration must produce the same Reconciliation Findings.
- The same `payment-captured.v1` Scenario Definition must produce structurally identical truth events.
- The same report inputs must produce byte-for-byte identical `reconciliation-report.v1` UTF-8 JSON.
- Reconciliation output ordering must be stable.
- Every finding must be traceable to source events and delivered events.
- Every payment snapshot contribution must use the snapshot currency, and contribution totals must equal the aggregate captured amount.
- Recovery recommendations must not mutate data automatically in v0.1.
- Expected State and Observed State must be compared using explicit transaction identity.
- Events beyond the configured Reconciliation Cutoff must not affect the corresponding Expected State or Observed State projection for that run.
