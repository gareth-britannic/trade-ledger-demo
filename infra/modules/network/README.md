# Network module

Creates a two-AZ VPC with public, private application, and isolated database
subnet tiers. Only the application tier has NAT egress; the database route table
has no internet route.

The single NAT gateway is an explicit cost trade-off for the reference/dev
environment. A production composition should use one NAT gateway per AZ or
private service endpoints, depending on availability and cost requirements.
Application traffic from the second AZ can cross AZ boundaries to reach this
NAT, adding a data-transfer cost and depending on the first AZ. Multiple
interface endpoints should be compared as a complete per-service, per-AZ set;
they can cost more than one low-traffic NAT rather than eliminating cost.
