# ADR-0012: Adopt An Out-Of-Band Operational Reconciliation Boundary

## Status

Accepted

## Context

The implemented v0.1 `PaymentCaptured` slices establish a reusable deterministic core: versioned synthetic scenario generation, truth and faulted-delivery separation, role-specific projections, logical cutoff handling, traceable findings, Fault Manifest isolation, and byte-repeatable versioned reports.

That foundation proves important semantics in memory, but the project does not yet demonstrate how reconciliation can process an operational event history incrementally, compare it with an existing projection, persist evidence, or resume safely. Remaining only a synthetic laboratory would leave the central integration and recovery questions unanswered.

The move toward an operational reference path does not invalidate v0.1. Deterministic comparison, stable evidence, explicit cutoffs, and infrastructure isolation are prerequisites for a trustworthy operational implementation.

## Decision

FinReconLab will pursue this product boundary:

> FinReconLab is intended to become an out-of-band projection-integrity and deterministic reconciliation toolkit for event-driven financial transaction systems.

The initial operational reference use case is:

> Incrementally compare the expected state derived from an authoritative payment event source with an observed payment projection, persist explainable reconciliation findings and versioned reports, and resume safely from explicit checkpoints.

### Out-Of-Band Execution

The planned worker runs outside the production transaction hot path. It does not replace the authoritative source, replace existing projections or consumers, intercept messages required for transaction completion, or control production transaction processing.

Source and projection access should normally be read-only. FinReconLab writes only its own versioned reconciliation definitions, incremental expected state, checkpoints, findings, versioned reports, deterministic batch metadata, and other FinReconLab-owned operational metadata unless a future accepted ADR explicitly authorizes another boundary.

This separation reduces the risk that reconciliation availability or defects affect transaction completion and allows reconciliation to operate on bounded histories at an independently controlled pace.

### Domain-Specific Adapters

The authoritative source is accessed through an explicit source-specific adapter. It may be a database, retained event source, approved export, replay interface, or another supported implementation. FinReconLab does not assume that every system uses an Event Store or that a broker contains authoritative historical data.

The observed projection is accessed through a separate domain-specific read adapter. The adapters must define identity, ordering, money, source high-watermark, projection observation boundary, and state-mapping semantics. A generic toolkit cannot safely infer those semantics or automatically understand arbitrary projection schemas.

Adapters translate operational representations into infrastructure-independent contracts. The deterministic Domain and Application core remains independent of databases, brokers, hosting, telemetry, and serialization infrastructure.

### Comparable Source And Projection Boundaries

A source high-watermark bounds authoritative input but does not prove that the observed projection processed the same source range. Before findings are treated as final, the observed-projection adapter must establish one of these planned comparability conditions:

- the projection processed through a source position comparable to the selected source high-watermark
- the projection can be read as of an equivalent immutable boundary
- an explicitly configured stabilization policy applies and records its limitations

Wall-clock timestamps alone are not sufficient evidence. If comparability cannot be established, the run must be rejected, deferred, or explicitly marked provisional by a future tested contract instead of silently publishing a conclusive discrepancy.

The first PostgreSQL reference slice will provide an explicit synthetic projection-processing checkpoint or equivalent source-position evidence.

### Incremental Checkpointed Processing

Operational histories may be too large or too dynamic for an unbounded full replay on every run. The planned worker therefore processes explicit bounded partitions up to a source high-watermark and records checkpoint progress through a persistence contract.

The planned model persists FinReconLab-owned deterministic expected state by reconciliation definition, partition, and business identity. Authoritative events update that state in stable order during bounded processing. This incremental reducer state survives across batches and remains distinct from the external observed projection. Expected-state persistence failure prevents checkpoint advancement, and deterministic replay can resume from a known compatible checkpoint.

A planned versioned reconciliation definition identifies the use case, adapter and mapping contract version, expected-state reducer or rule version, partition semantics, and compatible checkpoint and expected-state namespace. Persisted state cannot be silently reused after an incompatible definition change; the exact migration or replay API remains a future decision.

### Atomic Or Replay-Safe Batch Completion

Expected-state updates, findings, versioned reports, required batch metadata, and checkpoint advancement form one planned batch-completion boundary. A batch is complete only when this FinReconLab-owned state is committed atomically or through a documented replay-safe idempotent protocol.

