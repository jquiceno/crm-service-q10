terraform {
  backend "s3" {
    bucket       = "q10-terraform-state-764283926096"
    region       = "us-east-1"
    use_lockfile = true
    encrypt      = true
    # key and profile are passed via -backend-config at init time:
    #   terraform init \
    #     -backend-config="key=services/service-template/dev/terraform.tfstate" \
    #     -backend-config="profile=informes-dev"
    # In CI the profile flag is omitted; OIDC env vars are used automatically.
  }
}
