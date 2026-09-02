# AWS reference environment

This root module composes the production-shaped AWS architecture used for
portfolio review. It is deliberately not connected to a backend and is tested
with Terraform's mocked AWS provider; it has never been applied to AWS.

Required inputs are an immutable public-registry image URI, the packaged ARM64
processor Lambda zip, an ACM certificate ARN from the same AWS region as the
ALB, and the Cognito authority and audience used for JWT validation. ECS tasks
and the processor Lambda run in private subnets, RDS runs in isolated subnets,
and the HTTPS ALB is the only workload accepting public ingress. Terraform reads
the RDS-managed credentials and injects the required database environment into
the Lambda; use an encrypted, access-controlled Terraform backend before any
real deployment because those sensitive values are held in state. See the
repository infrastructure README for the test, trust-boundary, cost,
availability, and deliberate-teardown guidance.

Do not apply this root without an explicit cost review and teardown plan. It has
never been deployed, and its chargeable resources are protected against casual
deletion by ALB/RDS deletion protection and Terraform `prevent_destroy`.
