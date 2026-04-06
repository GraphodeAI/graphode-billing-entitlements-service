# Canonical .NET 10 Graphode Skeleton

This repository is the canonical Graphode baseline for future .NET microservices. It is a reference skeleton and template source, not a shared runtime kernel. Future services are expected to copy the structure, keep only the pieces they need, and then own their code independently.

## Hard boundaries

- This repository is a canonical baseline/reference skeleton.
- It must NOT become a central shared runtime kernel.
- The local contracts project in this repository is baseline-local only.
- Future services must copy and own their own local contracts project independently.
- Future services must NOT depend on a central runtime contracts package published from this repository.
- The baseline demonstrates implementation shape and conventions. It does not create central runtime ownership.

## What this baseline contains

- .NET 10 solution with one minimal but real reference service
- MongoDB integration with a practical repository pattern and index initializer
- Redis-compatible support for query cache, session/refresh operational state and ephemeral metadata
- read-side DTO model with paging, sorting and filtering
- command, event and PEM contracts with deterministic envelopes
- RabbitMQ producer support and one real command consumer path
- internal-only HTTP client extension point with correlation/context propagation
- structured options binding and startup wiring
- health checks for MongoDB, RabbitMQ and Redis
- JSON contract generation into `helper-ssot/contracts`
- baseline Helm and Terraform deployment scaffolding

## Project layout

- `src/Graphode.BillingEntitlementsService.Contracts`
  Baseline-local contracts project. It exists inside this reference solution to show the expected shape for service-owned DTOs, message envelopes and contract artifacts. Future services should copy this pattern into their own repo and own it there.
- `src/Graphode.BillingEntitlementsService.Domain`
  Minimal domain entity used only to prove the baseline.
- `src/Graphode.BillingEntitlementsService.Application`
  Query/write services, validation, repository abstractions and messaging abstractions.
- `src/Graphode.BillingEntitlementsService.Infrastructure`
  Mongo, Redis, RabbitMQ, health checks and internal HTTP client support.
- `src/Graphode.BillingEntitlementsService.Api`
  Minimal API wiring and example endpoints.
- `src/Graphode.BillingEntitlementsService.ContractGenerator`
  Generates machine-readable JSON Schema artifacts from the contracts project.
- `tests/Graphode.BillingEntitlementsService.Tests`
  Lightweight tests for contract catalog and request validation.
- `helper-ssot`
  Lightweight changelog, generated contracts and baseline notes.
- `deploy/helm`
  Reusable per-service Helm chart baseline.
- `deploy/terraform`
  Reusable Terraform scaffold for service deployment wiring.

## How to reuse this baseline

1. Copy this repository or scaffold a new service from it.
2. Rename the reference service projects and namespaces.
3. Replace the reference aggregate, DTOs and handlers with service-owned business code.
4. Keep the local structure and conventions, but do not introduce a shared runtime dependency back to this repo.
5. Regenerate JSON contracts after changing external DTOs or message contracts.

## Pattern vs example

Baseline infrastructure pattern:

- local contracts project shape
- Mongo repository structure
- Redis cache and operational-state hooks
- RabbitMQ envelope/publisher/consumer wiring
- internal HTTP context propagation
- JSON contract generation

Reference-service example code:

- `ReferenceItem` aggregate and DTOs
- example list/create/archive endpoints
- example command handler registration
- example RabbitMQ routing keys and queue names

Future services are expected to replace or specialize the example-level parts. In particular, command handlers, payloads, DTOs, routes and contract files are owned by each future service, not centrally by this repo.

## How to create a new microservice from this baseline

1. Clone the repository into a new service repo.
2. Rename:
   - solution name
   - project names
   - namespaces
   - chart/release names
3. Replace `ReferenceItem` with the real aggregate and collection names.
4. Update:
   - Mongo indexes
   - Rabbit exchanges, queues and routing keys
   - Redis cache categories/operational state categories
   - internal service endpoints under `InternalServices`
5. Regenerate contracts:

```bash
dotnet run --project src/Graphode.BillingEntitlementsService.ContractGenerator/Graphode.BillingEntitlementsService.ContractGenerator.csproj
```

## Paging, filtering and sorting

This baseline uses `page` + `pageSize`, not offset/limit.

Why:
- it is easier to understand for frontend consumers
- it produces explicit paging metadata
- it is deterministic enough for baseline service list endpoints

Filtering is structured as:

```json
{
  "field": "status",
  "operator": "eq",
  "values": ["active"]
}
```

Sorting is structured as:

```json
{
  "field": "createdAtUtc",
  "direction": "desc"
}
```

The HTTP GET example uses compact query syntax:

- `sort=createdAtUtc:desc`
- `filter=status:eq:active`
- `filter=name:contains:graph`

This keeps the public HTTP shape practical while the canonical contract remains machine-readable in JSON Schema.

## Messaging model

- Commands express intent and are useful for asynchronous or orchestration boundaries.
- Events describe durable facts that already happened.
- PEM means `Platform Event Model` in this baseline. It is a stable platform-facing payload for downstream consumers and cross-service integrations.

All three use explicit envelopes with:

- `id`
- `type`
- `schemaVersion`
- `correlation`
- `actor`
- `workspace`
- `metadata`
- `payload`

The reference service demonstrates:

