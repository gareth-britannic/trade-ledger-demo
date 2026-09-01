using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using TradeLedger.Api.Constants;
using TradeLedger.Api.Middleware;

namespace TradeLedger.Api.Extensions;

public static class WebApplicationExtensions
{
    private const string RequestLogMessage =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging(options => options.MessageTemplate = RequestLogMessage);
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        ConfigureTransportAndOpenApi(app);

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        MapHealthCheck(app);
        return app;
    }

    private static void ConfigureTransportAndOpenApi(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
            return;
        }

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint(OpenApiMetadata.SwaggerDocumentPath, OpenApiMetadata.SwaggerDisplayName);
            options.DocumentTitle = OpenApiMetadata.Title;
        });
    }

    private static void MapHealthCheck(WebApplication app)
    {
        app.MapHealthChecks(ApiRoutes.Health, new HealthCheckOptions
        {
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            },
            ResponseWriter = WriteHealthResponseAsync
        }).AllowAnonymous();
    }

    private static Task WriteHealthResponseAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(
            new { status = report.Status.ToString().ToLowerInvariant() },
            context.RequestAborted);
}
