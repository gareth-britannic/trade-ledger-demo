output "load_balancer_dns_name" {
  description = "Public DNS name of the HTTPS ALB."
  value       = module.public_api_edge.load_balancer_dns_name
}

output "fill_queue_url" {
  description = "URL of the FIFO fill queue."
  value       = module.fill_queue.queue_url
}

output "database_address" {
  description = "Private RDS endpoint."
  value       = module.database.address
}

output "processor_lambda_name" {
  description = "Fill processor Lambda function name."
  value       = module.processor_lambda.function_name
}
