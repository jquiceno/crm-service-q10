terraform {
  backend "s3" {
    region       = "us-east-1"
    use_lockfile = true # requires Terraform >= 1.10; replaces DynamoDB locking
    encrypt      = true
    # bucket, key and profile are passed via -backend-config at init time.
    # bucket = q10-terraform-state-<account_id> per environment:
    #   dev/qa: q10-terraform-state-764283926096
    #   prod:   q10-terraform-state-451828143717
    #   terraform init \
    #     -backend-config="bucket=q10-terraform-state-764283926096" \
    #     -backend-config="key=services/service-template/dev/terraform.tfstate" \
    #     -backend-config="profile=informes-dev"
    # In CI the profile flag is omitted; OIDC env vars are used automatically.
  }
}
