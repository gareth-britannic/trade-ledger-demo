using Shouldly;
using TradeLedger.Domain;
using TradeLedger.Application;
using Xunit;

namespace TradeLedger.UnitTests.Domain;

public sealed class SymbolNormalizerTests
{
    [Fact]
    public void IsValid_SymbolExceedsMaximumLength_ReturnsFalse()
    {
        var symbol = new string('A', SymbolNormalizer.MaximumLength + 1);

        SymbolNormalizer.IsValid(symbol).ShouldBeFalse();
    }
}
