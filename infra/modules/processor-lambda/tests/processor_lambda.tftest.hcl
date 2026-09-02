mock_provider "aws" {
  override_during = plan
}

override_resource {
  target          = aws_iam_role.processor
  override_during = plan
  values = {
    arn = "arn:aws:iam::000000000000:role/trade-ledger-processor-execution"
    id  = "trade-ledger-processor-execution"
  }
}

override_resource {
  target          = aws_lambda_function.processor
  override_during = plan
  values = {
    arn           = "arn:aws:lambda:eu-west-2:000000000000:function:trade-ledger-processor"
    function_name = "trade-ledger-processor"
  }
}

run "configures_net10_custom_runtime_fifo_partial_failures_and_scoped_iam" {
  command = plan

  variables {
    name               = "trade-ledger-processor"
    package_path       = "/tmp/trade-ledger-processor.zip"
    package_hash       = "dGVzdC1oYXNo"
    fill_queue_arn     = "arn:aws:sqs:eu-west-2:000000000000:trade-ledger-fills.fifo"
    aws_region         = "eu-west-2"
    subnet_ids         = ["subnet-private-a", "subnet-private-b"]
    security_group_ids = ["sg-processor"]
    environment_variables = {
      DOTNET_ENVIRONMENT             = "Development"
      Database__Host                 = "postgres"
      Database__Port                 = "5432"
      Database__Name                 = "trade_ledger"
      Database__Username             = "trade_ledger"
      Database__Password             = "trade_ledger"
      Serilog__MinimumLevel__Default = "Information"
    }
  }

  assert {
    condition = (
      aws_lambda_function.processor.runtime == "provided.al2023" &&
      aws_lambda_function.processor.handler == "bootstrap" &&
      length(aws_lambda_function.processor.architectures) == 1 &&
      aws_lambda_function.processor.architectures[0] == "arm64" &&
      aws_lambda_function.processor.filename == "/tmp/trade-ledger-processor.zip"
    )
    error_message = "The Lambda must use the packaged .NET 10 custom-runtime bootstrap."
  }

  assert {
    condition = (
      aws_lambda_event_source_mapping.fills.event_source_arn == var.fill_queue_arn &&
      aws_lambda_event_source_mapping.fills.function_name == aws_lambda_function.processor.arn &&
      aws_lambda_event_source_mapping.fills.enabled &&
      length(aws_lambda_event_source_mapping.fills.function_response_types) == 1 &&
      contains(aws_lambda_event_source_mapping.fills.function_response_types, "ReportBatchItemFailures")
    )
    error_message = "The enabled mapping must connect the fill queue and opt into partial failures."
  }

  assert {
    condition = (
      jsondecode(aws_iam_role.processor.assume_role_policy).Statement[0].Principal.Service == "lambda.amazonaws.com" &&
      jsondecode(aws_iam_role.processor.assume_role_policy).Statement[0].Action == "sts:AssumeRole"
    )
    error_message = "The execution role trust policy must permit only Lambda assumption."
  }

  assert {
    condition = (
      toset(jsondecode(aws_iam_role_policy.processor.policy).Statement[0].Action) == toset([
        "sqs:ReceiveMessage",
        "sqs:DeleteMessage",
        "sqs:GetQueueAttributes",
        "sqs:ChangeMessageVisibility"
      ]) &&
      jsondecode(aws_iam_role_policy.processor.policy).Statement[0].Resource == var.fill_queue_arn
    )
    error_message = "SQS permissions must be the queue-scoped event-source actions only."
  }

  assert {
    condition = (
      aws_lambda_function.processor.environment[0].variables["Database__Host"] == "postgres" &&
      aws_lambda_function.processor.environment[0].variables["Database__Name"] == "trade_ledger" &&
      aws_lambda_function.processor.environment[0].variables["DOTNET_ENVIRONMENT"] == "Development"
    )
    error_message = "The processor must receive its database and environment configuration."
  }

  assert {
    condition = (
      toset(aws_lambda_function.processor.vpc_config[0].subnet_ids) == toset(var.subnet_ids) &&
      toset(aws_lambda_function.processor.vpc_config[0].security_group_ids) == toset(var.security_group_ids) &&
      toset(jsondecode(aws_iam_role_policy.vpc_access[0].policy).Statement[0].Action) == toset([
        "ec2:CreateNetworkInterface",
        "ec2:DescribeNetworkInterfaces",
        "ec2:DeleteNetworkInterface",
        "ec2:AssignPrivateIpAddresses",
        "ec2:UnassignPrivateIpAddresses"
      ])
    )
    error_message = "The processor must attach to private subnets with the Lambda ENI permissions required for RDS access."
  }
}

run "rejects_missing_required_lambda_environment" {
  command = plan

  variables {
    name           = "trade-ledger-processor"
    package_path   = "/tmp/trade-ledger-processor.zip"
    package_hash   = "dGVzdC1oYXNo"
    fill_queue_arn = "arn:aws:sqs:eu-west-2:000000000000:trade-ledger-fills.fifo"
    aws_region     = "eu-west-2"
    environment_variables = {
      Database__Host     = "postgres"
      Database__Port     = "5432"
      Database__Name     = "trade_ledger"
      Database__Username = "trade_ledger"
    }
  }

  expect_failures = [var.environment_variables]
}
