# Project Charter

## Project Mission

FinReconLab is intended to become an open-source, out-of-band projection-integrity and deterministic reconciliation toolkit for event-driven financial transaction systems.

## Target Users

- Backend engineers building transactional workflows.
- Platform engineers supporting event-driven systems.
- Fintech engineering teams evaluating reconciliation patterns.
- E-commerce engineering teams modeling order, payment, refund, fee, and commission flows.
- Reliability engineers working with repeatable failure scenarios.

## Product Boundary

Event-driven financial systems can produce inconsistent states even when individual event handlers complete successfully. Duplicate delivery, missing delivery, retries, partial failures, delayed messages, out-of-order processing, inconsistent monetary values, and projection drift can cause Expected State and Observed State to diverge.

The implemented v0.1 core separates clean synthetic scenario truth from faulted observations:

- A Scenario Definition produces a Truth Event Stream.
- The Truth Event Stream produces Expected State.
- The Fault Injector transforms the Truth Event Stream into a Delivered Event Stream and emits a Fault Manifest.
- The Delivered Event Stream produces Observed State up to a Reconciliation Cutoff.
- The Reconciliation Engine compares Expected State and Observed State to produce Reconciliation Findings.
- A planned future Benchmark Evaluator may compare Reconciliation Findings with the Fault Manifest after reconciliation completes.

The Reconciliation Engine must never receive or access the Fault Manifest.

The planned operational reference path reuses this deterministic core outside the production transaction hot path. It incrementally compares expected state derived from an authoritative payment-event source with an observed payment projection, persists explainable findings and versioned reports, and resumes from explicit checkpoints.

The operational boundary must:

- Read an authoritative source through a domain-specific adapter.
- Read an observed projection through a separate domain-specific adapter.
- Avoid replacing, intercepting, or controlling the authoritative source, production projections, consumers, or transaction processing.
- Avoid assuming that an Event Store or broker is the authoritative historical source.
- Keep source and projection access normally read-only.
- Require a projection observation boundary comparable to the selected source high-watermark before treating findings as final. If comparability cannot be established, a future tested contract must reject, defer, or explicitly mark the run provisional.
- Persist FinReconLab-owned incremental expected state separately from the external observed projection so bounded batches do not discard prior deterministic aggregates.
- Namespace checkpoints and expected state by an explicit versioned reconciliation definition covering use case, adapter mapping, reducer rules, and partition semantics.
- Write only FinReconLab-owned expected state, checkpoints, findings, reports, batch metadata, and operational state unless a future accepted ADR defines another behavior.
- Complete each batch atomically or through a documented replay-safe idempotent protocol so checkpoint progress never advances after partial persistence failure.
- Preserve deterministic core behavior independently of hosting, persistence, brokers, telemetry, and serialization infrastructure.

## Why The Problem Matters Technically

Financial workflows often require deterministic answers to questions such as what should have happened, what actually happened, which source and delivered events contributed to a difference, and what recovery action should be considered. Logs and handler success indicators are useful but do not replace a reproducible reconciliation process with explicit invariants, cutoff semantics, source-event traceability, and stable outputs.

## Project Hypotheses

- Synthetic Scenario Definitions with controlled fault injection can make reconciliation failure modes reproducible.
- Separating Truth Event Stream, Delivered Event Stream, Expected State, and Observed State reduces ambiguity in reconciliation design.
- Discrepancy classification is more reliable when every finding is traceable to source events, delivered events, and explicit invariants.
- Domain-specific adapters can map source and projection schemas into explicit reconciliation contracts without claiming universal compatibility.
- Bounded, checkpointed processing can make large operational histories resumable while retaining deterministic behavior within each partition and high-watermark.
- Explicit source and projection observation boundaries can distinguish normal projection lag from conclusive reconciliation evidence.
- Versioned incremental expected state and replay-safe batch completion can preserve correctness across restarts and rule changes.
- Recovery recommendations, if introduced, should remain separate from automatic mutation.

## Measurable Outcomes For Future Releases

Functional evaluation should measure true positives, false positives, false negatives, recall, precision when applicable, and classification accuracy for known injected discrepancy categories. Performance benchmarking should separately measure total processed events, reconciliation duration, throughput, and p50 and p95 latency.

No benchmark or evaluation results are available in the current phase.

## Implemented v0.1 Foundation

v0.1 implements a deterministic `PaymentCaptured` reconciliation core using synthetic data and no external infrastructure. It includes versioned scenario generation, truth and delivered streams, five deterministic delivery fault types, role-specific projections, logical cutoff handling, stable findings with contribution traces, and deterministic `reconciliation-report.v1` JSON.

It does not include operational adapters, checkpointing, persistence, partitioning, a worker, an API, a CLI, PostgreSQL, Docker Compose, OpenTelemetry, benchmark results, or a deployable service.

## Explicit Non-Goals

- Building a production financial-processing system.
- Processing real financial, personal, merchant, or customer data.
- Automatic data mutation or repair in v0.1.
- Replacing the authoritative event source or existing projections.
- Intercepting production transaction processing.
- Automatically interpreting arbitrary projection schemas.
- Exchange-rate conversion in v0.1.
- Distributed microservice decomposition unless needed for a measurable experiment.
- Claims about performance, accuracy, adoption, or impact before evidence exists.

## Risks And Limitations

- Synthetic scenarios may not capture every production failure mode.
- Every operational source and projection requires a domain-specific adapter and explicit semantics.
- Deterministic baselines can be incomplete if domain invariants are poorly specified.
- A missing event can only be classified relative to a defined Reconciliation Cutoff.
- Reconciliation reports may be technically correct but operationally insufficient without review workflows.
- Projection comparability, reconciliation-definition compatibility, expected-state persistence, batch consistency, checkpoint correctness, partition ownership, and restart recovery remain planned and unproven.
- Future benchmark results will depend on hardware, configuration, dataset shape, and implementation details.

## Planned Preview Release Success Criteria

A future preview release should preserve the deterministic core, provide a reproducible synthetic end-to-end operational workflow, document clone-to-first-result only after it works, publish explicit limitations, and obtain independent installation, reproducibility, or technical evaluation feedback. GitHub stars are not evidence of operational validation.
