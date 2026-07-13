# Benchmark Methodology

This document defines methodology for later releases. It does not contain benchmark results.

## Functional Evaluation

Functional evaluation should use the Fault Manifest only after reconciliation completes. The Reconciliation Engine must not access expected benchmark answers or injected-fault oracle data.

The Benchmark Evaluator may compare Reconciliation Findings with the Fault Manifest to calculate:

- True positives.
- False positives.
- False negatives.
- Detection rate or recall.
- Precision when applicable.
- Classification accuracy for known injected discrepancy categories.

The evaluation unit should be an injected discrepancy identified by scenario ID, transaction ID, fault ID, and expected discrepancy category.

## Performance Benchmarking

Performance measurements must be reported separately from functional correctness results. Performance benchmarks should record:

- Total processed events.
- Reconciliation duration.
- Throughput.
- p50 processing latency.
- p95 processing latency.
- Environment and configuration metadata.

## Synthetic Scenarios

Benchmark scenarios must use synthetic transaction-event datasets only. Each scenario should define scenario version, event volume, transaction distribution, fault types, fault rates, currency distribution, fixed seed, Reconciliation Cutoff, and configuration values.

Known injected discrepancies should be recorded in the Fault Manifest, separate from Reconciliation Findings, so detection quality can be evaluated without allowing the Reconciliation Engine to know the injected answer.

## Reproducibility

Each benchmark run should preserve:

- Scenario Definition.
- Fixed random seed.
- Reconciliation Cutoff.
- Source revision.
- Runtime version.
- Operating system.
- CPU and memory information.
- Storage characteristics when persistence is involved.
- Broker and database configuration when infrastructure is involved.
- Full reconciliation configuration.
- Raw benchmark output.

Repeated runs using the same Scenario Definition, seed, Reconciliation Cutoff, source revision, and configuration should produce the same Reconciliation Findings. Performance measurements may vary by environment and should be reported with environment details.

## Result Publication

Published benchmark material should include raw output and enough scenario, configuration, and environment information to reproduce the run. Functional correctness results and performance results must be reported separately. Results should not be generalized beyond the measured scenario.
