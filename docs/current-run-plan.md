# Current Run Plan

## Scope

This run creates a baseline/template repository, not a business microservice.

## Target artifacts

- `Graphode.BillingEntitlementsService.sln`
- reference service with minimal end-to-end example
- local contracts and contract generator projects
- helper SSOT folder with changelog, contracts and reuse notes

## Concrete runtime paths in this run

- HTTP read path: list reference items with paging, filtering and sorting
- HTTP write path: create reference item and publish event
- Async command path: archive command consumed from RabbitMQ and handled in application layer
- Redis cache path: list endpoint cache and workspace cache invalidation
- Redis operational-state path: reusable backing store for refresh/session/ephemeral state
- Internal HTTP path: named internal service clients with correlation/context propagation over private network
- Contract path: generate JSON schemas from DTO/message contracts into helper SSOT output
- Deployment path: reusable Helm/Terraform baseline for DigitalOcean-hosted Kubernetes services

## Acceptance evidence to produce

- `dotnet build`
- `dotnet test`
- `dotnet run --project ...ContractGenerator...`
- `helm lint` or an explicit record if Helm CLI is unavailable
- `terraform fmt -check` and `terraform validate`
- generated JSON files present under `helper-ssot/contracts`
