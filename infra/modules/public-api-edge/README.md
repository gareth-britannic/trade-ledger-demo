# Public API edge module

Creates the only internet-facing application component: an HTTPS Application
Load Balancer. It forwards to IP targets in the VPC and is protected by a
regional WAF web ACL using AWS Common and Known Bad Inputs managed rule groups.

The ACM certificate must be in the same region as the ALB. ALB deletion
protection is intentional; disable it in a reviewed change before teardown.
