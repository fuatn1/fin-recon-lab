# Contributing

Thank you for your interest in FinReconLab. The project is currently pre-alpha and contains only foundation documentation. The contribution workflow below describes the intended process once implementation work begins.

## Issues

Use issues for focused bug reports, design questions, documentation improvements, and implementation proposals. A useful issue should include:

- A concise description of the problem or proposal.
- Expected behavior and observed behavior, when applicable.
- Reproduction steps using synthetic data only.
- Relevant configuration, environment details, and logs that do not contain secrets or real financial or personal data.

## Pull Requests

Pull requests should be focused and reviewable. Each pull request should:

- Address one coherent change.
- Include tests for behavior changes.
- Update documentation when architecture, behavior, configuration, or usage changes.
- Avoid introducing non-public third-party data, organization-specific details, or production-derived examples.
- Clearly label planned behavior as planned until implemented and verified.

## Generated or AI-Assisted Contributions

Generated or AI-assisted contributions remain the contributor's responsibility. Contributors are expected to review, test, and understand all submitted changes, including generated code, documentation, tests, and examples.

## Quality Expectations

- Keep deterministic reconciliation logic separate from infrastructure.
- Use `decimal` for monetary values and explicit ISO 4217 currency codes.
- Do not fabricate metrics, benchmark results, citations, users, or adoption claims.
- Do not weaken tests or remove validation to make a build pass.
