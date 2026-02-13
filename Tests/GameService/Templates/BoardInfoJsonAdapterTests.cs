using Xunit;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.GameService.Factory;
using Catan3.GameService.Data;

namespace Tests.GameService.Templates;

/// <summary>
/// Verifies that BoardInfoJsonAdapter produces identical IGameMetadata to the
/// hardcoded RegularBoardInfo and ExpansionBoardInfo singletons. This is the
/// critical parity test for the template system — if these tests pass, games
/// created from templates are bit-for-bit identical to games from singletons.
/// </summary>
public class BoardInfoJsonAdapterTests
{
    [Fact]
    public void RegularTemplate_ProducesIdenticalMetadata()
    {
        var original = Catan3.Shared.Models.RegularBoardInfo.Default;
        var templateData = BuildTemplateFromMetadata(original, "regular", "Regular Game", "Base");
        var adapter = new BoardInfoJsonAdapter(templateData);

        AssertMetadataEqual(original, adapter);
    }

    [Fact]
    public void ExpansionTemplate_ProducesIdenticalMetadata()
    {
        var original = Catan3.Shared.Models.ExpansionBoardInfo.Default;
        var templateData = BuildTemplateFromMetadata(original, "expansion", "Expansion Game", "Expansion");
        var adapter = new BoardInfoJsonAdapter(templateData);

        AssertMetadataEqual(original, adapter);
    }

    [Fact]
    public void RoundTrip_SerializeDeserialize_PreservesOrder()
    {
        var original = Catan3.Shared.Models.RegularBoardInfo.Default;
        var templateData = BuildTemplateFromMetadata(original, "regular", "Regular Game", "Base");

        // Serialize and deserialize (simulates database storage)
        var json = JsonHelper.Serialize(templateData);
        var deserialized = JsonHelper.Deserialize<GameTemplateData>(json);
        Assert.NotNull(deserialized);

        var adapter = new BoardInfoJsonAdapter(deserialized);
        AssertMetadataEqual(original, adapter);
    }

    [Fact]
    public void ExpansionRoundTrip_SerializeDeserialize_PreservesOrder()
    {
        var original = Catan3.Shared.Models.ExpansionBoardInfo.Default;
        var templateData = BuildTemplateFromMetadata(original, "expansion", "Expansion Game", "Expansion");

        var json = JsonHelper.Serialize(templateData);
        var deserialized = JsonHelper.Deserialize<GameTemplateData>(json);
        Assert.NotNull(deserialized);

        var adapter = new BoardInfoJsonAdapter(deserialized);
        AssertMetadataEqual(original, adapter);
    }

    private static void AssertMetadataEqual(Catan3.Shared.Models.IGameMetadata expected, Catan3.Shared.Models.IGameMetadata actual)
    {
        // Scalar properties
        Assert.Equal(expected.GameType, actual.GameType);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.HasSupplemental, actual.HasSupplemental);

        // ResourceRules
        Assert.Equal(expected.ResourceRules.MaxCities, actual.ResourceRules.MaxCities);
        Assert.Equal(expected.ResourceRules.MaxSettlements, actual.ResourceRules.MaxSettlements);
        Assert.Equal(expected.ResourceRules.MaxRoads, actual.ResourceRules.MaxRoads);
        Assert.Equal(expected.ResourceRules.MinPlayers, actual.ResourceRules.MinPlayers);
        Assert.Equal(expected.ResourceRules.MaxPlayers, actual.ResourceRules.MaxPlayers);

        // HouseRules
        Assert.Equal(expected.HouseRules.GoldTiles, actual.HouseRules.GoldTiles);
        Assert.Equal(expected.HouseRules.WallsProtectCities, actual.HouseRules.WallsProtectCities);
        Assert.Equal(expected.HouseRules.HideBaronBeforeInvasion, actual.HouseRules.HideBaronBeforeInvasion);
        Assert.Equal(expected.HouseRules.KnightMovesBaronBeforeRoll, actual.HouseRules.KnightMovesBaronBeforeRoll);

