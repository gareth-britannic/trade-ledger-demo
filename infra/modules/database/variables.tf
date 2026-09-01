variable "name" {
  description = "Name used for database resources."
  type        = string
}

variable "database_name" {
  description = "Initial PostgreSQL database name."
  type        = string
  default     = "trade_ledger"

  validation {
    condition     = can(regex("^[A-Za-z][A-Za-z0-9_]{0,62}$", var.database_name))
    error_message = "database_name must be a valid 1-63 character RDS PostgreSQL database name."
  }
}

variable "master_username" {
  description = "PostgreSQL master username; AWS manages its password in Secrets Manager."
  type        = string
  default     = "trade_ledger_adm"

  validation {
    condition     = can(regex("^[A-Za-z][A-Za-z0-9_]{0,15}$", var.master_username))
    error_message = "master_username must be a valid 1-16 character RDS master username."
  }
}

variable "subnet_ids" {
  description = "Isolated database subnet IDs."
  type        = list(string)

  validation {
    condition     = length(var.subnet_ids) == 2 && length(distinct(var.subnet_ids)) == 2
    error_message = "subnet_ids must contain exactly two distinct isolated database subnet IDs."
  }
}

variable "security_group_id" {
  description = "Security group controlling access to PostgreSQL."
  type        = string
}

variable "instance_class" {
  description = "RDS instance class."
  type        = string
  default     = "db.t4g.micro"
}

variable "multi_az" {
  description = "Whether RDS maintains a synchronous standby in another AZ."
  type        = bool
  default     = false
}

variable "tags" {
  description = "Tags applied to database resources."
  type        = map(string)
  default     = {}
}
