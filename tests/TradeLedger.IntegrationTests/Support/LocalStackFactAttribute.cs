using Xunit;

namespace TradeLedger.IntegrationTests.Support;

public sealed class LocalStackFactAttribute : FactAttribute
{
    public LocalStackFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("TRADE_LEDGER_RUN_LOCALSTACK_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Run deploy/scripts/bootstrap-all.sh and set TRADE_LEDGER_RUN_LOCALSTACK_INTEGRATION=1.";
        }
    }
}
