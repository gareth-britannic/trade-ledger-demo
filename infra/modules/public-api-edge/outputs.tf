output "target_group_arn" {
  description = "ARN of the API target group."
  value       = aws_lb_target_group.api.arn
  depends_on  = [aws_lb_listener.https]
}

output "load_balancer_dns_name" {
  description = "Public DNS name assigned to the ALB."
  value       = aws_lb.this.dns_name
}

output "load_balancer_arn" {
  description = "ARN of the public ALB."
  value       = aws_lb.this.arn
}
