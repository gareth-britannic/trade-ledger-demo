using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;
using TradeLedger.Api.Constants;
using TradeLedger.Api.Factories;
using TradeLedger.Application.Options;

namespace TradeLedger.Api.Extensions;

public static class ServiceCollectionExtensions
{
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddControllers(services);
        AddAuthentication(services, configuration);
        AddOpenApi(services);
        return services;
    }

    private static void AddControllers(IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = ApiValidationProblemFactory.CreateResponse);
    }

    private static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CognitoOptions>()
            .Bind(configuration.GetSection(CognitoOptions.SectionName))
            .Validate(IsAllowedAuthority,
                $"{CognitoOptions.SectionName}:{nameof(CognitoOptions.Authority)} must be an absolute HTTPS URI or an HTTP loopback URI for local development.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience),
                $"{CognitoOptions.SectionName}:{nameof(CognitoOptions.Audience)} is required.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<CognitoOptions>, IHostEnvironment>(ConfigureJwtBearer);
        services.AddAuthorization();
    }

    private static void AddOpenApi(IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(OpenApiMetadata.DocumentName, new OpenApiInfo
            {
                Title = OpenApiMetadata.Title,
                Version = OpenApiMetadata.ApiVersion,
                Description = OpenApiMetadata.Description
            });
            options.CustomOperationIds(description => description.ActionDescriptor.AttributeRouteInfo?.Name);
            options.IncludeXmlComments(GetXmlDocumentationPath());
            options.AddSecurityDefinition(OpenApiMetadata.SecuritySchemeName, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme.ToLowerInvariant(),
                BearerFormat = OpenApiMetadata.BearerFormat,
                Name = HeaderNames.Authorization,
                In = ParameterLocation.Header,
                Description = OpenApiMetadata.BearerDescription
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(OpenApiMetadata.SecuritySchemeName, document)] = []
            });
        });
    }

    private static bool IsAllowedAuthority(CognitoOptions options)
    {
        if (!Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority))
        {
            return false;
        }

        return authority.Scheme == Uri.UriSchemeHttps ||
               authority.Scheme == Uri.UriSchemeHttp && authority.IsLoopback;
    }

    private static void ConfigureJwtBearer(
        JwtBearerOptions jwtBearer,
        IOptions<CognitoOptions> cognito,
        IHostEnvironment environment)
    {
        jwtBearer.Authority = cognito.Value.Authority;
        jwtBearer.RequireHttpsMetadata = !environment.IsDevelopment();
        jwtBearer.MapInboundClaims = false;
        jwtBearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = cognito.Value.Authority,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "sub",
            ClockSkew = AllowedClockSkew
        };
        jwtBearer.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var principal = context.Principal;
                if (principal is null)
                {
                    context.Fail("Missing authenticated principal.");
                    return Task.CompletedTask;
                }

                var tokenUse = principal.FindFirst("token_use")?.Value;
                if (!string.Equals(tokenUse, "access", StringComparison.Ordinal))
                {
                    context.Fail("Invalid token_use. Expected an access token.");
                    return Task.CompletedTask;
                }

                var clientId = principal.FindFirst("client_id")?.Value;
                if (!string.Equals(clientId, cognito.Value.Audience, StringComparison.Ordinal))
                {
                    context.Fail("Invalid client_id for the configured Cognito app client.");
                }

                return Task.CompletedTask;
            }
        };
    }

    private static string GetXmlDocumentationPath()
    {
        var assemblyName = typeof(ServiceCollectionExtensions).Assembly.GetName().Name;
        return Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.xml");
    }
}
