# Baseline Evolution Notes

- Future services should copy the reference structure and then own their code independently.
- Contract JSON artifacts are intentionally generated into this helper SSOT area to reduce repeated re-description of API and message shapes.
- Local support projects inside the solution demonstrate patterns only for this baseline repository and are not intended to become a shared runtime dependency for all services.
- The local contracts project is baseline-local only. Future services should copy and own their own contracts project instead of depending on one from this repo.
- Redis-compatible support exists for cache, session/refresh operational state and ephemeral metadata only. Mongo remains the source of truth.
- Internal service-to-service communication assumes private networking, API Gateway as the only public ingress, and application-layer validation without mTLS coupling.
