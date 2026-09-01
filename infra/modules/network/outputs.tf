output "vpc_id" {
  description = "ID of the VPC."
  value       = aws_vpc.this.id
}

output "public_subnet_ids" {
  description = "Public subnet IDs used by internet-facing load balancers."
  value       = [for zone in var.availability_zones : aws_subnet.public[zone].id]
}

output "application_subnet_ids" {
  description = "Private subnet IDs used by application workloads."
  value       = [for zone in var.availability_zones : aws_subnet.application[zone].id]
}

output "database_subnet_ids" {
  description = "Isolated subnet IDs used by databases."
  value       = [for zone in var.availability_zones : aws_subnet.database[zone].id]
}
