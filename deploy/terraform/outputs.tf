output "namespace" {
  description = "Target namespace for the deployed service."
  value       = kubernetes_namespace.service.metadata[0].name
}

output "helm_release_name" {
  description = "Name of the Helm release."
  value       = helm_release.service.name
}

output "service_name" {
  description = "Logical internal service name."
  value       = var.service_name
}
