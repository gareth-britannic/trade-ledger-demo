variable "aws_region" {
  description = "AWS region for the reference environment."
  type        = string
  default     = "eu-west-2"
}

variable "availability_zones" {
  description = "Two availability zones used by the reference environment."
  type        = list(string)
  default     = ["eu-west-2a", "eu-west-2b"]

  validation {
    condition     = length(var.availability_zones) == 2 && length(distinct(var.availability_zones)) == 2
    error_message = "availability_zones must contain exactly two distinct availability zones."
  }
}

variable "container_image" {
  description = "Immutable public-registry image URI for the API container."
  type        = string

  validation {
    condition     = can(regex("@sha256:[0-9a-fA-F]{64}$", var.container_image))
    error_message = "container_image must use an immutable sha256 digest."
  }
}

variable "certificate_arn" {
  description = "ACM certificate ARN for the public HTTPS listener."
  type        = string

  validation {
    condition     = can(regex("^arn:aws[a-z-]*:acm:[a-z0-9-]+:[0-9]{12}:certificate/[A-Za-z0-9-]+$", var.certificate_arn))
    error_message = "certificate_arn must be an ACM certificate ARN."
  }
}
