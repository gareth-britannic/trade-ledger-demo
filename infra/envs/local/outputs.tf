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
