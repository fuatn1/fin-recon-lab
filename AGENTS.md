# Repository Instructions

These instructions apply to all future coding-agent sessions in this repository.

## Public Positioning

- All public content and code must be written in English.
- Never introduce non-public third-party code, data, names, architecture, business rules, or production-derived examples.
- Use only synthetic test data, examples, fixtures, and benchmark scenarios.
- Never fabricate metrics, users, adoption, citations, benchmarks, or results.
- Planned features must be labeled as planned until they are implemented, tested, and verified.
- Never reference private personal matters, private professional affiliations, non-public systems, or other non-public context in public repository content.
- Do not claim universal projection compatibility. Every authoritative source and observed projection requires an explicit domain-specific adapter.
- Do not add AI functionality without a separately justified and accepted future decision.

## Engineering Direction

- Prefer simple, testable architecture over decorative complexity.
- Map proposed work to a documented operational milestone acceptance criterion before implementation.
- Treat FinReconLab as an out-of-band projection-integrity and deterministic reconciliation toolkit, not as part of the production transaction hot path.
- Preserve the authoritative source, existing projections, and existing consumers. Source and projection access should normally be read-only.
- Keep authoritative-source adapters, observed-projection adapters, orchestration, and FinReconLab-owned persistence behind explicit boundaries.
- Production code will target .NET 10.
- Use `decimal` for monetary values and require explicit ISO 4217-style three-letter uppercase currency codes.
- Deterministic reconciliation logic must be separated from infrastructure concerns.
- Do not introduce infrastructure dependencies into deterministic domain logic.
- Avoid decorative infrastructure. Add infrastructure only when it serves a documented operational or evaluation outcome.
- Avoid adding synthetic fault categories unless an operational or evaluation use case requires them.
- Every behavior change requires tests.
- Benchmark results must include environment and reproducibility information.
- Update documentation when architecture or behavior changes.

## Deterministic Core Guardrails

- Do not call `DateTime.UtcNow`, `DateTime.Now`, `Guid.NewGuid`, `Random.Shared`, or equivalent nondeterministic APIs directly from deterministic core logic.
- Time, identifiers, and random generation must be supplied through explicit deterministic inputs or injectable abstractions.
- Reconciliation output ordering must be stable.
- The reconciliation core must never depend on the Fault Manifest.
- Tests must prove that identical scenario, seed, cutoff, and configuration inputs produce identical findings.
- Do not use real sleeps to simulate delayed delivery.
- Deterministic delayed delivery must be represented through logical delivery ordering or delivery position.

## Contribution Discipline

- Use Conventional Commits when commits are later authorized.
- Never commit or push unless explicitly requested.
- Before finishing a coding task, run relevant tests and report exact results.
- Do not silently weaken tests or remove validation to make a build pass.
- Verify that documentation distinguishes implemented capabilities from planned capabilities.
- Never fabricate adoption, pilots, external validation, benchmark evidence, production readiness, or operational impact.
