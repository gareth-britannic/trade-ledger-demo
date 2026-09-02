resource "aws_iam_role" "processor" {
  name = "${var.name}-execution"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Principal = {
        Service = "lambda.amazonaws.com"
      }
      Action = "sts:AssumeRole"
    }]
  })

  tags = var.tags
}

resource "aws_cloudwatch_log_group" "processor" {
  name              = "/aws/lambda/${var.name}"
  retention_in_days = 7
  tags              = var.tags
}

resource "aws_iam_role_policy" "processor" {
  name = "${var.name}-queue-and-logs"
  role = aws_iam_role.processor.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "ConsumeFillQueue"
        Effect = "Allow"
        Action = [
          "sqs:ReceiveMessage",
          "sqs:DeleteMessage",
          "sqs:GetQueueAttributes",
          "sqs:ChangeMessageVisibility"
        ]
        Resource = var.fill_queue_arn
      },
      {
        Sid    = "WriteProcessorLogs"
        Effect = "Allow"
        Action = [
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = "arn:aws:logs:${var.aws_region}:${var.aws_account_id}:log-group:${aws_cloudwatch_log_group.processor.name}:*"
      }
    ]
  })
}

resource "aws_iam_role_policy" "vpc_access" {
  count = length(var.subnet_ids) > 0 ? 1 : 0

  name = "${var.name}-vpc-access"
  role = aws_iam_role.processor.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid    = "ManageLambdaNetworkInterfaces"
      Effect = "Allow"
      Action = [
        "ec2:CreateNetworkInterface",
        "ec2:DescribeNetworkInterfaces",
        "ec2:DeleteNetworkInterface",
        "ec2:AssignPrivateIpAddresses",
        "ec2:UnassignPrivateIpAddresses"
      ]
      Resource = "*"
    }]
  })
}

resource "aws_lambda_function" "processor" {
  function_name    = var.name
  description      = "Applies persisted Trade Ledger fills in deterministic execution-time order."
  role             = aws_iam_role.processor.arn
  runtime          = "provided.al2023"
  handler          = "bootstrap"
  architectures    = ["arm64"]
  filename         = var.package_path
  source_code_hash = var.package_hash
  memory_size      = 512
  timeout          = 30

  environment {
    variables = var.environment_variables
  }

  dynamic "vpc_config" {
    for_each = length(var.subnet_ids) > 0 ? [true] : []

    content {
      subnet_ids         = var.subnet_ids
      security_group_ids = var.security_group_ids
    }
  }

  depends_on = [
    aws_cloudwatch_log_group.processor,
    aws_iam_role_policy.processor,
    aws_iam_role_policy.vpc_access
  ]

  tags = var.tags
}

resource "aws_lambda_event_source_mapping" "fills" {
  event_source_arn                   = var.fill_queue_arn
  function_name                      = aws_lambda_function.processor.arn
  enabled                            = true
  batch_size                         = var.batch_size
  maximum_batching_window_in_seconds = var.maximum_batching_window_seconds
  function_response_types            = ["ReportBatchItemFailures"]

  depends_on = [aws_iam_role_policy.processor]
}