        // TileKeys — exact order and coordinates
        Assert.Equal(expected.TileKeys.Count, actual.TileKeys.Count);
        for (int i = 0; i < expected.TileKeys.Count; i++)
        {
            Assert.Equal(expected.TileKeys[i].Q, actual.TileKeys[i].Q);
            Assert.Equal(expected.TileKeys[i].R, actual.TileKeys[i].R);
            Assert.Equal(expected.TileKeys[i].S, actual.TileKeys[i].S);
        }

        // Resources — exact order and types
        Assert.Equal(expected.Resources.Count, actual.Resources.Count);
        for (int i = 0; i < expected.Resources.Count; i++)
        {
            Assert.Equal(expected.Resources[i], actual.Resources[i]);
        }

        // Numbers — exact order and values
        Assert.Equal(expected.Numbers.Count, actual.Numbers.Count);
        for (int i = 0; i < expected.Numbers.Count; i++)
        {
            Assert.Equal(expected.Numbers[i], actual.Numbers[i]);
        }

        // Harbors — exact count, coordinates, sides, types
        Assert.Equal(expected.Harbors.Count, actual.Harbors.Count);
        for (int i = 0; i < expected.Harbors.Count; i++)
        {
            var expectedHarbor = expected.Harbors[i];
            var actualHarbor = actual.Harbors[i];
            Assert.Equal(expectedHarbor.HarborKey.HexCoordinates.Q, actualHarbor.HarborKey.HexCoordinates.Q);
            Assert.Equal(expectedHarbor.HarborKey.HexCoordinates.R, actualHarbor.HarborKey.HexCoordinates.R);
            Assert.Equal(expectedHarbor.HarborKey.HexCoordinates.S, actualHarbor.HarborKey.HexCoordinates.S);
            Assert.Equal(expectedHarbor.HarborKey.Side, actualHarbor.HarborKey.Side);
            Assert.Equal(expectedHarbor.HarborKey.HarborType, actualHarbor.HarborKey.HarborType);
        }

        // PurchaseableEntitlements — exact count and entitlements
        Assert.Equal(expected.PurchaseableEntitlements.Count, actual.PurchaseableEntitlements.Count);
        for (int i = 0; i < expected.PurchaseableEntitlements.Count; i++)
        {
            Assert.Equal(expected.PurchaseableEntitlements[i].Entitlement,
                         actual.PurchaseableEntitlements[i].Entitlement);
        }
    }

    /// <summary>
    /// Same logic as DatabaseSeeder.BuildTemplateFromMetadata — must produce identical output.
    /// </summary>
    private static GameTemplateData BuildTemplateFromMetadata(
        Catan3.Shared.Models.IGameMetadata metadata, string id, string name, string category)
    {
        var tiles = new List<TemplateTile>();
        for (int i = 0; i < metadata.TileKeys.Count; i++)
        {
            tiles.Add(new TemplateTile
            {
                Q = metadata.TileKeys[i].Q,
                R = metadata.TileKeys[i].R,
                Resource = metadata.Resources[i].ToString(),
                Number = metadata.Numbers[i]
            });
        }

        var harbors = metadata.Harbors.Select(h => new TemplateHarbor
        {
            Q = h.HarborKey.HexCoordinates.Q,
            R = h.HarborKey.HexCoordinates.R,
            Side = h.HarborKey.Side.ToString(),
            Type = h.HarborKey.HarborType.ToString()
        }).ToList();

        var entitlements = metadata.PurchaseableEntitlements.Select(e => new TemplateEntitlement
        {
            Entitlement = e.Entitlement.ToString()
        }).ToList();

        return new GameTemplateData
        {
            Id = id,
            Name = name,
            Category = category,
            Version = 1,
            Description = metadata.Description,
            Engine = "base",
            GameType = metadata.GameType.ToString(),
            ResourceRules = metadata.ResourceRules,
            HouseRules = metadata.HouseRules,
            HasSupplemental = metadata.HasSupplemental,
            Tiles = tiles,
            Harbors = harbors,
            Entitlements = entitlements
        };
    }
}
