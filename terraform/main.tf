data "aws_caller_identity" "current" {}

data "aws_iam_openid_connect_provider" "github_actions" {
  url = "https://token.actions.githubusercontent.com"
}

data "aws_instances" "bastions" {
  filter {
    name   = "tag:Name"
    values = ["q10-*-ssm-bastion"]
  }
  filter {
    name   = "instance-state-name"
    values = ["running"]
  }
}

resource "aws_ecr_repository" "this" {
  name                 = "q10-${var.service_name}"
  image_tag_mutability = var.environment == "prod" ? "IMMUTABLE" : "MUTABLE"
  image_scanning_configuration { scan_on_push = true }
}

resource "aws_ecr_lifecycle_policy" "this" {
  repository = aws_ecr_repository.this.name
  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep last 10 tagged images per env prefix"
        selection    = { tagStatus = "tagged", tagPrefixList = ["dev-", "qa-", "prod-"], countType = "imageCountMoreThan", countNumber = 10 }
        action       = { type = "expire" }
      },
      {
        rulePriority = 2
        description  = "Expire untagged images after 7 days"
        selection    = { tagStatus = "untagged", countType = "sinceImagePushed", countUnit = "days", countNumber = 7 }
        action       = { type = "expire" }
      }
    ]
  })
}

data "aws_iam_policy_document" "assume_role" {
  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]
    effect  = "Allow"
    principals {
      type        = "Federated"
      identifiers = [data.aws_iam_openid_connect_provider.github_actions.arn]
    }
    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }
    # Restrict to the specific GitHub environment, not the whole repo.
    # A workflow running in environment:dev cannot assume the qa/prod role.
    # GitHub now issues the sub claim with immutable org/repo IDs embedded
    # (repo:org@ORG_ID/repo@REPO_ID:environment:env); the legacy format is
    # kept as fallback in case the org setting is rolled back.
    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:sub"
      values = [
        "repo:${var.github_org}/${var.github_repo}:environment:${var.environment}",
        "repo:${var.github_org}@${var.github_org_id}/${var.github_repo}@${var.github_repo_id}:environment:${var.environment}",
      ]
    }
  }
}

resource "aws_iam_role" "github_deploy" {
  name               = "q10-${var.service_name}-github-deploy"
  assume_role_policy = data.aws_iam_policy_document.assume_role.json
}

data "aws_iam_policy_document" "deploy" {
  statement {
    sid       = "ECRAuth"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"]
  }
  statement {
    sid = "ECRPush"
    actions = [
      "ecr:BatchCheckLayerAvailability",
      "ecr:GetDownloadUrlForLayer",
      "ecr:BatchGetImage",
      "ecr:PutImage",
      "ecr:InitiateLayerUpload",
      "ecr:UploadLayerPart",
      "ecr:CompleteLayerUpload",
    ]
    resources = [aws_ecr_repository.this.arn]
  }
  statement {
    sid     = "EKSDescribe"
    actions = ["eks:DescribeCluster"]
    resources = length(var.eks_cluster_names) > 0 ? [
      for name in var.eks_cluster_names :
      "arn:aws:eks:${var.aws_region}:${data.aws_caller_identity.current.account_id}:cluster/${name}"
    ] : ["arn:aws:eks:${var.aws_region}:${data.aws_caller_identity.current.account_id}:cluster/*"]
  }
  statement {
    sid     = "SSMTunnel"
    actions = ["ssm:StartSession"]
    resources = concat(
      [for id in data.aws_instances.bastions.ids :
        "arn:aws:ec2:${var.aws_region}:${data.aws_caller_identity.current.account_id}:instance/${id}"],
      ["arn:aws:ssm:${var.aws_region}::document/AWS-StartPortForwardingSessionToRemoteHost"]
    )
  }
  statement {
    sid     = "SSMSession"
    actions = ["ssm:TerminateSession", "ssm:ResumeSession"]
    resources = ["arn:aws:ssm:${var.aws_region}:${data.aws_caller_identity.current.account_id}:session/*"]
  }
}

resource "aws_iam_role_policy" "deploy" {
  name   = "deploy-policy"
  role   = aws_iam_role.github_deploy.name
  policy = data.aws_iam_policy_document.deploy.json

  lifecycle {
    precondition {
      condition     = length(data.aws_instances.bastions.ids) > 0
      error_message = "No running EC2 instances with tag Name=q10-*-ssm-bastion found. Ensure the bastion is running before applying."
    }
  }
}
