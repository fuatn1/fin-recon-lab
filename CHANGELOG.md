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
- xUnit coverage for money semantics, duplicate delivery, cutoff behavior, Fault Manifest isolation, and repeatability.
- ADRs for .NET 10 project boundaries, money semantics, and deterministic fault injection with manifest isolation.
- Versioned `payment-captured.v1` Scenario Definition and deterministic `PaymentCaptured` truth-stream generator.
