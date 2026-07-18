# ADR-0004: Deterministic Fault Injection, Cutoff, And Manifest Isolation

## Status

Accepted

## Context

The first vertical slice demonstrates duplicate delivery by comparing a clean expected payment state with a non-idempotent observed projection. The reconciliation engine must detect the amount mismatch without knowing the injected fault answer. ADR-0006, ADR-0007, and ADR-0008 record later clarifications that the current v0.1 payment slices use a shared numeric sequence boundary for expected and observed projections.

## Decision

Duplicate delivery is injected deterministically from caller-supplied inputs. The duplicate retains the original source event identity and receives an explicit delivery sequence. The observed projection applies a logical reconciliation cutoff based on delivery sequence; it does not use wall-clock waiting or real sleeps.

The fault injector returns a delivered event stream and a separate Fault Manifest. The Reconciliation Engine receives only expected and observed payment snapshots. Tests and later evaluation code may inspect the Fault Manifest only after reconciliation completes.

## Consequences

The slice preserves a clean separation between implementation behavior and oracle data. Repeated runs with the same events, duplicate request, cutoff, and configuration produce structurally identical delivered streams and findings.
