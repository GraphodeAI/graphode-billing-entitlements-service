variable "digitalocean_token" {
  description = "DigitalOcean API token."
  type        = string
  sensitive   = true
}

variable "kubeconfig_path" {
  description = "Path to kubeconfig for the target DigitalOcean Kubernetes cluster."
  type        = string
}

variable "namespace" {
  description = "Kubernetes namespace for the service."
  type        = string
  default     = "graphode"
}

variable "release_name" {
  description = "Helm release name."
  type        = string
  default     = "billing-entitlements-service"
}

variable "chart_path" {
  description = "Path to the local Helm chart baseline."
  type        = string
  default     = "../helm/billing-entitlements-service"
}

variable "image_repository" {
  description = "Container image repository."
  type        = string
}

variable "image_tag" {
  description = "Container image tag."
  type        = string
}

variable "service_name" {
  description = "Logical service identity."
  type        = string
  default     = "graphode-billing-entitlements-service"
}

variable "mongo_existing_secret_name" {
  description = "Existing secret that contains MongoDB connection details."
  type        = string
}

variable "rabbit_existing_secret_name" {
  description = "Existing secret that contains RabbitMQ connection details."
  type        = string
}

variable "redis_existing_secret_name" {
  description = "Existing secret that contains Redis connection details."
  type        = string
}

variable "internal_service_map" {
  description = "Map of private service DNS names used for internal HTTP calls."
  type        = map(string)
  default     = {}
}
