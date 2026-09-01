# Terraform with LocalStack

The Terraform configuration is in `infra/bootstrap`. It can only use LocalStack
on `localhost:4566` and uses the London region (`eu-west-2`).

## Start

From the repository root:

```bash
deploy/scripts/bootstrap-all.sh
```

This starts LocalStack, initializes Terraform, and applies the configuration.
Terraform state is kept locally and ignored by Git.

## Stop

```bash
deploy/scripts/local-down.sh
```
