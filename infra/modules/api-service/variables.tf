variable "name" {
  description = "Name used for ECS and IAM resources."
  type        = string
}

variable "aws_region" {
  description = "AWS region exposed to the container."
  type        = string
}

variable "container_image" {
  description = "Immutable public-registry container image reference."
  type        = string

  validation {
    condition     = can(regex("@sha256:[0-9a-fA-F]{64}$", var.container_image))
    error_message = "container_image must use an immutable sha256 digest."
  }
}

variable "container_port" {
  description = "HTTP port exposed by the API container."
  type        = number
  default     = 8080

  validation {
    condition     = var.container_port >= 1 && var.container_port <= 65535
    error_message = "container_port must be between 1 and 65535."
  }
}

variable "subnet_ids" {
  description = "Private application subnet IDs used by ECS tasks."
  type        = list(string)

  validation {
    condition     = length(var.subnet_ids) == 2 && length(distinct(var.subnet_ids)) == 2
    error_message = "subnet_ids must contain exactly two distinct private application subnet IDs."
  }
}

variable "security_group_id" {
  description = "Security group attached to ECS task ENIs."
  type        = string
}

variable "target_group_arn" {
  description = "ALB target group receiving traffic for the service."
  type        = string
}

variable "fill_queue_arn" {
  description = "ARN of the FIFO queue the API may publish fills to."
  type        = string
}

variable "fill_queue_url" {
  description = "URL of the FIFO fill queue exposed to the API."
  type        = string
}

variable "database_host" {
  description = "RDS hostname exposed to the API."
  type        = string
}

variable "database_secret_arn" {
  description = "ARN of the Secrets Manager secret containing RDS credentials."
  type        = string
}

variable "desired_count" {
  description = "Number of ECS tasks maintained by the service."
  type        = number
  default     = 1

  validation {
    condition     = var.desired_count >= 0 && floor(var.desired_count) == var.desired_count
    error_message = "desired_count must be a non-negative whole number."
  }
}

variable "tags" {
  description = "Tags applied to service resources."
  type        = map(string)
  default     = {}
}
