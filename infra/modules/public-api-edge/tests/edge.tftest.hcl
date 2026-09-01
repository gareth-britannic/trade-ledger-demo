mock_provider "aws" {
  override_during = plan
}

override_resource {
  target          = aws_lb.this
  override_during = plan
  values = {
    arn = "arn:aws:elasticloadbalancing:eu-west-2:123456789012:loadbalancer/app/trade-ledger-test/1234"
  }
}

run "exposes_only_an_https_alb_protected_by_managed_waf_rules" {
  command = plan

  variables {
    name              = "trade-ledger-test"
    vpc_id            = "vpc-test"
    public_subnet_ids = ["subnet-public-a", "subnet-public-b"]
    security_group_id = "sg-alb"
    certificate_arn   = "arn:aws:acm:eu-west-2:123456789012:certificate/test"
  }

  assert {
    condition     = !aws_lb.this.internal && aws_lb.this.load_balancer_type == "application"
    error_message = "The edge must be an internet-facing Application Load Balancer."
  }

  assert {
    condition     = toset(aws_lb.this.subnets) == toset(["subnet-public-a", "subnet-public-b"])
    error_message = "The ALB must span both public subnets."
  }

  assert {
    condition = (
      aws_lb_listener.https.protocol == "HTTPS" &&
      aws_lb_listener.https.port == 443 &&
      aws_lb_listener.https.ssl_policy == "ELBSecurityPolicy-TLS13-1-2-2021-06" &&
      aws_lb_listener.https.certificate_arn == "arn:aws:acm:eu-west-2:123456789012:certificate/test"
    )
    error_message = "The ALB must expose HTTPS on port 443."
  }

  assert {
    condition = (
      aws_lb_target_group.api.target_type == "ip" &&
      aws_lb_target_group.api.protocol == "HTTP" &&
      aws_lb_target_group.api.port == 8080 &&
      aws_lb_target_group.api.health_check[0].path == "/health" &&
      aws_lb_target_group.api.health_check[0].matcher == "200-399"
    )
    error_message = "The ALB target group must match private awsvpc targets and the API health endpoint."
  }

  assert {
    condition     = aws_wafv2_web_acl_association.this.resource_arn == aws_lb.this.arn
    error_message = "The WAF web ACL must be associated with the public ALB."
  }

  assert {
    condition = (
      toset([
        for rule in aws_wafv2_web_acl.this.rule :
        rule.statement[0].managed_rule_group_statement[0].name
      ]) == toset(["AWSManagedRulesCommonRuleSet", "AWSManagedRulesKnownBadInputsRuleSet"]) &&
      alltrue([for rule in aws_wafv2_web_acl.this.rule : rule.statement[0].managed_rule_group_statement[0].vendor_name == "AWS"])
    )
    error_message = "The WAF must use the two configured AWS managed rule groups."
  }
}

run "rejects_invalid_edge_inputs" {
  command = plan

  variables {
    name              = "trade-ledger-test"
    vpc_id            = "vpc-test"
    public_subnet_ids = ["subnet-public-a"]
    security_group_id = "sg-alb"
    certificate_arn   = "not-an-acm-arn"
    target_port       = 70000
  }

  expect_failures = [var.public_subnet_ids, var.certificate_arn, var.target_port]
}
