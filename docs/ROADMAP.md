# Roadmap

This roadmap lists milestones without dates. Capabilities remain planned until implemented, tested, and documented.

## v0.1: Deterministic Reconciliation Core

Objective: provide a small deterministic reconciliation workflow using synthetic data and no external infrastructure requirement.

Acceptance criteria:

- A versioned Scenario Definition exists.
- A Truth Event Stream can be generated deterministically.
- Expected State can be built separately from Observed State.
- Fault injection is deterministic.
- The Fault Injector produces a Delivered Event Stream and Fault Manifest.
- The Fault Manifest is inaccessible to the Reconciliation Engine.
- A logical Reconciliation Cutoff defines which delivered events are included in Observed State.
- Reconciliation Findings are stable and traceable to source and delivered events.
- Repeatability tests prove that identical scenario, seed, cutoff, and configuration inputs produce identical findings.
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
