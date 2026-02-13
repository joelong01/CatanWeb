using Microsoft.EntityFrameworkCore;

namespace Catan3.GameService.Data;

public class CatanDbContext : DbContext
{
    public DbSet<PlayerEntity> Players { get; set; } = null!;
    public DbSet<ImageEntity> Images { get; set; } = null!;
    public DbSet<GameSaveDataEntity> GameSaveData { get; set; } = null!;
    public DbSet<GameSaveMetadataEntity> GameSaveMetadata { get; set; } = null!;
    public DbSet<CompletedGameEntity> CompletedGames { get; set; } = null!;
    public DbSet<RecordingEntity> Recordings { get; set; } = null!;
    public DbSet<GameTemplateEntity> GameTemplates { get; set; } = null!;

    public CatanDbContext(DbContextOptions<CatanDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Player entity - stores JSON document for player profile
        modelBuilder.Entity<PlayerEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(255);
            entity.Property(e => e.Data).IsRequired();
            // Data is NVARCHAR(MAX) in SQL Server, TEXT in SQLite
        });

        // Image entity - binary storage for player avatars
        modelBuilder.Entity<ImageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(255);
            entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Data).IsRequired();
            // Data is VARBINARY(MAX) in SQL Server, BLOB in SQLite
        });

        // Game save data - compressed game log blob
        modelBuilder.Entity<GameSaveDataEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CompressedData).IsRequired();
            // CompressedData is VARBINARY(MAX) in SQL Server, BLOB in SQLite
        });

        // Game save metadata - lightweight for queries
        modelBuilder.Entity<GameSaveMetadataEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.GameId).HasMaxLength(255);
            entity.Property(e => e.GameName).HasMaxLength(255);
            entity.Property(e => e.GameState).HasMaxLength(50);
            entity.Property(e => e.StartedBy).HasMaxLength(255);
            entity.Property(e => e.GameType).HasMaxLength(50);
            entity.Property(e => e.PlayerNames).HasMaxLength(500);

            // Indexes for common queries
            entity.HasIndex(e => e.GameId).IsUnique();
            entity.HasIndex(e => e.StartedBy);
            entity.HasIndex(e => e.GameState);
            entity.HasIndex(e => e.SavedAt);

            // Foreign key to game data
            entity.HasOne(e => e.GameData)
                  .WithMany()
                  .HasForeignKey(e => e.GameDataId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Completed games - archive of finished games with winner info
        modelBuilder.Entity<CompletedGameEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.GameId).HasMaxLength(255).IsRequired();
            entity.Property(e => e.GameName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.WinnerId).HasMaxLength(255).IsRequired();
            entity.Property(e => e.WinnerName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.PlayerNames).HasMaxLength(500).IsRequired();
            entity.Property(e => e.CompressedData).IsRequired();

            // Indexes for common queries
            entity.HasIndex(e => e.GameId);
            entity.HasIndex(e => e.WinnerId);
            entity.HasIndex(e => e.CompletedAt);
        });

        // Recordings - test recordings for replay verification
        modelBuilder.Entity<RecordingEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.GameType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PlayerIds).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Data).IsRequired();
            // Data is NVARCHAR(MAX) in SQL Server, TEXT in SQLite (JSON blob)

            // Indexes for common queries
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Game templates - document-style storage for board configurations
        modelBuilder.Entity<GameTemplateEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.HasIndex(e => e.Category);
            entity.Property(e => e.Data).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
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

/// <summary>
/// Archive of completed games with winner information.
/// Stores full game history for future replay/review.
/// </summary>
public class CompletedGameEntity
{
    /// <summary>
    /// Auto-increment primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Original game identifier
    /// </summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the game
    /// </summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>
    /// Player ID of the winner
    /// </summary>
    public string WinnerId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the winner
    /// </summary>
    public string WinnerName { get; set; } = string.Empty;

    /// <summary>
    /// When the winner was declared
    /// </summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>
    /// When the game was originally started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Number of players in the game
    /// </summary>
    public int PlayerCount { get; set; }

    /// <summary>
    /// Comma-separated list of player names
    /// </summary>
    public string PlayerNames { get; set; } = string.Empty;

    /// <summary>
    /// Number of turns/actions in the game
    /// </summary>
    public int TurnCount { get; set; }

    /// <summary>
    /// Compressed SerializableLog JSON (.catan format)
    /// </summary>
    public byte[] CompressedData { get; set; } = [];

    /// <summary>
    /// Size of compressed data in bytes
    /// </summary>
    public int Size { get; set; }
}

/// <summary>
/// Stores test recordings for replay verification.
/// Records game actions with expected GameHash results for automated testing.
/// </summary>
public class RecordingEntity
{
    /// <summary>
    /// Recording ID (GUID)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// User-provided name for the recording
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// When the recording was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Game type: "Regular" or "Expansion"
    /// </summary>
    public string GameType { get; set; } = string.Empty;

    /// <summary>
    /// Number of players in the recorded game
    /// </summary>
    public int PlayerCount { get; set; }

    /// <summary>
    /// Comma-separated list of player IDs
    /// </summary>
    public string PlayerIds { get; set; } = string.Empty;

    /// <summary>
    /// Number of recorded actions
    /// </summary>
    public int ActionCount { get; set; }

    /// <summary>
    /// JSON document containing initialGameModel and recorded actions
    /// </summary>
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// Game template entity with document-style storage for board configurations.
/// The Data column contains the full GameTemplateData JSON document.
/// </summary>
public class GameTemplateEntity
{
    /// <summary>
    /// Template ID (e.g., "regular", "expansion")
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name (e.g., "Regular Game", "Expansion Game")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Template category for grouping (e.g., "Base", "Expansion")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// True for built-in templates, false for user-created
    /// </summary>
    public bool IsSystemTemplate { get; set; }

    /// <summary>
    /// Schema version for forward compatibility
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// JSON document containing the full GameTemplateData
    /// </summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// When the template was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the template was last modified
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
