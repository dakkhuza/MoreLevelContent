﻿

using Barotrauma.Networking;

namespace Barotrauma.MoreLevelContent.Shared.Config
{
    [NetworkSerialize]
    public struct LevelConfig : INetSerializableStruct
    {
        // public bool MoveRuins;
        public bool EnableDistressMissions;
        public bool EnableConstructionSites;
        public bool EnableRelayStations;
        public bool EnableMapFeatures;
        public bool EnableThalamusCaves;
        public int RuinMoveChance;
        public int MaxActiveDistressBeacons;
        public int DistressSpawnChance;

        public float DistressSpawnPercentage => DistressSpawnChance / 100f;

        public static LevelConfig GetDefault()
        {
            LevelConfig config = new LevelConfig
            {
                EnableThalamusCaves = true,
                RuinMoveChance = 25,
                MaxActiveDistressBeacons = 5,
                DistressSpawnChance = 35,
                EnableConstructionSites = true,
                EnableDistressMissions = true,
                EnableMapFeatures = true,
                EnableRelayStations = true
            };
            return config;
        }

        public override string ToString() =>
            $"\n-General Config-\n" +
            $"RuinMoveChance: {RuinMoveChance}";
    }
}
