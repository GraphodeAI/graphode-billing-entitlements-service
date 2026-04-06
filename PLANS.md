# Canonical .NET Skeleton Plan

## Current objective

Create the canonical Graphode .NET 10 microservice skeleton baseline with:

- one reference service
- local baseline-only support projects
- MongoDB integration
- repository pattern
- read DTOs with filtering, sorting, paging
- command, PEM and event models
- RabbitMQ producer and command consumer support
- Redis-compatible cache and operational state support
- internal-only HTTP client baseline without mTLS assumptions
- Helm and Terraform deployment scaffolding
- deterministic startup/configuration wiring
- health checks
- machine-readable JSON contracts
- lightweight helper SSOT notes

## Guardrails

- No shared kernel.
- No shared runtime contracts package for future services.
- No fake business logic.
- No placeholder runtime behavior.
- No service mesh theater.
- No mTLS-dependent abstractions.
- Keep structure copy-forward friendly.

## Planned work

1. Create solution structure and repo-local documentation.
2. Implement contracts, DTOs, envelopes and JSON contract generation.
3. Implement reference domain, application services and API endpoints.
4. Implement Mongo, Redis, RabbitMQ and internal HTTP infrastructure patterns.
5. Add health checks, JSON contract generation, Helm/Terraform scaffolding, tests, README and helper SSOT outputs.
6. Build, test, smoke-check and publish to GitHub.
