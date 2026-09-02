output "fill_queue_url" {
  description = "Local URL of the FIFO fill queue."
  value       = module.fill_queue.queue_url
}

output "fill_queue_arn" {
  description = "ARN of the FIFO fill queue."
  value       = module.fill_queue.queue_arn
}

output "fill_dlq_url" {
  description = "Local URL of the fill dead-letter queue."
  value       = module.fill_queue.dlq_url
}

output "processor_lambda_name" {
  description = "Name of the local fill processor Lambda."
  value       = module.processor_lambda.function_name
}

output "processor_event_source_mapping_uuid" {
  description = "UUID of the local FIFO queue event source mapping."
  value       = module.processor_lambda.event_source_mapping_uuid
}
