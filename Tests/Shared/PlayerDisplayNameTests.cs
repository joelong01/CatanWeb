using System;
using System.Linq;
using Catan3.Shared.Models;
using Xunit;

namespace Tests.Shared
{
    /// <summary>
    /// Regression tests for issue #208 — player display names were derived by parsing the
    /// player ID instead of reading <c>PlayerProfile.Name</c>.
    ///
    /// The old implementation split the ID on <c>-</c> and returned the first segment. That
    /// silently produced the right answer for seeded <c>Joe-001</c> IDs and a GUID fragment
    /// (<c>1ffb33af</c>) for any player whose ID was a bare GUID.
    ///
    /// The fix removes name derivation from the shared model entirely: <c>PlayerModel</c>
    /// carries identity only, and display names are resolved from the profile by ID. These
    /// tests pin the parts of that contract which are expressible on the server.
    /// </summary>
    public class PlayerDisplayNameTests
    {
        /// <summary>
        /// The core guarantee: no member of <c>PlayerModel</c> exposes a display name.
        /// If someone reintroduces one, it will almost certainly be derived from the ID
        /// again — which is the bug.
        /// </summary>
        [Fact]
        public void PlayerModel_ExposesNoDisplayName()
        {
            var nameLike = typeof(PlayerModel)
                .GetProperties()
                .Where(p => p.Name.Contains("Name", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Name)
                .ToList();

            Assert.Empty(nameLike);
        }

        /// <summary>
        /// No type in the shared model may reintroduce ID-to-name parsing. There were three
        /// copies of <c>ExtractNameFromId</c> before the fix; all were removed.
        /// </summary>
        [Theory]
        [InlineData(typeof(PlayerModel))]
        [InlineData(typeof(GameModel))]
        [InlineData(typeof(GameInfo))]
        [InlineData(typeof(NewGameRequest))]
        public void SharedModels_HaveNoNameFromIdParsing(Type type)
        {
            var parsers = type
                .GetMethods(System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Instance)
                .Where(m => m.Name.Contains("ExtractName", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Name)
                .ToList();

            Assert.Empty(parsers);
        }

        /// <summary>
        /// <c>GetPlayerIds</c> returns identity in turn order and never a parsed name, even
        /// when the ID looks exactly like the old <c>Name-NNN</c> convention.
        /// </summary>
        [Fact]
        public void GetPlayerIds_ReturnsIdsVerbatim()
        {
            var model = new GameModel
            {
                Players =
                [
                    new PlayerModel { Id = "Joe-001" },
                    new PlayerModel { Id = "1ffb33af-9316-4870-b7db-32346965ed8b" },
                ]
            };

            Assert.Equal(
                ["Joe-001", "1ffb33af-9316-4870-b7db-32346965ed8b"],
                model.GetPlayerIds());
        }

        /// <summary>
        /// Auto-generated game names use a player count. They previously embedded the first
        /// player's derived name, which is exactly the leak this fix closes.
        /// </summary>
        [Fact]
        public void GetDisplayName_UsesPlayerCountNotAName()
        {
            var model = new GameModel
            {
                GameName = string.Empty,
                CreatedTime = new DateTime(2026, 8, 19, 17, 10, 0, DateTimeKind.Utc),
                Players =
                [
                    new PlayerModel { Id = "1ffb33af-9316-4870-b7db-32346965ed8b" },
                    new PlayerModel { Id = "Joe-001" },
                ]
            };

            var displayName = model.GetDisplayName();

            Assert.Contains("2 players", displayName);
            Assert.DoesNotContain("1ffb33af", displayName);
            Assert.DoesNotContain("Joe", displayName);
        }

        /// <summary>A custom game name is still used verbatim.</summary>
        [Fact]
        public void GetDisplayName_PrefersExplicitGameName()
        {
            var model = new GameModel { GameName = "Catan Wed 7PM" };
            Assert.Equal("Catan Wed 7PM", model.GetDisplayName());
        }
    }
}
