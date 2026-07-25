# Roadmap

This roadmap lists milestones without dates. Capabilities remain planned until implemented, tested, and documented.

## v0.1: Deterministic Reconciliation Core

Objective: provide a small deterministic reconciliation workflow using synthetic data and no external infrastructure requirement.

Status: implemented and tested.

Implemented outcomes: versioned `PaymentCaptured` scenario generation, duplicate `PaymentCaptured` delivery injection, missing `PaymentCaptured` delivery injection, delayed `PaymentCaptured` delivery injection, out-of-order `PaymentCaptured` delivery injection, caller-supplied inconsistent-amount `PaymentCaptured` delivery injection, role-specific expected and observed payment projections with contribution evidence, logical cutoff handling, traceable captured-amount mismatch reconciliation, and deterministic `reconciliation-report.v1` JSON output.

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

## v0.2: Operational Reconciliation Contracts

Status: planned.

Objective: define the smallest infrastructure-independent contracts needed to run the deterministic core incrementally against source and projection adapters.

Planned outcomes:

- Authoritative-source adapter contract.
- Observed-projection adapter contract.
- Explicit partition, bounded batch, source high-watermark, projection observation boundary, and worker checkpoint model.
- Projection comparability contract supporting comparable source-position evidence, an equivalent immutable as-of read, or an explicitly configured stabilization policy with recorded limitations.
- Rejection, deferral, or explicit provisional status when source and projection comparability cannot be established.
- Versioned reconciliation-definition contract covering use case, adapter and mapping version, expected-state reducer or rule version, partition semantics, and compatible persisted-state namespace.
- Incremental expected-state store or equivalent reducer-state contract, distinct from the external observed projection.
- Incremental reconciliation orchestration.
- Expected-state, finding, report, batch metadata, and checkpoint persistence contracts.
- Atomic or replay-safe idempotent batch-completion contract with monotonic checkpoint progress and deterministic batch or run identity.
- Deterministic behavior preserved independently of hosting, persistence, transport, and telemetry.
- In-memory test doubles and orchestration tests covering comparability, stable-order reduction, definition compatibility, partial persistence failure, resume boundaries, and repeatability.
- Explicit domain mapping requirements; no universal projection adapter claim.

## v0.3: Runnable PostgreSQL Reference Vertical Slice

Status: planned.

Objective: provide the first reproducible end-to-end operational workflow using PostgreSQL with only synthetic public data.

Planned outcomes:

- Synthetic PostgreSQL authoritative payment-event source.
- PostgreSQL observed payment projection with an explicit synthetic projection-processing checkpoint or equivalent source-position evidence; wall-clock time alone is insufficient.
- FinReconLab-owned PostgreSQL state for versioned reconciliation definitions, incremental expected state, worker checkpoints, findings, reports, deterministic batch metadata, and required operational metadata.
- Deterministic expected-state reduction by reconciliation definition, partition, and business identity, with authoritative events applied in stable order.
- Explicit compatibility validation before a checkpoint or expected-state namespace is reused with a reconciliation definition.
- Atomic PostgreSQL batch completion or a documented replay-safe idempotent protocol covering expected-state updates, findings, reports, batch metadata, and checkpoint advancement.
- Monotonic checkpoints that never advance after partial persistence failure.
- Deterministic batch or run identity so replay does not duplicate findings or reports.
- Failure-before-completion tests proving that the same batch remains safely repeatable from its known checkpoint.
- Resumable out-of-band worker.
- Testcontainers integration tests.
- Docker Compose reference environment.
- A small CLI or operational API exposing one implemented end-to-end workflow.
- A documented clone-to-first-result path only after that workflow is implemented and verified.
- Normally read-only source and projection adapters.

RabbitMQ with MassTransit is not required for this milestone. It may be introduced later as an optional transport adapter only after an accepted ADR justifies its operational role.

## v0.4: Scale, Recovery, And Observability

Status: planned.

Objective: make the reference workflow measurable, resumable, and diagnosable under reproducible synthetic load.

Planned outcomes:

- Scale validation of bounded batch processing across documented event volumes, batch sizes, and partition counts.
- Advanced partition isolation, ownership, leases, and concurrent worker coordination.
- Broader restart, partial-failure, and checkpoint recovery testing beyond the foundational v0.3 batch-completion guarantees.
- Scale and performance evaluation of replay-safe persistence.
- OpenTelemetry traces, metrics, and logs.
- Reproducible BenchmarkDotNet or end-to-end benchmark harness.
- Published raw benchmark evidence with runtime, hardware, storage, configuration, partition, batch, and dataset information.
- Clear separation of functional correctness from performance measurements.
- No performance, scale, or recovery claims without published evidence.

## v0.5: Release And External Validation

Status: planned.

Objective: package a reproducible preview and obtain independent technical evidence about installation and operational usefulness.

Planned outcomes:

- Versioned preview release.
- Reproducible public synthetic sample dataset.
- Independent installation or reproducibility feedback.
- At least one actual independently documented outcome: an external installation or reproducibility report, an independent technical evaluation, or a documented pilot outcome.
- Maintained issue and contribution workflow.
- Explicit release limitations and production-readiness criteria.

External validation is not measured by GitHub stars, and no users, adoption, evaluations, or pilots will be claimed before evidence exists.
