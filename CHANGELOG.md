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
