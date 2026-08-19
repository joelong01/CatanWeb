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

    /// <summary>
    /// Display names frozen at the moment the game completed. A completed game is a
    /// point-in-time document, so this is written from the profile and never updated --
    /// a later rename must not rewrite history (issue #208).
    /// </summary>
    public string PlayerNames { get; set; } = string.Empty;

    /// <summary>
    /// Authoritative player identity, used for click-through and to classify whether a
    /// stored name predates the display-name fix.
    /// </summary>
    public List<string> PlayerIds { get; set; } = [];
    public byte[] CompressedData { get; set; } = [];
    public int Size { get; set; }
}
