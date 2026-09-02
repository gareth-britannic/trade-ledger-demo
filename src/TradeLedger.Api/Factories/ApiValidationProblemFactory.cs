using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TradeLedger.Api.Constants;
using TradeLedger.Common;

namespace TradeLedger.Api.Factories;

internal static class ApiValidationProblemFactory
{
    private const string InvalidValueMessage = "The supplied value is invalid.";
    private const string ValidationTitle = "One or more validation errors occurred.";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static IActionResult CreateResponse(ActionContext context)
    {
        var problem = CreateProblem(GetErrors(context));
        problem.Instance = context.HttpContext.Request.Path;
        problem.Extensions[ProblemDetailsMetadata.CorrelationIdExtension] = context.HttpContext.RequestServices
            .GetRequiredService<ICorrelationIdProvider>()
            .CorrelationId;

        return new ContentResult
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentType = ApiMediaTypes.ProblemJson,
            Content = JsonSerializer.Serialize(problem, SerializerOptions)
        };
    }

    public static ProblemDetails CreateProblem(IDictionary<string, string[]> errors)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = ValidationTitle,
            Type = ProblemTypes.Validation
        };
        problem.Extensions["errors"] = errors;
        return problem;
    }

    private static Dictionary<string, string[]> GetErrors(ActionContext context) => context.ModelState
        .Where(entry => entry.Value?.Errors.Count > 0)
        .ToDictionary(
            entry => entry.Key,
            entry => entry.Value!.Errors
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? InvalidValueMessage
                    : error.ErrorMessage)
                .ToArray());
}
