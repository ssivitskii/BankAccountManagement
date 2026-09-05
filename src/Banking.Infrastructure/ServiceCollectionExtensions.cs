using Banking.Application;
using Banking.Application.Abstractions;
using Banking.Domain;
using Banking.Infrastructure.Auth;
using Banking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace Banking.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBankingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<BankingDbContext>((serviceProvider, options) =>
        {
            var currentConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
            string connectionString = currentConfiguration.GetConnectionString("Banking")
                ?? throw new InvalidOperationException("ConnectionStrings:Banking is required.");
            options.UseNpgsql(connectionString);
        });
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IOperationRepository, OperationRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IBankingTransaction, EfBankingTransaction>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IBankingService, BankingService>();
        services.AddScoped<ITransferService, EfTransferService>();
        services.AddScoped<IStatementService, EfStatementService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "JWT issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "JWT audience is required.")
            .Validate(options => Encoding.UTF8.GetByteCount(options.SigningKey) >= 32, "JWT signing key must be 32 bytes or longer.")
            .Validate(options => options.LifetimeMinutes > 0, "JWT lifetime must be positive.")
            .ValidateOnStart();
        return services;
    }
}
