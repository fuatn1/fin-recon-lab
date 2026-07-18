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

The financial state that should exist after applying configured rules and invariants to the Truth Event Stream.

### Fault Injector

The component that transforms the Truth Event Stream into a Delivered Event Stream by applying configured delivery faults. Duplicate, missing, and delayed `PaymentCaptured` delivery faults are implemented. Out-of-order delivery, inconsistent-amount faults, and broader event faults remain planned.

### Delivered Event Stream

The faulted event stream visible to the observed-side projection. Delayed delivery is represented by moving the observed delivery position forward on the synthetic sequence axis, not by real waiting.

### Observed State

The financial state projected from the Delivered Event Stream up to the configured Reconciliation Cutoff.

### Fault Manifest

Oracle data emitted by the Fault Injector describing injected faults. The Reconciliation Engine must never receive or access the Fault Manifest. Tests and the Benchmark Evaluator may use it after reconciliation completes. The implemented manifest uses explicit duplicate-delivery, missing-delivery, and delayed-delivery entry records rather than nullable placeholder fields.

### Reconciliation Cutoff

A deterministic logical boundary for a reconciliation run. In the current v0.1 payment slices, baseline delivered events use the source event `LogicalSequence` as `DeliverySequence`, so the same numeric sequence boundary can align Expected State and Observed State. Missing delivery preserves the sequence gap, and delayed delivery moves the observed delivery position forward on the same synthetic sequence axis. Independent truth and transport timelines may require separate cutoff semantics in a future version.

### Reconciliation Run

A deterministic execution that compares Expected State and Observed State for a defined Scenario Definition, seed, Reconciliation Cutoff, and configuration.

### Reconciliation Findings

Stable reconciliation output describing classified differences between Expected State and Observed State. The implemented payment slice currently reports captured-amount mismatches; explicit missing-record classification remains planned. Findings must be traceable to source events and delivered events as the model expands.

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
- Reconciliation output ordering must be stable.
- Every finding must be traceable to source events and delivered events.
- Recovery recommendations must not mutate data automatically in v0.1.
- Expected State and Observed State must be compared using explicit transaction identity.
- Events beyond the configured Reconciliation Cutoff must not affect the corresponding Expected State or Observed State projection for that run.
