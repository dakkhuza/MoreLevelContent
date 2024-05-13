using Barotrauma;
using Barotrauma.MoreLevelContent.Config;
using Barotrauma.MoreLevelContent.Shared.Config;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation.Interfaces;
using MoreLevelContent.Shared.Store;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MoreLevelContent.Shared.Generation.Pirate
{
    public class PirateOutpostDirector : GenerationDirector<PirateOutpostDirector>, IGenerateSubmarine, IGenerateNPCs, ILevelStartGenerate, IRoundStatus
    {
        public string ForcedPirateOutpost = "";
        public bool ForceSpawn { get; set; } = false;
        public bool ForceHusk { get; set; } = false;

        public static PirateConfig Config => ConfigManager.Instance.Config.NetworkedConfig.PirateConfig;

        private PirateOutpost _PirateOutpost;

        public override bool Active => PirateStore.HasContent;

        public override void Setup() => PirateStore.Instance.Setup();

        void ILevelStartGenerate.OnLevelGenerationStart(LevelData levelData, bool _)
        {
            _PirateOutpost = null;

            // Prevent an outpost from spawning if the mission is a pirate
            // It will brick the pirates if it does
            if (!Screen.Selected.IsEditor) // Don't check in editor
            {
                foreach (Mission mission in GameMain.GameSession.GameMode!.Missions)
                {
                    if (mission is PirateMission) return;
                }
            }

            var pirateData = levelData.MLC().PirateData;
            if (pirateData.HasPirateOutpost)
            {
                _PirateOutpost = new PirateOutpost(pirateData, ForcedPirateOutpost);
                Log.Verbose("Set pirate outpost");
            }
        }

        public void GenerateSub() => _PirateOutpost?.Generate();
        public void SpawnNPCs() => _PirateOutpost?.Populate();
        public void BeforeRoundStart() { }
        public void RoundEnd()
        {
            if (_PirateOutpost != null)
            {
                _PirateOutpost.OnRoundEnd(Level.Loaded.LevelData);
            }
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
            Log.Debug($"Modified pirate spawn chance for diff {levelDiff} is {modifiedSpawnChance}, base chance {baseChance}, offset {spawnOffset}");

            float diffOffset = Math.Abs(MathHelper.Lerp(-PirateOutpostDirector.Config.DifficultyNoise, PirateOutpostDirector.Config.DifficultyNoise, (float)rand.NextDouble()));
            PirateDifficulty = levelDiff + diffOffset;
            Log.Debug($"Modified pirate diff is {PirateDifficulty}, level diff {levelDiff}, offset {diffOffset}");

            modifiedHuskChance = MathF.Max(PirateOutpostDirector.Config.BaseHuskChance, levelDiff / 10);
            Log.Debug($"Modified chance for pirates to be husked is {modifiedHuskChance}");
        }

    }

}
