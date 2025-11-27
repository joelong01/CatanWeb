using System.Diagnostics;
using System.Numerics;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Catan3.Shared.Extensions
{
    public static class GameModelExtensions
    {
        /// <summary>
        /// Creates a new GameModel with the specified game metadata, players, and name.
        /// Initializes all tiles, buildings, roads, and harbors for the game board.
        /// </summary>
        /// <param name="gameInfo">The game metadata containing board configuration.</param>
        /// <param name="playerIds">The list of player IDs.</param>
        /// <param name="gameName">The name of the game.</param>
        /// <returns>A fully initialized GameModel ready to play.</returns>
        public static GameModel CreateNew(IGameMetadata gameInfo, IList<string> playerIds, string gameName)
        {
            Debug.Assert((gameInfo.TileKeys.Count == gameInfo.Numbers.Count) && (gameInfo.TileKeys.Count == gameInfo.Resources.Count));

            if (playerIds.Count < gameInfo.ResourceRules.MinPlayers || playerIds.Count > gameInfo.ResourceRules.MaxPlayers)
            {
                throw new GameException($"{gameInfo.Description} must have players between {gameInfo.ResourceRules.MinPlayers} and {gameInfo.ResourceRules.MaxPlayers}. You gave {playerIds.Count}");
            }

            List<PlayerModel> playerModels = playerIds.Select(id => new PlayerModel { Id = id }).ToList();

            GameModel game = new()
            {
                GameId = Guid.NewGuid().ToString(),
                GameName = gameName,
                CreatedTime = DateTime.UtcNow,
                GameType = gameInfo.GameType,
                Players = playerModels,
                CurrentPlayerId = playerModels.FirstOrDefault()?.Id ?? "",
                HouseRules = gameInfo.HouseRules,
                ResourceRules = gameInfo.ResourceRules,
                HasSupplementalBuildPhase = gameInfo.HasSupplemental,
                EntitlementPurchaseModel = gameInfo.PurchaseableEntitlements.ToList(),
                Tiles = [],
                Buildings = [],
                Roads = [],
                Harbors = [],
                GameResourcesModel = new ResourcesModel(),
                RollModel = new RollModel(),
                ActionFlags = new ActionFlags(),
                Robber = new RobberModel { Coordinates = HexCoordinates.Default },
                Random = new ReplayableRandom()
            };

            // Initialize tiles
            for (int i = 0; i < gameInfo.TileKeys.Count; i++)
            {
                var tile = new TileModel
                {
                    ResourceTileType = gameInfo.Resources[i],
                    Number = gameInfo.Numbers[i],
                    TileKey = gameInfo.TileKeys[i]
                };
                game.Tiles.Add(tile);
            }

            // Initialize buildings and roads for each tile
            foreach (var tile in game.Tiles)
            {
                // Add buildings for each position on the tile
                foreach (HexPosition buildingPosition in Enum.GetValues<HexPosition>())
                {
                    if (buildingPosition == HexPosition.None) continue;

                    var buildingKey = new BuildingKey(tile.TileKey, buildingPosition);
                    var building = game.Buildings.FindBuildingModel(buildingKey);
                    if (building is null)
                    {
                        game.Buildings.Add(new BuildingModel
                        {
                            BuildingKey = buildingKey,
                            BuildingState = BuildingState.NotBuildable
                        });
                    }
                }

                // Add roads for each side of the tile
                foreach (HexSide roadPosition in Enum.GetValues<HexSide>())
                {
                    if (roadPosition == HexSide.None) continue;

                    var roadKey = new RoadKey(tile.TileKey, roadPosition);
                    var road = game.Roads.FindRoad(roadKey);
                    if (road is null)
                    {
                        game.Roads.Add(new RoadModel
                        {
                            RoadKey = roadKey,
                            RoadState = RoadState.Unowned
                        });
                    }
                }
            }

            // Add harbors
            game.Harbors.AddRange(gameInfo.Harbors);


            // Shuffle the board
            game.Shuffle();

            // Set initial game state
            game.GameState = GameState.PickingBoard;

            // Compute initial game hash
            game.UpdateGameHash();

            return game;
        }

        public static bool AllocationPhase(this GameModel gameModel)
        {
            return (gameModel.GameState == GameState.AllocateResourceForward || gameModel.GameState == GameState.AllocateResourceReverse ||
                    gameModel.GameState == GameState.WaitingForRollForOrder || gameModel.GameState == GameState.FinishedRollOrder ||
                    gameModel.GameState == GameState.BeginResourceAllocation || gameModel.GameState == GameState.PickingBoard);
        }

        /// <summary>
        /// gets the next Random number using the ReplayableRandom class and updates the game state
        /// </summary>
        public static int NextRandom(this GameModel gameModel)
        {
            return gameModel.Random.Next();
        }

        public static int NextRandom(this GameModel gameModel, int maxValue)
        {
            return gameModel.Random.Next(maxValue);
        }

        public static int NextRandom(this GameModel gameModel, int minValue, int maxValue)
        {
            return gameModel.Random.Next(minValue, maxValue);
        }


        public static GamePhase Phase(this GameModel gameModel)
        {
            switch (gameModel.GameState)
            {
                case GameState.Uninitialized:
                case GameState.WaitingForNewGame:
                case GameState.BeginResourceAllocation:
                case GameState.WaitingForPlayers:
                    return GamePhase.Starting;
                case GameState.PickingBoard:
                case GameState.WaitingForRollForOrder:
                case GameState.FinishedRollOrder:
                    return GamePhase.PickingBoard;
                case GameState.AllocateResourceForward:
                case GameState.AllocateResourceReverse:
                case GameState.DoneResourceAllocation:
                    return GamePhase.PickingResources;
                case GameState.WaitingForRoll:
                    return GamePhase.Rolling;
                case GameState.WaitingForNext:
                case GameState.Supplemental:
                    return GamePhase.Purchase;
                case GameState.MustMoveRobber:
                case GameState.TooManyCards:
                case GameState.MustDestroyCity:
                    return GamePhase.ActionRequired;
                default:
                    return GamePhase.Unspecified;
            }
        }

        public static EntitlementPurchaseModel PurchaseModel(this GameModel gameModel, Entitlement entitlement)
        {
            var model = gameModel.EntitlementPurchaseModel.First(m => m.Entitlement == entitlement) ?? throw new System.Exception($"{entitlement} not found in Purchasable Entitlements!");
            return model;
        }

        /// <summary>
        /// Computes a deterministic hash code for a string using simple polynomial rolling hash.
        /// Unlike string.GetHashCode(), this produces the same result across application runs.
        /// </summary>
        private static int GetDeterministicStringHash(string? input)
        {
            if (string.IsNullOrEmpty(input)) return 0;

            int hash = 0;
            const int prime = 31;
            foreach (char c in input)
            {
                hash = hash * prime + c;
            }
            return hash;
        }

        /// <summary>
        /// Computes a prime-based hash representing the current state of the game tiles.
        /// Uses unique prime multipliers for each tile position to ensure mathematical uniqueness.
        /// This method is fast and deterministic - identical game states will always produce the same hash.
        /// Used for fast verification that all clients have identical game states in multi-player testing.
        /// </summary>
        /// <summary>
        /// Computes a hash representing the current game state.
        /// Used to detect changes in board layout, building placement, and game progression.
        /// </summary>
        public static string ComputeGameHash(this GameModel gameModel)
        {
            // Prime numbers for unique hash computation
            // Need enough primes for: tiles (60), harbors (22), roads (~100), buildings (80), entitlements (~50), etc.
            var primes = new Stack<int>(First750Primes.AsEnumerable());

            BigInteger hash = 0;

            // Include GameState and CurrentPlayerId for state verification
            hash += (int)gameModel.GameState * primes.Pop();
            hash += GetDeterministicStringHash(gameModel.CurrentPlayerId) * primes.Pop();


            // Include HasSupplementalBuildPhase for game configuration consistency
            hash += (gameModel.HasSupplementalBuildPhase ? 1 : 0) * primes.Pop();

            // Process tiles with unique prime multipliers (sorted by coordinates for consistency)
            var sortedTiles = gameModel.Tiles.OrderBy(t => t.TileKey.Q)
                .ThenBy(t => t.TileKey.R)
                .ThenBy(t => t.TileKey.S);

            foreach (var tile in sortedTiles)
            {
                // Each tile gets 2 unique primes: one for resource, one for number
                hash += primes.Pop() * (int)tile.ResourceTileType;
                hash += primes.Pop() * tile.Number;
            }

            // Include Harbors (sorted by coordinates for consistency) 
            if (gameModel.Harbors?.Any() == true)
            {
                var sortedHarbors = gameModel.Harbors.OrderBy(h => h.HarborKey.HexCoordinates.Q)
                    .ThenBy(h => h.HarborKey.HexCoordinates.R).ThenBy(h => h.HarborKey.Side);

                int harborIndex = 0;
                foreach (var harbor in sortedHarbors)
                {
                    // Use unique primes for harbors
                    hash += primes.Pop() * harborIndex; // Harbor position in sorted order
                    hash += primes.Pop() * (int)harbor.HarborKey.HarborType; // Harbor type value
                    harborIndex++;
                }
            }

            // Include Owned Roads (sorted by coordinates for consistency)
            if (gameModel.Roads?.Any() == true)
            {
                var ownedRoads = gameModel.Roads
                    .Where(r => !string.IsNullOrEmpty(r.OwnerId))
                    .OrderBy(r => r.RoadKey.TileKey.Q)
                    .ThenBy(r => r.RoadKey.TileKey.R)
                    .ThenBy(r => r.RoadKey.HexSide);

                int roadIndex = 0;
                foreach (var road in ownedRoads)
                {
                    // Use unique primes for each owned road
                    hash += primes.Pop() * roadIndex; // Road position in sorted order
                    hash += primes.Pop() * GetDeterministicStringHash(road.OwnerId); // Owner hash
                    roadIndex++;
                }
            }

            // Include Buildings (sorted by coordinates for consistency)
            if (gameModel.Buildings?.Any() == true)
            {
                var ownedBuildings = gameModel.Buildings
                    .Where(b => !string.IsNullOrEmpty(b.OwnerId))
                    .OrderBy(b => b.BuildingKey.HexCoordinates.Q)
                    .ThenBy(b => b.BuildingKey.HexCoordinates.R)
                    .ThenBy(b => b.BuildingKey.Position);

                int buildingIndex = 0;
                foreach (var building in ownedBuildings)
                {
                    // Use unique primes for each owned building
                    hash += primes.Pop() * buildingIndex; // Building position in sorted order
                    hash += primes.Pop() * (int)building.BuildingState; // Building state (Settlement vs City)
                    hash += primes.Pop() * GetDeterministicStringHash(building.OwnerId); // Owner hash
                    buildingIndex++;
                }
            }

            // Include Player Entitlements (sorted by player ID for consistency)
            if (gameModel.Players?.Any() == true)
            {
                var sortedPlayers = gameModel.Players.OrderBy(p => p.Id);

                foreach (var player in sortedPlayers)
                {
                    // Include ParticipatingInSupplemental flag for supplemental phase consistency
                    hash += primes.Pop() * (player.ParticipatingInSupplemental ? 1 : 0);
                    // Include FinishedSupplemental flag for supplemental phase state tracking
                    hash += primes.Pop() * (player.FinishedSupplemental ? 1 : 0);
                    hash += primes.Pop() * GetDeterministicStringHash(player.Id);


                    if (player.UnspentEntitlements?.Any() == true)
                    {
                        var sortedEntitlements = player.UnspentEntitlements.OrderBy(e => (int)e);
                        foreach (var entitlement in sortedEntitlements)
                        {
                            hash += primes.Pop() * (int)entitlement;
                            hash += primes.Pop() * GetDeterministicStringHash(player.Id);
                        }
                    }
                }
            }

            // Include Robber position if set
            if (gameModel.Robber?.Coordinates is not null &&
                !gameModel.Robber.Coordinates.Equals(HexCoordinates.Default))
            {
                hash += primes.Pop() * gameModel.Robber.Coordinates.Q;
                hash += primes.Pop() * gameModel.Robber.Coordinates.R;
                hash += primes.Pop() * gameModel.Robber.Coordinates.S;
            }
            var result = hash.ToString("X");
            return result;
        }

        /// <summary>
        /// First 750 prime numbers for unique hash computation
        /// Provides enough unique primes for comprehensive game state hashing
        /// </summary>
        private static readonly int[] First750Primes = new int[]
        {
            2, 3, 5, 7, 11, 13, 17, 19, 23, 29,
            31, 37, 41, 43, 47, 53, 59, 61, 67, 71,
            73, 79, 83, 89, 97, 101, 103, 107, 109, 113,
            127, 131, 137, 139, 149, 151, 157, 163, 167, 173,
            179, 181, 191, 193, 197, 199, 211, 223, 227, 229,
            233, 239, 241, 251, 257, 263, 269, 271, 277, 281,
            283, 293, 307, 311, 313, 317, 331, 337, 347, 349,
            353, 359, 367, 373, 379, 383, 389, 397, 401, 409,
            419, 421, 431, 433, 439, 443, 449, 457, 461, 463,
            467, 479, 487, 491, 499, 503, 509, 521, 523, 541,
            547, 557, 563, 569, 571, 577, 587, 593, 599, 601,
            607, 613, 617, 619, 631, 641, 643, 647, 653, 659,
            661, 673, 677, 683, 691, 701, 709, 719, 727, 733,
            739, 743, 751, 757, 761, 769, 773, 787, 797, 809,
            811, 821, 823, 827, 829, 839, 853, 857, 859, 863,
            877, 881, 883, 887, 907, 911, 919, 929, 937, 941,
            947, 953, 967, 971, 977, 983, 991, 997, 1009, 1013,
            1019, 1021, 1031, 1033, 1039, 1049, 1051, 1061, 1063, 1069,
            1087, 1091, 1093, 1097, 1103, 1109, 1117, 1123, 1129, 1151,
            1153, 1163, 1171, 1181, 1187, 1193, 1201, 1213, 1217, 1223,
            1229, 1231, 1237, 1249, 1259, 1277, 1279, 1283, 1289, 1291,
            1297, 1301, 1303, 1307, 1319, 1321, 1327, 1361, 1367, 1373,
            1381, 1399, 1409, 1423, 1427, 1429, 1433, 1439, 1447, 1451,
            1453, 1459, 1471, 1481, 1483, 1487, 1489, 1493, 1499, 1511,
            1523, 1531, 1543, 1549, 1553, 1559, 1567, 1571, 1579, 1583,
            1597, 1601, 1607, 1609, 1613, 1619, 1621, 1627, 1637, 1657,
            1663, 1667, 1669, 1693, 1697, 1699, 1709, 1721, 1723, 1733,
            1741, 1747, 1753, 1759, 1777, 1783, 1787, 1789, 1801, 1811,
            1823, 1831, 1847, 1861, 1867, 1871, 1873, 1877, 1879, 1889,
            1901, 1907, 1913, 1931, 1933, 1949, 1951, 1973, 1979, 1987,
            1993, 1997, 1999, 2003, 2011, 2017, 2027, 2029, 2039, 2053,
            2063, 2069, 2081, 2083, 2087, 2089, 2099, 2111, 2113, 2129,
            2131, 2137, 2141, 2143, 2153, 2161, 2179, 2203, 2207, 2213,
            2221, 2237, 2239, 2243, 2251, 2267, 2269, 2273, 2281, 2287,
            2293, 2297, 2309, 2311, 2333, 2339, 2341, 2347, 2351, 2357,
            2371, 2377, 2381, 2383, 2389, 2393, 2399, 2411, 2417, 2423,
            2437, 2441, 2447, 2459, 2467, 2473, 2477, 2503, 2521, 2531,
            2539, 2543, 2549, 2551, 2557, 2579, 2591, 2593, 2609, 2617,
            2621, 2633, 2647, 2657, 2659, 2663, 2671, 2677, 2683, 2687,
            2689, 2693, 2699, 2707, 2711, 2713, 2719, 2729, 2731, 2741,
            2749, 2753, 2767, 2777, 2789, 2791, 2797, 2801, 2803, 2819,
            2833, 2837, 2843, 2851, 2857, 2861, 2879, 2887, 2897, 2903,
            2909, 2917, 2927, 2939, 2953, 2957, 2963, 2969, 2971, 2999,
            3001, 3011, 3019, 3023, 3037, 3041, 3049, 3061, 3067, 3079,
            3083, 3089, 3109, 3119, 3121, 3137, 3163, 3167, 3169, 3181,
            3187, 3191, 3203, 3209, 3217, 3221, 3229, 3251, 3253, 3257,
            3259, 3271, 3299, 3301, 3307, 3313, 3319, 3323, 3329, 3331,
            3343, 3347, 3359, 3361, 3371, 3373, 3389, 3391, 3407, 3413,
            3433, 3449, 3457, 3461, 3463, 3467, 3469, 3491, 3499, 3511,
            3517, 3527, 3529, 3533, 3539, 3541, 3547, 3557, 3559, 3571,
            3581, 3583, 3593, 3607, 3613, 3617, 3623, 3631, 3637, 3643,
            3659, 3671, 3673, 3677, 3691, 3697, 3701, 3709, 3719, 3727,
            3733, 3739, 3761, 3767, 3769, 3779, 3793, 3797, 3803, 3821,
            3823, 3833, 3847, 3851, 3853, 3863, 3877, 3881, 3889, 3907,
            3911, 3917, 3919, 3923, 3929, 3931, 3943, 3947, 3967, 3989,
            4001, 4003, 4007, 4013, 4019, 4021, 4027, 4049, 4051, 4057,
            4073, 4079, 4091, 4093, 4099, 4111, 4127, 4129, 4133, 4139,
            4153, 4157, 4159, 4177, 4201, 4211, 4217, 4219, 4229, 4231,
            4241, 4243, 4253, 4259, 4261, 4271, 4273, 4283, 4289, 4297,
            4327, 4337, 4339, 4349, 4357, 4363, 4373, 4391, 4397, 4409,
            4421, 4423, 4441, 4447, 4451, 4457, 4463, 4481, 4483, 4493,
            4507, 4513, 4517, 4519, 4523, 4547, 4549, 4561, 4567, 4583,
            4591, 4597, 4603, 4621, 4637, 4639, 4643, 4649, 4651, 4657,
            4663, 4673, 4679, 4691, 4703, 4721, 4723, 4729, 4733, 4751,
            4759, 4783, 4787, 4789, 4793, 4799, 4801, 4813, 4817, 4831,
            4861, 4871, 4877, 4889, 4903, 4909, 4919, 4931, 4933, 4937,
            4943, 4951, 4957, 4967, 4969, 4973, 4987, 4993, 4999, 5003,
            5009, 5011, 5021, 5023, 5039, 5051, 5059, 5077, 5081, 5087,
            5099, 5101, 5107, 5113, 5119, 5147, 5153, 5167, 5171, 5179,
            5189, 5197, 5209, 5227, 5231, 5233, 5237, 5261, 5273, 5279,
            5281, 5297, 5303, 5309, 5323, 5333, 5347, 5351, 5381, 5387,
            5393, 5399, 5407, 5413, 5417, 5419, 5431, 5437, 5441, 5443,
            5449, 5471, 5477, 5479, 5483, 5501, 5503, 5507, 5519, 5521,
            5527, 5531, 5557, 5563, 5569, 5573, 5581, 5591, 5623, 5639,
            5641, 5647, 5651, 5653, 5657, 5659, 5669, 5683, 5689, 5693,
            5701, 5711, 5717, 5737, 5741, 5743, 5749, 5779, 5783, 5787
        };

        /// <summary>
        /// Updates the ExpectedGameHash by recomputing it from the current game state.
        /// This should be called by the GameStateMachine whenever the game state changes.
        /// </summary>
        public static void UpdateGameHash(this GameModel gameModel)
        {
            // Restore proper hash computation
            gameModel.GameHash = gameModel.ComputeGameHash();
        }

        /// <summary>
        /// Given a building, what roads are next to it?
        /// </summary>
        public static List<RoadModel> AdjacentRoads(this GameModel gameModel, BuildingKey buildingKey)
        {
            List<RoadModel> result = [];
            List<RoadKey> roadKeys = [];
            switch (buildingKey.Position)
            {
                case HexPosition.Right:
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.TopRight });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.BottomRight });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates.NorthEast, HexSide = HexSide.Bottom });
                    break;
                case HexPosition.BottomRight:
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.Bottom });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.BottomRight });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates.South, HexSide = HexSide.TopRight });
                    break;
                case HexPosition.BottomLeft:
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.Bottom });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.BottomLeft });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates.South, HexSide = HexSide.TopLeft });
                    break;
                case HexPosition.Left:
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.TopLeft });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.BottomLeft });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates.NorthWest, HexSide = HexSide.Bottom });
                    break;
                case HexPosition.TopLeft:
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.TopLeft });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.Top });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates.NorthWest, HexSide = HexSide.TopRight });
                    break;
                case HexPosition.TopRight:
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.TopRight });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.Top });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates.North, HexSide = HexSide.BottomRight });
                    break;
                case HexPosition.None:
                    break;
            }
            foreach (var key in roadKeys)
            {
                var road = gameModel.Roads.FindRoad(key);
                if (road is not null)
                {
                    result.Add(road);
                }
            }
            return result;
        }

        /// <summary>
        /// Given a road, what buildings are next to it?
        /// </summary>
        public static List<BuildingModel> AdjacentBuildings(this GameModel gameModel, RoadKey roadKey)
        {
            var result = new List<BuildingModel>();
            switch (roadKey.HexSide)
            {
                case HexSide.None:
                    throw new System.Exception($"Invalid Side for road key {roadKey}");
                case HexSide.Top:
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.TopLeft }));
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.TopRight }));
                    break;
                case HexSide.TopRight:
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.TopRight }));
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.Right }));
                    break;
                case HexSide.BottomRight:
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.Right }));
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.BottomRight }));
                    break;
                case HexSide.Bottom:
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.BottomRight }));
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.BottomLeft }));
                    break;
                case HexSide.BottomLeft:
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.BottomLeft }));
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.Left }));
                    break;
                case HexSide.TopLeft:
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.Left }));
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.TopLeft }));
                    break;
            }
            return result;
        }

        /// <summary>
        /// Return the building between the 2 road keys. Works by getting the building adjacent to each of the roads
        /// and then finding what the intersection is. It must be only 0 or 1 roads.
        /// </summary>
        public static BuildingModel? BuildingBetweenRoads(this GameModel gameModel, RoadKey road1, RoadKey road2)
        {
            var buildings1 = gameModel.AdjacentBuildings(road1);
            var buildings2 = gameModel.AdjacentBuildings(road2);
            var result = buildings1.Intersect(buildings2).ToList();
            Debug.Assert(result.Count <= 1);
            if (result.Count == 0) return null;
            return result is null ? null : result[0];
        }

        /// <summary>
        /// Calculates the player ID that is a specified number of positions away from a given start player ID.
        /// </summary>
        /// <param name="gameModel">The game model containing the players.</param>
        /// <param name="startPlayerId">The ID of the player from which to start counting.</param>
        /// <param name="numberOfPositions">The number of positions to move forward in the player list; can be negative.</param>
        /// <returns>The player ID of the player numberOfPositions away from the start player.</returns>
        /// <exception cref="System.Exception">Thrown if the start player ID is invalid or not in the game.</exception>
        public static string NextPlayerId(this GameModel gameModel, string startPlayerId, int numberOfPositions)
        {
            // Validate and find the starting player
            var startPlayer = gameModel.Players.PlayerFromId(startPlayerId) ??
                throw new System.Exception($"Invalid id: {startPlayerId}");
            int idx = gameModel.Players.IndexOf(startPlayer);
            if (idx == -1)
                throw new System.Exception("The player must be in the game!");
            int count = gameModel.Players.Count;
            // Calculate the index of the next player, wrapping around if necessary
            int newPlayerIndex = (idx + numberOfPositions) % count;
            if (newPlayerIndex < 0)
                newPlayerIndex += count;
            // Retrieve the new player's ID
            var newPlayer = gameModel.Players[newPlayerIndex];
            return newPlayer.Id;
        }

        /// <summary>
        /// Changes the current player to the player a specified number of positions forward.
        /// </summary>
        /// <param name="gameModel">The game model where the current player will be changed.</param>
        /// <param name="numberOfPositions">The number of positions to move forward in the player list.</param>
        /// <exception cref="System.Exception">Thrown if the player ID is invalid.</exception>
        public static void ChangePlayer(this GameModel gameModel, int numberOfPositions)
        {
            // Ensure the current player ID is valid
            if (string.IsNullOrEmpty(gameModel.CurrentPlayerId))
                throw new System.Exception("Current player ID must not be null or empty.");
            // Get the next player ID and change to it
            var id = NextPlayerId(gameModel, gameModel.CurrentPlayerId, numberOfPositions);
            gameModel.ChangePlayerTo(id);
        }

        /// <summary>
        /// Sets the current player to the specified player ID.
        /// </summary>
        /// <param name="gameModel">The game model where the current player will be set.</param>
        /// <param name="playerId">The player ID to set as current.</param>
        /// <exception cref="System.Exception">Thrown if the player ID is invalid.</exception>
        public static void ChangePlayerTo(this GameModel gameModel, string playerId)
        {
            // Validate and find the new player
            var newPlayer = gameModel.Players.PlayerFromId(playerId) ??
                throw new System.Exception($"Invalid id: {playerId}");
            // Set the current player ID
            gameModel.CurrentPlayerId = newPlayer.Id;
        }

        /// <summary>
        /// Given a BuildingKey, return the list of tiles that the Building connects to
        /// </summary>
        /// <param name="gameModel">The game model</param>
        /// <param name="key">The building key</param>
        /// <returns>List of connected tiles</returns>
        public static List<TileModel> TilesForBuildings(this GameModel gameModel, BuildingKey key)
        {
            List<TileModel> tiles = [];
            // get the tile
            var tileModel = gameModel.Tiles.TileFromCoords(key.HexCoordinates);
            Debug.Assert(tileModel is not null, "Bad HexCoordinates");
            tiles.Add(tileModel);
            // get the aliases
            var aliases = key.Aliases();
            foreach ((_, Direction direction) in aliases)
            {
                var neighbor = gameModel.Tiles.TileFromCoords(tileModel.TileKey.GetAdjacentTile(direction));
                if (neighbor is not null)
                {
                    tiles.Add(neighbor);
                }
            }
            return tiles;
        }

        public static PlayerModel CurrentPlayer(this GameModel gameModel)
        {
            return gameModel.Players.PlayerFromId(gameModel.CurrentPlayerId) ?? throw new System.Exception($"Can't find player {gameModel.CurrentPlayerId}");
        }

        /// <summary>
        /// Return the harbor that is adjacent to the given building key.
        /// </summary>
        /// <param name="gameModel">The game model</param>
        /// <param name="buildingKey">The building key</param>
        /// <returns>The adjacent Harbor or null if it has none</returns>
        public static HarborModel? FindAdjacentHarbor(this GameModel gameModel, BuildingKey buildingKey)
        {
            foreach (var (hex, side) in HarborModel.GetAdjacentHarborLocations(buildingKey))
            {
                var harbor = gameModel.Harbors.FirstOrDefault(h => h.HarborKey.HexCoordinates.Equals(hex) && h.HarborKey.Side == side);
                if (harbor is not null)
                    return harbor;
            }
            return null;
        }

        /// <summary>
        ///  an extension method to GameModel that takes a BuildingKey and returns a ResourceModel of all the resources you would get for that building
        /// </summary>
        /// 
        public static ResourcesModel ResourcesForBuilding(this GameModel gameModel, BuildingModel building)
        {
            var resources = new ResourcesModel();
            // Get the building model from the game model
            int count = building.BuildingState == BuildingState.City ? 2 : 1;
            var tiles = gameModel.TilesForBuildings(building.BuildingKey);
            foreach (var tile in tiles)
            {
                resources.AddResource(tile.ResourceTileType, count);
            }

            return resources;
        }

        /// <summary>
        /// Validates a GameModel's structural integrity for game operation.
        /// Does not check GameId as that's assigned by the service layer.
        /// </summary>
        /// <param name="gameModel">The GameModel to validate (must not be null).</param>
        /// <exception cref="GameException">Thrown with specific message when validation fails.</exception>
        public static void Validate(this GameModel gameModel)
        {
            if (string.IsNullOrEmpty(gameModel.GameId))
                throw new GameException("GameModel missing required GameID");


            if (string.IsNullOrEmpty(gameModel.GameHash))
                throw new GameException("GameModel missing required GameHash");

            // Check random state (needed for deterministic behavior)
            if (gameModel.Random?.Seed == 0)
                throw new GameException("GameModel missing Random or RandomSeed - required for deterministic game behavior");

            // Check essential collections exist and have content
            if (gameModel.Players == null || gameModel.Players.Count == 0)
                throw new GameException($"GameModel has no players (found: {gameModel.Players?.Count ?? 0})");

            if (gameModel.Tiles == null || gameModel.Tiles.Count == 0)
                throw new GameException($"GameModel has no tiles (found: {gameModel.Tiles?.Count ?? 0})");

            if (gameModel.Roads == null || gameModel.Roads.Count == 0)
                throw new GameException($"GameModel has no roads (found: {gameModel.Roads?.Count ?? 0})");

            if (gameModel.Buildings == null || gameModel.Buildings.Count == 0)
                throw new GameException($"GameModel has no buildings (found: {gameModel.Buildings?.Count ?? 0})");

            // Check game type is valid
            if (gameModel.GameType == GameType.Unset)
                throw new GameException($"GameModel has invalid GameType: {gameModel.GameType}");

            // Check that players have IDs
            for (int i = 0; i < gameModel.Players.Count; i++)
            {
                if (string.IsNullOrEmpty(gameModel.Players[i].Id))
                    throw new GameException($"Player at index {i} missing required Id");
            }
        }

        /// <summary>
        /// Generates a descriptive filename for saving this GameModel.
        /// Format: {GameName}-{GameType}-{GameId}.catan
        /// Example: "MyGame-Regular-abc123.catan"
        /// </summary>
        public static string SaveFileName(this GameModel gameModel)
        {
            return $"{gameModel.GameName}-{gameModel.GameType}-{gameModel.GameId}.catan";
        }

        /// <summary>
        /// Shuffles the content of a game model (tiles, numbers, resources).
        /// Uses ReplayableRandom for deterministic behavior across platforms.
        /// </summary>
        public static void Shuffle(this GameModel game)
        {
            // NOTE: The validation loop below is INTENTIONALLY deterministic and thread-safe:
            // - Each GameStateMachine instance operates on isolated game data (per-game concurrency isolation)
            // - ReplayableRandom produces the same sequence for the same seed+iterations
            // - The loop will always take the same number of iterations for the same starting conditions
            // - This is NOT a race condition - it's deterministic game logic by design
            do
            {
                ShuffleList<TileModel, ResourceType>(game.Tiles, game.Random,
                     tile => tile.ResourceTileType,
                     (tile, type) => tile.ResourceTileType = type);
                ShuffleList<TileModel, int>(game.Tiles, game.Random,
                    tile => tile.Number,
                    (tile, number) => tile.Number = number);
                ShuffleList<HarborModel, HarborType>(game.Harbors, game.Random,
                   harbor => harbor.HarborKey.HarborType,
                   (harbor, type) => harbor.HarborKey.HarborType = type);
                // Correct the placement of the number 7 on desert tiles
                EnsureDesertSeven(game);
            } while (!ValidateGame(game));

            // 1/14/2025: Robber starts off the board so the first move can be to a desert tile, so do NOT put the robber on a desert tile
        }

        /// <summary>
        /// Validates that the game board doesn't have adjacent 6s and 8s.
        /// </summary>
        public static bool ValidateGame(this GameModel Game)
        {
            var tiles6 = Game.Tiles.TilesWithNumber(6);
            System.Diagnostics.Debug.WriteLine($"ValidateGame: Found {tiles6.Count} tiles with number 6");

            foreach (var tile in tiles6)
            {
                var adjacent = Game.Tiles.AdjacentTiles(tile);
                var badAdjacent = adjacent?.TilesWithSixOrEight() ?? new List<TileModel>();
                System.Diagnostics.Debug.WriteLine($"  Tile 6 at {tile.TileKey}: {adjacent?.Count ?? 0} adjacent, {badAdjacent.Count} with 6/8");
                if (badAdjacent.Count != 0)
                {
                    foreach (var bad in badAdjacent)
                    {
                        System.Diagnostics.Debug.WriteLine($"    -> Adjacent {bad.TileKey} has number {bad.Number}");
                    }
                    return false;
                }
            }

            var tiles8 = Game.Tiles.TilesWithNumber(8);
            System.Diagnostics.Debug.WriteLine($"ValidateGame: Found {tiles8.Count} tiles with number 8");

            foreach (var tile in tiles8)
            {
                var adjacent = Game.Tiles.AdjacentTiles(tile);
                var badAdjacent = adjacent?.TilesWithSixOrEight() ?? new List<TileModel>();
                System.Diagnostics.Debug.WriteLine($"  Tile 8 at {tile.TileKey}: {adjacent?.Count ?? 0} adjacent, {badAdjacent.Count} with 6/8");
                if (badAdjacent.Count != 0)
                {
                    foreach (var bad in badAdjacent)
                    {
                        System.Diagnostics.Debug.WriteLine($"    -> Adjacent {bad.TileKey} has number {bad.Number}");
                    }
                    return false;
                }
            }

            return true;
        }

        private static void ShuffleList<T, TValue>(IList<T> list, ReplayableRandom random, Func<T, TValue> valueSelector, Action<T, TValue> valueSetter)
        {
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                int j = random.Next(i, count); // Correct Fisher-Yates shuffle
                var temp = valueSelector(list[j]);
                valueSetter(list[j], valueSelector(list[i]));
                valueSetter(list[i], temp);
            }
        }

        private static void EnsureDesertSeven(GameModel game)
        {
            var deserts = game.Tiles.Where(t => t.ResourceTileType == ResourceType.Desert).ToList();
            var sevens = game.Tiles.Where(t => t.Number == 7).ToList();
            System.Diagnostics.Debug.Assert(deserts.Count == sevens.Count, "Mismatch between deserts and tiles with number 7");

            for (int i = 0; i < deserts.Count; i++)
            {
                if (deserts[i].Number != 7)
                {
                    sevens[i].Number = deserts[i].Number;
                    deserts[i].Number = 7;
                }
            }
        }
    }
}