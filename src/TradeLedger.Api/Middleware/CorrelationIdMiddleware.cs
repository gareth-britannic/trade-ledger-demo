using Microsoft.Extensions.Primitives;
using Serilog.Context;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Services;

namespace TradeLedger.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const int MaximumLength = 128;

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
            var candidate = values[0]?.Trim();
            if (!string.IsNullOrEmpty(candidate) &&
                candidate.Length <= MaximumLength &&
                candidate.All(character => !char.IsControl(character)))
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}
