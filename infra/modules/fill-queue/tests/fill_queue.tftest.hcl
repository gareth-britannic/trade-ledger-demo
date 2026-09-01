mock_provider "aws" {
  override_during = plan
}

override_resource {
  target          = aws_sqs_queue.fills
  override_during = plan
  values = {
    arn = "arn:aws:sqs:eu-west-2:123456789012:trade-ledger-test-fills.fifo"
    id  = "https://sqs.eu-west-2.amazonaws.com/123456789012/trade-ledger-test-fills.fifo"
  }
}

override_resource {
  target          = aws_sqs_queue.dead_letter
  override_during = plan
  values = {
    arn = "arn:aws:sqs:eu-west-2:123456789012:trade-ledger-test-fills-dlq.fifo"
    id  = "https://sqs.eu-west-2.amazonaws.com/123456789012/trade-ledger-test-fills-dlq.fifo"
  }
}

run "creates_fifo_queue_with_explicit_deduplication_and_dlq" {
  command = plan

  variables {
    name = "trade-ledger-test-fills"
  }

  assert {
    condition = (
      aws_sqs_queue.fills.fifo_queue &&
      endswith(aws_sqs_queue.fills.name, ".fifo") &&
      aws_sqs_queue.fills.sqs_managed_sse_enabled
    )
    error_message = "The fill queue must be FIFO and use the .fifo suffix."
  }

  assert {
    condition     = !aws_sqs_queue.fills.content_based_deduplication
    error_message = "Content-based deduplication must remain disabled so producers supply the fill ID."
  }

  assert {
    condition = (
      aws_sqs_queue.dead_letter.fifo_queue &&
      endswith(aws_sqs_queue.dead_letter.name, ".fifo") &&
      !aws_sqs_queue.dead_letter.content_based_deduplication &&
      aws_sqs_queue.dead_letter.sqs_managed_sse_enabled
    )
    error_message = "The dead-letter queue must also be FIFO."
  }

  assert {
    condition = (
      jsondecode(aws_sqs_queue_redrive_policy.fills.redrive_policy).maxReceiveCount == 3 &&
      jsondecode(aws_sqs_queue_redrive_policy.fills.redrive_policy).deadLetterTargetArn == aws_sqs_queue.dead_letter.arn
    )
    error_message = "The source queue must redrive after three receives."
  }

  assert {
    condition = (
      jsondecode(aws_sqs_queue_redrive_allow_policy.dead_letter.redrive_allow_policy).redrivePermission == "byQueue" &&
      toset(jsondecode(aws_sqs_queue_redrive_allow_policy.dead_letter.redrive_allow_policy).sourceQueueArns) == toset([aws_sqs_queue.fills.arn])
    )
    error_message = "The DLQ must restrict redrive permission to its source queue."
  }
}

run "rejects_an_invalid_fifo_queue_name" {
  command = plan

  variables {
    name = "invalid.name"
  }

  expect_failures = [var.name]
}
