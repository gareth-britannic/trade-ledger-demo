namespace TradeLedger.Application;

public static class SymbolNormalizer
{
    public const int MaximumLength = 32;

    public static string Normalize(string symbol) => symbol.Trim().ToUpperInvariant();

    public static bool IsValid(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return false;
        }

        var normalized = Normalize(symbol);
        if (normalized.Length > MaximumLength || !char.IsAsciiLetterOrDigit(normalized[0]))
        {
            return false;
        }

        return normalized.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_' or '/');
    }
}
