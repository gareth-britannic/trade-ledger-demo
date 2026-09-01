output "queue_arn" {
  description = "ARN of the fill queue."
  value       = aws_sqs_queue.fills.arn
}

output "queue_url" {
  description = "URL of the fill queue."
  value       = aws_sqs_queue.fills.id
}

output "dlq_arn" {
  description = "ARN of the dead-letter queue."
  value       = aws_sqs_queue.dead_letter.arn
}

output "dlq_url" {
  description = "URL of the dead-letter queue."
  value       = aws_sqs_queue.dead_letter.id
}
