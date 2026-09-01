using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using TradeLedger.Api.Middleware;
using TradeLedger.Application.Exceptions;
using TradeLedger.Application.Services;
using Xunit;

namespace TradeLedger.UnitTests.Api.Middleware;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task RequestCancellation_IsRethrown()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var context = new DefaultHttpContext { RequestAborted = source.Token };
        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.FromException(new OperationCanceledException()),
            Mock.Of<ILogger<ExceptionHandlingMiddleware>>(),
            new TestEnvironment());

        await Should.ThrowAsync<OperationCanceledException>(() =>
            middleware.InvokeAsync(context, new CorrelationIdProvider()));
    }

    [Fact]
    public async Task ValidationException_MapsTo400WithErrorsAndCorrelationId()
    {
        var exception = new ValidationException([new ValidationFailure("Symbol", "Symbol is required.")]);

        var (context, body) = await Invoke(exception);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        body.RootElement.GetProperty("type").GetString().ShouldBe("urn:trade-ledger:problem:validation");
        body.RootElement.GetProperty("errors").GetProperty("Symbol")[0].GetString()
            .ShouldBe("Symbol is required.");
        body.RootElement.GetProperty("correlationId").GetString().ShouldBe("correlation-1");
    }

    [Fact]
    public async Task NotFound_MapsTo404()
    {
        var (context, _) = await Invoke(new ResourceNotFoundException("Position 'ACME' was not found."));

        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UnexpectedException_MapsTo500WithoutInternalDetails_AndLogsOnce()
    {
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();

        var (context, body) = await Invoke(new InvalidOperationException("database password leaked"), logger.Object);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        body.RootElement.ToString().ShouldNotContain("database password leaked");
        logger.Verify(
            instance => instance.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static async Task<(DefaultHttpContext Context, JsonDocument Body)> Invoke(
        Exception exception,
        ILogger<ExceptionHandlingMiddleware>? logger = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/test";
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        var provider = new CorrelationIdProvider { CorrelationId = "correlation-1" };
        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.FromException(exception),
            logger ?? Mock.Of<ILogger<ExceptionHandlingMiddleware>>(),
            new TestEnvironment());

        await middleware.InvokeAsync(context, provider);
        context.Response.Body.Position = 0;
        return (context, await JsonDocument.ParseAsync(context.Response.Body));
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Production";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
