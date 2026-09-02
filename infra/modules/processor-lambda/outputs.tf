output "function_arn" {
  description = "ARN of the fill processor Lambda."
  value       = aws_lambda_function.processor.arn
}

output "function_name" {
  description = "Name of the fill processor Lambda."
  value       = aws_lambda_function.processor.function_name
}

output "event_source_mapping_uuid" {
  description = "UUID of the FIFO SQS event source mapping."
  value       = aws_lambda_event_source_mapping.fills.uuid
}

output "runtime" {
  description = "Custom runtime used by the self-contained .NET 10 package."
  value       = aws_lambda_function.processor.runtime
}
