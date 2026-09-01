using Shouldly;
using TradeLedger.Application;
using Xunit;

namespace TradeLedger.UnitTests.Application;

public sealed class SymbolNormalizerTests
{
    [Fact]
    public void IsValid_SymbolExceedsMaximumLength_ReturnsFalse()
    {
        var symbol = new string('A', SymbolNormalizer.MaximumLength + 1);

        SymbolNormalizer.IsValid(symbol).ShouldBeFalse();
    }
}
