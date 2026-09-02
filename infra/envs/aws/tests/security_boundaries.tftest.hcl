mock_provider "aws" {
  override_during = plan

  mock_resource "aws_db_instance" {
    defaults = {
      address = "trade-ledger.internal"
      master_user_secret = [{
        kms_key_id    = "arn:aws:kms:eu-west-2:123456789012:key/test"
        secret_arn    = "arn:aws:secretsmanager:eu-west-2:123456789012:secret:trade-ledger"
        secret_status = "active"
      }]
    }
  }

  mock_data "aws_secretsmanager_secret_version" {
    defaults = {
      secret_string = "{\"username\":\"trade_ledger_adm\",\"password\":\"test-password\"}"
    }
  }
}

override_resource {
  target          = aws_security_group.alb
  override_during = plan
  values = {
    id = "sg-alb"
  }
}

override_resource {
  target          = aws_security_group.ecs
  override_during = plan
  values = {
    id = "sg-ecs"
  }
}

override_resource {
  target          = aws_security_group.database
  override_during = plan
  values = {
    id = "sg-database"
  }
}

override_resource {
  target          = aws_security_group.processor
  override_during = plan
  values = {
    id = "sg-processor"
  }
}

run "enforces_public_api_and_private_database_boundaries" {
  command = plan

  variables {
    container_image        = "public.example/trade-ledger@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
    certificate_arn        = "arn:aws:acm:eu-west-2:123456789012:certificate/test"
    cognito_authority      = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_example"
    cognito_audience       = "example-client"
    processor_package_path = "/tmp/trade-ledger-processor.zip"
    processor_package_hash = "dGVzdC1oYXNo"
  }

  assert {
    condition = (
      toset(keys(local.ingress_rules)) == toset(["internet_to_alb_https", "alb_to_ecs", "ecs_to_database", "processor_to_database"]) &&
      aws_vpc_security_group_ingress_rule.this["internet_to_alb_https"].cidr_ipv4 == "0.0.0.0/0" &&
      aws_vpc_security_group_ingress_rule.this["internet_to_alb_https"].from_port == 443 &&
      aws_vpc_security_group_ingress_rule.this["internet_to_alb_https"].to_port == 443 &&
      aws_vpc_security_group_ingress_rule.this["internet_to_alb_https"].ip_protocol == "tcp"
    )
    error_message = "The ALB must be the only security group accepting public ingress, on HTTPS only."
  }

  assert {
    condition = (
      aws_vpc_security_group_ingress_rule.this["alb_to_ecs"].referenced_security_group_id == aws_security_group.alb.id &&
      aws_vpc_security_group_ingress_rule.this["alb_to_ecs"].security_group_id == aws_security_group.ecs.id &&
      aws_vpc_security_group_ingress_rule.this["alb_to_ecs"].from_port == 8080 &&
      aws_vpc_security_group_ingress_rule.this["alb_to_ecs"].to_port == 8080 &&
      aws_vpc_security_group_ingress_rule.this["alb_to_ecs"].ip_protocol == "tcp"
    )
    error_message = "ECS ingress must be restricted to the ALB security group on the API port."
  }

  assert {
    condition = (
      aws_vpc_security_group_ingress_rule.this["ecs_to_database"].referenced_security_group_id == aws_security_group.ecs.id &&
      aws_vpc_security_group_ingress_rule.this["ecs_to_database"].security_group_id == aws_security_group.database.id &&
      aws_vpc_security_group_ingress_rule.this["ecs_to_database"].from_port == 5432 &&
      aws_vpc_security_group_ingress_rule.this["ecs_to_database"].to_port == 5432 &&
      aws_vpc_security_group_ingress_rule.this["ecs_to_database"].ip_protocol == "tcp"
    )
    error_message = "Database ingress must be restricted to the ECS security group on PostgreSQL."
  }

  assert {
    condition = (
      toset(keys(local.egress_rules)) == toset(["alb_to_ecs", "ecs_https", "ecs_to_database", "processor_to_database"]) &&
      aws_vpc_security_group_egress_rule.this["alb_to_ecs"].referenced_security_group_id == aws_security_group.ecs.id &&
      aws_vpc_security_group_egress_rule.this["alb_to_ecs"].from_port == 8080 &&
      aws_vpc_security_group_egress_rule.this["ecs_https"].cidr_ipv4 == "0.0.0.0/0" &&
      aws_vpc_security_group_egress_rule.this["ecs_https"].from_port == 443 &&
      aws_vpc_security_group_egress_rule.this["ecs_to_database"].referenced_security_group_id == aws_security_group.database.id &&
      aws_vpc_security_group_egress_rule.this["ecs_to_database"].from_port == 5432
    )
    error_message = "Egress must be limited to the explicit ALB, ECS, and processor workload paths."
  }

  assert {
    condition = (
      aws_vpc_security_group_ingress_rule.this["processor_to_database"].referenced_security_group_id == aws_security_group.processor.id &&
      aws_vpc_security_group_ingress_rule.this["processor_to_database"].security_group_id == aws_security_group.database.id &&
      aws_vpc_security_group_ingress_rule.this["processor_to_database"].from_port == 5432 &&
      aws_vpc_security_group_egress_rule.this["processor_to_database"].security_group_id == aws_security_group.processor.id &&
      aws_vpc_security_group_egress_rule.this["processor_to_database"].referenced_security_group_id == aws_security_group.database.id &&
      aws_vpc_security_group_egress_rule.this["processor_to_database"].to_port == 5432
    )
    error_message = "The processor Lambda must be able to reach only the database security group on PostgreSQL."
  }
}
