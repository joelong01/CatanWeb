namespace Catan3.GameService.Abstractions;

/// <summary>Archive record for a completed game (winner declared).</summary>
public class CompletedGameRecord
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string WinnerId { get; set; } = string.Empty;
    public string WinnerName { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
    public DateTime StartedAt { get; set; }
    public int PlayerCount { get; set; }
    public int TurnCount { get; set; }
    public string PlayerNames { get; set; } = string.Empty;
    public byte[] CompressedData { get; set; } = [];
    public int Size { get; set; }
}
