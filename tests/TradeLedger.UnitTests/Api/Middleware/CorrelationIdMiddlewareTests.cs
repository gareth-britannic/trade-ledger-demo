using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shouldly;
using TradeLedger.Api.Middleware;
using TradeLedger.Application.Services;
using TradeLedger.Common;
using Xunit;

namespace TradeLedger.UnitTests.Api.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Invoke_ValidIncomingId_IsReusedInContextLogsAndResponseThenCleared()
    {
        var provider = new CorrelationIdProvider();
        var sink = new CollectingSink();
        var previousLogger = Log.Logger;
        Log.Logger = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();
        try
        {
            var context = Context(" request-123 ");
            var middleware = new CorrelationIdMiddleware(async httpContext =>
            {
                provider.CorrelationId.ShouldBe("request-123");
                Log.Information("inside request");
                await httpContext.Response.WriteAsync("ok");
            });

            await middleware.InvokeAsync(context, provider);

            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().ShouldBe("request-123");
            sink.Events.ShouldHaveSingleItem()
                .Properties["CorrelationId"].ShouldBe(new ScalarValue("request-123"));
            provider.CorrelationId.ShouldBeNull();
        }
        finally
        {
            Log.CloseAndFlush();
            Log.Logger = previousLogger;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad\nheader")]
    public async Task Invoke_MissingOrInvalidId_GeneratesSafeReplacement(string? header)
    {
        var provider = new CorrelationIdProvider();
        var context = Context(header);
        string? observed = null;
        var middleware = new CorrelationIdMiddleware(httpContext =>
        {
            observed = provider.CorrelationId;
            return httpContext.Response.WriteAsync("ok");
        });

        await middleware.InvokeAsync(context, provider);

        Guid.TryParseExact(observed, "N", out _).ShouldBeTrue();
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().ShouldBe(observed);
        provider.CorrelationId.ShouldBeNull();
    }

    [Fact]
    public async Task Invoke_ConcurrentRequestScopes_DoNotShareIds()
    {
        var firstProvider = new CorrelationIdProvider();
        var secondProvider = new CorrelationIdProvider();
        var barrier = new Barrier(2);
        var observed = new List<string?>();
        var gate = new object();

        async Task InvokeAsync(string correlationId, CorrelationIdProvider provider)
        {
            var middleware = new CorrelationIdMiddleware(async _ =>
            {
                var before = provider.CorrelationId;
                await Task.Run(() => barrier.SignalAndWait());
                var after = provider.CorrelationId;
                lock (gate)
                {
                    observed.Add($"{before}:{after}");
                }
            });

            await middleware.InvokeAsync(Context(correlationId), provider);
        }

        await Task.WhenAll(
            InvokeAsync("first", firstProvider),
            InvokeAsync("second", secondProvider));

        observed.Order().ShouldBe(["first:first", "second:second"]);
        firstProvider.CorrelationId.ShouldBeNull();
        secondProvider.CorrelationId.ShouldBeNull();
    }

    private static DefaultHttpContext Context(string? header)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        if (header is not null)
        {
            context.Request.Headers[CorrelationIdMiddleware.HeaderName] = header;
        }

        return context;
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
