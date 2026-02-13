using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.GameService.Data;
using Microsoft.EntityFrameworkCore;

namespace Catan3.GameService.Services;

/// <summary>
/// Service for CRUD operations on game templates stored in the database.
/// </summary>
public class GameTemplateService
{
    private readonly CatanDbContext _context;

    public GameTemplateService(CatanDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lists all templates, optionally filtered by category.
    /// Returns lightweight summaries for listing UI.
    /// </summary>
    public async Task<List<GameTemplateSummary>> ListAsync(string? category = null)
    {
        var query = _context.GameTemplates.AsQueryable();
        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(t => t.Category == category);
        }

        var entities = await query.OrderBy(t => t.Name).ToListAsync();

        return entities.Select(e =>
        {
            var data = JsonHelper.Deserialize<GameTemplateData>(e.Data);
            return new GameTemplateSummary
            {
                Id = e.Id,
                Name = e.Name,
                Category = e.Category,
                IsSystemTemplate = e.IsSystemTemplate,
                Description = data?.Description ?? string.Empty,
                MinPlayers = data?.ResourceRules.MinPlayers ?? 0,
                MaxPlayers = data?.ResourceRules.MaxPlayers ?? 0,
                UpdatedAt = e.UpdatedAt
            };
        }).ToList();
    }

    /// <summary>
    /// Gets a full template by ID, deserialized from JSON.
    /// Returns null if not found.
    /// </summary>
    public async Task<GameTemplateData?> GetAsync(string id)
    {
        var entity = await _context.GameTemplates.FindAsync(id);
        if (entity == null)
        {
            return null;
        }

        return JsonHelper.Deserialize<GameTemplateData>(entity.Data);
    }

    /// <summary>
    /// Updates an existing user-created template.
    /// Throws InvalidOperationException for system templates.
    /// </summary>
    public async Task SaveAsync(GameTemplateData template)
    {
        var entity = await _context.GameTemplates.FindAsync(template.Id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Template '{template.Id}' not found.");
        }

        if (entity.IsSystemTemplate)
        {
            throw new InvalidOperationException("System templates cannot be modified.");
        }

        entity.Name = template.Name;
        entity.Category = template.Category;
        entity.Version = template.Version;
        entity.Data = JsonHelper.Serialize(template);
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a new user template with a new ID and name.
    /// </summary>
    public async Task<GameTemplateData> SaveAsAsync(GameTemplateData template, string newId, string newName)
    {
        var existing = await _context.GameTemplates.FindAsync(newId);
        if (existing != null)
        {
            throw new InvalidOperationException($"Template with ID '{newId}' already exists.");
        }

        template.Id = newId;
        template.Name = newName;

        var entity = new GameTemplateEntity
        {
            Id = newId,
            Name = newName,
            Category = template.Category,
            IsSystemTemplate = false,
            Version = template.Version,
            Data = JsonHelper.Serialize(template),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.GameTemplates.Add(entity);
        await _context.SaveChangesAsync();

        return template;
    }

    /// <summary>
    /// Deletes a user-created template by ID.
    /// Throws InvalidOperationException for system templates.
    /// </summary>
    public async Task DeleteAsync(string id)
    {
        var entity = await _context.GameTemplates.FindAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Template '{id}' not found.");
        }

        if (entity.IsSystemTemplate)
        {
            throw new InvalidOperationException("System templates cannot be deleted.");
        }

        _context.GameTemplates.Remove(entity);
        await _context.SaveChangesAsync();
    }
}
