using Catan3.Shared.Utility;
using Catan3.Shared.ViewData;

namespace Catan3.GameService.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(CatanDbContext context, string imagesSourcePath)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Check if already seeded
        if (context.Players.Any())
        {
            Console.WriteLine("Database already seeded.");
            return;
        }

        Console.WriteLine($"Seeding database from: {imagesSourcePath}");

        // Default players with their colors (primary, secondary for gradient, foreground)
        var players = new[]
        {
            new PlayerProfile("joe-001", "Joe", "#0000FF", "#000080", "#FFFFFF", "/api/images/joe-001"),
            new PlayerProfile("dodgy-001", "Dodgy", "#FF0000", "#800000", "#FFFFFF", "/api/images/dodgy-001"),
            new PlayerProfile("doug-001", "Doug", "#008000", "#004000", "#FFFFFF", "/api/images/doug-001"),
            new PlayerProfile("ryan-001", "Ryan", "#A9A9A9", "#696969", "#FFFFFF", "/api/images/ryan-001"),
            new PlayerProfile("adrian-001", "Adrian", "#800080", "#400040", "#FFFFFF", "/api/images/adrian-001"),
            new PlayerProfile("chris-001", "Chris", "#000000", "#333333", "#FFFFFF", "/api/images/chris-001"),
            new PlayerProfile("guest-001", "Guest", "#FFA500", "#CC8400", "#000000", "/api/images/guest-001")
        };

        // Map player IDs to image file names
        var imageFiles = new Dictionary<string, string>
        {
            ["joe-001"] = "joe.jpg",
            ["dodgy-001"] = "Dodgy.png",
            ["doug-001"] = "doug.jpg",
            ["ryan-001"] = "ryan.jpg",
            ["adrian-001"] = "adrian.jpg",
            ["chris-001"] = "chris.jpg",
            ["guest-001"] = "guest.png"
        };

        // Seed players
        foreach (var player in players)
        {
            var playerEntity = new PlayerEntity
            {
                Id = player.Id,
                Data = JsonHelper.Serialize(player)
            };
            context.Players.Add(playerEntity);
            Console.WriteLine($"  Added player: {player.Name}");
        }

        // Seed images
        foreach (var (playerId, fileName) in imageFiles)
        {
            var imagePath = Path.Combine(imagesSourcePath, fileName);
            if (File.Exists(imagePath))
            {
                var imageData = await File.ReadAllBytesAsync(imagePath);
                var contentType = GetContentType(fileName);

                var imageEntity = new ImageEntity
                {
                    Id = playerId,
                    ContentType = contentType,
                    Data = imageData
                };
                context.Images.Add(imageEntity);
                Console.WriteLine($"  Added image: {fileName} ({imageData.Length} bytes)");
            }
            else
            {
                Console.WriteLine($"  Warning: Image not found: {imagePath}");
            }
        }

        await context.SaveChangesAsync();
        Console.WriteLine("Database seeding complete.");
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
