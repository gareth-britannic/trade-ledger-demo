output "address" {
  description = "DNS name of the RDS instance."
  value       = aws_db_instance.this.address
}

output "port" {
  description = "PostgreSQL listener port."
  value       = aws_db_instance.this.port
}

output "master_user_secret_arn" {
  description = "ARN of the AWS-managed master-user secret."
  value       = try(aws_db_instance.this.master_user_secret[0].secret_arn, null)
}
