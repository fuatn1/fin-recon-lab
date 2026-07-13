# ADR-0001: Record Architecture Decisions

## Status

Accepted

## Context

FinReconLab is intended to evolve from a documentation foundation into a reference platform for deterministic reconciliation experiments. Future work will require choices about runtime, modular boundaries, money representation, event generation, fault injection, persistence, broker integration, observability, and benchmark publication.

These choices should be easy for contributors to discover and review. They should also distinguish implemented decisions from ideas that are still planned.

## Decision

The project will record important architecture decisions as lightweight Architecture Decision Records in `docs/adr`.

Each ADR should include:

- Status.
- Context.
- Decision.
- Consequences.

## Consequences

Architecture decisions will have a durable public record. Contributors can understand why a choice was made before proposing changes. The project will also have a clear place to revise decisions when new evidence, implementation experience, or benchmark results justify a change.
