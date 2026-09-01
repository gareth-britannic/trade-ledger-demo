mock_provider "aws" {
  override_during = plan
}

override_resource {
  target          = aws_nat_gateway.this
  override_during = plan
  values = {
    id = "nat-test"
  }
}

override_resource {
  target          = aws_internet_gateway.this
  override_during = plan
  values = {
    id = "igw-test"
  }
}

run "uses_two_availability_zones_and_three_subnet_tiers" {
  command = plan

  variables {
    name               = "trade-ledger-test"
    availability_zones = ["eu-west-2a", "eu-west-2b"]
  }

  assert {
    condition = (
      length(aws_subnet.public) == 2 &&
      length(aws_subnet.application) == 2 &&
      length(aws_subnet.database) == 2
    )
    error_message = "The VPC must contain public, application, and database subnets in both AZs."
  }

  assert {
    condition = toset([for subnet in values(aws_subnet.public) : subnet.availability_zone]) == toset([
      "eu-west-2a",
      "eu-west-2b"
    ])
    error_message = "Public subnets must span both configured availability zones."
  }

  assert {
    condition     = alltrue([for subnet in values(aws_subnet.public) : subnet.map_public_ip_on_launch])
    error_message = "Only public-tier subnets should assign public IP addresses."
  }

  assert {
    condition = alltrue(concat(
      [for subnet in values(aws_subnet.application) : !subnet.map_public_ip_on_launch],
      [for subnet in values(aws_subnet.database) : !subnet.map_public_ip_on_launch]
    ))
    error_message = "Application and database subnets must not assign public IP addresses."
  }

  assert {
    condition     = aws_route.default["application"].nat_gateway_id == aws_nat_gateway.this.id
    error_message = "Application egress must use the NAT gateway."
  }

  assert {
    condition = (
      toset(keys(local.default_routes)) == toset(["public", "application"]) &&
      !contains(keys(local.default_routes), "database") &&
      aws_route.default["public"].gateway_id == aws_internet_gateway.this.id &&
      aws_route.default["application"].nat_gateway_id == aws_nat_gateway.this.id
    )
    error_message = "The database route table must remain isolated from internet routes."
  }
}

run "rejects_an_invalid_availability_zone_count" {
  command = plan

  variables {
    name               = "trade-ledger-test"
    availability_zones = ["eu-west-2a"]
  }

  expect_failures = [var.availability_zones]
}
