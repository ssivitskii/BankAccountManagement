using Banking.Api;
using Banking.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddBankingInfrastructure(builder.Configuration);
builder.Services.AddBankingApi(builder.Configuration);

WebApplication app = builder.Build();
app.UseBankingApi();
await app.InitializeBankingDatabaseAsync(CancellationToken.None);
await app.RunAsync();

public partial class Program
{
}
