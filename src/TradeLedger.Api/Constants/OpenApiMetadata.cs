using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace TradeLedger.Api.Constants;

internal static class OpenApiMetadata
{
    public const string DocumentName = "v1";
    public const string ApiVersion = "v1";
    public const string Title = "Trade Ledger API";
    public const string Description = "Accepts fills and reads FIFO-derived positions.";
    public const string BearerFormat = "JWT";
    public const string BearerDescription = "Amazon Cognito JWT bearer token.";

    public static string SecuritySchemeName => JwtBearerDefaults.AuthenticationScheme;

    public static string SwaggerDocumentPath => $"/swagger/{DocumentName}/swagger.json";

    public static string SwaggerDisplayName => $"{Title} {ApiVersion}";
}
