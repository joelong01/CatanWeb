using Microsoft.EntityFrameworkCore;

namespace Catan3.GameService.Data;

public class CatanDbContext : DbContext
{
    public DbSet<PlayerEntity> Players { get; set; } = null!;
    public DbSet<ImageEntity> Images { get; set; } = null!;
    public DbSet<GameSaveEntity> GameSaves { get; set; } = null!;

    public CatanDbContext(DbContextOptions<CatanDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Data).IsRequired();
        });

        modelBuilder.Entity<ImageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ContentType).IsRequired();
            entity.Property(e => e.Data).IsRequired();
        });

        modelBuilder.Entity<GameSaveEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompressedData).IsRequired();
            entity.Property(e => e.SavedAt).IsRequired();
            entity.HasIndex(e => e.GameId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.StartedBy);
        });
    }
}

/// <summary>
/// Player entity with document-style storage (mirrors CosmosDB model)
/// </summary>
public class PlayerEntity
{
    /// <summary>
    /// Player ID (e.g., "joe-001")
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// JSON document containing PlayerProfile
    /// </summary>
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// Image entity for binary image storage
/// </summary>
public class ImageEntity
{
    /// <summary>
    /// Image ID (e.g., "joe-001")
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// MIME content type (e.g., "image/jpeg", "image/png")
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Binary image data
    /// </summary>
    public byte[] Data { get; set; } = [];
}

/// <summary>
/// Game save entity for persisting game state
/// </summary>
public class GameSaveEntity
{
    /// <summary>
    /// Auto-increment primary key for efficient indexing
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Game ID (indexed for lookups)
    /// </summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>
    /// Compressed game data (.catan format)
    /// </summary>
    public byte[] CompressedData { get; set; } = [];

    /// <summary>
    /// When the game was last saved
    /// </summary>
    public DateTime SavedAt { get; set; }

    /// <summary>
    /// When the game was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Game name for display
    /// </summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>
    /// Current game state (for filtering/cleanup)
    /// </summary>
    public string GameState { get; set; } = string.Empty;

    /// <summary>
    /// Player ID who started the game (for "My games" filter)
    /// </summary>
    public string StartedBy { get; set; } = string.Empty;

    /// <summary>
    /// Number of players in the game
    /// </summary>
    public int PlayerCount { get; set; }

    /// <summary>
    /// Game type (Regular or Expansion)
    /// </summary>
    public string GameType { get; set; } = string.Empty;
}
