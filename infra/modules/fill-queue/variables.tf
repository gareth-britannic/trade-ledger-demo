variable "name" {
  description = "Queue name without the .fifo suffix."
  type        = string

  validation {
    condition     = can(regex("^[A-Za-z0-9_-]{1,71}$", var.name))
    error_message = "name must be 1-71 characters using letters, numbers, underscores, or hyphens, without the .fifo suffix."
  }
}

variable "visibility_timeout_seconds" {
  description = "How long a received fill remains hidden from other consumers."
  type        = number
  default     = 30

  validation {
    condition     = var.visibility_timeout_seconds >= 0 && var.visibility_timeout_seconds <= 43200
    error_message = "visibility_timeout_seconds must be between 0 and 43200."
  }
}

variable "message_retention_seconds" {
  description = "How long unconsumed fill messages are retained."
  type        = number
  default     = 345600

  validation {
    condition     = var.message_retention_seconds >= 60 && var.message_retention_seconds <= 1209600
    error_message = "message_retention_seconds must be between 60 and 1209600."
  }
}

variable "dlq_message_retention_seconds" {
  description = "How long failed fill messages are retained in the DLQ."
  type        = number
  default     = 1209600

  validation {
    condition     = var.dlq_message_retention_seconds >= 60 && var.dlq_message_retention_seconds <= 1209600
    error_message = "dlq_message_retention_seconds must be between 60 and 1209600."
  }
}

variable "max_receive_count" {
  description = "Number of receives before SQS moves a message to the DLQ."
  type        = number
  default     = 3

  validation {
    condition     = var.max_receive_count >= 1 && var.max_receive_count <= 1000
    error_message = "max_receive_count must be between 1 and 1000."
  }
}

variable "tags" {
  description = "Tags applied to both queues."
  type        = map(string)
  default     = {}
}
