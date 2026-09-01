module "fill_queue" {
  source = "../../modules/fill-queue"

  name = "trade-ledger-fills"
  tags = {
    Application = "trade-ledger"
    Environment = "local"
    ManagedBy   = "terraform"
  }
}
