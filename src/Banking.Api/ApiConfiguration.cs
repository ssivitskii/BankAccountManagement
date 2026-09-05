using Banking.Application.Abstractions;
using Banking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

namespace Banking.Api;

public static class ApiConfiguration
{
    private static readonly string[] ReadyTags = ["ready"];

    public static IServiceCollection AddBankingApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<CurrentActorAccessor>();
        services.AddControllers();
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        AuthenticationApiConfiguration.Add(services, configuration);
        AddDatabaseHealthChecks(services);
        AddOpenApi(services);

        return services;
    }

    public static WebApplication UseBankingApi(this WebApplication app)
    {
        app.UseExceptionHandler();
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseRouting();
        AuthenticationApiConfiguration.Use(app);
        app.MapControllers();
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks(
            "/health/ready",
            new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains("ready"),
            });

        return app;
    }

    public static async Task InitializeBankingDatabaseAsync(
        this WebApplication app,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        BankingDbContext database = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        await database.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        string? adminUsername = app.Configuration["BootstrapAdmin:Username"];
        string? adminPassword = app.Configuration["BootstrapAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(adminUsername) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            IUserManagementService users = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
            await users.EnsureAdminAsync(adminUsername, adminPassword, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void AddDatabaseHealthChecks(IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<BankingDbContext>("postgresql", tags: ReadyTags);
    }

    private static void AddOpenApi(IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(
                "v1",
                new OpenApiInfo
                {
                    Title = "Bank Account Management API",
                    Version = "v1",
                });
            options.AddSecurityDefinition(
                "Bearer",
                new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter the JWT access token.",
                });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
            });
        });
    }
}
