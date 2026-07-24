# ADR-0011: Versioned Deterministic Reconciliation Report

## Status

Accepted

## Context

The implemented payment slice can generate deterministic scenario truth, project role-specific expected and observed state, and emit structurally stable reconciliation findings with contribution evidence. Consumers need a portable representation of that evidence whose bytes do not change because of wall-clock time, process state, reflection-based property discovery, or mutable collection inputs.

The Fault Manifest is oracle data describing injected faults. Including it in a reconciliation report would break the isolation established by ADR-0004 and would allow the report to reveal or imply a cause that the Reconciliation Engine did not derive from expected and observed state.

## Decision

Introduce the exact report schema version `reconciliation-report.v1`.

The immutable report contract contains:

- the complete implemented `payment-captured.v1` Scenario Definition
- the shared Reconciliation Cutoff
- expected and observed payment snapshots ordered by order id using ordinal comparison
- Reconciliation Findings ordered by order id, category, and monetary values
- the deterministic contribution order already enforced by each role-specific snapshot and finding

The report constructor defensively copies collection inputs, exposes read-only collections, validates snapshot pairing and cutoff consistency, and requires every finding to match report snapshot evidence.

The Application layer builds the report from only the Scenario Definition, cutoff, snapshots, and findings. Its public surface does not accept the Fault Manifest, fault requests, injectors, or fault-injection result types.

Serialization uses `System.Text.Json` through `Utf8JsonWriter`. Properties are written in explicit order. Money amounts are JSON `decimal` numbers with explicit currency. The scenario starting timestamp uses the invariant round-trip `O` representation, and the event interval is represented as ticks. Collections are serialized only after deterministic ordering. The report has no generated timestamp, random identifier, or ambient-culture value, so structurally identical inputs produce byte-for-byte identical UTF-8 JSON.

## Consequences

Reports provide reproducible reconciliation evidence without identifying or guessing an injected fault. They preserve source and delivery traceability while maintaining Fault Manifest isolation.

Version negotiation beyond exact v1 matching, additional event vocabularies and finding categories, schema migration, signing, persistence, transport, streaming, API or CLI exposure, infrastructure integration, and production readiness remain planned.
