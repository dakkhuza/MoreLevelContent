using Barotrauma.Networking;
using MoreLevelContent.Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace Barotrauma.MoreLevelContent.Shared.Config
{
    [NetworkSerialize]
    public struct PirateConfig : INetSerializableStruct
    {
        public Int32 BasePirateSpawnChance;
        public Int32 BaseHuskChance;
        public Single SpawnChanceNoise;
        public Single DifficultyNoise;
        public bool AddDiffPerPlayer;
        public bool DisplaySonarMarker;

        public static PirateConfig GetDefault()
        {
            PirateConfig config = new PirateConfig()
            {
                BasePirateSpawnChance = 0,
                BaseHuskChance = 1,
                SpawnChanceNoise = 10.0f,
                DifficultyNoise = 10.0f,
                AddDiffPerPlayer = true,
                DisplaySonarMarker = false
            };
            return config;
        }

        public override string ToString() => 
            $"\n-Pirate Config-\n" +
            $"Spawn Chance: {BasePirateSpawnChance}\n" +
            $"Husk Chance: {BaseHuskChance}\n" +
            $"Spawn Noise: {SpawnChanceNoise}\n" +
            $"Diff Noise: {DifficultyNoise}\n" +
            $"Add Diff: {AddDiffPerPlayer}";
    }
}
