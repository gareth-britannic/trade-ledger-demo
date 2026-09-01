# Database module

Creates an encrypted PostgreSQL RDS instance in isolated subnets. It is not
publicly accessible, uses an AWS-managed Secrets Manager password, retains seven
days of backups, takes a final snapshot, and has both AWS deletion protection
and Terraform `prevent_destroy` enabled.

The reference defaults to a Single-AZ `db.t4g.micro` instance with 20 GiB of
gp3 storage, conservative autoscaling to 100 GiB, and Performance Insights
disabled. Single-AZ is a deliberate reference-cost choice and does not provide
a synchronous standby or automatic Multi-AZ failover.

Before teardown, deletion protection must be set to `false` and the
`prevent_destroy` lifecycle rule must be removed in a reviewed change. The final
snapshot and RDS-managed Secrets Manager secret should then be reviewed and
deleted when no longer needed because they can outlive the DB instance.
