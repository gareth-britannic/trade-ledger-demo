resource "aws_sqs_queue" "dead_letter" {
  name                        = "${var.name}-dlq.fifo"
  fifo_queue                  = true
  content_based_deduplication = false
  message_retention_seconds   = var.dlq_message_retention_seconds

  tags = var.tags
}

resource "aws_sqs_queue" "fills" {
  name                        = "${var.name}.fifo"
  fifo_queue                  = true
  content_based_deduplication = false
  visibility_timeout_seconds  = var.visibility_timeout_seconds
  message_retention_seconds   = var.message_retention_seconds

  tags = var.tags
}

resource "aws_sqs_queue_redrive_policy" "fills" {
  queue_url = aws_sqs_queue.fills.id

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.dead_letter.arn
    maxReceiveCount     = var.max_receive_count
  })
}

resource "aws_sqs_queue_redrive_allow_policy" "dead_letter" {
  queue_url = aws_sqs_queue.dead_letter.id

  redrive_allow_policy = jsonencode({
    redrivePermission = "byQueue"
    sourceQueueArns   = [aws_sqs_queue.fills.arn]
  })
}
