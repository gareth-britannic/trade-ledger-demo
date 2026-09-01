variable "aws_region" {
  description = "Region emulated by LocalStack."
  type        = string
  default     = "eu-west-2"
}

variable "localstack_endpoint" {
  description = "LocalStack endpoint. Only loopback addresses are accepted."
  type        = string
  default     = "http://localhost:4566"

  validation {
    condition     = can(regex("^https?://(localhost|127\\.0\\.0\\.1)(:[0-9]+)?/?$", var.localstack_endpoint))
    error_message = "localstack_endpoint must use localhost or 127.0.0.1."
  }
}

