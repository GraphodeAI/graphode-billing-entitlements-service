# Current Run Plan

## Scope

This repository now owns the real billing entitlements service boundary for Graphode. The current run is about keeping the Stripe-backed commercial substrate honest, not about a template scaffold.

## Target artifacts

- billing workspace snapshot store
- Stripe setup-intent / payment-method capture
- subscription start/change/cancel
- signed Stripe webhook reconciliation
- ledger and plan surfaces exposed through the gateway

## Concrete runtime paths in this run

- billing plans query -> gateway -> service-local plan catalog
- setup-intent creation -> Stripe -> payment-method capture
- subscription start/change/cancel -> workspace billing account persistence
- signed webhook event -> reconciliation -> account/subscription update
- ledger query -> reserve/commit/release -> append-only workspace billing history

## Acceptance evidence to produce

- `dotnet build`
- `dotnet test`
- live gateway smoke for `/api/v1/billing/plans`
- live gateway smoke for `/api/v1/workspaces/{workspaceId}/billing-account`
- live Stripe payment-method and subscription proof
- signed webhook reconciliation proof

## Current status

- Stripe-backed payment-method and subscription flow is live
- Mongo-backed workspace snapshot persistence is live
- webhook reconciliation is live through the edge gateway
- remaining work is cleanup and hardening, not scaffold completion
