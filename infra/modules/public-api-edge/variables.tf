variable "name" {
  description = "Name used for edge resources."
  type        = string
}

variable "vpc_id" {
  description = "VPC containing the load balancer and targets."
  type        = string
}

variable "public_subnet_ids" {
  description = "Public subnet IDs for the internet-facing ALB."
  type        = list(string)

  validation {
    condition     = length(var.public_subnet_ids) == 2 && length(distinct(var.public_subnet_ids)) == 2
    error_message = "public_subnet_ids must contain exactly two distinct public subnet IDs."
  }
}

variable "security_group_id" {
  description = "Security group attached to the ALB."
  type        = string
}

variable "certificate_arn" {
  description = "ACM certificate ARN for the HTTPS listener."
  type        = string

  validation {
    condition     = can(regex("^arn:aws[a-z-]*:acm:[a-z0-9-]+:[0-9]{12}:certificate/[A-Za-z0-9-]+$", var.certificate_arn))
    error_message = "certificate_arn must be an ACM certificate ARN."
  }
}

variable "target_port" {
  description = "Port exposed by the ECS task."
  type        = number
  default     = 8080

  validation {
    condition     = var.target_port >= 1 && var.target_port <= 65535
    error_message = "target_port must be between 1 and 65535."
  }
}

variable "tags" {
  description = "Tags applied to edge resources."
  type        = map(string)
  default     = {}
}
