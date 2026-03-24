using Catan3.Shared.Models;
using Catan3.GameService.Abstractions;

namespace Catan3.GameService.Services;

/// <summary>
/// Service for CRUD operations on game templates stored in the database.
/// </summary>
public class GameTemplateService
{
    private readonly ICatanDb _db;

    public GameTemplateService(ICatanDb db)
    {
        _db = db;
    }

    /// <summary>
    /// Lists all templates, optionally filtered by category.
    /// Returns lightweight summaries for listing UI.
    /// </summary>
    public async Task<List<GameTemplateSummary>> ListAsync(string? category = null)
    {
        var summaries = await _db.ListTemplatesAsync(category);
        return summaries.ToList();
    }

    /// <summary>
    /// Gets a full template by ID, deserialized from JSON.
    /// Returns null if not found.
    /// </summary>
    public async Task<GameTemplateData?> GetAsync(string id)
    {
        return await _db.LoadTemplateAsync(id);
    }

    /// <summary>
    /// Updates an existing user-created template.
    /// Throws InvalidOperationException for system templates.
    /// </summary>
    public async Task SaveAsync(GameTemplateData template)
    {
        // Check if it exists and is not a system template
        var existing = (await _db.ListTemplatesAsync())
            .FirstOrDefault(t => t.Id == template.Id);

        if (existing == null)
            throw new KeyNotFoundException($"Template '{template.Id}' not found.");

        if (existing.IsSystemTemplate)
            throw new InvalidOperationException("System templates cannot be modified.");

        await _db.SaveTemplateAsync(
            template.Id, template.Name, template.Category,
            isSystemTemplate: false, template);
    }

    /// <summary>
    /// Creates a new user template with a new ID and name.
    /// </summary>
    public async Task<GameTemplateData> SaveAsAsync(GameTemplateData template, string newId, string newName)
    {
        var existing = await _db.LoadTemplateAsync(newId);
        if (existing != null)
            throw new InvalidOperationException($"Template with ID '{newId}' already exists.");

        template.Id = newId;
        template.Name = newName;

        await _db.SaveTemplateAsync(
            newId, newName, template.Category,
            isSystemTemplate: false, template);

        return template;
    }

    /// <summary>
    /// Deletes a user-created template by ID.
    /// Throws InvalidOperationException for system templates.
    /// </summary>
    public async Task DeleteAsync(string id)
    {
        var existing = (await _db.ListTemplatesAsync())
            .FirstOrDefault(t => t.Id == id);

        if (existing == null)
            throw new KeyNotFoundException($"Template '{id}' not found.");

        if (existing.IsSystemTemplate)
            throw new InvalidOperationException("System templates cannot be deleted.");

        await _db.DeleteTemplateAsync(id);
    }
}