Checkpoint progress is monotonic and never advances after partial persistence failure. Findings and reports use deterministic batch or run identity, replaying the same batch cannot duplicate them, and failure before completion leaves the batch safely repeatable. External authoritative sources and observed projections remain read-only.

Partition identity, checkpoint advancement, restart behavior, and partial-failure recovery must be explicit and tested. A worker checkpoint is operational progress state; it is distinct from the source high-watermark, projection observation boundary, and implemented v0.1 synthetic shared sequence cutoff.

### First Reference Integration

The first planned runnable reference integration will use PostgreSQL with only synthetic public data for:

- a synthetic authoritative payment-event source
- an observed payment projection
- an explicit synthetic projection-processing checkpoint or equivalent source-position evidence
- versioned reconciliation definitions
- incremental expected state
- FinReconLab-owned checkpoints
- reconciliation findings
- versioned reports, deterministic batch metadata, and required operational metadata

PostgreSQL provides one reproducible reference environment and must support atomic transactions or a documented replay-safe idempotent batch protocol. It is not a universal source requirement. Source and projection adapters remain replaceable behind documented contracts.

RabbitMQ with MassTransit may be evaluated later as an optional transport adapter. It is not mandatory for the first operational slice and is not assumed to be the authoritative source. Its role requires a separate ADR before implementation.

### External Validation

A future preview release must seek independent installation, reproducibility, or technical evaluation feedback. External validation is a release outcome because local tests alone cannot establish that setup instructions, adapter boundaries, evidence, and recovery behavior are understandable outside the project.

GitHub stars are not operational validation. The project will not claim users, adoption, pilots, performance, or production impact before evidence exists.

## Consequences

- Roadmap milestones are organized around operational outcomes rather than accumulating infrastructure.
- v0.2 defines adapter, partition, checkpoint, orchestration, and persistence contracts before selecting concrete integrations.
- v0.3 plans a runnable synthetic PostgreSQL reference vertical slice.
- Foundational v0.3 correctness includes comparable source and projection boundaries, incremental expected state, reconciliation-definition compatibility, monotonic checkpoints, and atomic or replay-safe result consistency.
- Scale, recovery, telemetry, and reproducible benchmark evidence follow only after the end-to-end reference workflow exists.
- The project must preserve clear implemented-versus-planned language while it remains pre-alpha.
- Additional synthetic fault categories should be added only when an operational or evaluation use case requires them.
- FinReconLab findings and reports continue to describe reconciliation evidence without identifying or guessing an injected or historical fault.
- Adapter development requires explicit domain knowledge and may be the main integration cost for a new projection.

Current limitations remain substantial: there is no operational adapter, comparability contract, versioned operational reconciliation definition, incremental expected-state store, worker, partition model, batch-completion protocol, checkpoint persistence, PostgreSQL integration, API, CLI, Docker environment, telemetry, benchmark result, external evaluation, or production-ready behavior.

## Rejected Alternatives

### Put Reconciliation In The Transaction Hot Path

Rejected because reconciliation availability and defects could affect transaction completion, and because historical or bounded reprocessing needs a lifecycle independent of live processing.

### Replace Existing Projections Or Consumers

Rejected because FinReconLab is intended to measure projection integrity, not become the authoritative transaction processor or force replacement of existing systems.

### Require An Event Store

Rejected because authoritative histories may reside in databases, retained sources, approved exports, replay interfaces, or other source-specific systems.

### Treat RabbitMQ As The Mandatory Authoritative Source

Rejected because a transport broker may not retain complete history and because transport topology is system-specific. Broker support, if added, is an optional adapter concern.

### Build A Universal Projection Adapter

Rejected because transaction identity, ordering, monetary semantics, cutoff rules, and projection state cannot be inferred safely across arbitrary domains.

### Add PostgreSQL, A Broker, Telemetry, And Containers Before Contracts

Rejected as decorative infrastructure. Concrete dependencies should follow tested operational contracts and documented milestone outcomes.

### Continue Adding Synthetic Fault Categories As The Primary Roadmap

Rejected because the implemented fault set is sufficient to establish the current deterministic core. New categories need a concrete operational or evaluation purpose.

### Add Automated Repair Or AI-Based Explanation Now

Rejected because the project does not yet have an operational evidence path, recovery model, or separate accepted decision establishing safety and evaluation criteria.
