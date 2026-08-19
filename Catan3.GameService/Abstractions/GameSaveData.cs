namespace Catan3.GameService.Abstractions;

/// <summary>Full game save including compressed data blob.</summary>
public record GameSaveData
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string GameState { get; set; } = string.Empty;
    public string GameType { get; set; } = string.Empty;
    public string StartedBy { get; set; } = string.Empty;
    public int PlayerCount { get; set; }

    /// <summary>
    /// Display names as of the last save. Retained permanently: for records written before
    /// <see cref="PlayerIds"/> existed this is the only record of who played (issue #208).
    /// </summary>
    public string PlayerNames { get; set; } = string.Empty;

    /// <summary>
    /// Authoritative player identity. Callers resolve display names from the player profile
    /// by these IDs rather than parsing them or reading <see cref="PlayerNames"/>.
    /// </summary>
    public List<string> PlayerIds { get; set; } = [];

    public int TurnCount { get; set; }
    public DateTime SavedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] CompressedData { get; set; } = [];
    public int Size { get; set; }
}

/// <summary>Lightweight game listing row (no blob).</summary>
public record GameSummary
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string GameState { get; set; } = string.Empty;
    public string GameType { get; set; } = string.Empty;
    public string StartedBy { get; set; } = string.Empty;
    public int PlayerCount { get; set; }

    /// <summary>
    /// Display names as of the last save. Retained permanently: for records written before
    /// <see cref="PlayerIds"/> existed this is the only record of who played (issue #208).
    /// </summary>
    public string PlayerNames { get; set; } = string.Empty;

    /// <summary>
    /// Authoritative player identity. Callers resolve display names from the player profile
    /// by these IDs rather than parsing them or reading <see cref="PlayerNames"/>.
    /// </summary>
    public List<string> PlayerIds { get; set; } = [];

    public int TurnCount { get; set; }
    public DateTime SavedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Size { get; set; }
}
