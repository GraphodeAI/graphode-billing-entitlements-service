terraform {
  required_version = ">= 1.10, < 2.0"

  required_providers {
    helm = {
      source  = "hashicorp/helm"
      version = "~> 3.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.37"
    }
    digitalocean = {
      source  = "digitalocean/digitalocean"
      version = "~> 2.66"
    }
  }
}
