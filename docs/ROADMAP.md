# Roadmap

This roadmap lists milestones without dates. Capabilities remain planned until implemented, tested, and documented.

## v0.1: Deterministic Reconciliation Core

Objective: provide a small deterministic reconciliation workflow using synthetic data and no external infrastructure requirement.

Current implemented subset: versioned `PaymentCaptured` scenario generation, duplicate `PaymentCaptured` delivery injection, missing `PaymentCaptured` delivery injection, delayed `PaymentCaptured` delivery injection, out-of-order `PaymentCaptured` delivery injection, caller-supplied inconsistent-amount `PaymentCaptured` delivery injection, role-specific expected and observed payment projections with contribution evidence, logical cutoff handling, traceable captured-amount mismatch reconciliation, and deterministic `reconciliation-report.v1` JSON output.

Acceptance criteria:

- A versioned Scenario Definition exists for the implemented `PaymentCaptured` slice.
- A `PaymentCaptured` Truth Event Stream can be generated deterministically.
- Expected State can be built separately from Observed State.
- Fault injection is deterministic.
- The Fault Injector produces a Delivered Event Stream and Fault Manifest.
- The Fault Manifest is inaccessible to the Reconciliation Engine.
- A logical Reconciliation Cutoff defines which truth and delivered events are included in the current payment projections.
- Reconciliation Findings carry stable, immutable expected source-event contributions and observed delivered-event contributions.
- A versioned reconciliation report serializes scenario configuration, cutoff, ordered snapshots, findings, and contribution traces deterministically without Fault Manifest access.
- Repeatability tests prove that identical scenario, seed, cutoff, and configuration inputs produce identical findings.
- Identical report inputs produce byte-for-byte identical UTF-8 JSON.
- v0.1 has no external infrastructure requirement.

## v0.2: Event Broker And Persistence Integration

Objective: integrate event broker and persistence components while preserving deterministic core behavior.

Acceptance criteria:

- PostgreSQL persistence is introduced behind clear interfaces.
- RabbitMQ with MassTransit is introduced for broker-based scenarios.
- Infrastructure integration tests cover broker and persistence behavior.
- Deterministic reconciliation logic remains testable without infrastructure.
- Documentation describes local execution requirements and limitations.

## v0.3: Observability And Repeatable Benchmarks

Objective: add observability and repeatable benchmark workflows.

Acceptance criteria:

- OpenTelemetry instrumentation is available for key processing stages.
- BenchmarkDotNet scenarios record configured measurements.
- Benchmark output includes hardware, runtime, configuration, seed, cutoff, and raw results.
- Functional evaluation uses the Fault Manifest only after reconciliation completes.
- Repeated benchmark runs with fixed inputs produce consistent Reconciliation Findings.
- Documentation distinguishes measured results from expected or planned behavior.

## v0.4: Explainable Anomaly Assistance

Objective: explore anomaly-assistance features only after deterministic baselines exist.

Acceptance criteria:

- Deterministic baseline reconciliation remains the source of truth.
- Assistance features explain or prioritize discrepancies without replacing deterministic findings.
- Evaluation scenarios use synthetic data only.
- Documentation clearly identifies limitations and avoids unsupported accuracy claims.
