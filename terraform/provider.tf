terraform {
  required_providers {
    aws = { source = "hashicorp/aws", version = "~> 5.70" }
  }
}

provider "aws" {
  profile = var.aws_profile
  region  = var.aws_region

  default_tags {
    tags = {
      Service     = var.service_name
      Environment = var.environment
      ManagedBy   = "terraform"
      Repository  = "${var.github_org}/${var.github_repo}"
    }
  }
}
