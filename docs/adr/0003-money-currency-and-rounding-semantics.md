# ADR-0003: Money, Currency, And Rounding Semantics

## Status

Accepted

## Context

Financial reconciliation requires explicit money semantics. Silent rounding, implicit currency conversion, or mixed-currency arithmetic can hide discrepancies that reconciliation should expose.

## Decision

The initial `Money` value object stores a `decimal` amount and a required three-letter uppercase ISO 4217 currency code. Addition and subtraction are allowed only when currencies match. Mixed-currency arithmetic fails explicitly.

The implementation does not silently round monetary values. Exchange-rate conversion is outside v0.1. Debit, credit, sign, rounding, and precision policy for additional financial record types require future ADRs before broader implementation.

## Consequences

The duplicate-payment slice preserves caller-supplied monetary precision and prevents accidental mixed-currency comparison. Broader accounting semantics remain intentionally unresolved until the project has explicit requirements and tests for them.
