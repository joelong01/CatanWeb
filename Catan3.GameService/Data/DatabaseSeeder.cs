using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Microsoft.Extensions.Logging;

namespace Catan3.GameService.Data;

/// <summary>
/// Seeds system templates into CosmosDB on startup.
/// Templates are built from RegularBoardInfo.Default and ExpansionBoardInfo.Default.
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    /// Upserts system templates via ICatanDb (CosmosDB).
    /// Called by DatabaseSeedingService on every startup to keep templates in sync with code.
    /// </summary>
    internal static async Task UpsertSystemTemplatesAsync(Abstractions.ICatanDb db, ILogger? logger = null)
    {
        (string Id, string Name, string Category, IGameMetadata Metadata)[] candidates =
        [
            ("regular",   "Regular Game",   "Base",      RegularBoardInfo.Default),
            ("expansion", "Expansion Game", "Expansion", ExpansionBoardInfo.Default),
        ];

        foreach (var (id, name, category, metadata) in candidates)
        {
            var templateData = BuildTemplateFromMetadata(metadata, id, name, category);
            await db.SaveTemplateAsync(id, name, category, isSystemTemplate: true, templateData);
        }

        logger?.LogInformation("  Upserted {Count} code-defined system game templates", candidates.Length);

        // File-based system templates: expansions ship as data (a serialized
        // GameTemplateData) under "Default Data/SystemTemplates/*.json". Dropping a
        // new JSON there adds a system template with no code change. (This folder is
        // deliberately separate from "Default Data/Templates", which the catan.ps1
        // install snapshots as raw Cosmos TemplateDoc dumps — a different format.)
        await UpsertFileTemplatesAsync(db, logger);
    }

    /// <summary>
    /// Upserts file-based system templates found under "Default Data/SystemTemplates/*.json",
    /// each a raw serialized <see cref="GameTemplateData"/>. Idempotent (upsert by Id). A
    /// missing folder or a malformed file is logged and skipped — it never aborts seeding.
    /// </summary>
    private static async Task UpsertFileTemplatesAsync(Abstractions.ICatanDb db, ILogger? logger)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Default Data", "SystemTemplates");
        if (!Directory.Exists(dir))
        {
            logger?.LogInformation("  No file-template folder at {Dir}; skipping file templates", dir);
            return;
        }

        int count = 0;
        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(path);
                var data = JsonHelper.Deserialize<GameTemplateData>(json);
                if (data is null || string.IsNullOrWhiteSpace(data.Id))
                {
                    logger?.LogWarning("  Skipping template file {File}: empty or missing Id", Path.GetFileName(path));
                    continue;
                }

                await db.SaveTemplateAsync(data.Id, data.Name, data.Category, isSystemTemplate: true, data);
                count++;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "  Skipping malformed template file {File}", Path.GetFileName(path));
            }
        }

        logger?.LogInformation("  Upserted {Count} file-based system templates from {Dir}", count, dir);
    }

    private static GameTemplateData BuildTemplateFromMetadata(
        IGameMetadata metadata, string id, string name, string category)
    {
        var tiles = new List<TemplateTile>();
        for (int i = 0; i < metadata.TileKeys.Count; i++)
        {
            tiles.Add(new TemplateTile
            {
                Q = metadata.TileKeys[i].Q,
                R = metadata.TileKeys[i].R,
                Resource = metadata.Resources[i].ToString(),
                Number = metadata.Numbers[i]
            });
        }

        var harbors = metadata.Harbors.Select(h => new TemplateHarbor
        {
            Q = h.HarborKey.HexCoordinates.Q,
            R = h.HarborKey.HexCoordinates.R,
            Side = h.HarborKey.Side.ToString(),
            Type = h.HarborKey.HarborType.ToString()
        }).ToList();

        var entitlements = metadata.PurchaseableEntitlements.Select(e => new TemplateEntitlement
        {
            Entitlement = e.Entitlement.ToString()
        }).ToList();

        return new GameTemplateData
        {
            Id = id,
            Name = name,
            Category = category,
            Version = 1,
            Description = metadata.Description,
            Engine = "base",
            GameType = metadata.GameType.ToString(),
            ResourceRules = metadata.ResourceRules,
            HouseRules = metadata.HouseRules,
            HasSupplemental = metadata.HasSupplemental,
            Features = [], // Regular/Expansion are plain boards; features ride the same field, empty.
            Tiles = tiles,
            Harbors = harbors,
            Entitlements = entitlements
        };
    }
}
