using Barotrauma;
using MoreLevelContent.Missions;
using MoreLevelContent.Shared.Data;
using System.Collections.Generic;
using System.Xml.Linq;
using System;
using System.Linq;
using MoreLevelContent.Shared.Generation.Pirate;
using Microsoft.Xna.Framework;

namespace MoreLevelContent.Shared.Generation
{
    internal partial class PirateOutpostMapModule : MapModule
    {
        protected override void InitProjSpecific() { }

        public override void OnLevelDataGenerate(LevelData __instance, LocationConnection locationConnection) => SetPirateData(locationConnection.LevelData, locationConnection.LevelData.MLC(), locationConnection);

        public override void OnMapLoad(Map __instance)
        {
            // Map has no pirate outposts, lets generate some
            if (!__instance.Connections.Any(c => c.LevelData.MLC().PirateData.HasPirateOutpost))
            {
                Log.Debug("Map has no pirate outposts, adding some...");
                for (int i = 0; i < __instance.Connections.Count; i++)
                {
                    var connection = __instance.Connections[i];
                    SetPirateData(connection.LevelData, connection.LevelData.MLC(), connection);
                }
            }
        }

        void SetPirateData(LevelData levelData, LevelData_MLCData additionalData, LocationConnection locationConnection)
        {
            Random rand = new MTRandom(ToolBox.StringToInt(levelData.Seed));
            PirateSpawnData spawnData = new PirateSpawnData(rand, levelData.Difficulty);
            additionalData.PirateData = new PirateData(spawnData);
        }
    }

    public class PirateSpawnData
    {
        public PirateSpawnData(Random rand, float levelDiff)
        {
            UpdatePirateSpawnData(levelDiff, rand);

            int spawnInt = rand.Next(100);
            int huskInt = rand.Next(100);

            WillSpawn = PirateOutpostDirector.Instance.ForceSpawn ? PirateOutpostDirector.Instance.ForceSpawn : modifiedSpawnChance > spawnInt;
            Husked = modifiedHuskChance > huskInt;

            Log.InternalDebug($"spawn int {spawnInt}, husk int {huskInt}");
        }

        public bool WillSpawn { get; set; }
        public bool Husked { get; set; }
        public float PirateDifficulty { get; private set; }

        public override string ToString() => $"Will Spawn: {WillSpawn}, Is Husked: {Husked}";

        private float modifiedSpawnChance;
        private float modifiedHuskChance;

        private void UpdatePirateSpawnData(float levelDiff, Random rand)
        {
            float baseChance = levelDiff < 100 ?
                MathF.Min(levelDiff / 2, (levelDiff / 5) + 15) :
                100f;
            float spawnOffset = MathHelper.Lerp(-PirateOutpostDirector.Config.SpawnChanceNoise, PirateOutpostDirector.Config.SpawnChanceNoise, (float)rand.NextDouble());

            modifiedSpawnChance = baseChance + spawnOffset + PirateOutpostDirector.Config.BasePirateSpawnChance;
            if (PirateOutpostDirector.Config.BasePirateSpawnChance == 100) modifiedSpawnChance = 100;

            float diffOffset = Math.Abs(MathHelper.Lerp(-PirateOutpostDirector.Config.DifficultyNoise, PirateOutpostDirector.Config.DifficultyNoise, (float)rand.NextDouble()));
            PirateDifficulty = levelDiff + diffOffset;

            modifiedHuskChance = MathF.Max(PirateOutpostDirector.Config.BaseHuskChance, levelDiff / 10);
        }

    }
}
