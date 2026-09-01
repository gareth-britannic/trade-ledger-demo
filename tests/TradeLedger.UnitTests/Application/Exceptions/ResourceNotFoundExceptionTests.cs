using Shouldly;
using TradeLedger.Application.Exceptions;
using Xunit;

namespace TradeLedger.UnitTests.Application.Exceptions;

public sealed class ResourceNotFoundExceptionTests
{
    [Fact]
    public void Constructors_PreserveMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");

        var empty = new ResourceNotFoundException();
        var withInner = new ResourceNotFoundException("not found", inner);

        empty.InnerException.ShouldBeNull();
        withInner.Message.ShouldBe("not found");
        withInner.InnerException.ShouldBeSameAs(inner);
    }
}
