using Banking.Domain;
using Banking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Banking.IntegrationTests;

public sealed class BankingApiTests : IClassFixture<BankingApiFactory>
{
    private readonly BankingApiFactory _factory;

    public BankingApiTests(BankingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegistrationLoginAndAuthenticationSemanticsWork()
    {
        using HttpClient client = _factory.CreateClient();
        string username = Unique("auth");

        HttpResponseMessage registration = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { username, password = "customer-password" });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        HttpResponseMessage login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password = "customer-password" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        HttpResponseMessage invalidLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, invalidLogin.StatusCode);

        HttpResponseMessage unauthorized = await client.GetAsync($"/api/accounts/{Guid.NewGuid()}");
        await AssertProblemAsync(unauthorized, HttpStatusCode.Unauthorized, "Authentication required");

        using HttpClient invalidTokenClient = _factory.CreateClient();
        Authorize(invalidTokenClient, "not-a-valid-token");
        HttpResponseMessage invalidToken = await invalidTokenClient.GetAsync($"/api/accounts/{Guid.NewGuid()}");
        await AssertProblemAsync(invalidToken, HttpStatusCode.Unauthorized, "Authentication required");

        HttpResponseMessage oversizedUsername = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = new string('u', 101), password = "customer-password" });
        Assert.Equal(HttpStatusCode.BadRequest, oversizedUsername.StatusCode);
        HttpResponseMessage oversizedPassword = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password = new string('p', 201) });
        Assert.Equal(HttpStatusCode.BadRequest, oversizedPassword.StatusCode);
    }

    [Fact]
    public async Task AccountWorkflowPersistsBalanceAndCompleteHistory()
    {
        using HttpClient client = _factory.CreateClient();
        string token = await RegisterAndGetTokenAsync(client, Unique("workflow"));
        Authorize(client, token);
        Guid accountId = await CreateAccountAsync(client, Unique("ACC"), 100);

        HttpResponseMessage get = await client.GetAsync($"/api/accounts/{accountId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        HttpResponseMessage deposit = await client.PostAsJsonAsync(
            $"/api/accounts/{accountId}/deposit",
            new { amount = 25 });
        Assert.Equal(HttpStatusCode.NoContent, deposit.StatusCode);

        HttpResponseMessage withdrawal = await client.PostAsJsonAsync(
            $"/api/accounts/{accountId}/withdraw",
            new { amount = 20 });
        Assert.Equal(HttpStatusCode.NoContent, withdrawal.StatusCode);

        HttpResponseMessage insufficient = await client.PostAsJsonAsync(
            $"/api/accounts/{accountId}/withdraw",
            new { amount = 1000 });
        Assert.Equal(HttpStatusCode.Conflict, insufficient.StatusCode);

        JsonElement balance = await client.GetFromJsonAsync<JsonElement>($"/api/accounts/{accountId}/balance");
        Assert.Equal(105, balance.GetProperty("balance").GetDecimal());
        JsonElement[] operations = await GetOperationsAsync(client, accountId);
        Assert.Equal(2, operations.Length);
        Assert.Equal(2, operations.Select(item => item.GetProperty("id").GetGuid()).Distinct().Count());
    }

    [Fact]
    public async Task ValidationDuplicateAndOwnershipFailuresUseCorrectStatusCodes()
    {
        using HttpClient ownerClient = _factory.CreateClient();
        string ownerToken = await RegisterAndGetTokenAsync(ownerClient, Unique("owner"));
        Authorize(ownerClient, ownerToken);
        string number = Unique("DUP");
        Guid accountId = await CreateAccountAsync(ownerClient, number, 10);

        HttpResponseMessage duplicate = await ownerClient.PostAsJsonAsync(
            "/api/accounts",
            new { number, initialBalance = 0 });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        HttpResponseMessage negative = await ownerClient.PostAsJsonAsync(
            $"/api/accounts/{accountId}/deposit",
            new { amount = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        HttpResponseMessage zero = await ownerClient.PostAsJsonAsync(
            $"/api/accounts/{accountId}/withdraw",
            new { amount = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        HttpResponseMessage preciseInitialBalance = await ownerClient.PostAsJsonAsync(
            "/api/accounts",
            new { number = Unique("PRECISION"), initialBalance = 1.001m });
        Assert.Equal(HttpStatusCode.BadRequest, preciseInitialBalance.StatusCode);
        HttpResponseMessage preciseDeposit = await ownerClient.PostAsJsonAsync(
            $"/api/accounts/{accountId}/deposit",
            new { amount = 1.001m });
        Assert.Equal(HttpStatusCode.BadRequest, preciseDeposit.StatusCode);
        HttpResponseMessage preciseWithdrawal = await ownerClient.PostAsJsonAsync(
            $"/api/accounts/{accountId}/withdraw",
            new { amount = 1.001m });
        Assert.Equal(HttpStatusCode.BadRequest, preciseWithdrawal.StatusCode);

        HttpResponseMessage roleForbidden = await ownerClient.PostAsJsonAsync(
            "/api/admin/users",
            new { username = Unique("denied"), password = "managed-password", role = 0 });
        await AssertProblemAsync(roleForbidden, HttpStatusCode.Forbidden, "Access denied");

        using HttpClient strangerClient = _factory.CreateClient();
        string strangerToken = await RegisterAndGetTokenAsync(strangerClient, Unique("stranger"));
        Authorize(strangerClient, strangerToken);
        HttpResponseMessage forbidden = await strangerClient.GetAsync($"/api/accounts/{accountId}");
        await AssertProblemAsync(forbidden, HttpStatusCode.Forbidden, "Access denied");
    }

    [Fact]
    public async Task AdminCanCreateUsers()
    {
        using HttpClient client = _factory.CreateClient();
        string token = await LoginAndGetTokenAsync(
            client,
            BankingApiFactory.AdminUsername,
            BankingApiFactory.AdminPassword);
        Authorize(client, token);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/admin/users",
            new { username = Unique("managed"), password = "managed-password", role = 0 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        HttpResponseMessage invalidRole = await client.PostAsJsonAsync(
            "/api/admin/users",
            new { username = Unique("invalid-role"), password = "managed-password", role = 99 });
        Assert.Equal(HttpStatusCode.BadRequest, invalidRole.StatusCode);
    }

    [Fact]
    public async Task ConcurrentWithdrawalsCannotSpendTheSameBalanceTwice()
    {
        using HttpClient client = _factory.CreateClient();
        string token = await RegisterAndGetTokenAsync(client, Unique("concurrency"));
        Authorize(client, token);
        Guid accountId = await CreateAccountAsync(client, Unique("RACE"), 100);

        Task<HttpResponseMessage> first = client.PostAsJsonAsync(
            $"/api/accounts/{accountId}/withdraw",
            new { amount = 80 });
        Task<HttpResponseMessage> second = client.PostAsJsonAsync(
            $"/api/accounts/{accountId}/withdraw",
            new { amount = 80 });
        HttpResponseMessage[] responses = await Task.WhenAll(first, second);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        JsonElement balance = await client.GetFromJsonAsync<JsonElement>($"/api/accounts/{accountId}/balance");
        Assert.Equal(20, balance.GetProperty("balance").GetDecimal());
        JsonElement[] operations = await GetOperationsAsync(client, accountId);
        Assert.Single(operations);
    }

    [Fact]
    public async Task TransferIsAtomicLinkedAndIdempotent()
    {
        using HttpClient client = _factory.CreateClient();
        string token = await RegisterAndGetTokenAsync(client, Unique("transfer"));
        Authorize(client, token);
        Guid sourceId = await CreateAccountAsync(client, Unique("SRC"), 100);
        Guid destinationId = await CreateAccountAsync(client, Unique("DST"), 10);
        string key = Unique("key");

        HttpResponseMessage created = await SendTransferAsync(client, sourceId, destinationId, 25, key);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonElement createdPayload = await created.Content.ReadFromJsonAsync<JsonElement>();
        Guid transferId = createdPayload.GetProperty("id").GetGuid();
        Assert.False(createdPayload.GetProperty("isReplay").GetBoolean());

        HttpResponseMessage replay = await SendTransferAsync(client, sourceId, destinationId, 25, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        JsonElement replayPayload = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(transferId, replayPayload.GetProperty("id").GetGuid());
        Assert.True(replayPayload.GetProperty("isReplay").GetBoolean());

        HttpResponseMessage conflict = await SendTransferAsync(client, sourceId, destinationId, 26, key);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(75, await GetBalanceAsync(client, sourceId));
        Assert.Equal(35, await GetBalanceAsync(client, destinationId));

        JsonElement[] sourceOperations = await GetOperationsAsync(client, sourceId);
        JsonElement[] destinationOperations = await GetOperationsAsync(client, destinationId);
        JsonElement sourceOperation = Assert.Single(sourceOperations);
        JsonElement destinationOperation = Assert.Single(destinationOperations);
        Assert.Equal(2, sourceOperation.GetProperty("type").GetInt32());
        Assert.Equal(3, destinationOperation.GetProperty("type").GetInt32());
        Assert.Equal(transferId, sourceOperation.GetProperty("transferId").GetGuid());
        Assert.Equal(transferId, destinationOperation.GetProperty("transferId").GetGuid());
    }

    [Fact]
    public async Task ConcurrentTransferReplayExecutesOnlyOnceAndFailuresRollBack()
    {
        using HttpClient client = _factory.CreateClient();
        string token = await RegisterAndGetTokenAsync(client, Unique("transfer-race"));
        Authorize(client, token);
        Guid sourceId = await CreateAccountAsync(client, Unique("SRC"), 100);
        Guid destinationId = await CreateAccountAsync(client, Unique("DST"), 0);
        string key = Unique("concurrent-key");

        Task<HttpResponseMessage> first = SendTransferAsync(client, sourceId, destinationId, 20, key);
        Task<HttpResponseMessage> second = SendTransferAsync(client, sourceId, destinationId, 20, key);
        HttpResponseMessage[] responses = await Task.WhenAll(first, second);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        JsonElement[] payloads = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<JsonElement>()));
        Assert.Single(payloads.Select(payload => payload.GetProperty("id").GetGuid()).Distinct());
        Assert.Equal(80, await GetBalanceAsync(client, sourceId));
        Assert.Equal(20, await GetBalanceAsync(client, destinationId));

        HttpResponseMessage insufficient = await SendTransferAsync(
            client,
            sourceId,
            destinationId,
            1000,
            Unique("insufficient"));
        Assert.Equal(HttpStatusCode.Conflict, insufficient.StatusCode);
        Assert.Equal(80, await GetBalanceAsync(client, sourceId));
        Assert.Equal(20, await GetBalanceAsync(client, destinationId));
        Assert.Single(await GetOperationsAsync(client, sourceId));
        Assert.Single(await GetOperationsAsync(client, destinationId));
    }

    [Fact]
    public async Task OperationCursorPaginatesEqualTimestampsWithoutDuplicates()
    {
        using HttpClient client = _factory.CreateClient();
        string token = await RegisterAndGetTokenAsync(client, Unique("pagination"));
        Authorize(client, token);
        Guid accountId = await CreateAccountAsync(client, Unique("PAGE"), 0);
        Guid[] expectedIds;
        await using (AsyncServiceScope scope = _factory.Services.CreateAsyncScope())
        {
            BankingDbContext database = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
            Operation[] operations = Enumerable.Range(0, 3)
                .Select(_ => new Operation(
                    accountId,
                    OperationType.Deposit,
                    new Money(1),
                    DateTimeOffset.UnixEpoch))
                .ToArray();
            database.Operations.AddRange(operations);
            await database.SaveChangesAsync();
            expectedIds = operations.Select(operation => operation.Id).ToArray();
        }

        JsonElement firstPage = await client.GetFromJsonAsync<JsonElement>(
            $"/api/accounts/{accountId}/operations?limit=2");
        JsonElement[] firstItems = firstPage.GetProperty("items").EnumerateArray().Select(item => item.Clone()).ToArray();
        string cursor = firstPage.GetProperty("nextCursor").GetString()
            ?? throw new Xunit.Sdk.XunitException("Next cursor is missing.");
        JsonElement secondPage = await client.GetFromJsonAsync<JsonElement>(
            $"/api/accounts/{accountId}/operations?limit=2&cursor={Uri.EscapeDataString(cursor)}");
        JsonElement[] secondItems = secondPage.GetProperty("items").EnumerateArray().Select(item => item.Clone()).ToArray();
        Guid[] actualIds = firstItems.Concat(secondItems)
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();

        Assert.Equal(2, firstItems.Length);
        Assert.Single(secondItems);
        Assert.Equal(expectedIds.Order(), actualIds.Order());
        Assert.Equal(actualIds.Length, actualIds.Distinct().Count());
        HttpResponseMessage invalidCursor = await client.GetAsync(
            $"/api/accounts/{accountId}/operations?cursor=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, invalidCursor.StatusCode);
    }

    [Fact]
    public async Task TransferValidationOwnershipAndOpposingDirectionsAreSafe()
    {
        using HttpClient ownerClient = _factory.CreateClient();
        string ownerToken = await RegisterAndGetTokenAsync(ownerClient, Unique("transfer-owner"));
        Authorize(ownerClient, ownerToken);
        Guid firstId = await CreateAccountAsync(ownerClient, Unique("FIRST"), 100);
        Guid secondId = await CreateAccountAsync(ownerClient, Unique("SECOND"), 100);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendTransferAsync(ownerClient, firstId, firstId, 1, Unique("same"))).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendTransferAsync(ownerClient, firstId, Guid.NewGuid(), 1, Unique("missing"))).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendTransferAsync(ownerClient, firstId, secondId, 1.001m, Unique("precision"))).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendTransferAsync(ownerClient, firstId, secondId, 1, new string('k', 129))).StatusCode);

        using HttpClient strangerClient = _factory.CreateClient();
        string strangerToken = await RegisterAndGetTokenAsync(strangerClient, Unique("transfer-stranger"));
        Authorize(strangerClient, strangerToken);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendTransferAsync(strangerClient, firstId, secondId, 1, Unique("foreign"))).StatusCode);

        Task<HttpResponseMessage> forward = SendTransferAsync(
            ownerClient,
            firstId,
            secondId,
            10,
            Unique("forward"));
        Task<HttpResponseMessage> reverse = SendTransferAsync(
            ownerClient,
            secondId,
            firstId,
            10,
            Unique("reverse"));
        HttpResponseMessage[] responses = await Task.WhenAll(forward, reverse);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Equal(100, await GetBalanceAsync(ownerClient, firstId));
        Assert.Equal(100, await GetBalanceAsync(ownerClient, secondId));
    }

    [Fact]
    public async Task ConcurrentIdempotencyConflictAndRollbackRetryAreDeterministic()
    {
        using HttpClient client = _factory.CreateClient();
        string token = await RegisterAndGetTokenAsync(client, Unique("idempotency-boundary"));
        Authorize(client, token);
        Guid sourceId = await CreateAccountAsync(client, Unique("SOURCE"), 100);
        Guid destinationId = await CreateAccountAsync(client, Unique("DEST"), 0);
        string conflictKey = Unique("payload-conflict");

        HttpResponseMessage[] differentPayloads = await Task.WhenAll(
            SendTransferAsync(client, sourceId, destinationId, 20, conflictKey),
            SendTransferAsync(client, sourceId, destinationId, 30, conflictKey));
        Assert.Single(differentPayloads, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(differentPayloads, response => response.StatusCode == HttpStatusCode.Conflict);
        decimal sourceBalance = await GetBalanceAsync(client, sourceId);
        decimal destinationBalance = await GetBalanceAsync(client, destinationId);
        Assert.Equal(100, sourceBalance + destinationBalance);
        Assert.True(sourceBalance is 70 or 80);
        Assert.Single(await GetOperationsAsync(client, sourceId));
        Assert.Single(await GetOperationsAsync(client, destinationId));

        Guid retrySourceId = await CreateAccountAsync(client, Unique("RETRY-SOURCE"), 10);
        Guid retryDestinationId = await CreateAccountAsync(client, Unique("RETRY-DEST"), 0);
        string retryKey = Unique("rollback-retry");
        HttpResponseMessage insufficient = await SendTransferAsync(
            client,
            retrySourceId,
            retryDestinationId,
            20,
            retryKey);
        Assert.Equal(HttpStatusCode.Conflict, insufficient.StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync($"/api/accounts/{retrySourceId}/deposit", new { amount = 10 })).StatusCode);
        HttpResponseMessage retry = await SendTransferAsync(
            client,
            retrySourceId,
            retryDestinationId,
            20,
            retryKey);

        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(0, await GetBalanceAsync(client, retrySourceId));
        Assert.Equal(20, await GetBalanceAsync(client, retryDestinationId));
        JsonElement[] retryOperations = await GetOperationsAsync(client, retrySourceId);
        Assert.Contains(retryOperations, operation => operation.GetProperty("type").GetInt32() == 0);
        Assert.Contains(retryOperations, operation => operation.GetProperty("type").GetInt32() == 2);
        Assert.Single(await GetOperationsAsync(client, retryDestinationId));
    }

    [Fact]
    public async Task StatementCalculatesBalancesAndExportsCsv()
    {
        using HttpClient client = _factory.CreateClient();
        string token = await RegisterAndGetTokenAsync(client, Unique("statement"));
        Authorize(client, token);
        Guid accountId = await CreateAccountAsync(client, Unique("STMT"), 100);
        DateTimeOffset from = DateTimeOffset.UtcNow.AddSeconds(-1);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync($"/api/accounts/{accountId}/deposit", new { amount = 25 })).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync($"/api/accounts/{accountId}/withdraw", new { amount = 10 })).StatusCode);
        DateTimeOffset to = DateTimeOffset.UtcNow;
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync($"/api/accounts/{accountId}/deposit", new { amount = 30 })).StatusCode);
        string query = $"from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";

        JsonElement statement = await client.GetFromJsonAsync<JsonElement>(
            $"/api/accounts/{accountId}/statement?{query}");
        Assert.Equal(100, statement.GetProperty("openingBalance").GetDecimal());
        Assert.Equal(115, statement.GetProperty("closingBalance").GetDecimal());
        decimal[] signedAmounts = statement.GetProperty("operations").EnumerateArray()
            .Select(operation => operation.GetProperty("signedAmount").GetDecimal())
            .ToArray();
        Assert.Equal([25, -10], signedAmounts);

        HttpResponseMessage csv = await client.GetAsync($"/api/accounts/{accountId}/statement.csv?{query}");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            $"statement-{accountId:D}.csv",
            csv.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        string csvText = await csv.Content.ReadAsStringAsync();
        Assert.StartsWith("accountId,fromInclusive,toExclusive", csvText, StringComparison.Ordinal);
        Assert.Contains("\"100.00\",\"115.00\"", csvText, StringComparison.Ordinal);

        using HttpClient strangerClient = _factory.CreateClient();
        string strangerToken = await RegisterAndGetTokenAsync(strangerClient, Unique("statement-stranger"));
        Authorize(strangerClient, strangerToken);
        HttpResponseMessage forbidden = await strangerClient.GetAsync(
            $"/api/accounts/{accountId}/statement?{query}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task AuthenticationRateLimitReturnsProblemDetails()
    {
        using WebApplicationFactory<Program> rateLimitedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AuthRateLimit:PermitLimit"] = "2",
                    ["AuthRateLimit:WindowSeconds"] = "60",
                })));
        using HttpClient client = rateLimitedFactory.CreateClient();

        HttpResponseMessage first = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = Unique("missing"), password = "customer-password" });
        HttpResponseMessage second = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = Unique("missing"), password = "customer-password" });
        HttpResponseMessage limited = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = Unique("missing"), password = "customer-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        await AssertProblemAsync(limited, HttpStatusCode.TooManyRequests, "Too many authentication requests");
        Assert.NotNull(limited.Headers.RetryAfter);
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string username)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { username, password = "customer-password" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadTokenAsync(response);
    }

    private static async Task<decimal> GetBalanceAsync(HttpClient client, Guid accountId)
    {
        JsonElement payload = await client.GetFromJsonAsync<JsonElement>($"/api/accounts/{accountId}/balance");
        return payload.GetProperty("balance").GetDecimal();
    }

    private static async Task<JsonElement[]> GetOperationsAsync(HttpClient client, Guid accountId)
    {
        JsonElement page = await client.GetFromJsonAsync<JsonElement>($"/api/accounts/{accountId}/operations");
        return page.GetProperty("items").EnumerateArray().Select(item => item.Clone()).ToArray();
    }

    private static async Task<HttpResponseMessage> SendTransferAsync(
        HttpClient client,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
        {
            Content = JsonContent.Create(new
            {
                fromAccountId = sourceAccountId,
                toAccountId = destinationAccountId,
                amount,
            }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string username, string password)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadTokenAsync(response);
    }

    private static async Task<string> ReadTokenAsync(HttpResponseMessage response)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.GetProperty("accessToken").GetString()
            ?? throw new Xunit.Sdk.XunitException("Access token is missing.");
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string number, decimal initialBalance)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/accounts",
            new { number, initialBalance });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("id").GetGuid();
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, problem.GetProperty("title").GetString());
    }

    private static void Authorize(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static string Unique(string prefix)
    {
        string suffix = $"{Guid.NewGuid():N}"[..20];
        return $"{prefix}-{suffix}";
    }
}
