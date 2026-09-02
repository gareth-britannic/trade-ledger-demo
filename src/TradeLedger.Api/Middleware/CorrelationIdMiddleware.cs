using Microsoft.Extensions.Primitives;
using Serilog.Context;
using TradeLedger.Common;

namespace TradeLedger.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const int MaximumLength = CorrelationIdFactory.MaximumLength;

    public async Task InvokeAsync(HttpContext context, ICorrelationIdProvider correlationIdProvider)
    {
        var correlationId = GetOrCreateCorrelationId(context.Request.Headers[HeaderName]);
        correlationIdProvider.CorrelationId = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        try
        {
            using (LogContext.PushProperty(CorrelationIdMetadata.PropertyName, correlationId))
            {
                await next(context);
            }
        }
        finally
        {
            correlationIdProvider.CorrelationId = null;
        }
    }

    internal static string GetOrCreateCorrelationId(StringValues values)
    {
        if (values.Count == 1)
        {
            return CorrelationIdFactory.NormalizeOrCreate(values[0]);
        }

        return CorrelationIdFactory.NormalizeOrCreate(null);
    }
}
