# Billing Entitlements Service Plan

## Current objective

Keep the Stripe-backed billing entitlements service honest and resumable. The repo now owns real billing workspace snapshots, payment-method capture, subscriptions, ledger behavior and webhook reconciliation.

## Guardrails

- No shared kernel.
- No shared runtime contracts package for future services.
- No fake billing state.
- No placeholder runtime behavior.
- No service mesh theater.
- Keep Stripe boundaries explicit and service-local.

## Planned work

1. Keep payment-method setup-intent and subscription flows wired to the live Stripe boundary.
2. Keep webhook reconciliation and workspace snapshot persistence aligned with the edge gateway route.
3. Keep ledger and plan responses consistent with the persisted billing account state.
4. Clean up any stale scaffold text or repo-local drift before closure.
5. Build, test and smoke-check any billing change against the real gateway path.
