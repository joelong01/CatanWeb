using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Catan3.GameService.Data;
using Catan3.Shared.Profiles;
using Catan3.Shared.Utility;

namespace Catan3.GameService.Controllers;

/// <summary>
/// Player stats summary for list display.
/// </summary>
public class PlayerStatsSummary
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public double WinRate { get; set; }
    public int HighestScore { get; set; }
    public double AverageStars { get; set; }
    public DateTime? LastPlayed { get; set; }
}

/// <summary>
/// Full stats export for a single player.
/// </summary>
public class PlayerStatsExport
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public LifetimeStats? Stats { get; set; }
}

/// <summary>
/// Complete stats export document.
/// </summary>
public class StatsExportDocument
{
    public DateTime ExportedAt { get; set; }
    public string Source { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public List<PlayerStatsExport> Players { get; set; } = [];
}

/// <summary>
/// Request to import stats.
/// </summary>
public class StatsImportRequest
{
    public StatsExportDocument Document { get; set; } = new();
    public bool Replace { get; set; } = false;
}

/// <summary>
/// Controller for managing player lifetime statistics.
/// </summary>
[ApiController]
[Route("api/stats")]
public class StatsController : ControllerBase
{
    private readonly CatanDbContext _dbContext;
    private readonly ILogger<StatsController> _logger;

