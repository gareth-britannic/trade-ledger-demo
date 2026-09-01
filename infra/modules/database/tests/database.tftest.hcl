mock_provider "aws" {
  override_during = plan
}

run "keeps_postgres_private_encrypted_and_protected" {
  command = plan

  variables {
    name              = "trade-ledger-test-postgres"
    subnet_ids        = ["subnet-db-a", "subnet-db-b"]
    security_group_id = "sg-database"
  }

  assert {
    condition     = !aws_db_instance.this.publicly_accessible
    error_message = "RDS must not be publicly accessible."
  }

  assert {
    condition = (
      aws_db_instance.this.engine == "postgres" &&
      aws_db_instance.this.instance_class == "db.t4g.micro" &&
      aws_db_instance.this.allocated_storage == 20 &&
      aws_db_instance.this.max_allocated_storage == 100 &&
      aws_db_instance.this.storage_type == "gp3" &&
      !aws_db_instance.this.multi_az &&
      !aws_db_instance.this.performance_insights_enabled
    )
    error_message = "RDS must retain the low-cost Single-AZ burstable configuration without Performance Insights."
  }

  assert {
    condition = (
      toset(aws_db_subnet_group.this.subnet_ids) == toset(["subnet-db-a", "subnet-db-b"]) &&
      aws_db_instance.this.db_subnet_group_name == aws_db_subnet_group.this.name &&
      aws_db_instance.this.backup_retention_period == 7 &&
      aws_db_instance.this.copy_tags_to_snapshot &&
      aws_db_instance.this.final_snapshot_identifier == "trade-ledger-test-postgres-final"
    )
    error_message = "RDS must use both isolated subnets and retain its backup and final-snapshot controls."
  }

  assert {
    condition     = aws_db_instance.this.storage_encrypted
    error_message = "RDS storage must be encrypted."
  }

  assert {
    condition     = aws_db_instance.this.deletion_protection
    error_message = "RDS deletion protection must be enabled."
  }

  assert {
    condition     = !aws_db_instance.this.skip_final_snapshot
    error_message = "RDS must take a final snapshot when it is intentionally removed."
  }

  assert {
    condition     = aws_db_instance.this.manage_master_user_password
    error_message = "RDS credentials must be managed by AWS Secrets Manager."
  }

  assert {
    condition     = toset(aws_db_instance.this.vpc_security_group_ids) == toset(["sg-database"])
    error_message = "RDS must use only the supplied database security group."
  }
}

run "rejects_invalid_database_inputs" {
  command = plan

  variables {
    name              = "trade-ledger-test-postgres"
    database_name     = "invalid-name"
    master_username   = "1invalid"
    subnet_ids        = ["subnet-db-a"]
    security_group_id = "sg-database"
  }

  expect_failures = [var.database_name, var.master_username, var.subnet_ids]
}
