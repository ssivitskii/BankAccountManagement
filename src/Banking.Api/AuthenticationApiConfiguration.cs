using Banking.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;

namespace Banking.Api;

internal static class AuthenticationApiConfiguration
{
    internal static void Add(IServiceCollection services, IConfiguration configuration)
    {
        AddJwtAuthentication(services);
        services.AddAuthorization();
        AddAuthenticationRateLimit(services, configuration);
    }

    internal static void Use(WebApplication app)
    {
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
    }

    private static void AddAuthenticationRateLimit(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AuthRateLimitOptions>()
            .Bind(configuration.GetSection(AuthRateLimitOptions.SectionName))
            .Validate(options => options.PermitLimit > 0, "Authentication rate-limit permit count must be positive.")
            .Validate(options => options.WindowSeconds > 0, "Authentication rate-limit window must be positive.")
            .ValidateOnStart();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(
                AuthRateLimitOptions.PolicyName,
                context =>
                {
                    AuthRateLimitOptions settings = context.RequestServices
                        .GetRequiredService<IOptions<AuthRateLimitOptions>>()
                        .Value;
                    string partition = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partition,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.PermitLimit,
                            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                            QueueLimit = 0,
                            AutoReplenishment = true,
                        });
                });
            options.OnRejected = WriteRateLimitProblemAsync;
        });
    }

    private static void AddJwtAuthentication(IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                JwtOptions jwt = jwtOptions.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "unique_name",
                    RoleClaimType = "role",
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                            .CreateLogger("Banking.Authentication")
                            .LogWarning("Authentication challenge for {Path}", context.HttpContext.Request.Path);
                        return WriteAuthorizationProblemAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "Authentication required",
                            "A valid bearer token is required.");
                    },
                    OnForbidden = context =>
                    {
                        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                            .CreateLogger("Banking.Authorization")
                            .LogWarning(
                                "Authorization denied for actor {ActorId} on {Path}",
                                context.HttpContext.User.FindFirst("sub")?.Value,
                                context.HttpContext.Request.Path);
                        return WriteAuthorizationProblemAsync(
                            context.HttpContext,
                            StatusCodes.Status403Forbidden,
                            "Access denied",
                            "The authenticated user is not allowed to access this resource.");
                    },
                };
            });
    }

    private static Task<bool> WriteAuthorizationProblemAsync(
        HttpContext httpContext,
        int status,
        string title,
        string detail)
    {
        httpContext.Response.StatusCode = status;
        var problemDetails = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        return problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path,
            },
        }).AsTask();
    }

    private static async ValueTask WriteRateLimitProblemAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);
        }

        ILoggerFactory loggers = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        loggers.CreateLogger("Banking.AuthRateLimit")
            .LogWarning("Authentication request rate limit exceeded for {Path}", context.HttpContext.Request.Path);
        IProblemDetailsService problems = context.HttpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many authentication requests",
                Detail = "Retry the authentication request later.",
                Instance = context.HttpContext.Request.Path,
            },
        }).ConfigureAwait(false);
    }
}
