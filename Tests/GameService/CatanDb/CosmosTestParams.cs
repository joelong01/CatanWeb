using System.Text.Json;

namespace Tests.GameService.CatanDb;

/// <summary>
/// Reads CosmosDB connection parameters from a JSON file written by catan.ps1 before
/// dotnet test runs. The file lives next to the test assembly in the output directory.
/// Falls back to local emulator defaults when the file is absent.
///
/// File shapes:
///   Local emulator: { "endpoint": "https://localhost:8081", "key": "C2y6..." }
///   Azure:          { "endpoint": "https://cosmos-catan.documents.azure.com:443/" }
///                   (no key → DefaultAzureCredential used by CosmosCatanDbTests)
/// </summary>
internal static class CosmosTestParams
{
    private static readonly Lazy<(string Endpoint, string? Key)> _params =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static string Endpoint => _params.Value.Endpoint;
    public static string? Key     => _params.Value.Key;

    private const string EmulatorEndpoint = "http://localhost:8081";
    private const string EmulatorKey =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw=";

    private static (string Endpoint, string? Key) Load()
    {
        var dir  = Path.GetDirectoryName(typeof(CosmosTestParams).Assembly.Location)!;
        var path = Path.Combine(dir, ".cosmos-test-params.json");

        if (!File.Exists(path))
            return (EmulatorEndpoint, EmulatorKey);

        using var doc  = JsonDocument.Parse(File.ReadAllText(path));
        var root       = doc.RootElement;
        var endpoint   = root.GetProperty("endpoint").GetString()!;
        var key        = root.TryGetProperty("key", out var k) ? k.GetString() : null;
        return (endpoint, key);
    }
}
