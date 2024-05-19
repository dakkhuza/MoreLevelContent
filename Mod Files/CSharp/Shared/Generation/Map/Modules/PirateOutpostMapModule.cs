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

        public override void OnLevelDataGenerate(LevelData __instance, LocationConnection locationConnection) => SetPirateData(__instance, __instance.MLC(), locationConnection);

        public override void OnMapLoad(Map __instance)
        {
            // Map has no pirate outposts, lets generate some
            if (!__instance.Connections.Any(c => c.LevelData.MLC().PirateData.HasPirateOutpost))
            {
                Log.Debug("Map has no pirate bases, adding some...");
                for (int i = 0; i < __instance.Connections.Count; i++)
                {
                    var connection = __instance.Connections[i];
                    SetPirateData(connection.LevelData, connection.LevelData.MLC(), connection);
                }
            } else
            {
                Log.Debug("Map has pirate bases");
            }
        }

        void SetPirateData(LevelData levelData, LevelData_MLCData additionalData, LocationConnection locationConnection)
        {
            PirateSpawnData spawnData = new PirateSpawnData(levelData, locationConnection);
            additionalData.PirateData = new PirateData(spawnData);
        }
    }

    internal class PirateSpawnData
    {
        public PirateSpawnData(LevelData levelData, LocationConnection connection)
        {
            Random rand = new MTRandom(ToolBox.StringToInt(levelData.Seed));
            UpdatePirateSpawnData(rand, levelData, connection);

            int spawnInt = rand.Next(100);
            int huskInt = rand.Next(100);

            WillSpawn = PirateOutpostDirector.Instance.ForceSpawn ? PirateOutpostDirector.Instance.ForceSpawn : _ModifiedSpawnChance > spawnInt;
            Husked = _ModifiedHuskChance > huskInt;
        }

        public bool WillSpawn { get; set; }
        public bool Husked { get; set; }
        public float PirateDifficulty { get; private set; }

        public override string ToString() => $"Will Spawn: {WillSpawn}, Is Husked: {Husked}";

        private float _ModifiedSpawnChance;
        private float _ModifiedHuskChance;

        private void UpdatePirateSpawnData(Random rand, LevelData levelData, LocationConnection connection)
        {
            var levelDiff = levelData.Difficulty;
            float a = PirateOutpostDirector.Config.PeakSpawnChance;
            float b = a / 2500;
            float c = MathF.Pow(levelDiff - 50.0f, 2);
            var spawnChance = (-b * c) + a;
            var huskChance = MathF.Max(PirateOutpostDirector.Config.BaseHuskChance, levelDiff / 10);
            ModifyChances();

            _ModifiedSpawnChance = spawnChance;
            _ModifiedHuskChance = huskChance;

            float difficultyNoise = Math.Abs(MathHelper.Lerp(-PirateOutpostDirector.Config.DifficultyNoise, PirateOutpostDirector.Config.DifficultyNoise, (float)rand.NextDouble()));
            PirateDifficulty = levelDiff + difficultyNoise;

            void ModifyChances()
            {
                // Don't spawn bases on routes with an abyss creature
                if (levelData.HasHuntingGrounds)
                {
                    spawnChance = 0;
                    Log.Debug("Set spawn chance to 0 due to hunting grounds");
                    return;
                }

                foreach (var location in connection.Locations)
                {
                    var identifier = location.Type.Identifier;
                    if (CompatabilityHelper.Instance.DynamicEuropaInstalled)
                    {
                        // Double spawn chance on routes leading to pirate outposts
                        ModifySpawn("PirateOutpost", 2);

                        // Don't spawn on areas leading to military
                        ModifySpawn("Camp", 0);

                        ModifySpawn("Base", 0);

                        ModifySpawn("Blockade", 0);

                        ModifyHusk("HuskgroundsDE", 10f);

                        ModifyHusk("OuterHuskLair", 5f);
                    }


                    // Increased chance to spawn next to natural formations
                    ModifySpawn("None", 1.5f);

                    // Increased chance to spawn next to abandoned outposts
                    ModifySpawn("Abandoned", 1.3f);

                    // Never spawn if one of the connections is a military outpost
                    ModifySpawn("Military", 0);

                    // No chance if city
                    ModifySpawn("City", 0f);

                    // Slightly reduced chance if leading to a outpost
                    ModifySpawn("Outpost", 0.25f);

                    // Slightly reduced chance if leading to a research outpost
                    ModifySpawn("Research", 0.25f);

                    // Slightly reduced chance if leading to a research outpost
                    ModifySpawn("Mine", 0.25f);

                    // Never spawn leading to the end
                    ModifySpawn("EndLocation", 0);


                    void ModifySpawn(string input, float multi)
                    {
                        if (identifier == input) spawnChance *= multi;
                        //Log.Debug($"M SC {input}: {spawnChance}");
                    }

                    void ModifyHusk(string input, float multi)
                    {
                        if (identifier == input) spawnChance *= multi;
                        //Log.Debug($"M HC {input}: {spawnChance}");
                    }
                }
            }
        }
    }
}
