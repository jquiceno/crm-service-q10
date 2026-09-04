variable "service_name" {
  type        = string
  description = "Service name used for resource naming (lowercase alphanumeric and hyphens only — ECR requirement)."
  validation {
    condition     = can(regex("^[a-z0-9-]+$", var.service_name))
    error_message = "service_name must be lowercase alphanumeric with hyphens only."
  }
}

variable "environment" {
  type        = string
  description = "Deployment environment. Each environment maps to a separate AWS account."
  validation {
    condition     = contains(["dev", "qa", "prod"], var.environment)
    error_message = "environment must be one of: dev, qa, prod."
  }
}

variable "github_org" {
  type        = string
  description = "GitHub organization that owns the repository."
  default     = "Q10-Software"
}

variable "github_repo" {
  type        = string
  description = "GitHub repository name (exact case, e.g. 'crm-service')."
}

variable "github_org_id" {
  type        = string
  description = "Numeric GitHub organization ID. GitHub embeds it in the OIDC sub claim (repo:org@ORG_ID/repo@REPO_ID:...) to protect against org/repo renames."
  default     = "198831707"
}

variable "github_repo_id" {
  type        = string
  description = "Numeric GitHub repository ID, embedded in the OIDC sub claim alongside github_org_id."
}

variable "github_org_id" {
  type        = string
  description = "Numeric GitHub organization ID. GitHub embeds it in the OIDC sub claim (repo:org@ORG_ID/repo@REPO_ID:...) to protect against org/repo renames."
  default     = "198831707"
}

variable "github_repo_id" {
  type        = string
  description = "Numeric GitHub repository ID, embedded in the OIDC sub claim alongside github_org_id."
}

variable "aws_region" {
  type        = string
  description = "AWS region where resources are created."
  default     = "us-east-1"
}

variable "aws_profile" {
  type        = string
  description = "AWS CLI profile for local runs. Set to null when running in CI (OIDC credentials are used automatically)."
  default     = null
}

variable "eks_cluster_names" {
  type        = list(string)
  description = "Names of EKS clusters this service is allowed to describe in this account."
  default     = []
}