- direct synchronous REST write: `POST /api/reference-items`
- REST-triggered async command dispatch: `POST /api/reference-items/commands/archive`
- RabbitMQ command consumer handling `ReferenceItemArchiveRequested`
- event publishing for create/archive
- PEM publishing for Platform Event Model downstream consumers

The command handler registration in the reference service is example-level pattern proof only. Future services will own their own command handlers, payloads, queue bindings and contracts.

## Mongo, Redis and internal HTTP

- Mongo is the canonical service data store in this baseline.
- Redis-compatible infrastructure is for cache, session/refresh operational state and other ephemeral data only.
- The list endpoint uses Redis query cache with workspace-level invalidation.
- `IOperationalStateStore` is the baseline hook for refresh token state, short-lived sessions and other operational records.
- `IRateLimitStateStore` is the extension point for anti-abuse or rate-limit metadata.
- Internal HTTP communication assumes private network addressing only. No service mesh and no mTLS-dependent abstractions are required.
- `IInternalServiceClient` propagates correlation and actor headers over internal calls and resolves service addresses from configuration.

## RabbitMQ producer/consumer wiring

- `RabbitMqPublisher` publishes command, event and PEM envelopes to dedicated topic exchanges.
- `RabbitMqCommandConsumerHostedService` binds a durable queue to the command exchange and dispatches by envelope `type`.
- Failures nack without requeue, which keeps dead-letter exchange support straightforward when configured.
- Idempotency baseline:
  archive handling is a no-op if the entity is missing or already archived.

## JSON contract generation

Machine-readable contracts live under `helper-ssot/contracts`.

Generate or refresh them with:

```bash
dotnet run --project src/Graphode.BillingEntitlementsService.ContractGenerator/Graphode.BillingEntitlementsService.ContractGenerator.csproj
```

The generator emits:

- read request/response schemas
- write request/response schemas
- command payload and envelope schemas
- event payload and envelope schemas
- PEM payload and envelope schemas
- `index.json` manifest

The manifest is intentionally human-readable first:

- stable `contractId`
- category/subject/artifact kind split
- schema file name
- readable type name
- CLR type for tooling identity

Future services should treat contract refresh as part of any external API or messaging change.

## Template and scaffold readiness

This repository is prepared for both:

- `dotnet new` template packaging
- lightweight copy-forward scaffold scripting

`dotnet new` readiness:

- template metadata lives in `.template.config/template.json`
- the canonical repo remains the source of truth
- template output should still produce a fully local service repo, not a dependency on this repository
- the public template surface is intentionally small:
  `--ServiceName`, `--RootNamespace` and optional `--ServiceSlug`
- contracts and contract-generator namespaces are derived from `RootNamespace`
- `.git`, `.terraform`, `bin` and `obj` content are excluded from generated output

Example local install and use:

```bash
dotnet new install /absolute/path/to/canonical-dotnet-skeleton
dotnet new graphode-microservice \
  --ServiceName Acme.Catalog.Service \
  --RootNamespace Acme.Catalog.Service \
  --ServiceSlug catalog-service \
  -o acme-catalog-service
```

Placeholders replaced by the template:

- `Graphode.BillingEntitlementsService` -> `ServiceName`
- `Graphode.BillingEntitlementsService` -> `RootNamespace`
- `Graphode.BillingEntitlementsService.Contracts` -> `RootNamespace + ".Contracts"`
- `Graphode.BillingEntitlementsService.ContractGenerator` -> `RootNamespace + ".ContractGenerator"`
- `billing-entitlements-service` -> `ServiceSlug`
- `graphode-billing-entitlements-service` -> `graphode-<ServiceSlug>`

Scaffold script readiness:

- `scripts/scaffold-service.sh` is a transparent copy-forward helper
- it copies the repo into a new target directory
- it applies the same core name replacements explicitly
- it keeps the resulting service repo fully local and independently owned
- it takes only:
  `target-dir`, `service-name`, `root-namespace` and optional `service-slug`

Example scaffold run:

```bash
./scripts/scaffold-service.sh \
  /path/to/acme-catalog-service \
  Acme.Catalog.Service \
  Acme.Catalog.Service \
  catalog-service
```

Values a future scaffold should parameterize:

- service name
- root namespace
- optional service slug

The scaffold script differs from `dotnet new` in one important way:

- `dotnet new` uses installed template metadata
- the scaffold script is a plain copy-and-replace shell path you can inspect line by line

Use the scaffold script when explicitness matters more than template installation convenience.

## Deployment scaffolding

- `deploy/helm/billing-entitlements-service`
  baseline per-service Helm chart with internal-only defaults.
- `deploy/terraform`
  baseline Terraform scaffold for namespace + Helm release deployment wiring against a DigitalOcean-oriented Kubernetes environment.

These files are intentionally incomplete for production and are meant to be copied forward, then refined by the owning service.

## Future service fill-in points

Real services are expected to add next:

- real aggregates and business invariants
- service-owned repositories and indexes
- real command/event sets
- service-specific internal clients
- authorization rules at the application boundary
- production secrets sourcing and environment overlays
- service-owned dashboards, alerts and SLOs

What they should not add:

- dependency on a central shared kernel
- dependency on a forever-shared contracts runtime package
- fake placeholder runtime paths
- mTLS-coupled abstractions as a baseline assumption
