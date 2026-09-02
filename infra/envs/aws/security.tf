resource "aws_security_group" "alb" {
  name        = "${local.name}-alb"
  description = "Public HTTPS ingress to the application load balancer."
  vpc_id      = module.network.vpc_id
  tags        = merge(local.tags, { Name = "${local.name}-alb" })
}

resource "aws_security_group" "ecs" {
  name        = "${local.name}-ecs"
  description = "Private ECS tasks; ingress is restricted to the ALB."
  vpc_id      = module.network.vpc_id
  tags        = merge(local.tags, { Name = "${local.name}-ecs" })
}

resource "aws_security_group" "database" {
  name        = "${local.name}-database"
  description = "Private PostgreSQL; ingress is restricted to application workloads."
  vpc_id      = module.network.vpc_id
  tags        = merge(local.tags, { Name = "${local.name}-database" })
}

resource "aws_security_group" "processor" {
  name        = "${local.name}-processor"
  description = "Private fill processor Lambda; egress is restricted to PostgreSQL."
  vpc_id      = module.network.vpc_id
  tags        = merge(local.tags, { Name = "${local.name}-processor" })
}

locals {
  ingress_rules = {
    internet_to_alb_https = {
      security_group_id            = aws_security_group.alb.id
      description                  = "Public HTTPS"
      cidr_ipv4                    = "0.0.0.0/0"
      referenced_security_group_id = null
      from_port                    = 443
      to_port                      = 443
    }
    alb_to_ecs = {
      security_group_id            = aws_security_group.ecs.id
      description                  = "Accept API traffic only from the ALB"
      cidr_ipv4                    = null
      referenced_security_group_id = aws_security_group.alb.id
      from_port                    = 8080
      to_port                      = 8080
    }
    ecs_to_database = {
      security_group_id            = aws_security_group.database.id
      description                  = "Accept PostgreSQL only from ECS tasks"
      cidr_ipv4                    = null
      referenced_security_group_id = aws_security_group.ecs.id
      from_port                    = 5432
      to_port                      = 5432
    }
    processor_to_database = {
      security_group_id            = aws_security_group.database.id
      description                  = "Accept PostgreSQL only from the fill processor Lambda"
      cidr_ipv4                    = null
      referenced_security_group_id = aws_security_group.processor.id
      from_port                    = 5432
      to_port                      = 5432
    }
  }

  egress_rules = {
    alb_to_ecs = {
      security_group_id            = aws_security_group.alb.id
      description                  = "Forward API traffic to ECS"
      cidr_ipv4                    = null
      referenced_security_group_id = aws_security_group.ecs.id
      from_port                    = 8080
      to_port                      = 8080
    }
    ecs_https = {
      security_group_id            = aws_security_group.ecs.id
      description                  = "Reach AWS service APIs through NAT"
      cidr_ipv4                    = "0.0.0.0/0"
      referenced_security_group_id = null
      from_port                    = 443
      to_port                      = 443
    }
    ecs_to_database = {
      security_group_id            = aws_security_group.ecs.id
      description                  = "Connect to PostgreSQL"
      cidr_ipv4                    = null
      referenced_security_group_id = aws_security_group.database.id
      from_port                    = 5432
      to_port                      = 5432
    }
    processor_to_database = {
      security_group_id            = aws_security_group.processor.id
      description                  = "Connect to PostgreSQL"
      cidr_ipv4                    = null
      referenced_security_group_id = aws_security_group.database.id
      from_port                    = 5432
      to_port                      = 5432
    }
  }
}

resource "aws_vpc_security_group_ingress_rule" "this" {
  for_each = local.ingress_rules

  security_group_id            = each.value.security_group_id
  description                  = each.value.description
  cidr_ipv4                    = each.value.cidr_ipv4
  referenced_security_group_id = each.value.referenced_security_group_id
  from_port                    = each.value.from_port
  to_port                      = each.value.to_port
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "this" {
  for_each = local.egress_rules

  security_group_id            = each.value.security_group_id
  description                  = each.value.description
  cidr_ipv4                    = each.value.cidr_ipv4
  referenced_security_group_id = each.value.referenced_security_group_id
  from_port                    = each.value.from_port
  to_port                      = each.value.to_port
  ip_protocol                  = "tcp"
}
