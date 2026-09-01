locals {
  name = "trade-ledger-reference"
  tags = {
    Application = "trade-ledger"
    Environment = "reference"
    ManagedBy   = "terraform"
    Owner       = "garethmoore"
    Purpose     = "reference-architecture"
  }
}

module "network" {
  source = "../../modules/network"

  name               = local.name
  vpc_cidr           = "10.20.0.0/16"
  availability_zones = var.availability_zones
  tags               = local.tags
}

module "fill_queue" {
  source = "../../modules/fill-queue"

  name = "trade-ledger-fills"
  tags = local.tags
}

module "database" {
  source = "../../modules/database"

  name              = "${local.name}-postgres"
  subnet_ids        = module.network.database_subnet_ids
  security_group_id = aws_security_group.database.id
  tags              = local.tags
}

module "public_api_edge" {
  source = "../../modules/public-api-edge"

  name              = "trade-ledger-api"
  vpc_id            = module.network.vpc_id
  public_subnet_ids = module.network.public_subnet_ids
  security_group_id = aws_security_group.alb.id
  certificate_arn   = var.certificate_arn
  tags              = local.tags
}

module "api_service" {
  source = "../../modules/api-service"

  name                = "trade-ledger-api"
  aws_region          = var.aws_region
  container_image     = var.container_image
  subnet_ids          = module.network.application_subnet_ids
  security_group_id   = aws_security_group.ecs.id
  target_group_arn    = module.public_api_edge.target_group_arn
  fill_queue_arn      = module.fill_queue.queue_arn
  fill_queue_url      = module.fill_queue.queue_url
  database_host       = module.database.address
  database_secret_arn = module.database.master_user_secret_arn
  tags                = local.tags
}
