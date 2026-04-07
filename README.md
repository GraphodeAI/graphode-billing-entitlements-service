# Graphode Billing Entitlements Service

This repository owns billing plans, billing accounts, subscriptions, ledger state and payment-method onboarding for Graphode.

## Scope

- plans and billing-account lookup
- subscriptions and onboarding completion
- wallet, ledger and budget policy state
- payment-method capture support
- edge-routed billing APIs

## Hard boundaries

- DTOs, enums and contracts stay service-local.
- Public ingress remains edge-only.
- The repo must stay independently buildable and testable.

## Current surfaces

- `GET /api/v1/billing/plans`
- `GET /api/v1/workspaces/{workspaceId}/billing-account`
- `GET /api/v1/workspaces/{workspaceId}/billing/subscription`
- `GET /api/v1/workspaces/{workspaceId}/billing/ledger`
- `POST /api/v1/workspaces/{workspaceId}/billing/ledger/reserve`
- `POST /api/v1/workspaces/{workspaceId}/billing/ledger/commit`
- `POST /api/v1/workspaces/{workspaceId}/billing/ledger/release`
- `POST /api/v1/workspaces/{workspaceId}/billing/payment-methods`
- `POST /api/v1/workspaces/{workspaceId}/billing/payment-methods/setup-intent`

## Notes

- Keep billing and ledger behavior real and workspace-tied.
- Use the SSOT and local tests as the current truth.
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
