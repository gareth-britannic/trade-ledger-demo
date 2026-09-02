module "fill_queue" {
  source = "../../modules/fill-queue"

  name = "trade-ledger-fills"
  tags = {
    Application = "trade-ledger"
    Environment = "local"
    ManagedBy   = "terraform"
  }
}

module "processor_lambda" {
  source = "../../modules/processor-lambda"

  name           = "trade-ledger-processor"
  package_path   = abspath(var.processor_package_path)
  package_hash   = filebase64sha256(abspath(var.processor_package_path))
  fill_queue_arn = module.fill_queue.queue_arn
  aws_region     = var.aws_region
  environment_variables = {
    DOTNET_ENVIRONMENT             = "Development"
    Database__Host                 = "postgres"
    Database__Port                 = "5432"
    Database__Name                 = "trade_ledger"
    Database__Username             = "trade_ledger"
    Database__Password             = "trade_ledger"
    Serilog__MinimumLevel__Default = "Information"
  }

  tags = {
    Application = "trade-ledger"
    Environment = "local"
    ManagedBy   = "terraform"
  }
}
