using TradeLedger.Domain;

namespace TradeLedger.Database.Entities;

internal sealed class RealisedPnlEntryEntity
{
    public Guid FillId { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTimeOffset RealisedAt { get; set; }

    public static RealisedPnlEntryEntity FromModel(RealisedPnlEntry entry) => new()
    {
        FillId = entry.FillId,
        Symbol = entry.Symbol,
        Amount = entry.Amount,
        RealisedAt = entry.RealisedAt
    };
}
