locals {
  ecs_tasks_assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Principal = {
        Service = "ecs-tasks.amazonaws.com"
      }
      Action = "sts:AssumeRole"
    }]
  })
}

resource "aws_cloudwatch_log_group" "this" {
  name              = "/ecs/${var.name}"
  retention_in_days = 30
  tags              = var.tags
}

resource "aws_ecs_cluster" "this" {
  name = var.name

  setting {
    name  = "containerInsights"
    value = "enabled"
  }

  tags = var.tags
}

resource "aws_iam_role" "execution" {
  name               = "${var.name}-execution"
  assume_role_policy = local.ecs_tasks_assume_role_policy
  tags               = var.tags
}

resource "aws_iam_role_policy" "execute_task" {
  name = "execute-task"
  role = aws_iam_role.execution.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "WriteContainerLogs"
        Effect = "Allow"
        Action = [
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = "${aws_cloudwatch_log_group.this.arn}:*"
      }
    ]
  })
}

resource "aws_iam_role_policy" "read_database_secret" {
  name = "read-database-secret"
  role = aws_iam_role.execution.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid      = "ReadDatabaseCredentials"
      Effect   = "Allow"
      Action   = ["secretsmanager:GetSecretValue"]
      Resource = var.database_secret_arn
    }]
  })
}

resource "aws_iam_role" "task" {
  name               = "${var.name}-task"
  assume_role_policy = local.ecs_tasks_assume_role_policy
  tags               = var.tags
}

resource "aws_iam_role_policy" "publish_fills" {
  name = "access-fill-queue"
  role = aws_iam_role.task.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid    = "AccessFillQueue"
      Effect = "Allow"
      Action = [
        "sqs:DeleteMessage",
        "sqs:ReceiveMessage",
        "sqs:SendMessage"
      ]
      Resource = var.fill_queue_arn
    }]
  })
}

resource "aws_ecs_task_definition" "this" {
  family                   = var.name
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = 256
  memory                   = 512
  execution_role_arn       = aws_iam_role.execution.arn
  task_role_arn            = aws_iam_role.task.arn

  runtime_platform {
    cpu_architecture        = "ARM64"
    operating_system_family = "LINUX"
  }

  container_definitions = jsonencode([{
    name      = "api"
    image     = var.container_image
    essential = true

    portMappings = [{
      containerPort = var.container_port
      hostPort      = var.container_port
      protocol      = "tcp"
    }]

    environment = [
      { name = "AWS_REGION", value = var.aws_region },
      { name = "FillQueue__Url", value = var.fill_queue_url },
      { name = "Database__Host", value = var.database_host },
      { name = "Authentication__Cognito__Authority", value = var.cognito_authority },
      { name = "Authentication__Cognito__Audience", value = var.cognito_audience }
    ]

    secrets = [
      { name = "Database__Username", valueFrom = "${var.database_secret_arn}:username::" },
      { name = "Database__Password", valueFrom = "${var.database_secret_arn}:password::" }
    ]

    logConfiguration = {
      logDriver = "awslogs"
      options = {
        awslogs-group         = aws_cloudwatch_log_group.this.name
        awslogs-region        = var.aws_region
        awslogs-stream-prefix = "api"
      }
    }
  }])

  tags = var.tags
}

resource "aws_ecs_service" "this" {
  name            = var.name
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.this.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"

  enable_execute_command            = false
  health_check_grace_period_seconds = 60
  wait_for_steady_state             = false

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  network_configuration {
    assign_public_ip = false
    security_groups  = [var.security_group_id]
    subnets          = var.subnet_ids
  }

  load_balancer {
    target_group_arn = var.target_group_arn
    container_name   = "api"
    container_port   = var.container_port
  }

  tags = var.tags

  depends_on = [
    aws_iam_role_policy.execute_task,
    aws_iam_role_policy.publish_fills,
    aws_iam_role_policy.read_database_secret
  ]
}
