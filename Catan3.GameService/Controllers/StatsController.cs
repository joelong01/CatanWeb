using Microsoft.AspNetCore.Mvc;
using Catan3.GameService.Abstractions;
using Catan3.Shared.Profiles;

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
    private readonly ICatanDb _db;
    private readonly ILogger<StatsController> _logger;

    public StatsController(ICatanDb db, ILogger<StatsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get all player statistics summary.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<PlayerStatsSummary>>> GetStats()
    {
        var players = await _db.LoadPlayersAsync();
        var summaries = players.Select(profile =>
        {
            var stats = profile.LifetimeStats ?? LifetimeStats.Empty;
            return new PlayerStatsSummary
            {
                PlayerId = profile.Id,
                PlayerName = profile.Name,
                GamesPlayed = stats.GamesPlayed,
                Wins = stats.Wins,
                WinRate = stats.WinRate,
                HighestScore = stats.HighestScoreRecord,
                AverageStars = stats.AverageStars,
                LastPlayed = null
            };
        }).OrderByDescending(s => s.Wins).ThenByDescending(s => s.GamesPlayed).ToList();

        return Ok(summaries);
    }

    /// <summary>
    /// Export all player statistics as JSON.
    /// </summary>
    [HttpGet("export")]
    public async Task<ActionResult<StatsExportDocument>> ExportStats()
    {
        var players = await _db.LoadPlayersAsync();
        var exports = players.Select(profile => new PlayerStatsExport
        {
            PlayerId = profile.Id,
            PlayerName = profile.Name,
            Stats = profile.LifetimeStats
        }).ToList();

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
            return BadRequest(new { error = "No players in import document" });

        var imported = 0;
        var merged = 0;
        var skipped = 0;

        if (request.Replace)
        {
            // Reset all existing stats first
            var allPlayers = await _db.LoadPlayersAsync();
            foreach (var profile in allPlayers)
            {
                var resetProfile = new PlayerProfile(
                    profile.Id, profile.Name, profile.Colors, profile.ImageUri, null);
                await _db.SavePlayerAsync(resetProfile);
            }
            _logger.LogInformation("Cleared existing stats for replace import");
        }

        foreach (var playerExport in request.Document.Players)
        {
            var profile = await _db.LoadPlayerAsync(playerExport.PlayerId);
            if (profile == null)
            {
                _logger.LogWarning("Player {PlayerId} not found, skipping import", playerExport.PlayerId);
                skipped++;
                continue;
            }

            LifetimeStats newStats;
            if (request.Replace || profile.LifetimeStats == null)
            {
                newStats = playerExport.Stats ?? LifetimeStats.Empty;
                imported++;
            }
            else
            {
                var existing = profile.LifetimeStats;
                var incoming = playerExport.Stats ?? LifetimeStats.Empty;

                newStats = existing with
                {
                    GamesPlayed = existing.GamesPlayed + incoming.GamesPlayed,
                    Wins = existing.Wins + incoming.Wins,
                    LongestRoadWins = existing.LongestRoadWins + incoming.LongestRoadWins,
                    LargestArmyWins = existing.LargestArmyWins + incoming.LargestArmyWins,
                    LongestRoadRecord = Math.Max(existing.LongestRoadRecord, incoming.LongestRoadRecord),
                    MostSoldiersRecord = Math.Max(existing.MostSoldiersRecord, incoming.MostSoldiersRecord),
                    MostStarsRecord = Math.Max(existing.MostStarsRecord, incoming.MostStarsRecord),
                    HighestScoreRecord = Math.Max(existing.HighestScoreRecord, incoming.HighestScoreRecord),
                    MostTargetedRecord = Math.Max(existing.MostTargetedRecord, incoming.MostTargetedRecord),
                    MostRobberRecord = Math.Max(existing.MostRobberRecord, incoming.MostRobberRecord),
                    MinSoldiersRecord = Math.Min(existing.MinSoldiersRecord, incoming.MinSoldiersRecord),
                    MinStarsRecord = Math.Min(existing.MinStarsRecord, incoming.MinStarsRecord),
                    MinTargetedRecord = Math.Min(existing.MinTargetedRecord, incoming.MinTargetedRecord),
                    MinRobberRecord = Math.Min(existing.MinRobberRecord, incoming.MinRobberRecord),
                    Totals = existing.Totals + incoming.Totals
                };
                merged++;
            }

            var updatedProfile = new PlayerProfile(
                profile.Id, profile.Name, profile.Colors, profile.ImageUri, newStats);
            await _db.SavePlayerAsync(updatedProfile);
        }

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
        var players = await _db.LoadPlayersAsync();
        var resetCount = 0;

        foreach (var profile in players)
        {
            if (profile.LifetimeStats == null) continue;

            var resetProfile = new PlayerProfile(
                profile.Id, profile.Name, profile.Colors, profile.ImageUri, null);
            await _db.SavePlayerAsync(resetProfile);
            resetCount++;
        }

        _logger.LogInformation("Reset stats for {Count} players", resetCount);

        return Ok(new
        {
            success = true,
            playersReset = resetCount,
            message = $"Reset statistics for {resetCount} players"
        });
    }
}
