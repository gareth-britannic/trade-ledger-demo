variable "name" {
  description = "Lambda function name."
  type        = string
}

variable "package_path" {
  description = "Absolute path to the self-contained .NET 10 custom-runtime zip."
  type        = string
}

variable "package_hash" {
  description = "Base64 SHA-256 of the Lambda zip so code changes update the function."
  type        = string
}

variable "fill_queue_arn" {
  description = "ARN of the FIFO fill queue consumed by this processor."
  type        = string
}

variable "aws_region" {
  description = "AWS region used for resource-scoped log permissions."
  type        = string
}

variable "aws_account_id" {
  description = "AWS account ID (LocalStack uses 000000000000)."
  type        = string
  default     = "000000000000"
}

variable "environment_variables" {
  description = "Validated processor configuration supplied to the Lambda environment."
  type        = map(string)
  sensitive   = true

  validation {
    condition = alltrue([
      for name in [
        "Database__Host",
        "Database__Port",
        "Database__Name",
        "Database__Username",
        "Database__Password"
      ] : try(length(trimspace(var.environment_variables[name])) > 0, false)
    ])
    error_message = "The processor Lambda requires all Database__Host, Database__Port, Database__Name, Database__Username, and Database__Password environment variables."
  }
}

variable "subnet_ids" {
  description = "Private subnet IDs for database access; leave empty for LocalStack."
  type        = list(string)
  default     = []

  validation {
    condition     = length(var.subnet_ids) == 0 || length(var.subnet_ids) >= 2
    error_message = "subnet_ids must be empty or contain at least two private subnets."
  }
}

variable "security_group_ids" {
  description = "Security groups attached to Lambda ENIs; required when subnet_ids are set."
  type        = list(string)
  default     = []

  validation {
    condition     = (length(var.subnet_ids) == 0) == (length(var.security_group_ids) == 0)
    error_message = "subnet_ids and security_group_ids must either both be empty or both be set."
  }
}

variable "batch_size" {
  description = "Maximum SQS records delivered per invocation."
  type        = number
  default     = 10

  validation {
    condition     = var.batch_size >= 1 && var.batch_size <= 10
    error_message = "FIFO SQS batch_size must be between 1 and 10."
  }
}

variable "maximum_batching_window_seconds" {
  description = "Optional short batching window; correctness never depends on this value."
  type        = number
  default     = 1
}

variable "tags" {
  description = "Tags applied to processor resources."
  type        = map(string)
  default     = {}
}
