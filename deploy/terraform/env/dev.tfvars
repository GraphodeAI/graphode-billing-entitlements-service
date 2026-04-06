namespace                   = "graphode-dev"
release_name                = "billing-entitlements-service-dev"
image_repository            = "ghcr.io/graphode-ai/canonical-dotnet-skeleton/billing-entitlements-service"
image_tag                   = "dev"
mongo_existing_secret_name  = "graphode-reference-mongo-dev"
rabbit_existing_secret_name = "graphode-reference-rabbit-dev"
redis_existing_secret_name  = "graphode-reference-redis-dev"

internal_service_map = {
  identity  = "http://graphode-identity.graphode-dev.svc.cluster.local"
  workspace = "http://graphode-workspace.graphode-dev.svc.cluster.local"
}
