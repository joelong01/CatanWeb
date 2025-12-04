using Microsoft.EntityFrameworkCore;

namespace Catan3.GameService.Data;

public class CatanDbContext : DbContext
{
    public DbSet<PlayerEntity> Players { get; set; } = null!;
    public DbSet<ImageEntity> Images { get; set; } = null!;
    public DbSet<GameSaveDataEntity> GameSaveData { get; set; } = null!;
    public DbSet<GameSaveMetadataEntity> GameSaveMetadata { get; set; } = null!;

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

        modelBuilder.Entity<GameSaveDataEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompressedData).IsRequired();
        });

        modelBuilder.Entity<GameSaveMetadataEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.GameId).IsUnique();
            entity.HasIndex(e => e.StartedBy);
            entity.HasIndex(e => e.GameState);
            entity.HasIndex(e => e.SavedAt);
            entity.HasOne(e => e.GameData)
                  .WithMany()
                  .HasForeignKey(e => e.GameDataId)
                  .OnDelete(DeleteBehavior.Cascade);
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
    /// JSON document containing Profiles
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
/// Stores the compressed game log data (heavy blob storage).
/// </summary>
public class GameSaveDataEntity
{
    /// <summary>
    /// Auto-increment primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Compressed SerializableLog JSON (.catan format)
    /// </summary>
    public byte[] CompressedData { get; set; } = [];

    /// <summary>
    /// Size of compressed data in bytes (for display without loading blob)
    /// </summary>
    public int Size { get; set; }
}

/// <summary>
/// Lightweight metadata for querying and displaying saved games.
/// </summary>
public class GameSaveMetadataEntity
{
    /// <summary>
    /// Auto-increment primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique game identifier (indexed)
    /// </summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>
    /// User who created the game - "WebUI" for now, user ID when auth is added (indexed)
    /// </summary>
    public string StartedBy { get; set; } = string.Empty;

    /// <summary>
    /// When the game was last saved (indexed for sorting)
    /// </summary>
    public DateTime SavedAt { get; set; }

    /// <summary>
    /// When the game was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Current game state for filtering (indexed)
    /// </summary>
    public string GameState { get; set; } = string.Empty;

    /// <summary>
    /// Game type: "Regular" or "Expansion"
    /// </summary>
    public string GameType { get; set; } = string.Empty;

    /// <summary>
    /// Number of players in the game
    /// </summary>
    public int PlayerCount { get; set; }

    /// <summary>
    /// Comma-separated list of player names for display
    /// </summary>
    public string PlayerNames { get; set; } = string.Empty;

    /// <summary>
    /// Number of state transitions (DoneStack count)
    /// </summary>
    public int TurnCount { get; set; }

    /// <summary>
    /// Display name for the game
    /// </summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to GameSaveData
    /// </summary>
    public int GameDataId { get; set; }

    /// <summary>
    /// Navigation property to the game data
    /// </summary>
    public GameSaveDataEntity GameData { get; set; } = null!;
}
