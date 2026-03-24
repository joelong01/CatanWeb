using System.Text.Json;
using Azure.Identity;
using Catan3.GameService.Abstractions;
using Microsoft.Azure.Cosmos;

namespace Tests.GameService.CatanDb;

/// <summary>
/// Runs all ICatanDb contract tests against a real CosmosDB endpoint.
/// Target is controlled by catan.ps1 via .cosmos-test-params.json:
///   - Default / -Local: CosmosDB Emulator (http://localhost:8081)
///   - -Azure / azure test: Real Azure CosmosDB account (DefaultAzureCredential)
///
/// xUnit creates a new test class instance per [Fact], so each test method gets its own
/// isolated database. The database is deleted in DisposeAsync() after the test completes.
/// </summary>
public class CosmosCatanDbTests : CatanDbContractTests
{
    // Unique per test-class run — prevents interference between parallel or sequential runs
    private readonly string _dbName = $"catan-test-{Guid.NewGuid():N}";
    private CosmosClient? _client;

    protected override async Task<ICatanDb> CreateDbAsync()
    {
        var endpoint = CosmosTestParams.Endpoint;
        var key = CosmosTestParams.Key;

        var stjOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        _client = string.IsNullOrEmpty(key)
            ? new CosmosClient(endpoint, new DefaultAzureCredential(), new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = stjOptions,
                ConnectionMode = ConnectionMode.Direct,
            })
            : new CosmosClient(endpoint, key, new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = stjOptions,
                ConnectionMode = ConnectionMode.Gateway,
            });

        var db = new CosmosCatanDb(_client, _dbName);
        await db.InitializeAsync();
        return db;
    }

    protected override async Task DeleteDatabaseAsync()
    {
        if (_client is not null)
        {
            try
            {
                await _client.GetDatabase(_dbName).DeleteAsync();
            }
            catch
            {
                // Best-effort cleanup — don't fail test teardown
            }
            finally
            {
                _client.Dispose();
                _client = null;
            }
        }
    }
}
