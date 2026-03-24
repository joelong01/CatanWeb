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
    public string PlayerNames { get; set; } = string.Empty;
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
    public string PlayerNames { get; set; } = string.Empty;
    public int TurnCount { get; set; }
    public DateTime SavedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Size { get; set; }
}
