locals {
  chart_values = templatefile("${path.module}/values/billing-entitlements-service-values.yaml.tftpl", {
    image_repository       = var.image_repository
    image_tag              = var.image_tag
    service_name           = var.service_name
    mongo_secret_name      = var.mongo_existing_secret_name
    rabbit_secret_name     = var.rabbit_existing_secret_name
    redis_secret_name      = var.redis_existing_secret_name
    internal_services_json = jsonencode(var.internal_service_map)
  })
}

resource "kubernetes_namespace" "service" {
  metadata {
    name = var.namespace
    labels = {
      "app.kubernetes.io/part-of" = "graphode"
      "graphode.io/internal-only" = "true"
    }
  }
}

resource "helm_release" "service" {
  name      = var.release_name
  namespace = kubernetes_namespace.service.metadata[0].name
  chart     = var.chart_path
  timeout   = 300
  atomic    = false
  wait      = true

  values = [local.chart_values]
}
