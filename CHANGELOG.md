# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to semantic versioning once releases begin.

## [Unreleased]

### Added

- Initial project foundation, public documentation structure, license, and engineering charter.
- .NET 10 solution foundation with Domain and Application projects.
- First deterministic duplicate-payment reconciliation vertical slice.
- Money, payment event, delivered event, reconciliation cutoff, snapshot, and finding domain types.
- Expected payment projection, deterministic duplicate-delivery fault injector, non-idempotent observed projection for the experiment, and payment reconciliation engine.
- Deterministic missing-delivery fault injector for `PaymentCaptured` events.
- Deterministic delayed-delivery fault injector for `PaymentCaptured` events.
- Deterministic out-of-order delivery fault injector for `PaymentCaptured` events.
- Deterministic inconsistent-amount delivery fault injector for `PaymentCaptured` events.
- Delivered payment payload amount separated from the preserved clean source-event amount.
- Role-specific expected and observed payment snapshots with immutable, structurally value-semantic contribution evidence.
- Captured-amount mismatch findings with deterministic source-event and delivered-event trace collections.
- Typed Fault Manifest entries for duplicate-delivery, missing-delivery, delayed-delivery, out-of-order delivery, and inconsistent-amount delivery faults.
- Single-source and pairwise Fault Manifest entry hierarchy.
- Reconciliation cutoff naming clarified around a shared sequence boundary for the current payment slices.
- xUnit coverage for money semantics, scenario generation, duplicate delivery, missing delivery, delayed delivery, out-of-order delivery, inconsistent-amount delivery, reconciliation traceability, cutoff behavior, Fault Manifest isolation, and repeatability.
- ADRs for .NET 10 project boundaries, money semantics, and deterministic fault injection with manifest isolation.
- Versioned `payment-captured.v1` Scenario Definition and deterministic `PaymentCaptured` truth-stream generator.
- ADR for deterministic missing-delivery fault injection.
- ADR for deterministic delayed-delivery fault injection.
- ADR for deterministic out-of-order delivery fault injection.
- ADR for deterministic inconsistent-amount delivery fault injection.
- ADR for deterministic reconciliation traceability.
- Versioned `reconciliation-report.v1` contract with immutable, ordered scenario-level reconciliation evidence.
- Deterministic UTF-8 JSON report serialization with explicit property, snapshot, finding, and contribution ordering.
- ADR for deterministic reconciliation reporting and continued Fault Manifest isolation.
- ADR-0012 documentation for the planned out-of-band boundary, comparable source and projection observations, versioned incremental expected state, replay-safe batch completion, and optional transport adapters.

### Changed

- Product direction realigned around an out-of-band operational reconciliation boundary while preserving the implemented v0.1 deterministic core.
- Outcome-led v0.2 through v0.5 roadmap for operational contracts, a synthetic PostgreSQL reference slice, scale and recovery evidence, and external technical validation.
