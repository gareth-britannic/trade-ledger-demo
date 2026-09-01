using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using TradeLedger.Api.Constants;
using TradeLedger.Application.Exceptions;
using TradeLedger.Application.Interfaces;

namespace TradeLedger.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context, ICorrelationIdProvider correlationIdProvider)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await WriteProblemAsync(context, exception, correlationIdProvider.CorrelationId);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception, string? correlationId)
    {
        var problem = exception switch
        {
            ValidationException validation => ValidationProblem(validation),
            ResourceNotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "The requested resource was not found.",
                Detail = notFound.Message,
                Type = ProblemTypes.NotFound
            },
            _ => UnexpectedProblem()
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled request exception");
        }

        problem.Instance = context.Request.Path;
        problem.Extensions[ProblemDetailsMetadata.CorrelationIdExtension] = correlationId;
        context.Response.Clear();
        context.Response.StatusCode = problem.Status!.Value;
        context.Response.ContentType = ApiMediaTypes.ProblemJson;
        await context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: ApiMediaTypes.ProblemJson,
            cancellationToken: context.RequestAborted);
    }

    private static ProblemDetails ValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
        return ValidationProblem(errors);
    }

    private static ProblemDetails ValidationProblem(IReadOnlyDictionary<string, string[]> errors)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Type = ProblemTypes.Validation
        };
        problem.Extensions["errors"] = errors;
        return problem;
    }

    private ProblemDetails UnexpectedProblem() => new()
    {
        Status = StatusCodes.Status500InternalServerError,
        Title = "An unexpected error occurred.",
        Detail = environment.IsDevelopment()
            ? "See the correlated server log for diagnostic details."
            : "The server could not complete the request.",
        Type = ProblemTypes.Unexpected
    };
}
