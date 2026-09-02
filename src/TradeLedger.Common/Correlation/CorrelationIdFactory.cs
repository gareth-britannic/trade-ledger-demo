namespace TradeLedger.Common;

public static class CorrelationIdFactory
{
    public const int MaximumLength = 128;

    public static string NormalizeOrCreate(string? value)
    {
        var candidate = value?.Trim();
        return !string.IsNullOrEmpty(candidate) &&
               candidate.Length <= MaximumLength &&
               candidate.All(character => !char.IsControl(character))
            ? candidate
            : Guid.NewGuid().ToString("N");
    }
}
