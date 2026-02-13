namespace Catan3.Shared.Models;

/// <summary>
/// DTO representing a game template's full JSON document.
/// Stored in GameTemplateEntity.Data as serialized JSON.
/// </summary>
public class GameTemplateData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string Description { get; set; } = string.Empty;
    public string Engine { get; set; } = "base";
    public string GameType { get; set; } = "Regular";
    public ResourceRules ResourceRules { get; set; } = new();
    public HouseRules HouseRules { get; set; } = new();
    public bool HasSupplemental { get; set; }
    public List<TemplateTile> Tiles { get; set; } = [];
    public List<TemplateHarbor> Harbors { get; set; } = [];
    public List<TemplateEntitlement> Entitlements { get; set; } = [];
}

public class TemplateTile
{
    public int Q { get; set; }
    public int R { get; set; }
    public string Resource { get; set; } = "Desert";
    public int Number { get; set; }
}

public class TemplateHarbor
{
    public int Q { get; set; }
    public int R { get; set; }
    public string Side { get; set; } = "Bottom";
    public string Type { get; set; } = "ThreeForOne";
}

public class TemplateEntitlement
{
    public string Entitlement { get; set; } = "Road";
}

/// <summary>
/// Lightweight summary for template listing endpoints.
/// </summary>
public class GameTemplateSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsSystemTemplate { get; set; }
    public string Description { get; set; } = string.Empty;
    public int MinPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public DateTime UpdatedAt { get; set; }
}
