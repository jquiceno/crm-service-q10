output "deploy_role_arn" {
  value     = aws_iam_role.github_deploy.arn
  sensitive = true
}

output "deploy_role_name" {
  value = aws_iam_role.github_deploy.name
}

output "ecr_repository_url" {
  value = aws_ecr_repository.this.repository_url
}

output "ecr_repository_arn" {
  value = aws_ecr_repository.this.arn
}
