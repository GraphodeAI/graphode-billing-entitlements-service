# Template And Scaffold Notes

This repository supports two bootstrap paths without changing its role as the canonical baseline source:

## `dotnet new`

- Metadata lives in `.template.config/template.json`.
- The template is intentionally thin.
- Its purpose is to stamp out a fresh copy of this baseline with service-local names.
- It must not introduce any dependency back to this repository.

Recommended parameters:

- `ServiceName`
- `RootNamespace`
- `ServiceSlug`

Derived automatically:

- `ContractsNamespace = RootNamespace + ".Contracts"`
- `ContractGeneratorNamespace = RootNamespace + ".ContractGenerator"`
- service identity strings that contain `graphode-billing-entitlements-service` become `graphode-<ServiceSlug>`

## Scaffold script

- `scripts/scaffold-service.sh` is the transparent copy-forward path.
- It applies explicit string replacements and file renames.
- It is intentionally small so teams can inspect and adapt it easily.
- Inputs:
  - `target-dir`
  - `service-name`
  - `root-namespace`
  - optional `service-slug`
- Derived automatically:
  - `ContractsNamespace = RootNamespace + ".Contracts"`
  - `ContractGeneratorNamespace = RootNamespace + ".ContractGenerator"`
  - `service identity = graphode-<ServiceSlug>`

Example:

```bash
./scripts/scaffold-service.sh \
  /path/to/acme-catalog-service \
  Acme.Catalog.Service \
  Acme.Catalog.Service \
  catalog-service
```

Placeholders replaced by the script:

- `Graphode.BillingEntitlementsService`
- `Graphode.BillingEntitlementsService`
- `Graphode.BillingEntitlementsService.Contracts`
- `Graphode.BillingEntitlementsService.ContractGenerator`
- `billing-entitlements-service`
- `graphode-billing-entitlements-service`

When to use which:

- Use `dotnet new` when you want standard local template installation and a familiar .NET CLI flow.
- Use the scaffold script when you want a transparent copy-forward bootstrap you can read, debug and tweak directly in shell.
- Use neither path to create a dependency back to this repository. Both outputs must remain fully local to the new service repo.

Keeping generated services aligned with the canonical baseline:

- treat this repository as the source of truth
- copy or re-scaffold only when you intentionally want baseline updates
- do not turn scaffolded output into a shared runtime package

## Important boundary

Both bootstrap paths create a new service repository that owns:

- its local contracts project
- its DTOs
- its message contracts
- its handlers
- its infrastructure configuration

Neither bootstrap path should be used to create a central shared runtime package strategy.