    public StatsController(CatanDbContext dbContext, ILogger<StatsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all player statistics summary.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<PlayerStatsSummary>>> GetStats()
    {
        var players = await _dbContext.Players.ToListAsync();
        var summaries = new List<PlayerStatsSummary>();

        foreach (var playerEntity in players)
        {
            var profile = JsonHelper.Deserialize<PlayerProfile>(playerEntity.Data);
            if (profile == null) continue;

            var stats = profile.LifetimeStats ?? LifetimeStats.Empty;
            summaries.Add(new PlayerStatsSummary
            {
                PlayerId = profile.Id,
                PlayerName = profile.Name,
                GamesPlayed = stats.GamesPlayed,
                Wins = stats.Wins,
                WinRate = stats.WinRate,
                HighestScore = stats.HighestScoreRecord,
                AverageStars = stats.AverageStars,
                LastPlayed = null // Could track this in the future
            });
        }

        // Sort by wins descending, then games played
        return Ok(summaries.OrderByDescending(s => s.Wins).ThenByDescending(s => s.GamesPlayed).ToList());
    }

    /// <summary>
    /// Export all player statistics as JSON.
    /// </summary>
    [HttpGet("export")]
    public async Task<ActionResult<StatsExportDocument>> ExportStats()
    {
        var players = await _dbContext.Players.ToListAsync();
        var exports = new List<PlayerStatsExport>();

        foreach (var playerEntity in players)
        {
            var profile = JsonHelper.Deserialize<PlayerProfile>(playerEntity.Data);
            if (profile == null) continue;

            exports.Add(new PlayerStatsExport
            {
                PlayerId = profile.Id,
                PlayerName = profile.Name,
                Stats = profile.LifetimeStats
            });
        }

        var document = new StatsExportDocument
        {
            ExportedAt = DateTime.UtcNow,
            Source = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "local",
            SchemaVersion = LifetimeStats.CurrentSchemaVersion,
            Players = exports
        };

        _logger.LogInformation("Exported stats for {Count} players", exports.Count);
        return Ok(document);
    }

    /// <summary>
    /// Import player statistics from JSON.
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult> ImportStats([FromBody] StatsImportRequest request)
    {
        if (request.Document.Players.Count == 0)
        {
            return BadRequest(new { error = "No players in import document" });
        }

        var imported = 0;
        var merged = 0;
        var skipped = 0;

        if (request.Replace)
        {
            // Reset all existing stats first
            var allPlayers = await _dbContext.Players.ToListAsync();
            foreach (var playerEntity in allPlayers)
            {
                var profile = JsonHelper.Deserialize<PlayerProfile>(playerEntity.Data);
                if (profile == null) continue;

                var resetProfile = new PlayerProfile(
                    profile.Id,
                    profile.Name,
                    profile.Colors,
                    profile.ImageUri,
                    null // Clear stats
                );
                playerEntity.Data = JsonHelper.Serialize(resetProfile);
            }
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Cleared existing stats for replace import");
        }

        foreach (var playerExport in request.Document.Players)
        {
            var playerEntity = await _dbContext.Players.FindAsync(playerExport.PlayerId);
            if (playerEntity == null)
            {
                _logger.LogWarning("Player {PlayerId} not found, skipping import", playerExport.PlayerId);
                skipped++;
                continue;
            }

            var profile = JsonHelper.Deserialize<PlayerProfile>(playerEntity.Data);
            if (profile == null)
            {
                skipped++;
                continue;
            }

            LifetimeStats newStats;
            if (request.Replace || profile.LifetimeStats == null)
            {
                // Direct import
                newStats = playerExport.Stats ?? LifetimeStats.Empty;
                imported++;
            }
            else
            {
                // Merge: add games/wins, keep max records
                var existing = profile.LifetimeStats;
                var incoming = playerExport.Stats ?? LifetimeStats.Empty;

                newStats = existing with
                {
                    GamesPlayed = existing.GamesPlayed + incoming.GamesPlayed,
                    Wins = existing.Wins + incoming.Wins,
                    LongestRoadWins = existing.LongestRoadWins + incoming.LongestRoadWins,
                    LargestArmyWins = existing.LargestArmyWins + incoming.LargestArmyWins,
                    // Max records: keep higher
                    LongestRoadRecord = Math.Max(existing.LongestRoadRecord, incoming.LongestRoadRecord),
                    MostSoldiersRecord = Math.Max(existing.MostSoldiersRecord, incoming.MostSoldiersRecord),
                    MostStarsRecord = Math.Max(existing.MostStarsRecord, incoming.MostStarsRecord),
                    HighestScoreRecord = Math.Max(existing.HighestScoreRecord, incoming.HighestScoreRecord),
                    MostTargetedRecord = Math.Max(existing.MostTargetedRecord, incoming.MostTargetedRecord),
                    MostRobberRecord = Math.Max(existing.MostRobberRecord, incoming.MostRobberRecord),
                    // Min records: keep lower
                    MinSoldiersRecord = Math.Min(existing.MinSoldiersRecord, incoming.MinSoldiersRecord),
                    MinStarsRecord = Math.Min(existing.MinStarsRecord, incoming.MinStarsRecord),
                    MinTargetedRecord = Math.Min(existing.MinTargetedRecord, incoming.MinTargetedRecord),
                    MinRobberRecord = Math.Min(existing.MinRobberRecord, incoming.MinRobberRecord),
                    // Totals: add together
                    Totals = existing.Totals + incoming.Totals
                };
                merged++;
            }

            var updatedProfile = new PlayerProfile(
                profile.Id,
                profile.Name,
                profile.Colors,
                profile.ImageUri,
                newStats
            );
            playerEntity.Data = JsonHelper.Serialize(updatedProfile);
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Import complete: {Imported} imported, {Merged} merged, {Skipped} skipped",
            imported, merged, skipped);

        return Ok(new
        {
            success = true,
            imported,
            merged,
            skipped,
            message = $"Import complete: {imported} imported, {merged} merged, {skipped} skipped"
        });
    }

    /// <summary>
    /// Reset all player statistics.
    /// </summary>
    [HttpDelete]
    public async Task<ActionResult> ResetStats()
    {
        var players = await _dbContext.Players.ToListAsync();
        var resetCount = 0;

        foreach (var playerEntity in players)
        {
            var profile = JsonHelper.Deserialize<PlayerProfile>(playerEntity.Data);
            if (profile == null) continue;

            // Only reset if they have stats
            if (profile.LifetimeStats == null) continue;

            var resetProfile = new PlayerProfile(
                profile.Id,
                profile.Name,
                profile.Colors,
                profile.ImageUri,
                null // Clear stats
            );
            playerEntity.Data = JsonHelper.Serialize(resetProfile);
            resetCount++;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Reset stats for {Count} players", resetCount);

        return Ok(new
        {
            success = true,
            playersReset = resetCount,
            message = $"Reset statistics for {resetCount} players"
        });
    }
}
