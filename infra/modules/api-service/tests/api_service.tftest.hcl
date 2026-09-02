mock_provider "aws" {
  override_during = plan
}

override_resource {
  target          = aws_iam_role.execution
  override_during = plan
  values = {
    arn = "arn:aws:iam::123456789012:role/trade-ledger-test-api-execution"
  }
}

override_resource {
  target          = aws_iam_role.task
  override_during = plan
  values = {
    arn = "arn:aws:iam::123456789012:role/trade-ledger-test-api-task"
  }
}

override_resource {
  target          = aws_cloudwatch_log_group.this
  override_during = plan
  values = {
    arn = "arn:aws:logs:eu-west-2:123456789012:log-group:/ecs/trade-ledger-test-api"
  }
}

run "keeps_fargate_private_and_iam_least_privilege" {
  command = plan

  variables {
    name                = "trade-ledger-test-api"
    aws_region          = "eu-west-2"
    container_image     = "public.example/trade-ledger@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
    subnet_ids          = ["subnet-app-a", "subnet-app-b"]
    security_group_id   = "sg-ecs"
    target_group_arn    = "arn:aws:elasticloadbalancing:eu-west-2:123456789012:targetgroup/test/1234"
    fill_queue_arn      = "arn:aws:sqs:eu-west-2:123456789012:trade-ledger-fills.fifo"
    fill_queue_url      = "https://sqs.eu-west-2.amazonaws.com/123456789012/trade-ledger-fills.fifo"
    database_host       = "trade-ledger.cluster.example.internal"
    database_secret_arn = "arn:aws:secretsmanager:eu-west-2:123456789012:secret:trade-ledger"
    cognito_authority   = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_example"
    cognito_audience    = "example-client"
  }

  assert {
    condition     = !aws_ecs_service.this.network_configuration[0].assign_public_ip
    error_message = "ECS tasks must not receive public IP addresses."
  }

  assert {
    condition = (
      aws_ecs_task_definition.this.requires_compatibilities == toset(["FARGATE"]) &&
      aws_ecs_task_definition.this.cpu == "256" &&
      aws_ecs_task_definition.this.memory == "512" &&
      one(aws_ecs_task_definition.this.runtime_platform).cpu_architecture == "ARM64" &&
      one(aws_ecs_task_definition.this.runtime_platform).operating_system_family == "LINUX" &&
      aws_ecs_service.this.desired_count == 1 &&
      !aws_ecs_service.this.enable_execute_command
    )
    error_message = "The reference service must use one minimum-sized ARM64 Linux Fargate task with ECS Exec disabled."
  }

  assert {
    condition = (
      one(aws_ecs_cluster.this.setting).name == "containerInsights" &&
      one(aws_ecs_cluster.this.setting).value == "enabled" &&
      aws_cloudwatch_log_group.this.retention_in_days == 30 &&
      aws_ecs_service.this.deployment_circuit_breaker[0].enable &&
      aws_ecs_service.this.deployment_circuit_breaker[0].rollback
    )
    error_message = "Container Insights, finite log retention, and deployment rollback must remain enabled."
  }

  assert {
    condition     = toset(aws_ecs_service.this.network_configuration[0].subnets) == toset(["subnet-app-a", "subnet-app-b"])
    error_message = "ECS tasks must run in both private application subnets."
  }

  assert {
    condition     = aws_ecs_task_definition.this.network_mode == "awsvpc"
    error_message = "Fargate tasks must use awsvpc networking."
  }

  assert {
    condition = (
      toset(jsondecode(aws_iam_role_policy.publish_fills.policy).Statement[0].Action) == toset(["sqs:SendMessage"]) &&
      jsondecode(aws_iam_role_policy.publish_fills.policy).Statement[0].Resource == "arn:aws:sqs:eu-west-2:123456789012:trade-ledger-fills.fifo"
    )
    error_message = "The task role must only publish messages to the supplied fill queue."
  }

  assert {
    condition = (
      toset(jsondecode(aws_iam_role_policy.read_database_secret.policy).Statement[0].Action) == toset(["secretsmanager:GetSecretValue"]) &&
      jsondecode(aws_iam_role_policy.read_database_secret.policy).Statement[0].Resource == "arn:aws:secretsmanager:eu-west-2:123456789012:secret:trade-ledger"
    )
    error_message = "The execution role must only read the supplied database secret."
  }


  assert {
    condition = (
      jsondecode(aws_iam_role.execution.assume_role_policy).Statement[0].Principal.Service == "ecs-tasks.amazonaws.com" &&
      jsondecode(aws_iam_role.task.assume_role_policy).Statement[0].Principal.Service == "ecs-tasks.amazonaws.com" &&
      aws_ecs_task_definition.this.execution_role_arn == aws_iam_role.execution.arn &&
      aws_ecs_task_definition.this.task_role_arn == aws_iam_role.task.arn &&
      aws_iam_role.execution.arn != aws_iam_role.task.arn
    )
    error_message = "Execution and application roles must remain separate and trust only ECS tasks."
  }

  assert {
    condition = (
      toset([for statement in jsondecode(aws_iam_role_policy.execute_task.policy).Statement : statement.Sid]) == toset(["WriteContainerLogs"]) &&
      endswith(one([for statement in jsondecode(aws_iam_role_policy.execute_task.policy).Statement : statement if statement.Sid == "WriteContainerLogs"]).Resource, ":*")
    )
    error_message = "Execution permissions must be limited to this service's CloudWatch log streams."
  }

  assert {
    condition = (
      jsondecode(aws_ecs_task_definition.this.container_definitions)[0].portMappings[0].containerPort == 8080 &&
      jsondecode(aws_ecs_task_definition.this.container_definitions)[0].secrets[0].valueFrom == "arn:aws:secretsmanager:eu-west-2:123456789012:secret:trade-ledger:username::" &&
      jsondecode(aws_ecs_task_definition.this.container_definitions)[0].secrets[1].valueFrom == "arn:aws:secretsmanager:eu-west-2:123456789012:secret:trade-ledger:password::" &&
      one(aws_ecs_service.this.load_balancer).container_name == "api" &&
      one(aws_ecs_service.this.load_balancer).container_port == 8080
    )
    error_message = "The target group, container port, logs, and secret JSON-key injection must remain coherent."
  }
}

run "rejects_invalid_service_inputs" {
  command = plan

  variables {
    name                = "trade-ledger-test-api"
    aws_region          = "eu-west-2"
    container_image     = "example.invalid/image:latest"
    container_port      = 70000
    subnet_ids          = ["subnet-app-a"]
    security_group_id   = "sg-ecs"
    target_group_arn    = "arn:aws:elasticloadbalancing:eu-west-2:123456789012:targetgroup/test/1234"
    fill_queue_arn      = "arn:aws:sqs:eu-west-2:123456789012:trade-ledger-fills.fifo"
    fill_queue_url      = "https://sqs.eu-west-2.amazonaws.com/123456789012/trade-ledger-fills.fifo"
    database_host       = "trade-ledger.internal"
    database_secret_arn = "arn:aws:secretsmanager:eu-west-2:123456789012:secret:trade-ledger"
    cognito_authority   = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_example"
    cognito_audience    = "example-client"
  }

  expect_failures = [var.container_image, var.container_port, var.subnet_ids]
}
